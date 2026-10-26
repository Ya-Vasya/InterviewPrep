namespace InterviewPrep.Data.Models;

/// <summary>The scheduling state of a card. Stored on the card and snapshotted into each review log entry.</summary>
public sealed record CardState(
    double Ease,
    int IntervalDays,
    int Repetitions,
    int Lapses,
    DateTime DueAt,
    DateTime? LastReviewedAt);
