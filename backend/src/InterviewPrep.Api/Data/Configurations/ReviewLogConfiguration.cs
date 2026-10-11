using InterviewPrep.Api.Features.Study;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewPrep.Api.Data.Configurations;

public sealed class ReviewLogConfiguration : IEntityTypeConfiguration<ReviewLog>
{
    public void Configure(EntityTypeBuilder<ReviewLog> builder)
    {
        builder.HasKey(r => r.Id);

        builder.HasOne(r => r.Card).WithMany().HasForeignKey(r => r.CardId).OnDelete(DeleteBehavior.Cascade);

        // The idempotency key: a repeated request with the same ReviewId must not insert a second row.
        builder.HasIndex(r => r.ReviewId).IsUnique();
        builder.HasIndex(r => r.RatedAt);
        builder.HasIndex(r => r.CardId);

        builder.Property(r => r.RatedAt).HasConversion<UtcDateTimeConverter>();
        builder.Property(r => r.ResultDueAt).HasConversion<UtcDateTimeConverter>();

        builder.ComplexProperty(r => r.Before, before =>
        {
            before.Property(s => s.DueAt).HasConversion<UtcDateTimeConverter>();
            before.Property(s => s.LastReviewedAt).HasConversion(new UtcDateTimeConverter());
        });
    }
}
