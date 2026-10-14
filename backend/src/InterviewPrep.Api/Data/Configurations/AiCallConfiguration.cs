using InterviewPrep.Api.Features.Ai;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewPrep.Api.Data.Configurations;

public sealed class AiCallConfiguration : IEntityTypeConfiguration<AiCall>
{
    public void Configure(EntityTypeBuilder<AiCall> builder)
    {
        builder.HasKey(c => c.Id);

        // The daily count is "calls since the start of the user's day".
        builder.HasIndex(c => c.CalledAt);
    }
}
