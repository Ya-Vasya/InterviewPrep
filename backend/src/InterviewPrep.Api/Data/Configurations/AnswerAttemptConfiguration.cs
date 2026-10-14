using InterviewPrep.Api.Features.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewPrep.Api.Data.Configurations;

public sealed class AnswerAttemptConfiguration : IEntityTypeConfiguration<AnswerAttempt>
{
    public void Configure(EntityTypeBuilder<AnswerAttempt> builder)
    {
        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.Question).WithMany().HasForeignKey(a => a.QuestionId).OnDelete(DeleteBehavior.Cascade);

        // "Latest attempts of this question" is the only way attempts are read.
        builder.HasIndex(a => new { a.QuestionId, a.CreatedAt });
    }
}
