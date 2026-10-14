using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;

namespace InterviewPrep.Api.Features.Ai;

/// <summary>
/// The real provider, using the official Anthropic SDK. Not exercised by tests, which use a fake
/// <see cref="IAiService"/> and never call the API. Only registered when an API key is configured.
/// </summary>
public sealed class AnthropicAiService : IAiService
{
    private readonly AnthropicClient _client;
    private readonly AnthropicOptions _anthropic;
    private readonly AiOptions _ai;

    public AnthropicAiService(IOptions<AnthropicOptions> anthropic, IOptions<AiOptions> ai)
    {
        _anthropic = anthropic.Value;
        _ai = ai.Value;
        _client = new AnthropicClient
        {
            ApiKey = _anthropic.ApiKey,
            Timeout = TimeSpan.FromSeconds(_ai.TimeoutSeconds),
            // The gateway retries invalid output and counts every call that reaches the provider, so the SDK
            // must not silently retry (and bill) on its own.
            MaxRetries = 0
        };
    }

    public bool IsConfigured => true;

    public async Task<string> CompleteJsonAsync(AiRequest request, CancellationToken ct)
    {
        var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.JsonSchema)!;

        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = _anthropic.Model,
                MaxTokens = _ai.MaxOutputTokensFor(request.Feature),
                System = request.System,
                Messages = [new() { Role = Role.User, Content = request.User }],
                OutputConfig = new OutputConfig
                {
                    Effort = Effort.Low,
                    Format = new JsonOutputFormat { Schema = schema }
                }
            }, ct);

            return string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        }
        catch (AnthropicRateLimitException)
        {
            // The SDK exception does not expose Retry-After, so no retry hint is passed on.
            throw new AiProviderException(AiFailure.RateLimited, "The provider is rate limiting requests.");
        }
        catch (Exception e) when (e is TaskCanceledException or TimeoutException && !ct.IsCancellationRequested)
        {
            throw new AiProviderException(AiFailure.Timeout, "The provider did not answer in time.");
        }
        catch (Exception e) when (e is AnthropicException or HttpRequestException)
        {
            // The message is deliberately not forwarded: it could echo request details.
            throw new AiProviderException(AiFailure.Unavailable, "The provider returned an error.");
        }
    }
}
