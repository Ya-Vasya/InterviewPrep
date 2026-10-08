using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InterviewPrep.Api.Common;
using InterviewPrep.Api.Features.Questions;
using Microsoft.AspNetCore.Mvc;

namespace InterviewPrep.Api.Tests;

/// <summary>Read-only tests against the seeded database.</summary>
public class QuestionEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    private Task<List<QuestionResponse>?> GetQuestions(string query = "") =>
        _client.GetFromJsonAsync<List<QuestionResponse>>($"/api/questions{query}", Json);

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTopics_ReturnsExactlyTheFourTopicsWithCounts()
    {
        var topics = await _client.GetFromJsonAsync<List<TopicSummary>>("/api/topics", Json);

        Assert.NotNull(topics);
        Assert.Equal(
            [Topic.CSharp, Topic.AspNetCore, Topic.Sql, Topic.SystemDesign],
            topics.Select(t => t.Topic));
        Assert.Equal(["C#", "ASP.NET Core", "SQL", "System Design"], topics.Select(t => t.Name));
    }

    [Fact]
    public async Task SeedData_HasAtLeastTenQuestionsPerTopic()
    {
        var topics = await _client.GetFromJsonAsync<List<TopicSummary>>("/api/topics", Json);
        var all = await GetQuestions();

        Assert.NotNull(topics);
        Assert.NotNull(all);
        Assert.All(topics, t =>
        {
            Assert.True(t.QuestionCount >= 10, $"{t.Topic} has {t.QuestionCount} questions");
            Assert.Equal(t.QuestionCount, all.Count(q => q.Topic == t.Topic));
        });
        Assert.All(all, q => Assert.Equal(QuestionSource.Seed, q.Source));
    }

    [Fact]
    public async Task GetQuestions_FiltersByTopic()
    {
        var questions = await GetQuestions("?topic=Sql");

        Assert.NotNull(questions);
        Assert.NotEmpty(questions);
        Assert.All(questions, q => Assert.Equal(Topic.Sql, q.Topic));
    }

    [Fact]
    public async Task GetQuestions_TopicFilterIsCaseInsensitive()
    {
        var questions = await GetQuestions("?topic=csharp");

        Assert.NotNull(questions);
        Assert.NotEmpty(questions);
        Assert.All(questions, q => Assert.Equal(Topic.CSharp, q.Topic));
    }

    [Fact]
    public async Task GetQuestions_FiltersByDifficulty()
    {
        var questions = await GetQuestions("?difficulty=Senior");

        Assert.NotNull(questions);
        Assert.NotEmpty(questions);
        Assert.All(questions, q => Assert.Equal(Difficulty.Senior, q.Difficulty));
    }

    [Fact]
    public async Task GetQuestions_FiltersByTag_CaseInsensitively()
    {
        var questions = await GetQuestions("?tag=ASYNC");

        Assert.NotNull(questions);
        Assert.NotEmpty(questions);
        Assert.All(questions, q => Assert.Contains("async", q.Tags));
    }

    [Fact]
    public async Task GetQuestions_CombinesFiltersWithAnd()
    {
        var combined = await GetQuestions("?topic=CSharp&difficulty=Middle&tag=async");
        var topicOnly = await GetQuestions("?topic=CSharp");

        Assert.NotNull(combined);
        Assert.NotNull(topicOnly);
        Assert.NotEmpty(combined);
        Assert.True(combined.Count < topicOnly.Count);
        Assert.All(combined, q =>
        {
            Assert.Equal(Topic.CSharp, q.Topic);
            Assert.Equal(Difficulty.Middle, q.Difficulty);
            Assert.Contains("async", q.Tags);
        });
    }

    [Fact]
    public async Task GetQuestions_WithNoMatches_ReturnsEmptyList()
    {
        var questions = await GetQuestions("?tag=no-such-tag");

        Assert.NotNull(questions);
        Assert.Empty(questions);
    }

    [Theory]
    [InlineData("?topic=Cobol", "topic")]
    [InlineData("?topic=1", "topic")]
    [InlineData("?difficulty=Expert", "difficulty")]
    public async Task GetQuestions_WithUnknownFilterValue_ReturnsValidationProblem(string query, string field)
    {
        var response = await _client.GetAsync($"/api/questions{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);
        Assert.Contains(field, problem!.Errors.Keys);
    }

    [Fact]
    public async Task EfCoreQuestions_LiveUnderSqlWithEfCoreTag()
    {
        var questions = await GetQuestions("?tag=ef core");

        Assert.NotNull(questions);
        Assert.NotEmpty(questions);
        Assert.All(questions, q => Assert.Equal(Topic.Sql, q.Topic));
    }

    [Fact]
    public async Task GetQuestionById_WithUnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/questions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Questions_AreSerializedWithStringEnums()
    {
        var json = await _client.GetStringAsync("/api/questions?topic=CSharp&difficulty=Junior");

        using var doc = JsonDocument.Parse(json);
        var first = doc.RootElement[0];
        Assert.Equal("CSharp", first.GetProperty("topic").GetString());
        Assert.Equal("Junior", first.GetProperty("difficulty").GetString());
        Assert.Equal("Seed", first.GetProperty("source").GetString());
    }
}
