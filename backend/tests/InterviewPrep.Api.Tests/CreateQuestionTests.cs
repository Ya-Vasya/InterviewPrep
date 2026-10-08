using System.Net;
using System.Net.Http.Json;
using InterviewPrep.Api.Common;
using InterviewPrep.Api.Features.Questions;
using Microsoft.AspNetCore.Mvc;

namespace InterviewPrep.Api.Tests;

/// <summary>Tests that write data. Kept in their own class so they get their own database.</summary>
public class CreateQuestionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = QuestionEndpointsTests.Json;

    private readonly HttpClient _client = factory.CreateClient();

    private static CreateQuestionRequest Valid(
        string? topic = "CSharp", string? difficulty = "Junior", string? text = "What is boxing?",
        string? answer = "Wrapping a value type in an object.", IReadOnlyList<string>? tags = null) =>
        new(topic, text, answer, difficulty, tags);

    private Task<HttpResponseMessage> Post(CreateQuestionRequest request) =>
        _client.PostAsJsonAsync("/api/questions", request, Json);

    [Fact]
    public async Task CreateQuestion_ThenGetById_ReturnsIt()
    {
        var response = await Post(Valid());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<QuestionResponse>(Json);
        var fetched = await _client.GetFromJsonAsync<QuestionResponse>($"/api/questions/{created!.Id}", Json);

        Assert.Equal("What is boxing?", fetched!.Text);
        Assert.Equal(Topic.CSharp, fetched.Topic);
        Assert.Equal(Difficulty.Junior, fetched.Difficulty);
    }

    [Fact]
    public async Task CreateQuestion_IsRecordedAsManual_WithNormalizedTags()
    {
        var response = await Post(Valid(text: "  What is a closure?  ", tags: ["  Lambdas ", "lambdas", "Closures"]));

        var created = await response.Content.ReadFromJsonAsync<QuestionResponse>(Json);

        Assert.Equal(QuestionSource.Manual, created!.Source);
        Assert.Equal("What is a closure?", created.Text);
        Assert.Equal(["lambdas", "closures"], created.Tags);
    }

    [Fact]
    public async Task CreateQuestion_IncreasesTopicCount_AndIsFoundByFilters()
    {
        var before = await TopicCount(Topic.SystemDesign);

        await Post(Valid(topic: "systemdesign", difficulty: "Senior", text: "Design a rate limiter.", tags: ["rate-limiting"]));

        Assert.Equal(before + 1, await TopicCount(Topic.SystemDesign));
        var found = await _client.GetFromJsonAsync<List<QuestionResponse>>(
            "/api/questions?topic=SystemDesign&tag=rate-limiting", Json);
        Assert.Equal("Design a rate limiter.", Assert.Single(found!).Text);
    }

    [Theory]
    [InlineData("Cobol", "topic")]
    [InlineData("1", "topic")]
    [InlineData("C#", "topic")]
    [InlineData("", "topic")]
    [InlineData(null, "topic")]
    public async Task CreateQuestion_WithInvalidTopic_ReturnsValidationProblem(string? topic, string field)
    {
        var response = await Post(Valid(topic: topic));

        await AssertValidationProblem(response, field);
    }

    [Theory]
    [InlineData("Expert")]
    [InlineData("0")]
    [InlineData("")]
    [InlineData(null)]
    public async Task CreateQuestion_WithInvalidDifficulty_ReturnsValidationProblem(string? difficulty)
    {
        var response = await Post(Valid(difficulty: difficulty));

        await AssertValidationProblem(response, "difficulty");
    }

    [Fact]
    public async Task CreateQuestion_WithEmptyText_ReturnsValidationProblem()
    {
        var response = await Post(Valid(text: ""));

        await AssertValidationProblem(response, "text");
    }

    [Fact]
    public async Task CreateQuestion_WithEmptyAnswer_ReturnsValidationProblem()
    {
        var response = await Post(Valid(answer: "  "));

        await AssertValidationProblem(response, "answer");
    }

    [Fact]
    public async Task CreateQuestion_WithTooLongText_ReturnsValidationProblem()
    {
        var response = await Post(Valid(text: new string('x', QuestionLimits.TextMaxLength + 1)));

        await AssertValidationProblem(response, "text");
    }

    [Fact]
    public async Task CreateQuestion_WithTooManyTags_ReturnsValidationProblem()
    {
        var tags = Enumerable.Range(0, QuestionLimits.MaxTags + 1).Select(i => $"tag{i}").ToList();

        var response = await Post(Valid(tags: tags));

        await AssertValidationProblem(response, "tags");
    }

    [Fact]
    public async Task CreateQuestion_ReportsAllValidationErrorsAtOnce()
    {
        var response = await Post(new CreateQuestionRequest(null, "", "", null));

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);

        Assert.Equal(["answer", "difficulty", "text", "topic"], problem!.Errors.Keys.Order());
    }

    [Fact]
    public async Task CreateQuestion_WithInvalidInput_DoesNotStoreAnything()
    {
        var before = await TopicCount(Topic.CSharp);

        await Post(Valid(text: ""));

        Assert.Equal(before, await TopicCount(Topic.CSharp));
    }

    private async Task<int> TopicCount(Topic topic)
    {
        var topics = await _client.GetFromJsonAsync<List<TopicSummary>>("/api/topics", Json);
        return topics!.Single(t => t.Topic == topic).QuestionCount;
    }

    private static async Task AssertValidationProblem(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);
        Assert.Contains(field, problem!.Errors.Keys);
    }
}
