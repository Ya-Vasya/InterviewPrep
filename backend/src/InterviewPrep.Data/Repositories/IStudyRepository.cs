using InterviewPrep.Data.Models;
using InterviewPrep.Data.Models.Enums;

namespace InterviewPrep.Data.Repositories;

/// <summary>Cards and their review log, which always change together.</summary>
public interface IStudyRepository
{
    /// <summary>Cards created at or after <paramref name="sinceUtc"/> (new cards introduced).</summary>
    Task<int> CountCardsCreatedSinceAsync(DateTime sinceUtc, CancellationToken ct);

    /// <summary>Reviews rated at or after <paramref name="sinceUtc"/> that did not create their card.</summary>
    Task<int> CountReviewsSinceAsync(DateTime sinceUtc, CancellationToken ct);

    Task<IReadOnlyDictionary<Topic, int>> CountDueByTopicAsync(DateTime nowUtc, CancellationToken ct);

    /// <summary>The earliest due time of the not-yet-due, unsuspended cards per topic.</summary>
    Task<IReadOnlyDictionary<Topic, DateTime>> GetNextDueByTopicAsync(DateTime nowUtc, CancellationToken ct);

    /// <summary>Questions that have no card yet, per topic.</summary>
    Task<IReadOnlyDictionary<Topic, int>> CountNewByTopicAsync(CancellationToken ct);

    /// <summary>Due, unsuspended cards (question loaded), most overdue first. No topics means all topics.</summary>
    Task<IReadOnlyList<Card>> GetDueCardsAsync(IReadOnlyCollection<Topic> topics, DateTime nowUtc, int take, CancellationToken ct);

    /// <summary>Questions that have no card yet, easiest first. No topics means all topics.</summary>
    Task<IReadOnlyList<Question>> GetNewQuestionsAsync(IReadOnlyCollection<Topic> topics, int take, CancellationToken ct);

    /// <summary>The review with this client-generated id, with its card, or null.</summary>
    Task<ReviewLog?> GetReviewAsync(Guid reviewId, CancellationToken ct);

    /// <summary>The review with its card, tracked so Undo can change them.</summary>
    Task<ReviewLog?> GetReviewForUndoAsync(Guid reviewId, CancellationToken ct);

    /// <summary>True when the card has a review after <paramref name="reviewLogId"/>.</summary>
    Task<bool> HasLaterReviewAsync(Guid cardId, long reviewLogId, CancellationToken ct);

    /// <summary>The question's card, tracked so a review can change it.</summary>
    Task<Card?> GetCardForUpdateAsync(Guid questionId, CancellationToken ct);

    /// <summary>Sets the flag directly in the database; returns how many cards matched.</summary>
    Task<int> SetSuspendedAsync(Guid cardId, bool suspended, CancellationToken ct);

    void AddCard(Card card);

    void AddReview(ReviewLog review);

    void RemoveCard(Card card);

    void RemoveReview(ReviewLog review);
}
