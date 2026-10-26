using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace InterviewPrep.Api.Tests;

/// <summary>3a: accepting Drafts turns them into AI-sourced questions, validated like manual ones.</summary>
public class AcceptDraftsTests(ApiFactory factory) : AiTestBase(factory), IClassFixture<ApiFactory>
{
    private static CreateQuestionRequest Draft(
        string text = "What does ConfigureAwait do?", string topic = "CSharp", string answer = "It controls the context.",
        string difficulty = "Middle", params string[] tags) =>
        new(topic, text, answer, difficulty, tags);

    private Task<HttpResponseMessage> PostAccept(params CreateQuestionRequest?[] drafts) =>
        Client.PostAsJsonAsync("/api/ai/questions/accept", new AcceptDraftsRequest(drafts), Json);

    private async Task<AcceptDraftsResponse> Accept(params CreateQuestionRequest?[] drafts)
    {
        var response = await PostAccept(drafts);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AcceptDraftsResponse>(Json))!;
    }

    private int QuestionCount() => WithDb(db => db.Questions.Count());

    [Fact]
    public async Task Accept_SavesTheDraftsAsAiQuestions_WithNormalizedTags()
    {
        var result = await Accept(
            Draft("  First draft?  ", tags: [" Async ", "TASKS", "async"]),
            Draft("Second draft?", "Sql", difficulty: "Senior"));

        Assert.Empty(result.Rejected);
        Assert.Equal(2, result.Accepted.Count);
        Assert.All(result.Accepted, q => Assert.Equal(QuestionSource.Ai, q.Source));
        Assert.Equal("First draft?", result.Accepted[0].Text);
        Assert.Equal(["async", "tasks"], result.Accepted[0].Tags);

        var stored = (await GetQuestions(Topic.Sql)).Single(q => q.Text == "Second draft?");
        Assert.Equal(QuestionSource.Ai, stored.Source);
        Assert.Equal(Difficulty.Senior, stored.Difficulty);
    }

    [Fact]
    public async Task Accept_ReportsAnInvalidDraftByPosition_AndStillSavesTheOthers()
    {
        var before = QuestionCount();

        var result = await Accept(
            Draft(text: ""),
            Draft("A good one?"),
            Draft("Bad topic?", topic: "Cobol"),
            Draft("No answer?", answer: " "));

        Assert.Equal(["A good one?"], result.Accepted.Select(q => q.Text));
        Assert.Equal([0, 2, 3], result.Rejected.Select(r => r.Index));
        Assert.Contains("text", result.Rejected[0].Errors.Keys);
        Assert.Contains("topic", result.Rejected[1].Errors.Keys);
        Assert.Contains("answer", result.Rejected[2].Errors.Keys);
        Assert.Equal(before + 1, QuestionCount());
    }

    [Fact]
    public async Task Accept_UsesTheSameRulesAsManualCreation()
    {
        var result = await Accept(
            Draft(new string('q', QuestionLimits.TextMaxLength + 1)),
            Draft("Too many tags?", tags: Enumerable.Range(0, QuestionLimits.MaxTags + 1).Select(i => $"t{i}").ToArray()));

        Assert.Empty(result.Accepted);
        Assert.Equal(2, result.Rejected.Count);
    }

    [Fact]
    public async Task Accept_RejectsAnExistingQuestion_IgnoringCaseSpacingAndPunctuation()
    {
        var existing = (await GetQuestions(Topic.CSharp))[0].Text;
        var before = QuestionCount();

        var result = await Accept(Draft(" " + existing.ToUpperInvariant().TrimEnd('?') + " "), Draft("Fresh one?"));

        Assert.Equal(["Fresh one?"], result.Accepted.Select(q => q.Text));
        var rejected = Assert.Single(result.Rejected);
        Assert.Equal(0, rejected.Index);
        Assert.Contains("already exists", rejected.Errors["text"][0]);
        Assert.Equal(before + 1, QuestionCount());
    }

    [Fact]
    public async Task Accept_TheSameDraftTwice_CreatesNoSecondCopy()
    {
        var before = QuestionCount();

        var first = await Accept(Draft("Double click?"));
        var second = await Accept(Draft("Double click?"));

        Assert.Single(first.Accepted);
        Assert.Empty(second.Accepted);
        Assert.Single(second.Rejected);
        Assert.Equal(before + 1, QuestionCount());
    }

    [Fact]
    public async Task Accept_TheSameTextTwiceInOneRequest_KeepsTheFirst()
    {
        var result = await Accept(Draft("Twice?"), Draft("twice"));

        Assert.Single(result.Accepted);
        Assert.Equal(1, Assert.Single(result.Rejected).Index);
    }

    [Fact]
    public async Task Accept_AllowsTheSameTextInAnotherTopic()
    {
        var sqlText = (await GetQuestions(Topic.Sql))[0].Text;

        var result = await Accept(Draft(sqlText, "CSharp"));

        Assert.Single(result.Accepted);
    }

    [Fact]
    public async Task Accept_WithoutADraft_OrWithTooMany_IsAValidationProblem()
    {
        var none = await PostAccept();
        var tooMany = await PostAccept(Enumerable.Range(0, 11).Select(i => Draft($"Draft {i}?")).ToArray());
        var missing = await Client.PostAsJsonAsync("/api/ai/questions/accept", new AcceptDraftsRequest(null), Json);

        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task Accept_ReportsANullDraft_AsRejected()
    {
        var result = await Accept(null, Draft("Still saved?"));

        Assert.Single(result.Accepted);
        Assert.Equal(0, Assert.Single(result.Rejected).Index);
    }

    [Fact]
    public async Task AnAcceptedQuestion_JoinsTheStudyQueue_AndTheQueueTellsItIsAiSourced()
    {
        await Accept(Draft("Queue me?", "Sql"));

        var queue = await GetQueue("?topic=Sql&limit=100");

        Assert.Equal(QuestionSource.Ai, queue.Single(i => i.Text == "Queue me?").Source);
        Assert.All(queue.Where(i => i.Text != "Queue me?"), i => Assert.Equal(QuestionSource.Seed, i.Source));
    }

    [Fact]
    public async Task Accept_NeedsNoProviderCall_AndCostsNothing()
    {
        Ai.IsConfigured = false;

        var result = await Accept(Draft("Offline accept?"));

        Assert.Single(result.Accepted);
        Assert.Empty(Ai.Requests);
        Assert.Equal(0, (await GetAiStatus()).UsedToday);
    }

    [Fact]
    public async Task ManualCreation_StillRecordsManual_AndAllowsAnExistingText()
    {
        var existing = (await GetQuestions(Topic.CSharp))[0];

        var response = await Client.PostAsJsonAsync(
            "/api/questions", Draft(existing.Text), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<QuestionResponse>(Json);
        Assert.Equal(QuestionSource.Manual, created!.Source);
        WithDb(db => db.Questions.Where(q => q.Id == created.Id).ExecuteDelete());
    }
}
