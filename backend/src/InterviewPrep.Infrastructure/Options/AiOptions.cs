using InterviewPrep.Data.Models.Enums;

namespace InterviewPrep.Infrastructure.Options;

/// <summary>Limits, bound from the "Ai" section of appsettings.json. No values are hardcoded here.</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Most provider calls per day.</summary>
    public int DailyCap { get; set; }

    /// <summary>How long the provider may take before a check or explain call counts as timed out.</summary>
    public int TimeoutSeconds { get; set; }

    /// <summary>Generation writes up to ten long answers in one call, so it gets longer.</summary>
    public int GenerateTimeoutSeconds { get; set; }

    /// <summary>Output budget per call, thinking included. Bounds the cost of one call.</summary>
    public int CheckMaxOutputTokens { get; set; }
    public int ExplainMaxOutputTokens { get; set; }

    /// <summary>Generation scales with the number of questions: this much per question plus the base.</summary>
    public int GenerateTokensPerQuestion { get; set; }
    public int GenerateBaseOutputTokens { get; set; }

    /// <summary>How many existing question texts go into the generate prompt. Duplicates are still checked against all of them.</summary>
    public int MaxExistingQuestionTexts { get; set; }

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
    public string Model { get; set; } = "";
}
