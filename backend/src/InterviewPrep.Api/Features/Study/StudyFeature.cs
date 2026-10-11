using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InterviewPrep.Api.Features.Study;

public static class StudyFeature
{
    public static IServiceCollection AddStudyFeature(this IServiceCollection services)
    {
        // Tests replace TimeProvider with a fake clock.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IScheduler, Sm2Scheduler>();

        services.AddOptions<StudyOptions>()
            .BindConfiguration(StudyOptions.SectionName)
            .Validate(o => o.NewCardsPerDay >= 0 && o.ReviewsPerDay >= 0, "Study daily limits cannot be negative.")
            .ValidateOnStart();

        return services;
    }
}
