using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewPrep.Api.Tests;

/// <summary>
/// Shared helpers for study tests. Tests in one class share a database, so each test starts by
/// wiping cards and reviews and rewinding the clock.
/// </summary>
public abstract class StudyTestBase
{
    protected static readonly System.Text.Json.JsonSerializerOptions Json = QuestionEndpointsTests.Json;

    protected readonly ApiFactory Factory;
    protected readonly HttpClient Client;

    protected FakeClock Clock => Factory.Clock;

    protected StudyTestBase(ApiFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
        factory.ResetStudyData();
    }

    protected async Task<List<QuestionResponse>> GetQuestions(Topic topic) =>
        (await Client.GetFromJsonAsync<PagedResponse<QuestionResponse>>(
            $"/api/questions?topic={topic}&limit={Factory.Services.GetRequiredService<IOptions<PagingOptions>>().Value.MaxLimit}", Json))!.Items.ToList();

    protected Task<HttpResponseMessage> PostReview(Guid questionId, string rating, Guid? reviewId = null) =>
        Client.PostAsJsonAsync(
            "/api/study/reviews",
            new ReviewRequest(questionId, rating, reviewId ?? Guid.NewGuid()),
            Json);

    /// <summary>Records a rating and returns its response, failing the test if it was rejected.</summary>
    protected async Task<ReviewResponse> Rate(Guid questionId, string rating, Guid? reviewId = null)
    {
        var response = await PostReview(questionId, rating, reviewId);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReviewResponse>(Json))!;
    }

    protected async Task<List<StudyQueueItem>> GetQueue(string query = "") =>
        (await Client.GetFromJsonAsync<List<StudyQueueItem>>($"/api/study/queue{query}", Json))!;

    protected async Task<StudySummaryResponse> GetSummary(string query = "") =>
        (await Client.GetFromJsonAsync<StudySummaryResponse>($"/api/study/summary{query}", Json))!;

    protected T WithDb<T>(Func<AppDbContext, T> query)
    {
        using var scope = Factory.Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected int CardCount() => WithDb(db => db.Cards.Count());

    protected int ReviewCount() => WithDb(db => db.ReviewLogs.Count());

    protected Card GetCard(Guid questionId) => WithDb(db => db.Cards.Single(c => c.QuestionId == questionId));
}
