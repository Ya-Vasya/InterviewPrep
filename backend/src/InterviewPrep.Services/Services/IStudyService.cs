using InterviewPrep.Services.Dtos;

namespace InterviewPrep.Services;

public interface IStudyService
{
    /// <exception cref="Exceptions.ValidationException">Unknown time zone.</exception>
    Task<StudySummaryResponse> GetSummaryAsync(string? timeZone, CancellationToken ct);

    /// <summary>A pure read: it never creates cards, so abandoning a session leaves no trace.</summary>
    /// <exception cref="Exceptions.ValidationException">Unknown topic or time zone, or a limit out of range.</exception>
    Task<IReadOnlyList<StudyQueueItem>> GetQueueAsync(
        IReadOnlyList<string>? topics, int? limit, string? timeZone, CancellationToken ct);

    /// <summary>
    /// Records a rating, creating the card with its first one. Idempotent on the review id: a repeated request
    /// returns the first result.
    /// </summary>
    /// <exception cref="Exceptions.ValidationException">The request breaks a rule.</exception>
    /// <exception cref="Exceptions.ConflictException">The review id was used for something else, or a concurrent change won.</exception>
    Task<ReviewResponse> RecordReviewAsync(ReviewRequest request, CancellationToken ct);

    /// <summary>Restores the card from the snapshot and deletes the review. Only the card's latest review can be undone.</summary>
    /// <exception cref="Exceptions.NotFoundException">No review has this id.</exception>
    /// <exception cref="Exceptions.ConflictException">A later review of the card exists.</exception>
    Task UndoReviewAsync(Guid reviewId, CancellationToken ct);

    /// <exception cref="Exceptions.NotFoundException">No card has this id.</exception>
    Task SetSuspendedAsync(Guid cardId, bool suspended, CancellationToken ct);
}
