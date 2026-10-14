using System.Net.Http.Json;
using InterviewPrep.Api.Common;
using InterviewPrep.Api.Features.Ai;

namespace InterviewPrep.Api.Tests;

/// <summary>Shared helpers for AI tests: a clean slate, the scripted provider and the status call.</summary>
public abstract class AiTestBase : StudyTestBase
{
    protected FakeAiService Ai => Factory.Ai;

    protected AiTestBase(ApiFactory factory) : base(factory) => factory.ResetAiData();

    protected async Task<AiStatusResponse> GetAiStatus(string query = "") =>
        (await Client.GetFromJsonAsync<AiStatusResponse>($"/api/ai/status{query}", Json))!;

    protected Task<HttpResponseMessage> PostCheck(Guid questionId, string? answer, string query = "") =>
        Client.PostAsJsonAsync($"/api/ai/answers/check{query}", new CheckAnswerRequest(questionId, answer), Json);

    protected async Task<Guid> AnyQuestionId(Topic topic = Topic.CSharp) => (await GetQuestions(topic))[0].Id;

    /// <summary>The JSON a well-behaved model returns for a check.</summary>
    protected static string CheckReply(string verdict = "partial", int score = 55) =>
        $$"""{"verdict":"{{verdict}}","score":{{score}},"covered":["Singleton lifetime"],"missing":["Scoped vs transient"],"feedback":"Good start."}""";
}
