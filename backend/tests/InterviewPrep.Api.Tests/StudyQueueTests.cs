using System.Net;
using System.Net.Http.Json;
using InterviewPrep.Api.Common;
using Microsoft.AspNetCore.Mvc;

namespace InterviewPrep.Api.Tests;

/// <summary>Queue and summary behaviour with the default daily limits (20 new, 200 reviews).</summary>
public class StudyQueueTests(ApiFactory factory) : StudyTestBase(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Queue_WithNoCards_ReturnsNewQuestionsUpToTheDailyLimit()
    {
        var queue = await GetQueue("?limit=100");

        Assert.Equal(20, queue.Count);
        Assert.All(queue, item =>
        {
            Assert.True(item.IsNew);
            Assert.Null(item.CardId);
            Assert.NotEmpty(item.Text);
            Assert.NotEmpty(item.Answer);
        });
        Assert.Equal(queue.Count, queue.Select(i => i.QuestionId).Distinct().Count());
    }

    [Fact]
    public async Task Queue_DefaultLimit_AppliesWhenNoneGiven_AndLimitParameterCapsIt()
    {
        Assert.Equal(5, (await GetQueue("?limit=5")).Count);
        Assert.Equal(20, (await GetQueue()).Count);
    }

    [Fact]
    public async Task Queue_ItemsShowTheIntervalEachRatingWouldProduce()
    {
        var item = (await GetQueue("?limit=1")).Single();

        Assert.Equal(0, item.Intervals.Again);
        Assert.Equal(1, item.Intervals.Hard);
        Assert.Equal(1, item.Intervals.Good);
        Assert.True(item.Intervals.Easy > item.Intervals.Good);

        // After a first Good the next Good is 6 days; the queue previews it.
        await Rate(item.QuestionId, "Good");
        Clock.Advance(TimeSpan.FromDays(1));

        var due = (await GetQueue("?limit=1")).Single();
        Assert.Equal(item.QuestionId, due.QuestionId);
        Assert.Equal(6, due.Intervals.Good);
    }

    [Fact]
    public async Task QueueAndSummary_CreateNoCards_AndDoNotChangeTheCounts()
    {
        var before = await GetSummary();

        await GetQueue("?limit=100");
        await GetQueue("?topic=Sql");
        var after = await GetSummary();

        Assert.Equal(0, CardCount());
        Assert.Equal(0, ReviewCount());
        Assert.Equal(before.Topics.Select(t => (t.Topic, t.DueCount, t.NewCount)),
            after.Topics.Select(t => (t.Topic, t.DueCount, t.NewCount)));
        Assert.Equal(before.NewRemainingToday, after.NewRemainingToday);
    }

    [Fact]
    public async Task Queue_ListsDueCardsOldestFirst_ThenNewCards()
    {
        var sql = await GetQuestions(Topic.Sql);
        var (a, b, c) = (sql[0].Id, sql[1].Id, sql[2].Id);

        await Rate(a, "Good");                  // due in 1 day, at 09:00 on day 2
        Clock.Advance(TimeSpan.FromHours(1));
        await Rate(b, "Good");                  // due at 10:00 on day 2
        Clock.Advance(TimeSpan.FromDays(3));    // both are overdue now
        await Rate(c, "Again");                 // due immediately, but later than a and b

        var queue = await GetQueue("?limit=100");

        Assert.Equal([a, b, c], queue.Take(3).Select(i => i.QuestionId));
        Assert.All(queue.Take(3), i => Assert.False(i.IsNew));
        Assert.All(queue.Take(3), i => Assert.NotNull(i.CardId));
        Assert.All(queue.Skip(3), i => Assert.True(i.IsNew));
        Assert.DoesNotContain(queue.Skip(3), i => i.QuestionId is var id && (id == a || id == b || id == c));
    }

    [Fact]
    public async Task Queue_DoesNotListCardsThatAreNotDueYet()
    {
        var question = (await GetQuestions(Topic.Sql))[0];

        await Rate(question.Id, "Good");

        var queue = await GetQueue("?topic=Sql&limit=100");
        Assert.DoesNotContain(queue, i => i.QuestionId == question.Id);
    }

    [Fact]
    public async Task Queue_TopicParameterIsRepeatableAndFilters()
    {
        var queue = await GetQueue("?topic=Sql&topic=CSharp&limit=100");

        Assert.NotEmpty(queue);
        Assert.All(queue, i => Assert.True(i.Topic is Topic.Sql or Topic.CSharp));
        Assert.Contains(queue, i => i.Topic == Topic.Sql);
        Assert.Contains(queue, i => i.Topic == Topic.CSharp);

        var sqlOnly = await GetQueue("?topic=sql&limit=100"); // case-insensitive
        Assert.All(sqlOnly, i => Assert.Equal(Topic.Sql, i.Topic));
    }

    [Fact]
    public async Task Queue_DueCardsOfOtherTopicsAreLeftOut()
    {
        var csharp = (await GetQuestions(Topic.CSharp))[0];
        await Rate(csharp.Id, "Again");

        var sqlQueue = await GetQueue("?topic=Sql&limit=100");
        var csharpQueue = await GetQueue("?topic=CSharp&limit=1");

        Assert.DoesNotContain(sqlQueue, i => i.QuestionId == csharp.Id);
        Assert.Equal(csharp.Id, csharpQueue.Single().QuestionId);
    }

    [Theory]
    [InlineData("?topic=Cobol", "topic")]
    [InlineData("?topic=1", "topic")]
    [InlineData("?limit=0", "limit")]
    [InlineData("?limit=100000", "limit")]
    [InlineData("?tz=Mars/Olympus", "tz")]
    [InlineData("?tz=../../etc/passwd", "tz")]
    [InlineData("?tz=%00", "tz")]
    [InlineData("?tz=..%5C..%5Cwindows", "tz")]
    public async Task Queue_WithInvalidParameters_ReturnsValidationProblem(string query, string field)
    {
        var response = await Client.GetAsync($"/api/study/queue{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);
        Assert.Contains(field, problem!.Errors.Keys);
    }

    [Fact]
    public async Task Queue_ExcludesSuspendedCards_UntilUnsuspended()
    {
        var question = (await GetQuestions(Topic.Sql))[0];
        var cardId = (await Rate(question.Id, "Again")).CardId;
        Assert.Contains(await GetQueue("?topic=Sql&limit=100"), i => i.QuestionId == question.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await Client.PostAsync($"/api/study/cards/{cardId}/suspend", null)).StatusCode);

        Assert.DoesNotContain(await GetQueue("?topic=Sql&limit=100"), i => i.QuestionId == question.Id);
        var summary = await GetSummary();
        Assert.Equal(0, summary.Topics.Single(t => t.Topic == Topic.Sql).DueCount);
        Assert.Null(summary.Topics.Single(t => t.Topic == Topic.Sql).NextDueAt);

        await Client.PostAsync($"/api/study/cards/{cardId}/unsuspend", null);
        Assert.Contains(await GetQueue("?topic=Sql&limit=100"), i => i.QuestionId == question.Id);
    }

    [Fact]
    public async Task Suspend_WithUnknownCard_ReturnsNotFound()
    {
        var response = await Client.PostAsync($"/api/study/cards/{Guid.NewGuid()}/suspend", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Summary_ReturnsTheFourTopicsWithDueAndNewCounts()
    {
        var summary = await GetSummary();

        Assert.Equal(
            [Topic.CSharp, Topic.AspNetCore, Topic.Sql, Topic.SystemDesign],
            summary.Topics.Select(t => t.Topic));
        Assert.Equal(["C#", "ASP.NET Core", "SQL", "System Design"], summary.Topics.Select(t => t.Name));
        Assert.All(summary.Topics, t =>
        {
            Assert.Equal(0, t.DueCount);
            Assert.True(t.NewCount >= 10, $"{t.Topic}: {t.NewCount}"); // at least 10 seeded questions per topic
            Assert.Null(t.NextDueAt);
        });
        Assert.Equal(20, summary.NewRemainingToday);
        Assert.Equal(200, summary.ReviewsRemainingToday);
    }

    [Fact]
    public async Task Summary_CountsDueAndNewCardsPerTopic_AndTheNextDueTime()
    {
        var sql = await GetQuestions(Topic.Sql);
        var sqlTotal = sql.Count;

        await Rate(sql[0].Id, "Again");                 // due now
        await Rate(sql[1].Id, "Again");                 // due now
        await Rate(sql[2].Id, "Good");                  // due in 1 day

        var summary = await GetSummary();
        var sqlSummary = summary.Topics.Single(t => t.Topic == Topic.Sql);

        Assert.Equal(2, sqlSummary.DueCount);
        Assert.Equal(Math.Min(sqlTotal - 3, 17), sqlSummary.NewCount); // 3 of today's 20 new cards are used
        Assert.Equal(Clock.GetUtcNow().UtcDateTime.AddDays(1), sqlSummary.NextDueAt);
        Assert.Equal(17, summary.NewRemainingToday);
    }

    [Fact]
    public async Task Summary_WithUnknownTimeZone_ReturnsValidationProblem()
    {
        var response = await Client.GetAsync("/api/study/summary?tz=Mars/Olympus");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
