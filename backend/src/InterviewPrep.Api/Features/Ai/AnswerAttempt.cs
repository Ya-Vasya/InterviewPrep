using InterviewPrep.Api.Features.Questions;

namespace InterviewPrep.Api.Features.Ai;

/// <summary>
/// The user's typed answer to a question with the AI's verdict on it. Kept so mistakes can be revisited and
/// counted later. Different from a review: checking an attempt never rates a card. EF entity, never serialized.
/// </summary>
public sealed class AnswerAttempt
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid QuestionId { get; init; }
    public Question Question { get; init; } = null!;

    public required string Answer { get; init; }
    public required Verdict Verdict { get; init; }

    /// <summary>0-100.</summary>
    public required int Score { get; init; }

    public required List<string> Covered { get; init; }
    public required List<string> Missing { get; init; }
    public required string Feedback { get; init; }
    public required DateTime CreatedAt { get; init; }
}
