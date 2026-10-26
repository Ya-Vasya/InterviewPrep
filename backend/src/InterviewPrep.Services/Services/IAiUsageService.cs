using InterviewPrep.Infrastructure.Ai;
using InterviewPrep.Services.Dtos;

namespace InterviewPrep.Services;

/// <summary>
/// The one door to the AI provider. Refuses when AI is off or the daily cap is used up, records every call that
/// reaches the provider (also failed ones, they cost money) and retries invalid output once.
/// </summary>
public interface IAiUsageService
{
    /// <exception cref="Exceptions.ValidationException">Unknown time zone.</exception>
    Task<AiStatusResponse> GetStatusAsync(string? timeZone, CancellationToken ct);

    /// <param name="parse">Returns null when the model's JSON does not satisfy the schema.</param>
    /// <param name="dayStartUtc">When the user's current day began; the daily cap counts from here.</param>
    /// <exception cref="Exceptions.AiNotConfiguredException">No API key is configured.</exception>
    /// <exception cref="Exceptions.AiDailyCapReachedException">The daily cap is used up.</exception>
    /// <exception cref="Exceptions.AiInvalidOutputException">The model's output was unusable twice.</exception>
    /// <exception cref="AiProviderException">The provider failed.</exception>
    Task<T> RunAsync<T>(AiRequest request, Func<string, T?> parse, DateTime dayStartUtc, CancellationToken ct)
        where T : class;
}
