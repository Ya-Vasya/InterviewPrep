using InterviewPrep.Services.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InterviewPrep.Services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        // Tests replace TimeProvider with a fake clock.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IScheduler, Sm2Scheduler>();

        services.AddScoped<IQuestionService, QuestionService>();
        services.AddScoped<IStudyService, StudyService>();
        services.AddScoped<INoteService, NoteService>();
        services.AddScoped<IAiUsageService, AiUsageService>();
        services.AddScoped<IAnswerCheckService, AnswerCheckService>();
        services.AddScoped<IQuestionGenerationService, QuestionGenerationService>();

        return services;
    }
}
