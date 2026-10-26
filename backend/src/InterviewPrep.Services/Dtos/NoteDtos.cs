using InterviewPrep.Data.Models;
using InterviewPrep.Data.Models.Enums;

namespace InterviewPrep.Services.Dtos;

/// <summary>Loosely typed so a missing value becomes a validation problem, not a JSON binding failure.</summary>
public sealed record SaveNoteRequest(string? Text);

public sealed record NoteResponse(Guid QuestionId, string Text, DateTime UpdatedAt);

public static class NoteMappings
{
    public static NoteResponse ToResponse(this Note note) => new(note.QuestionId, note.Text, note.UpdatedAt);
}
