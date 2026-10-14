using Microsoft.Extensions.Options;

namespace InterviewPrep.Api.Features.Ai;

public static class AiFeatureRegistration
{
    public static IServiceCollection AddAiFeature(this IServiceCollection services)
    {
        services.AddOptions<AiOptions>()
            .BindConfiguration(AiOptions.SectionName)
            .Validate(o => o.DailyCap >= 0, "The AI daily cap cannot be negative.")
            .Validate(o => o.TimeoutSeconds > 0, "The AI timeout must be positive.")
            .Validate(
                o => o.CheckMaxOutputTokens > 0 && o.ExplainMaxOutputTokens > 0 && o.GenerateMaxOutputTokens > 0,
                "AI output limits must be positive.")
            .ValidateOnStart();

        services.AddOptions<AnthropicOptions>().BindConfiguration(AnthropicOptions.SectionName);

        services.AddScoped<AiGateway>();

        // Tests replace IAiService with a fake. Without a key the real client is never constructed.
        services.AddSingleton<IAiService>(sp =>
            string.IsNullOrWhiteSpace(sp.GetRequiredService<IOptions<AnthropicOptions>>().Value.ApiKey)
                ? new NotConfiguredAiService()
                : ActivatorUtilities.CreateInstance<AnthropicAiService>(sp));

        return services;
    }
}
