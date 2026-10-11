using System.Text.Json;
using InterviewPrep.Api.Data;
using InterviewPrep.Api.Features.Questions;
using InterviewPrep.Api.Features.Study;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewPrep.Api.Tests;

/// <summary>
/// Seeding must be idempotent and keep question ids stable: later features (cards, review logs)
/// reference questions by id. Each test re-runs the seeder after changing data, so the database is
/// back to its canonical state when the test ends.
/// </summary>
public class SeedDataTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string SeedFile = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", "questions.json");

    private static Guid[] SeedFileIds()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(SeedFile));
        return doc.RootElement.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToArray();
    }

    private async Task<T> WithDb<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task Reseed(string? path = null) =>
        WithDb(async db =>
        {
            await DatabaseInitializer.SeedQuestionsAsync(db, path ?? SeedFile, CancellationToken.None);
            return 0;
        });

    private Task<List<Guid>> SeedRowIds() =>
        WithDb(db => db.Questions.Where(q => q.Source == QuestionSource.Seed).Select(q => q.Id).ToListAsync());

    [Fact]
    public void SeedFile_HasUniqueNonEmptyIds()
    {
        var ids = SeedFileIds();

        Assert.NotEmpty(ids);
        Assert.DoesNotContain(Guid.Empty, ids);
        Assert.Equal(ids.Length, ids.Distinct().Count());
    }

    [Fact]
    public async Task SeededQuestions_UseTheIdsFromTheSeedFile()
    {
        Assert.Equal(SeedFileIds().Order(), (await SeedRowIds()).Order());
    }

    [Fact]
    public async Task SeedIds_AreTheSameInAnotherDatabase()
    {
        using var other = new ApiFactory();
        await using var scope = other.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var otherIds = await db.Questions.Select(q => q.Id).ToListAsync();

        Assert.Equal(SeedFileIds().Order(), otherIds.Order());
    }

    [Fact]
    public async Task Reseeding_DoesNotDuplicateQuestions()
    {
        await Reseed();
        await Reseed();

        Assert.Equal(SeedFileIds().Length, (await SeedRowIds()).Count);
    }

    [Fact]
    public async Task Reseeding_RestoresADeletedSeedQuestion_WithTheSameId()
    {
        var id = SeedFileIds()[0];
        await WithDb(db => db.Questions.Where(q => q.Id == id).ExecuteDeleteAsync());

        await Reseed();

        Assert.Contains(id, await SeedRowIds());
    }

    [Fact]
    public async Task Reseeding_RefreshesChangedSeedContent()
    {
        var id = SeedFileIds()[0];
        var original = await WithDb(db => db.Questions.Where(q => q.Id == id).Select(q => q.Answer).SingleAsync());
        await WithDb(db => db.Questions.Where(q => q.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(q => q.Answer, "changed")));

        await Reseed();

        var restored = await WithDb(db => db.Questions.Where(q => q.Id == id).Select(q => q.Answer).SingleAsync());
        Assert.Equal(original, restored);
    }

    [Fact]
    public async Task Reseeding_MovesLegacySeedRowsToTheStableId_KeepingTheirCard()
    {
        var seedId = SeedFileIds()[0];
        var legacyId = Guid.NewGuid();
        var cardId = await WithDb(async db =>
        {
            // Recreate a database seeded before ids were stable: a random-id question that already has a card.
            var seeded = await db.Questions.SingleAsync(q => q.Id == seedId);
            await db.Questions.Where(q => q.Id == seedId).ExecuteDeleteAsync();
            db.Questions.Add(new Question
            {
                Id = legacyId, Topic = seeded.Topic, Difficulty = seeded.Difficulty, Text = seeded.Text,
                Answer = seeded.Answer, Tags = seeded.Tags, Source = QuestionSource.Seed
            });
            var card = new Card { QuestionId = legacyId, CreatedAt = DateTime.UtcNow, Ease = 2.5, DueAt = DateTime.UtcNow };
            db.Cards.Add(card);
            await db.SaveChangesAsync();
            return card.Id;
        });

        await Reseed();

        var ids = await SeedRowIds();
        Assert.Contains(seedId, ids);
        Assert.DoesNotContain(legacyId, ids);
        Assert.Equal(SeedFileIds().Length, ids.Count);
        var cardQuestionId = await WithDb(db => db.Cards.Where(c => c.Id == cardId).Select(c => c.QuestionId).SingleAsync());
        Assert.Equal(seedId, cardQuestionId);

        await WithDb(db => db.Cards.Where(c => c.Id == cardId).ExecuteDeleteAsync());
    }

    [Fact]
    public async Task Reseeding_LeavesManualQuestionsAlone()
    {
        var manualId = await WithDb(async db =>
        {
            var seeded = await db.Questions.FirstAsync();
            var manual = new Question
            {
                Topic = seeded.Topic, Difficulty = seeded.Difficulty, Text = seeded.Text, Answer = "mine",
                Source = QuestionSource.Manual
            };
            db.Questions.Add(manual);
            await db.SaveChangesAsync();
            return manual.Id;
        });

        await Reseed();

        var answer = await WithDb(db => db.Questions.Where(q => q.Id == manualId).Select(q => q.Answer).SingleAsync());
        Assert.Equal("mine", answer);
        await WithDb(db => db.Questions.Where(q => q.Id == manualId).ExecuteDeleteAsync());
    }

    [Theory]
    [InlineData("""[{ "topic": "CSharp", "difficulty": "Junior", "text": "q", "answer": "a", "tags": [] }]""", "id")]
    [InlineData("""
        [
          { "id": "11111111-1111-1111-1111-111111111111", "topic": "CSharp", "difficulty": "Junior", "text": "q1", "answer": "a", "tags": [] },
          { "id": "11111111-1111-1111-1111-111111111111", "topic": "CSharp", "difficulty": "Junior", "text": "q2", "answer": "a", "tags": [] }
        ]
        """, "Duplicate")]
    public async Task Reseeding_WithMissingOrDuplicateIds_Throws(string json, string expectedMessagePart)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, json);
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Reseed(path));
            Assert.Contains(expectedMessagePart, ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
