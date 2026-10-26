using InterviewPrep.Infrastructure.Ai;
using InterviewPrep.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace InterviewPrep.Infrastructure.Extensions;

public static class AiProviderExtensions
{
    /// <summary>Tests replace <see cref="IAiProvider"/> with a fake. Without a key the real client is never constructed.</summary>
    public static IServiceCollection AddAiProvider(this IServiceCollection services)
    {
        services.AddSingleton<IAiProvider>(sp =>
            string.IsNullOrWhiteSpace(sp.GetRequiredService<IOptions<AnthropicOptions>>().Value.ApiKey)
                ? new NotConfiguredAiProvider()
                : ActivatorUtilities.CreateInstance<AnthropicAiProvider>(sp));

        return services;
    }
}
