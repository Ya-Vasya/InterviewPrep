using System.Net.Http.Json;

namespace InterviewPrep.Api.Tests;

/// <summary>Shared helpers for AI tests: a clean slate, the scripted provider and the status call.</summary>
public abstract class AiTestBase : StudyTestBase
{
    protected FakeAiProvider Ai => Factory.Ai;

    protected AiTestBase(ApiFactory factory) : base(factory) => factory.ResetAiData();

    protected async Task<AiStatusResponse> GetAiStatus(string query = "") =>
        (await Client.GetFromJsonAsync<AiStatusResponse>($"/api/ai/status{query}", Json))!;

    protected Task<HttpResponseMessage> PostCheck(Guid questionId, string? answer, string query = "") =>
        Client.PostAsJsonAsync($"/api/ai/answers/check{query}", new CheckAnswerRequest(questionId, answer), Json);

    protected Task<HttpResponseMessage> PostExplain(Guid? attemptId, string query = "") =>
        Client.PostAsJsonAsync($"/api/ai/answers/explain{query}", new ExplainMistakesRequest(attemptId), Json);

    /// <summary>Runs a check the fake model answers with <paramref name="verdict"/> and returns the stored attempt's id.</summary>
    protected async Task<Guid> CheckedAttempt(Guid questionId, string verdict, string answer = "my answer")
    {
        Ai.Reply(CheckReply(verdict));
        var response = await PostCheck(questionId, answer);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CheckAnswerResponse>(Json))!.AttemptId;
    }

    protected async Task<Guid> AnyQuestionId(Topic topic = Topic.CSharp) => (await GetQuestions(topic))[0].Id;

    /// <summary>The JSON a well-behaved model returns for a check.</summary>
    protected static string CheckReply(string verdict = "partial", int score = 55) =>
        $$"""{"verdict":"{{verdict}}","score":{{score}},"covered":["Singleton lifetime"],"missing":["Scoped vs transient"],"feedback":"Good start."}""";

    /// <summary>Where <paramref name="needle"/> occurs in <paramref name="text"/>, for "appears exactly once" checks.</summary>
    protected static IEnumerable<int> OccurrencesOf(string text, string needle)
    {
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = text.IndexOf(needle, i + 1, StringComparison.Ordinal))
            yield return i;
    }

    /// <summary>The JSON a well-behaved model returns for an explanation.</summary>
    protected const string ExplainMarkdown = "### Scoped vs transient\n**Why:** a scoped service lives per request.\n\n```csharp\nservices.AddScoped<IFoo, Foo>();\n```\n\nFollow-up: when would you pick transient?";

    protected static string ExplainReply(string markdown = ExplainMarkdown) =>
        System.Text.Json.JsonSerializer.Serialize(new { explanation = markdown });
}
