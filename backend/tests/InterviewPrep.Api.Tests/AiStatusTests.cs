using System.Net.Http.Json;
using InterviewPrep.Api.Features.Ai;

namespace InterviewPrep.Api.Tests;

/// <summary>The real wiring, with no API key configured.</summary>
public sealed class NoApiKeyFactory : ApiFactory
{
    protected override bool UseFakeAi => false;

    protected override IReadOnlyDictionary<string, string?> Settings { get; } =
        new Dictionary<string, string?> { ["Anthropic:ApiKey"] = "" };
}

public class AiStatusWithoutKeyTests(NoApiKeyFactory factory) : IClassFixture<NoApiKeyFactory>
{
    [Fact]
    public async Task Status_WithoutApiKey_ReportsAiAsDisabled()
    {
        var status = await factory.CreateClient()
            .GetFromJsonAsync<AiStatusResponse>("/api/ai/status", QuestionEndpointsTests.Json);

        Assert.False(status!.Enabled);
        Assert.Equal(0, status.UsedToday);
        Assert.Equal(AiOptions.DefaultDailyCap, status.DailyCap);
    }
}

public class AiStatusTests(ApiFactory factory) : AiTestBase(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Status_WithAProvider_ReportsAiAsEnabled_WithTheDefaultCap()
    {
        var status = await GetAiStatus();

        Assert.True(status.Enabled);
        Assert.Equal(0, status.UsedToday);
        Assert.Equal(50, status.DailyCap);
    }
}
