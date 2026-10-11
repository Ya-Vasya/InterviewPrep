namespace InterviewPrep.Api.Features.Study;

/// <summary>
/// One recorded rating. Append-only: the only way to remove an entry is Undo. Holds a snapshot of the
/// card state before the rating so Undo can restore it. EF entity, never serialized.
/// </summary>
public sealed class ReviewLog
{
    /// <summary>Insertion order; the latest entry of a card is the one with the highest value.</summary>
    public long Id { get; init; }

    /// <summary>Client-generated idempotency key (unique).</summary>
    public required Guid ReviewId { get; init; }

    public required Guid CardId { get; init; }
    public Card Card { get; init; } = null!;

    public required DateTime RatedAt { get; init; }
    public required Rating Rating { get; init; }
    public required ReviewSource Source { get; init; }

    /// <summary>True when this review created the card, so Undo deletes the card again.</summary>
    public required bool CardCreated { get; init; }

    /// <summary>Card state before the rating (for a created card: the initial state).</summary>
    public required CardState Before { get; init; }

    /// <summary>What the review returned to the client, replayed for a repeated request.</summary>
    public required DateTime ResultDueAt { get; init; }
    public required int ResultIntervalDays { get; init; }
}
