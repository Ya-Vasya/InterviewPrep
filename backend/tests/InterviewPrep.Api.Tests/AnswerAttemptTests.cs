using System.Net;
using System.Net.Http.Json;
using InterviewPrep.Api.Common;
using InterviewPrep.Api.Features.Ai;
using InterviewPrep.Api.Features.Study;

namespace InterviewPrep.Api.Tests;

/// <summary>Answer attempts are kept, and an accepted suggestion marks the review as AI-assisted.</summary>
public class AnswerAttemptTests(ApiFactory factory) : AiTestBase(factory), IClassFixture<ApiFactory>
{
    private async Task<CheckAnswerResponse> Check(Guid questionId, string answer, string verdict = "partial")
    {
        Ai.Reply(CheckReply(verdict));
        var response = await PostCheck(questionId, answer);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CheckAnswerResponse>(Json))!;
    }

    private async Task<PagedResponse<AnswerAttemptResponse>> GetAttempts(Guid questionId, string extraQuery = "") =>
        (await Client.GetFromJsonAsync<PagedResponse<AnswerAttemptResponse>>(
            $"/api/ai/answers/attempts?questionId={questionId}{extraQuery}", Json))!;

    private Task<HttpResponseMessage> PostReviewWithAttempt(Guid questionId, string rating, Guid? attemptId, Guid reviewId) =>
        Client.PostAsJsonAsync("/api/study/reviews", new ReviewRequest(questionId, rating, reviewId, attemptId), Json);

    private ReviewSource SourceOf(Guid reviewId) => WithDb(db => db.ReviewLogs.Single(r => r.ReviewId == reviewId).Source);

    // ---- stored attempts ----

    [Fact]
    public async Task EveryCheck_IsKept_NewestFirst_WithTheUsersAnswerAndTheResult()
    {
        var questionId = await AnyQuestionId();
        var first = await Check(questionId, "first try", "incorrect");
        Clock.Advance(TimeSpan.FromMinutes(5));
        var second = await Check(questionId, "second try", "correct");

        var page = await GetAttempts(questionId);
        var attempts = page.Items;

        Assert.Equal(2, page.Total);
        Assert.Equal([second.AttemptId, first.AttemptId], attempts.Select(a => a.Id));
        Assert.Equal("second try", attempts[0].Answer);
        Assert.Equal(Verdict.Correct, attempts[0].Verdict);
        Assert.Equal(55, attempts[0].Score);
        Assert.Equal(["Singleton lifetime"], attempts[0].Covered);
        Assert.Equal(["Scoped vs transient"], attempts[0].Missing);
        Assert.Equal("Good start.", attempts[0].Feedback);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, attempts[0].CreatedAt);
        Assert.Equal("first try", attempts[1].Answer);
        Assert.Equal(Verdict.Incorrect, attempts[1].Verdict);
    }

    [Fact]
    public async Task Attempts_AreListedPerQuestion()
    {
        var csharp = await AnyQuestionId(Topic.CSharp);
        var sql = await AnyQuestionId(Topic.Sql);
        await Check(csharp, "about c#");

        var forSql = await GetAttempts(sql);

        Assert.Empty(forSql.Items);
        Assert.Equal(0, forSql.Total);
    }

    [Fact]
    public async Task Attempts_ArePaged_NewestFirst_WithAStableTotal()
    {
        var questionId = await AnyQuestionId();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add((await Check(questionId, $"try {i}")).AttemptId);
            Clock.Advance(TimeSpan.FromMinutes(1));
        }

        var firstPage = await GetAttempts(questionId, "&limit=2");
        var secondPage = await GetAttempts(questionId, "&limit=2&offset=2");

        Assert.Equal(3, firstPage.Total);
        Assert.Equal(2, firstPage.Limit);
        Assert.Equal([ids[2], ids[1]], firstPage.Items.Select(a => a.Id));
        Assert.Equal([ids[0]], secondPage.Items.Select(a => a.Id));
        Assert.Equal(2, secondPage.Offset);
    }

    [Fact]
    public async Task ListingAttempts_RejectsAnOutOfRangeLimit()
    {
        var response = await Client.GetAsync($"/api/ai/answers/attempts?questionId={await AnyQuestionId()}&limit=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListingAttempts_NeedsAQuestionId()
    {
        var response = await Client.GetAsync("/api/ai/answers/attempts");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- accepting the suggestion ----

    [Fact]
    public async Task RatingWhatTheAiSuggested_IsRecordedAsAiAssisted()
    {
        var questionId = await AnyQuestionId();
        var attempt = await Check(questionId, "x", "partial"); // suggests Hard
        var reviewId = Guid.NewGuid();

        var response = await PostReviewWithAttempt(questionId, "Hard", attempt.AttemptId, reviewId);

        response.EnsureSuccessStatusCode();
        Assert.Equal(ReviewSource.Ai, SourceOf(reviewId));
    }

    [Fact]
    public async Task OverridingTheSuggestion_IsRecordedAsSelfRated()
    {
        var questionId = await AnyQuestionId();
        var attempt = await Check(questionId, "x", "partial"); // suggests Hard
        var reviewId = Guid.NewGuid();

        await PostReviewWithAttempt(questionId, "Easy", attempt.AttemptId, reviewId);

        Assert.Equal(ReviewSource.Self, SourceOf(reviewId));
    }

    [Fact]
    public async Task AnAttemptForAnotherQuestion_DoesNotMakeTheReviewAiAssisted()
    {
        var questionId = await AnyQuestionId(Topic.CSharp);
        var otherQuestionId = await AnyQuestionId(Topic.Sql);
        var attempt = await Check(otherQuestionId, "x", "correct"); // suggests Good
        var reviewId = Guid.NewGuid();

        await PostReviewWithAttempt(questionId, "Good", attempt.AttemptId, reviewId);

        Assert.Equal(ReviewSource.Self, SourceOf(reviewId));
    }

    [Fact]
    public async Task AnUnknownAttemptId_IsIgnored_ItIsASelfRating()
    {
        var questionId = await AnyQuestionId();
        var reviewId = Guid.NewGuid();

        var response = await PostReviewWithAttempt(questionId, "Good", Guid.NewGuid(), reviewId);

        response.EnsureSuccessStatusCode();
        Assert.Equal(ReviewSource.Self, SourceOf(reviewId));
    }

    [Fact]
    public async Task ARatingWithoutAnAttempt_IsSelfRated()
    {
        var questionId = await AnyQuestionId();
        var reviewId = Guid.NewGuid();

        await Rate(questionId, "Good", reviewId);

        Assert.Equal(ReviewSource.Self, SourceOf(reviewId));
    }

    [Fact]
    public async Task ARepeatedReview_KeepsItsOriginalSource()
    {
        var questionId = await AnyQuestionId();
        var attempt = await Check(questionId, "x", "partial");
        var reviewId = Guid.NewGuid();

        await PostReviewWithAttempt(questionId, "Hard", attempt.AttemptId, reviewId);
        var replay = await PostReviewWithAttempt(questionId, "Hard", null, reviewId);

        replay.EnsureSuccessStatusCode();
        Assert.Equal(1, ReviewCount());
        Assert.Equal(ReviewSource.Ai, SourceOf(reviewId));
    }
}
