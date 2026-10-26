using InterviewPrep.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewPrep.Infrastructure.Extensions;

public static class OptionsExtensions
{
    /// <summary>
    /// Binds every options class from appsettings.json and checks it at startup, so a missing or nonsensical value
    /// stops the app at boot instead of failing a request later.
    /// </summary>
    public static IServiceCollection AddConfigurationOptions(this IServiceCollection services)
    {
        services.AddOptions<StudyOptions>()
            .BindConfiguration(StudyOptions.SectionName)
            .Validate(o => o.NewCardsPerDay >= 0 && o.ReviewsPerDay >= 0, "Study daily limits cannot be negative.")
            .Validate(
                o => o.DefaultQueueLimit >= 1 && o.MaxQueueLimit >= o.DefaultQueueLimit,
                "Study queue limits must be positive and the maximum at least the default.")
            .ValidateOnStart();

        services.AddOptions<PagingOptions>()
            .BindConfiguration(PagingOptions.SectionName)
            .Validate(o => o.DefaultLimit >= 1 && o.MaxLimit >= o.DefaultLimit,
                "Paging limits must be positive and the maximum at least the default.")
            .ValidateOnStart();

        services.AddOptions<AiOptions>()
            .BindConfiguration(AiOptions.SectionName)
            .Validate(o => o.DailyCap >= 0, "The AI daily cap cannot be negative.")
            .Validate(o => o.TimeoutSeconds > 0 && o.GenerateTimeoutSeconds > 0, "The AI timeouts must be positive.")
            .Validate(
                o => o.CheckMaxOutputTokens > 0 && o.ExplainMaxOutputTokens > 0 && o.GenerateTokensPerQuestion > 0
                     && o.GenerateBaseOutputTokens >= 0,
                "AI output limits must be positive.")
            .Validate(o => o.MaxExistingQuestionTexts >= 0, "The number of existing question texts cannot be negative.")
            .ValidateOnStart();

        services.AddOptions<AnthropicOptions>()
            .BindConfiguration(AnthropicOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.Model), "Anthropic:Model is required.")
            .ValidateOnStart();

        return services;
    }
}
