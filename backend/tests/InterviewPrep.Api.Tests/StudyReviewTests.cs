using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InterviewPrep.Api.Common;
using InterviewPrep.Api.Features.Study;
using Microsoft.AspNetCore.Mvc;

namespace InterviewPrep.Api.Tests;

/// <summary>Recording, idempotency and undo of reviews.</summary>
public class StudyReviewTests(ApiFactory factory) : StudyTestBase(factory), IClassFixture<ApiFactory>
{
    private async Task<Guid> FirstQuestionId(Topic topic = Topic.CSharp) => (await GetQuestions(topic))[0].Id;

    private Task<HttpResponseMessage> Undo(Guid reviewId) => Client.DeleteAsync($"/api/study/reviews/{reviewId}");

    [Fact]
    public async Task FirstRating_CreatesTheCardLazily_AndReturnsTheNewSchedule()
    {
        var questionId = await FirstQuestionId();
        Assert.Equal(0, CardCount());

        var reviewId = Guid.NewGuid();
        var result = await Rate(questionId, "Good", reviewId);

        Assert.Equal(reviewId, result.ReviewId);
        Assert.Equal(1, result.IntervalDays);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime.AddDays(1), result.DueAt);
        Assert.Equal(1, CardCount());
        Assert.Equal(1, ReviewCount());

        var card = GetCard(questionId);
        Assert.Equal(result.CardId, card.Id);
        Assert.Equal(1, card.Repetitions);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, card.LastReviewedAt);
    }

    [Fact]
    public async Task SecondGood_GivesSixDays_AndAQuestionNeverGetsASecondCard()
    {
        var questionId = await FirstQuestionId();

        var first = await Rate(questionId, "Good");
        Clock.Advance(TimeSpan.FromDays(1));
        var second = await Rate(questionId, "Good");

        Assert.Equal(first.CardId, second.CardId);
        Assert.Equal(6, second.IntervalDays);
        Assert.Equal(1, CardCount());
        Assert.Equal(2, ReviewCount());
    }

    [Fact]
    public async Task Again_MakesTheCardDueImmediately()
    {
        var questionId = await FirstQuestionId();

        var result = await Rate(questionId, "Again");

        Assert.Equal(0, result.IntervalDays);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, result.DueAt);
        Assert.Equal(1, GetCard(questionId).Lapses);
        Assert.Contains(await GetQueue("?limit=5"), i => i.QuestionId == questionId && !i.IsNew);
    }

    [Fact]
    public async Task Response_PreviewsTheIntervalsOfTheNextReview_AlsoWhenReplayed()
    {
        var questionId = await FirstQuestionId();
        var reviewId = Guid.NewGuid();

        var result = await Rate(questionId, "Again", reviewId);
        var replay = await Rate(questionId, "Again", reviewId);

        // After Again the card starts over: the next Good is 1 day, not the 6 a second Good would give.
        Assert.Equal(0, result.NextIntervals.Again);
        Assert.Equal(1, result.NextIntervals.Good);
        Assert.Equal(result.NextIntervals, replay.NextIntervals);
    }

    [Fact]
    public async Task Rating_IsCaseInsensitive_AndTheLogStoresTheRating()
    {
        var questionId = await FirstQuestionId();

        await Rate(questionId, "hArD");

        var log = WithDb(db => db.ReviewLogs.Single());
        Assert.Equal(Rating.Hard, log.Rating);
        Assert.Equal(ReviewSource.Self, log.Source);
        Assert.True(log.CardCreated);
    }

    [Fact]
    public async Task RepeatedRequestWithTheSameReviewId_RecordsOneReview_AndReturnsTheOriginalResult()
    {
        var questionId = await FirstQuestionId();
        var reviewId = Guid.NewGuid();

        var first = await Rate(questionId, "Good", reviewId);
        Clock.Advance(TimeSpan.FromMinutes(5));
        var second = await Rate(questionId, "Good", reviewId);

        Assert.Equal(first, second);
        Assert.Equal(1, ReviewCount());
        Assert.Equal(1, GetCard(questionId).Repetitions); // not rescheduled
    }

    [Fact]
    public async Task ConcurrentRequestsWithTheSameReviewId_RecordOneReview()
    {
        var questionId = await FirstQuestionId();
        var reviewId = Guid.NewGuid();

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => PostReview(questionId, "Good", reviewId)));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var bodies = await Task.WhenAll(results.Select(r => r.Content.ReadFromJsonAsync<ReviewResponse>(Json)));
        Assert.Single(bodies.Distinct());
        Assert.Equal(1, ReviewCount());
        Assert.Equal(1, CardCount());
        Assert.Equal(1, GetCard(questionId).Repetitions);
    }

    [Fact]
    public async Task ConcurrentFirstRatingsWithDifferentReviewIds_StillCreateOneCard()
    {
        var questionId = await FirstQuestionId();

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => PostReview(questionId, "Good")));

        Assert.All(results, r => Assert.Contains(r.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Equal(1, CardCount());
    }

    [Fact]
    public async Task SameReviewIdForAnotherRating_ReturnsConflict()
    {
        var questionId = await FirstQuestionId();
        var reviewId = Guid.NewGuid();
        await Rate(questionId, "Good", reviewId);

        var response = await PostReview(questionId, "Easy", reviewId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, ReviewCount());
    }

    [Fact]
    public async Task UnknownQuestion_ReturnsValidationProblem()
    {
        var response = await PostReview(Guid.NewGuid(), "Good");

        await AssertValidationProblem(response, "questionId");
        Assert.Equal(0, CardCount());
    }

    [Theory]
    [InlineData("Perfect")]
    [InlineData("2")]
    [InlineData("")]
    [InlineData(null)]
    public async Task UnknownOrMissingRating_ReturnsValidationProblem(string? rating)
    {
        var questionId = await FirstQuestionId();

        var response = await PostReview(questionId, rating!);

        await AssertValidationProblem(response, "rating");
        Assert.Equal(0, CardCount());
    }

    [Fact]
    public async Task MissingIds_ReturnValidationProblem()
    {
        var response = await Client.PostAsJsonAsync("/api/study/reviews", new { rating = "Good" }, Json);

        var problem = await AssertValidationProblem(response, "questionId");
        Assert.Contains("reviewId", problem.Errors.Keys);
    }

    [Fact]
    public async Task Undo_OfAFirstRating_RemovesTheCard_SoTheQuestionIsNewAgain()
    {
        var questionId = await FirstQuestionId();
        var reviewId = Guid.NewGuid();
        await Rate(questionId, "Good", reviewId);

        var response = await Undo(reviewId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, CardCount());
        Assert.Equal(0, ReviewCount());
        var item = (await GetQueue("?topic=CSharp&limit=100")).Single(i => i.QuestionId == questionId);
        Assert.True(item.IsNew);
        Assert.Equal(20, (await GetSummary()).NewRemainingToday); // the daily new-card slot is given back
    }

    [Fact]
    public async Task Undo_OfALaterRating_RestoresTheCardFromTheSnapshot()
    {
        var questionId = await FirstQuestionId();
        await Rate(questionId, "Good");
        var afterFirst = GetCard(questionId);

        Clock.Advance(TimeSpan.FromDays(1));
        var secondId = Guid.NewGuid();
        await Rate(questionId, "Again", secondId);
        Assert.Equal(1, GetCard(questionId).Lapses);

        Assert.Equal(HttpStatusCode.NoContent, (await Undo(secondId)).StatusCode);

        var restored = GetCard(questionId);
        Assert.Equal(afterFirst.Ease, restored.Ease);
        Assert.Equal(afterFirst.IntervalDays, restored.IntervalDays);
        Assert.Equal(afterFirst.Repetitions, restored.Repetitions);
        Assert.Equal(afterFirst.Lapses, restored.Lapses);
        Assert.Equal(afterFirst.DueAt, restored.DueAt);
        Assert.Equal(afterFirst.LastReviewedAt, restored.LastReviewedAt);
        Assert.Equal(1, ReviewCount());
    }

    [Fact]
    public async Task Undo_ThenRatingAgain_ReschedulesFromTheRestoredState()
    {
        var questionId = await FirstQuestionId();
        await Rate(questionId, "Good");
        Clock.Advance(TimeSpan.FromDays(1));
        var hardId = Guid.NewGuid();
        await Rate(questionId, "Hard", hardId);
        await Undo(hardId);

        var result = await Rate(questionId, "Good");

        Assert.Equal(6, result.IntervalDays);
    }

    [Fact]
    public async Task Undo_WithUnknownOrAlreadyUndoneReview_ReturnsNotFound()
    {
        var questionId = await FirstQuestionId();
        var reviewId = Guid.NewGuid();
        await Rate(questionId, "Good", reviewId);
        await Undo(reviewId);

        Assert.Equal(HttpStatusCode.NotFound, (await Undo(reviewId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Undo(Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task Undo_OfAReviewThatIsNotTheCardsLatest_ReturnsConflict()
    {
        var questionId = await FirstQuestionId();
        var firstId = Guid.NewGuid();
        await Rate(questionId, "Good", firstId);
        Clock.Advance(TimeSpan.FromDays(1));
        await Rate(questionId, "Good");

        var response = await Undo(firstId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(2, ReviewCount());
    }

    [Fact]
    public async Task Responses_UseStringEnumsAndUtcTimestamps()
    {
        var questionId = await FirstQuestionId();
        var response = await PostReview(questionId, "Good");

        using var review = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.EndsWith("Z", review.RootElement.GetProperty("dueAt").GetString());

        using var queue = JsonDocument.Parse(await Client.GetStringAsync("/api/study/queue?topic=CSharp&limit=1"));
        var item = queue.RootElement[0];
        Assert.Equal("CSharp", item.GetProperty("topic").GetString());
        Assert.True(item.TryGetProperty("difficulty", out var d) && d.ValueKind == JsonValueKind.String);
        Assert.True(item.GetProperty("isNew").GetBoolean());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("cardId").ValueKind);
    }

    private static async Task<ValidationProblemDetails> AssertValidationProblem(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);
        Assert.Contains(field, problem!.Errors.Keys);
        return problem;
    }
}
