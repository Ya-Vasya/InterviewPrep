using InterviewPrep.Api.Features.Questions;

namespace InterviewPrep.Api.Features.Study;

/// <summary>
/// The user's learning state for one question (at most one per question). Created lazily when the
/// first rating is recorded. EF entity, never serialized.
/// </summary>
public sealed class Card
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid QuestionId { get; init; }
    public Question Question { get; init; } = null!;

    public required DateTime CreatedAt { get; init; }

    public double Ease { get; set; }
    public int IntervalDays { get; set; }
    public int Repetitions { get; set; }
    public int Lapses { get; set; }
    public DateTime DueAt { get; set; }
    public DateTime? LastReviewedAt { get; set; }

    /// <summary>Suspended cards are excluded from study sessions but keep their history.</summary>
    public bool Suspended { get; set; }

    public CardState ToState() => new(Ease, IntervalDays, Repetitions, Lapses, DueAt, LastReviewedAt);

    public void Apply(CardState state)
    {
        Ease = state.Ease;
        IntervalDays = state.IntervalDays;
        Repetitions = state.Repetitions;
        Lapses = state.Lapses;
        DueAt = state.DueAt;
        LastReviewedAt = state.LastReviewedAt;
    }
}
