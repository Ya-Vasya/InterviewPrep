namespace InterviewPrep.Api.Features.Ai;

/// <summary>Which AI feature a provider call is for. Limits and effort are configured per feature.</summary>
public enum AiFeature
{
    CheckAnswer = 0,
    ExplainMistakes = 1,
    GenerateQuestions = 2
}

/// <param name="System">Instructions only. Untrusted user text never goes in here.</param>
/// <param name="User">The task data, with user-written text inside clearly delimited blocks.</param>
/// <param name="JsonSchema">Schema the model's JSON output must satisfy.</param>
public sealed record AiRequest(AiFeature Feature, string System, string User, string JsonSchema);

public enum AiFailure
{
    Timeout,
    RateLimited,
    Unavailable
}

/// <summary>Thrown by a provider adapter; the gateway maps it to an HTTP problem.</summary>
public sealed class AiProviderException(AiFailure failure, string message, TimeSpan? retryAfter = null)
    : Exception(message)
{
    public AiFailure Failure { get; } = failure;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

/// <summary>The only thing feature code knows about the LLM. Tests replace it with a fake.</summary>
public interface IAiService
{
    /// <summary>False when no API key is configured; AI endpoints then answer 501.</summary>
    bool IsConfigured { get; }

    /// <summary>Returns the model's JSON text. Throws <see cref="AiProviderException"/> on provider failure.</summary>
    Task<string> CompleteJsonAsync(AiRequest request, CancellationToken ct);
}
