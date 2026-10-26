using InterviewPrep.Data.Models.Enums;

namespace InterviewPrep.Infrastructure.Ai;

/// <param name="System">Instructions only. Untrusted user text never goes in here.</param>
/// <param name="User">The task data, with user-written text inside clearly delimited blocks.</param>
/// <param name="JsonSchema">Schema the model's JSON output must satisfy.</param>
/// <param name="MaxOutputTokens">Overrides the feature's configured budget, for output whose size depends on the request.</param>
public sealed record AiRequest(
    AiFeature Feature, string System, string User, string JsonSchema, int? MaxOutputTokens = null);

public enum AiFailure
{
    Timeout,
    RateLimited,
    Unavailable
}

/// <summary>Thrown by a provider adapter. The API turns it into a 503 problem.</summary>
public sealed class AiProviderException(AiFailure failure, string message, TimeSpan? retryAfter = null)
    : Exception(message)
{
    public AiFailure Failure { get; } = failure;
    public TimeSpan? RetryAfter { get; } = retryAfter;
}

/// <summary>The only thing the rest of the app knows about the LLM. Tests replace it with a fake.</summary>
public interface IAiProvider
{
    /// <summary>False when no API key is configured; AI endpoints then answer 501.</summary>
    bool IsConfigured { get; }

    /// <summary>Returns the model's JSON text. Throws <see cref="AiProviderException"/> on provider failure.</summary>
    Task<string> CompleteJsonAsync(AiRequest request, CancellationToken ct);
}
