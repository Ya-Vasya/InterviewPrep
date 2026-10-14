using InterviewPrep.Api.Common;

namespace InterviewPrep.Api.Features.Study;

/// <summary>
/// Rating and ids arrive loosely typed so a bad value becomes a validation problem
/// instead of a generic JSON binding failure.
/// </summary>
/// <param name="AttemptId">
/// The answer attempt this rating follows, if the user checked an answer first. The review is recorded as
/// AI-assisted only when that attempt is for the same question and the rating equals its suggested rating.
/// </param>
public sealed record ReviewRequest(Guid? QuestionId, string? Rating, Guid? ReviewId, Guid? AttemptId = null);

/// <param name="NextIntervals">What each rating would produce next time this card is shown, e.g. when an Again card comes back in the same session.</param>
public sealed record ReviewResponse(Guid ReviewId, Guid CardId, DateTime DueAt, int IntervalDays, RatingIntervals NextIntervals);

/// <summary>
/// Counts are already capped by what is left of today's limits. Several topics can each show the
/// full remaining allowance, so a client summing a selection must cap the sum at the remaining values.
/// </summary>
public sealed record StudySummaryResponse(
    IReadOnlyList<TopicStudySummary> Topics,
    int NewRemainingToday,
    int ReviewsRemainingToday);

/// <param name="DueCount">Cards due now.</param>
/// <param name="NewCount">Questions without a card that may be introduced today.</param>
/// <param name="NextDueAt">When the earliest not-yet-due card of this topic comes due, if any.</param>
public sealed record TopicStudySummary(Topic Topic, string Name, int DueCount, int NewCount, DateTime? NextDueAt);

/// <summary>The interval, in days, each rating would produce. 0 means due again immediately.</summary>
public sealed record RatingIntervals(int Again, int Hard, int Good, int Easy);

/// <param name="CardId">Null for a new card: it does not exist until its first rating is recorded.</param>
public sealed record StudyQueueItem(
    Guid QuestionId,
    Guid? CardId,
    Topic Topic,
    Difficulty Difficulty,
    string Text,
    string Answer,
    IReadOnlyList<string> Tags,
    bool IsNew,
    RatingIntervals Intervals);
