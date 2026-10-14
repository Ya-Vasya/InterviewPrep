namespace InterviewPrep.Api.Features.Ai;

/// <summary>Used when no API key is configured. AI endpoints check <see cref="IsConfigured"/> and answer 501.</summary>
public sealed class NotConfiguredAiService : IAiService
{
    public bool IsConfigured => false;

    public Task<string> CompleteJsonAsync(AiRequest request, CancellationToken ct) =>
        throw new InvalidOperationException("No Anthropic API key is configured.");
}
