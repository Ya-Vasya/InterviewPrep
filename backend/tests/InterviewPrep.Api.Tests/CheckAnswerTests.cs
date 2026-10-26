using System.Net;
using System.Net.Http.Json;

namespace InterviewPrep.Api.Tests;

/// <summary>3b: the AI checks the user's typed answer against the reference answer.</summary>
public class CheckAnswerTests(ApiFactory factory) : AiTestBase(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Check_ReturnsTheVerdict_AndSuggestsARating()
    {
        var questionId = await AnyQuestionId();
        Ai.Reply(CheckReply("partial", 55));

        var response = await PostCheck(questionId, "Singletons live for the whole app.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content.ReadFromJsonAsync<CheckAnswerResponse>(Json))!;
        Assert.NotEqual(Guid.Empty, result.AttemptId);
        Assert.Equal(Verdict.Partial, result.Verdict);
        Assert.Equal(55, result.Score);
        Assert.Equal(["Singleton lifetime"], result.Covered);
        Assert.Equal(["Scoped vs transient"], result.Missing);
        Assert.Equal("Good start.", result.Feedback);
        Assert.Equal(Rating.Hard, result.SuggestedRating);
    }

    [Theory]
    [InlineData("correct", Rating.Good)]
    [InlineData("partial", Rating.Hard)]
    [InlineData("incorrect", Rating.Again)]
    public async Task SuggestedRating_FollowsTheVerdict(string verdict, Rating expected)
    {
        Ai.Reply(CheckReply(verdict));

        var result = (await (await PostCheck(await AnyQuestionId(), "x")).Content
            .ReadFromJsonAsync<CheckAnswerResponse>(Json))!;

        Assert.Equal(expected, result.SuggestedRating);
    }

    [Fact]
    public async Task Check_CountsAsOneProviderCall()
    {
        Ai.Reply(CheckReply());

        await PostCheck(await AnyQuestionId(), "x");

        Assert.Single(Ai.Requests);
        Assert.Equal(1, (await GetAiStatus()).UsedToday);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyAnswer_IsAValidationProblem_AndNothingIsSpent(string? answer)
    {
        var response = await PostCheck(await AnyQuestionId(), answer);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Ai.Requests);
        Assert.Equal(0, (await GetAiStatus()).UsedToday);
    }

    [Fact]
    public async Task AnswerLongerThan4000Characters_IsAValidationProblem()
    {
        var questionId = await AnyQuestionId();
        Ai.Reply(CheckReply());

        Assert.Equal(HttpStatusCode.BadRequest, (await PostCheck(questionId, new string('a', 4001))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostCheck(questionId, new string('a', 4000))).StatusCode);
    }

    [Fact]
    public async Task UnknownQuestion_IsAValidationProblem()
    {
        var response = await PostCheck(Guid.NewGuid(), "x");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Ai.Requests);
    }

    [Fact]
    public async Task UnknownTimeZone_IsAValidationProblem()
    {
        var response = await PostCheck(await AnyQuestionId(), "x", "?tz=Mars/Olympus");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(Ai.Requests);
    }

    [Fact]
    public async Task Check_NeverRatesTheCard()
    {
        Ai.Reply(CheckReply("correct", 95));

        await PostCheck(await AnyQuestionId(), "a perfect answer");

        Assert.Equal(0, CardCount());
        Assert.Equal(0, ReviewCount());
    }
}
