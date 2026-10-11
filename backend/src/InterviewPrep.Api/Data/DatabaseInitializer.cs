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

    /// <summary>Applies pending migrations, then syncs the shipped seed questions into the database.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app, CancellationToken ct = default)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync(ct);
        await SeedQuestionsAsync(db, app.Environment.ContentRootFileProvider.GetFileInfo(QuestionsSeedFile).PhysicalPath, ct);
    }

    /// <summary>
    /// Makes the <c>Seed</c> rows match the seed file. Every seed entry has a stable <c>id</c> (cards reference
    /// questions by it), so this inserts entries that are missing, refreshes the content of existing ones and
    /// never duplicates. Seed rows that are no longer in the file are left alone.
    /// </summary>
    internal static async Task SeedQuestionsAsync(AppDbContext db, string? seedPath, CancellationToken ct)
    {
        if (seedPath is null || !File.Exists(seedPath))
            throw new FileNotFoundException($"Seed file '{QuestionsSeedFile}' was not found.", seedPath);

        await using var stream = File.OpenRead(seedPath);
        var seeds = await JsonSerializer.DeserializeAsync<List<SeedQuestion>>(stream, SeedJson, ct) ?? [];
        EnsureValidIds(seeds);

        var existing = await db.Questions.AsTracking()
            .Where(q => q.Source == QuestionSource.Seed)
            .ToListAsync(ct);
        var existingById = existing.ToDictionary(q => q.Id);

        // Databases created before seed ids were stable hold seed rows with random ids. Match them to the seed
        // entry with the same text so they can be moved over to the stable id (below).
        var seedIds = seeds.Select(s => s.Id).ToHashSet();
        var seedByText = seeds.GroupBy(s => s.Text).ToDictionary(g => g.Key, g => g.First());
        var claimed = new HashSet<Guid>();
        var legacy = new List<(Guid OldId, Guid NewId)>();
        foreach (var q in existing)
        {
            if (!seedIds.Contains(q.Id) &&
                seedByText.TryGetValue(q.Text, out var match) &&
                !existingById.ContainsKey(match.Id) &&
                claimed.Add(match.Id))
            {
                legacy.Add((q.Id, match.Id));
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        foreach (var seed in seeds)
        {
            var tags = seed.Tags.Select(t => t.Trim().ToLowerInvariant()).Distinct().ToList();

            if (existingById.TryGetValue(seed.Id, out var question))
            {
                question.Topic = seed.Topic;
                question.Difficulty = seed.Difficulty;
                question.Text = seed.Text;
                question.Answer = seed.Answer;
                question.Tags = tags;
            }
            else
            {
                db.Questions.Add(new Question
                {
                    Id = seed.Id,
                    Topic = seed.Topic,
                    Difficulty = seed.Difficulty,
                    Text = seed.Text,
                    Answer = seed.Answer,
                    Tags = tags,
                    Source = QuestionSource.Seed
                });
            }
        }

        await db.SaveChangesAsync(ct);

        // Move each legacy question's card (and with it the review history) to the stable id, then drop the old
        // row. Deleting it first would cascade to the card and its reviews.
        foreach (var (oldId, newId) in legacy)
        {
            await db.Cards.Where(c => c.QuestionId == oldId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.QuestionId, newId), ct);
            await db.Questions.Where(q => q.Id == oldId).ExecuteDeleteAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    private static void EnsureValidIds(List<SeedQuestion> seeds)
    {
        if (seeds.Any(s => s.Id == Guid.Empty))
            throw new InvalidOperationException($"Every entry in '{QuestionsSeedFile}' needs a non-empty \"id\".");

        var duplicate = seeds.GroupBy(s => s.Id).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException($"Duplicate id {duplicate.Key} in '{QuestionsSeedFile}'.");
    }

    private sealed record SeedQuestion(
        Guid Id, Topic Topic, Difficulty Difficulty, string Text, string Answer, List<string> Tags);
}
