namespace InterviewPrep.Data.Models;

/// <summary>
/// The user's personal text for one question, such as a saved explanation. At most one per question, keyed by
/// the question and independent of the card, so it works before a card exists and survives suspending one
/// (see ADR 0001). EF entity, never serialized.
/// </summary>
public sealed class Note
{
    public required Guid QuestionId { get; init; }
    public Question Question { get; init; } = null!;

    public required string Text { get; set; }
    public required DateTime UpdatedAt { get; set; }
}

