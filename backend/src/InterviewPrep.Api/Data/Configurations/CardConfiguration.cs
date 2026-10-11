using InterviewPrep.Api.Features.Study;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewPrep.Api.Data.Configurations;

public sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.HasKey(c => c.Id);

        builder.HasOne(c => c.Question).WithMany().HasForeignKey(c => c.QuestionId).OnDelete(DeleteBehavior.Cascade);

        // At most one card per question.
        builder.HasIndex(c => c.QuestionId).IsUnique();
        builder.HasIndex(c => c.DueAt);
    }
}
