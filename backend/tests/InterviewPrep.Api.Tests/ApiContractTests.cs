using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;

namespace InterviewPrep.Api.Tests;

/// <summary>
/// Guards the HTTP contract. The OpenAPI snapshot makes every change to routes, request or response
/// shapes show up in a pull request diff: review <c>*.verified.json</c> changes, then approve them.
/// </summary>
public class ApiContractTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OpenApiDocument_MatchesApprovedSnapshot()
    {
        var json = await _client.GetStringAsync("/openapi/v1.json");

        var indented = JsonNode.Parse(json)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        await Verify(indented, "json");
    }

    [Theory]
    [InlineData("/api/questions/6f0b6a5e-0000-4000-8000-000000000000")]
    [InlineData("/api/does-not-exist")]
    public async Task NotFound_IsProblemDetails(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(404, problem!.Status);
    }

    [Fact]
    public async Task ValidationErrors_AreProblemDetailsToo()
    {
        var response = await _client.GetAsync("/api/questions?topic=Cobol");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
