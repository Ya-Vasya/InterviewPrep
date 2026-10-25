namespace InterviewPrep.Api.Features.Ai;

/// <summary>Limits, bound from the "Ai" configuration section.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";
    public const int DefaultDailyCap = 50;

    /// <summary>Most provider calls per day.</summary>
    public int DailyCap { get; set; } = DefaultDailyCap;

    /// <summary>How long the provider may take before a check or explain call counts as timed out.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Generation writes up to ten long answers in one call, so it gets longer.</summary>
    public int GenerateTimeoutSeconds { get; set; } = 120;

    /// <summary>Output budget per call, thinking included. Bounds the cost of one call.</summary>
    public int CheckMaxOutputTokens { get; set; } = 1500;
    public int ExplainMaxOutputTokens { get; set; } = 2000;

    /// <summary>Generation scales with the number of questions: this much per question plus the base.</summary>
    public int GenerateTokensPerQuestion { get; set; } = 800;
    public int GenerateBaseOutputTokens { get; set; } = 500;

    public int GenerateOutputTokensFor(int count) => GenerateBaseOutputTokens + GenerateTokensPerQuestion * count;

    public int TimeoutSecondsFor(AiFeature feature) =>
        feature == AiFeature.GenerateQuestions ? GenerateTimeoutSeconds : TimeoutSeconds;
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
