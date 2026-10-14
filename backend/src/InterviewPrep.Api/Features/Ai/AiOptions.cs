namespace InterviewPrep.Api.Features.Ai;

/// <summary>Limits, bound from the "Ai" configuration section.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";
    public const int DefaultDailyCap = 50;

    /// <summary>Most provider calls per day.</summary>
    public int DailyCap { get; set; } = DefaultDailyCap;

    /// <summary>How long the provider may take before a call counts as timed out.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Output budget per call, thinking included. Bounds the cost of one call.</summary>
    public int CheckMaxOutputTokens { get; set; } = 1500;
    public int ExplainMaxOutputTokens { get; set; } = 2000;
    public int GenerateMaxOutputTokens { get; set; } = 4000;

    public int MaxOutputTokensFor(AiFeature feature) => feature switch
    {
        AiFeature.CheckAnswer => CheckMaxOutputTokens,
        AiFeature.ExplainMistakes => ExplainMaxOutputTokens,
        _ => GenerateMaxOutputTokens
    };
}

/// <summary>Provider settings, bound from the "Anthropic" section. The key never leaves the backend.</summary>
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>User-secrets in development, an environment variable in production. Never committed.</summary>
    public string? ApiKey { get; set; }

    /// <summary>One model serves every AI feature.</summary>
    public string Model { get; set; } = "claude-sonnet-5-5";
}
