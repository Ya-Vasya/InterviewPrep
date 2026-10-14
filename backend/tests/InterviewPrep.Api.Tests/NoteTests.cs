using System.Net;
using System.Net.Http.Json;
using InterviewPrep.Api.Features.Notes;

namespace InterviewPrep.Api.Tests;

/// <summary>A Note is the user's saved text for a question. It belongs to the question, not to a card (ADR 0001).</summary>
public class NoteTests(ApiFactory factory) : AiTestBase(factory), IClassFixture<ApiFactory>
{
    private Task<HttpResponseMessage> PutNote(Guid questionId, string? text) =>
        Client.PutAsJsonAsync($"/api/questions/{questionId}/note", new SaveNoteRequest(text), Json);

    private Task<HttpResponseMessage> GetNote(Guid questionId) => Client.GetAsync($"/api/questions/{questionId}/note");

    private static async Task<NoteResponse> Read(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<NoteResponse>(Json))!;

    [Fact]
    public async Task AQuestionWithoutANote_Answers404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await GetNote(await AnyQuestionId())).StatusCode);
    }

    [Fact]
    public async Task SavedNote_CanBeReadBack_BeforeAnyCardExists()
    {
        var questionId = await AnyQuestionId();

        var saved = await PutNote(questionId, "Scoped lives per request.");

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var note = await Read(await GetNote(questionId));
        Assert.Equal(questionId, note.QuestionId);
        Assert.Equal("Scoped lives per request.", note.Text);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, note.UpdatedAt);
        Assert.Equal(0, CardCount()); // saving a note never creates a card
    }

    [Fact]
    public async Task SavingAgain_ReplacesTheNote()
    {
        var questionId = await AnyQuestionId();
        await PutNote(questionId, "first");
        Clock.Advance(TimeSpan.FromMinutes(5));

        await PutNote(questionId, "second");

        var note = await Read(await GetNote(questionId));
        Assert.Equal("second", note.Text);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, note.UpdatedAt);
        Assert.Equal(1, WithDb(db => db.Notes.Count()));
    }

    [Fact]
    public async Task NotesAreKeptPerQuestion()
    {
        var csharp = await AnyQuestionId(Api.Common.Topic.CSharp);
        var sql = await AnyQuestionId(Api.Common.Topic.Sql);

        await PutNote(csharp, "about c#");

        Assert.Equal(HttpStatusCode.NotFound, (await GetNote(sql)).StatusCode);
    }

    [Fact]
    public async Task NoteSurvives_SuspendingTheCard()
    {
        var questionId = await AnyQuestionId();
        var review = await Rate(questionId, "Good");
        await PutNote(questionId, "keep me");

        var suspended = await Client.PostAsync($"/api/study/cards/{review.CardId}/suspend", null);

        Assert.Equal(HttpStatusCode.NoContent, suspended.StatusCode);
        Assert.Equal("keep me", (await Read(await GetNote(questionId))).Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyNote_IsAValidationProblem(string? text)
    {
        var questionId = await AnyQuestionId();
        await PutNote(questionId, "existing");

        var response = await PutNote(questionId, text);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("existing", (await Read(await GetNote(questionId))).Text);
    }

    [Fact]
    public async Task TooLongNote_IsAValidationProblem()
    {
        var questionId = await AnyQuestionId();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await PutNote(questionId, new string('a', NoteLimits.MaxTextLength + 1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await PutNote(questionId, new string('a', NoteLimits.MaxTextLength))).StatusCode);
    }

    [Fact]
    public async Task UnknownQuestion_Answers404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await PutNote(Guid.NewGuid(), "x")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await GetNote(Guid.NewGuid())).StatusCode);
    }
}
