using InterviewPrep.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InterviewPrep.Data.Configurations;

public sealed class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> builder)
    {
        // The question is the key: at most one note per question, and no dependency on a card.
        builder.HasKey(n => n.QuestionId);

        builder.HasOne(n => n.Question).WithMany().HasForeignKey(n => n.QuestionId).OnDelete(DeleteBehavior.Cascade);
    }
}
