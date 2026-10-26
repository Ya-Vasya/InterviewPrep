using InterviewPrep.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace InterviewPrep.Infrastructure.Extensions;

public static class RepositoryExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IQuestionRepository, QuestionRepository>();
        services.AddScoped<IStudyRepository, StudyRepository>();
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<IAnswerAttemptRepository, AnswerAttemptRepository>();
        services.AddScoped<IAiCallRepository, AiCallRepository>();

        return services;
    }
}
