using InterviewPrep.Api.Features.Ai;
using InterviewPrep.Api.Features.Questions;
using InterviewPrep.Api.Features.Study;
using Microsoft.EntityFrameworkCore;

namespace InterviewPrep.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<ReviewLog> ReviewLogs => Set<ReviewLog>();
    public DbSet<AiCall> AiCalls => Set<AiCall>();
    public DbSet<AnswerAttempt> AnswerAttempts => Set<AnswerAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
