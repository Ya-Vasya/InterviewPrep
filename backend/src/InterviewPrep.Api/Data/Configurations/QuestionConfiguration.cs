using InterviewPrep.Api.Features.Questions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewPrep.Api.Data.Configurations;

public sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Text).IsRequired().HasMaxLength(QuestionLimits.TextMaxLength);
        builder.Property(q => q.Answer).IsRequired().HasMaxLength(QuestionLimits.AnswerMaxLength);

        // Tags is a primitive collection: stored as a JSON array, queryable with Contains().
        builder.PrimitiveCollection(q => q.Tags);

        builder.HasIndex(q => new { q.Topic, q.Difficulty });
    }
}
