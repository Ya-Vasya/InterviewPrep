using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using InterviewPrep.Data.Models.Enums;
using InterviewPrep.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace InterviewPrep.Infrastructure.Ai;

/// <summary>
/// The real provider, using the official Anthropic SDK. Not exercised by tests, which use a fake
/// <see cref="IAiProvider"/> and never call the API. Only registered when an API key is configured.
/// </summary>
public sealed class AnthropicAiProvider : IAiProvider
{
    private readonly AnthropicClient _client;
    private readonly AnthropicOptions _anthropic;
    private readonly AiOptions _ai;

    public AnthropicAiProvider(IOptions<AnthropicOptions> anthropic, IOptions<AiOptions> ai)
    {
        _anthropic = anthropic.Value;
        _ai = ai.Value;
        _client = new AnthropicClient
        {
            ApiKey = _anthropic.ApiKey,
            // The longest any feature may take; each call enforces its own, shorter, limit (see CompleteJsonAsync).
            Timeout = TimeSpan.FromSeconds(Math.Max(_ai.TimeoutSeconds, _ai.GenerateTimeoutSeconds)),
            // The AI usage service retries invalid output and counts every call that reaches the provider, so the SDK
            // must not silently retry (and bill) on its own.
            MaxRetries = 0
        };
    }

    public bool IsConfigured => true;

    public async Task<string> CompleteJsonAsync(AiRequest request, CancellationToken ct)
    {
        var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.JsonSchema)!;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(_ai.TimeoutSecondsFor(request.Feature)));

        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = _anthropic.Model,
                MaxTokens = request.MaxOutputTokens ?? DefaultMaxOutputTokens(request.Feature),
                System = request.System,
                Messages = [new() { Role = Role.User, Content = request.User }],
                OutputConfig = new OutputConfig
                {
                    Effort = Effort.Low,
                    Format = new JsonOutputFormat { Schema = schema }
                }
            }, timeout.Token);

            return string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        }
        catch (AnthropicRateLimitException)
        {
            // The SDK exception does not expose Retry-After, so no retry hint is passed on.
            throw new AiProviderException(AiFailure.RateLimited, "The provider is rate limiting requests.");
        }
        catch (Exception e) when (e is OperationCanceledException or TimeoutException && !ct.IsCancellationRequested)
        {
            throw new AiProviderException(AiFailure.Timeout, "The provider did not answer in time.");
        }
        catch (Exception e) when (e is AnthropicException or HttpRequestException)
        {
            // The message is deliberately not forwarded: it could echo request details.
            throw new AiProviderException(AiFailure.Unavailable, "The provider returned an error.");
        }
    }

    private int DefaultMaxOutputTokens(AiFeature feature) => feature switch
    {
        AiFeature.CheckAnswer => _ai.CheckMaxOutputTokens,
        AiFeature.ExplainMistakes => _ai.ExplainMaxOutputTokens,
        _ => throw new InvalidOperationException($"{feature} requests must set MaxOutputTokens.")
    };
}
