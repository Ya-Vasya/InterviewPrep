using System.Text.Json;
using System.Text.Json.Serialization;
using InterviewPrep.Api.Common;
using InterviewPrep.Api.Features.Questions;
using Microsoft.EntityFrameworkCore;

namespace InterviewPrep.Api.Data;

public static class DatabaseInitializer
{
    private const string QuestionsSeedFile = "Data/SeedData/questions.json";

    private static readonly JsonSerializerOptions SeedJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Applies pending migrations, then loads the shipped seed questions on first run.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken ct = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync(ct);
        await SeedQuestionsAsync(db, app.Environment.ContentRootFileProvider.GetFileInfo(QuestionsSeedFile).PhysicalPath, ct);
    }

    private static async Task SeedQuestionsAsync(AppDbContext db, string? seedPath, CancellationToken ct)
    {
        if (await db.Questions.AnyAsync(q => q.Source == QuestionSource.Seed, ct))
            return;

        if (seedPath is null || !File.Exists(seedPath))
            throw new FileNotFoundException($"Seed file '{QuestionsSeedFile}' was not found.", seedPath);

        await using var stream = File.OpenRead(seedPath);
        var seeds = await JsonSerializer.DeserializeAsync<List<SeedQuestion>>(stream, SeedJson, ct) ?? [];

        db.Questions.AddRange(seeds.Select(s => new Question
        {
            Topic = s.Topic,
            Difficulty = s.Difficulty,
            Text = s.Text,
            Answer = s.Answer,
            Tags = s.Tags.Select(t => t.Trim().ToLowerInvariant()).Distinct().ToList(),
            Source = QuestionSource.Seed
        }));

        await db.SaveChangesAsync(ct);
    }

    private sealed record SeedQuestion(
        Topic Topic, Difficulty Difficulty, string Text, string Answer, List<string> Tags);
}
