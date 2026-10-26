using InterviewPrep.Data.Exceptions;
using InterviewPrep.Data.Models;
using InterviewPrep.Data.Models.Enums;
using InterviewPrep.Data.Repositories;
using InterviewPrep.Infrastructure.Options;
using InterviewPrep.Services.Dtos;
using InterviewPrep.Services.Exceptions;
using InterviewPrep.Services.Helpers;
using InterviewPrep.Services.Scheduling;
using Microsoft.Extensions.Options;

namespace InterviewPrep.Services;

public sealed class StudyService(
    IStudyRepository study,
    IQuestionRepository questions,
    IAnswerAttemptRepository attempts,
    IUnitOfWork unitOfWork,
    IScheduler scheduler,
    TimeProvider time,
    IOptions<StudyOptions> options) : IStudyService
{
    // What is left of today's limits.
    private sealed record Allowance(int NewRemaining, int ReviewsRemaining);

    private async Task<Allowance> GetAllowanceAsync(DateTime dayStartUtc, CancellationToken ct)
    {
        var newToday = await study.CountCardsCreatedSinceAsync(dayStartUtc, ct);
        // A review that created its card is a new-card review, already counted above.
        var reviewsToday = await study.CountReviewsSinceAsync(dayStartUtc, ct);

        return new Allowance(
            Math.Max(0, options.Value.NewCardsPerDay - newToday),
            Math.Max(0, options.Value.ReviewsPerDay - reviewsToday));
    }

    public async Task<StudySummaryResponse> GetSummaryAsync(string? timeZone, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var zone = errors.RequireZone(timeZone);
        errors.ThrowIfAny();

        var now = time.GetUtcNow().UtcDateTime;
        var allowance = await GetAllowanceAsync(StudyDay.StartOfDayUtc(now, zone), ct);

        var dueByTopic = await study.CountDueByTopicAsync(now, ct);
        var nextDueByTopic = await study.GetNextDueByTopicAsync(now, ct);
        var newByTopic = await study.CountNewByTopicAsync(ct);

        // Always return the full fixed set, including topics with nothing to study.
        var topics = Enum.GetValues<Topic>()
            .Select(t => new TopicStudySummary(
                t,
                t.DisplayName(),
                Math.Min(dueByTopic.GetValueOrDefault(t), allowance.ReviewsRemaining),
                Math.Min(newByTopic.GetValueOrDefault(t), allowance.NewRemaining),
                nextDueByTopic.TryGetValue(t, out var next) ? next : null))
            .ToList();

        return new StudySummaryResponse(topics, allowance.NewRemaining, allowance.ReviewsRemaining);
    }

    public async Task<IReadOnlyList<StudyQueueItem>> GetQueueAsync(
        IReadOnlyList<string>? topicNames, int? limit, string? timeZone, CancellationToken ct)
    {
        var errors = new ValidationErrors();

        var topics = new List<Topic>();
        foreach (var name in topicNames ?? [])
        {
            if (EnumParsing.TryParseName<Topic>(name, out var parsed))
                topics.Add(parsed);
            else
                errors.AddNotOneOf<Topic>("topic");
        }

        if (limit is < 1 || limit > options.Value.MaxQueueLimit)
            errors.Add("limit", $"Limit must be between 1 and {options.Value.MaxQueueLimit}.");

        var zone = errors.RequireZone(timeZone);
        errors.ThrowIfAny();

        var now = time.GetUtcNow().UtcDateTime;
        var allowance = await GetAllowanceAsync(StudyDay.StartOfDayUtc(now, zone), ct);
        var remaining = limit ?? options.Value.DefaultQueueLimit;

        var dueCards = await study.GetDueCardsAsync(topics, now, Math.Min(remaining, allowance.ReviewsRemaining), ct);
        remaining -= dueCards.Count;

        var newQuestions = await study.GetNewQuestionsAsync(topics, Math.Min(remaining, allowance.NewRemaining), ct);

        var newState = scheduler.NewCard();

        return dueCards
            .Select(c => ToItem(c.Question, c.Id, c.ToState(), isNew: false))
            .Concat(newQuestions.Select(q => ToItem(q, null, newState, isNew: true)))
            .ToList();
    }

    public async Task<ReviewResponse> RecordReviewAsync(ReviewRequest request, CancellationToken ct)
    {
        var errors = new ValidationErrors();

        if (request.QuestionId is null || request.QuestionId == Guid.Empty)
            errors.Add("questionId", "Question id is required.");

        if (request.ReviewId is null || request.ReviewId == Guid.Empty)
            errors.Add("reviewId", "Review id is required.");

        errors.TryParseRequired<Rating>("rating", request.Rating, out var rating);

        if (!errors.Any && !await questions.ExistsAsync(request.QuestionId!.Value, ct))
            errors.Add("questionId", "No question with this id exists.");

        errors.ThrowIfAny();

        var questionId = request.QuestionId!.Value;
        var reviewId = request.ReviewId!.Value;
        var source = await GetSourceAsync(request.AttemptId, questionId, rating, ct);

        // Two attempts: the first can lose a race (same reviewId, or two first ratings of one question
        // creating the card). The unique indexes reject the loser, and the retry then finds the winner's rows.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var existing = await study.GetReviewAsync(reviewId, ct);
            if (existing is not null)
            {
                if (existing.Card.QuestionId == questionId && existing.Rating == rating)
                {
                    return new ReviewResponse(
                        reviewId, existing.CardId, existing.ResultDueAt, existing.ResultIntervalDays,
                        IntervalsFor(scheduler.Review(existing.Before, existing.Rating)));
                }

                throw new ConflictException(
                    "Review id already used",
                    "This review id was already recorded for a different question or rating.");
            }

            var card = await study.GetCardForUpdateAsync(questionId, ct);
            var created = card is null;
            var now = time.GetUtcNow().UtcDateTime;

            var before = card?.ToState() ?? scheduler.NewCard();
            if (card is null)
            {
                card = new Card { QuestionId = questionId, CreatedAt = now };
                card.Apply(before);
                study.AddCard(card);
            }

            var after = scheduler.Review(before, rating);
            card.Apply(after);

            study.AddReview(new ReviewLog
            {
                ReviewId = reviewId,
                CardId = card.Id,
                RatedAt = now,
                Rating = rating,
                Source = source,
                CardCreated = created,
                Before = before,
                ResultDueAt = after.DueAt,
                ResultIntervalDays = after.IntervalDays
            });

            try
            {
                // One save is one transaction: the card is created together with its first review.
                await unitOfWork.SaveChangesAsync(ct);
                return new ReviewResponse(reviewId, card.Id, after.DueAt, after.IntervalDays, IntervalsFor(after));
            }
            catch (UniqueConstraintViolationException)
            {
                unitOfWork.DiscardChanges();
            }
        }

        throw new ConflictException(
            "Review conflict",
            "The review could not be recorded because of a concurrent change. Try again.");
    }

    public async Task UndoReviewAsync(Guid reviewId, CancellationToken ct)
    {
        var log = await study.GetReviewForUndoAsync(reviewId, ct) ?? throw new NotFoundException();

        if (await study.HasLaterReviewAsync(log.CardId, log.Id, ct))
        {
            throw new ConflictException(
                "Review cannot be undone",
                "Only the latest review of a card can be undone.");
        }

        if (log.CardCreated)
        {
            // The question goes back to being a new card. The log entry is removed with the card.
            study.RemoveCard(log.Card);
        }
        else
        {
            log.Card.Apply(log.Before);
            study.RemoveReview(log);
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SetSuspendedAsync(Guid cardId, bool suspended, CancellationToken ct)
    {
        if (await study.SetSuspendedAsync(cardId, suspended, ct) == 0)
            throw new NotFoundException();
    }

    /// <summary>
    /// A rating is AI-assisted only when the user accepted the suggestion: the attempt must be for this question and
    /// the rating must equal its suggested rating. Decided here so a client cannot claim it. Anything else is self-rated.
    /// </summary>
    private async Task<ReviewSource> GetSourceAsync(Guid? attemptId, Guid questionId, Rating rating, CancellationToken ct)
    {
        if (attemptId is null || attemptId == Guid.Empty)
            return ReviewSource.Self;

        var attempt = await attempts.GetForQuestionAsync(attemptId.Value, questionId, ct);

        return attempt?.Verdict.SuggestedRating() == rating ? ReviewSource.Ai : ReviewSource.Self;
    }

    private StudyQueueItem ToItem(Question q, Guid? cardId, CardState state, bool isNew) =>
        new(q.Id, cardId, q.Topic, q.Difficulty, q.Text, q.Answer, q.Tags, q.Source, isNew, IntervalsFor(state));

    /// <summary>Intervals do not depend on the clock, so this is also correct for a replayed review.</summary>
    private RatingIntervals IntervalsFor(CardState state) =>
        new(
            scheduler.Review(state, Rating.Again).IntervalDays,
            scheduler.Review(state, Rating.Hard).IntervalDays,
            scheduler.Review(state, Rating.Good).IntervalDays,
            scheduler.Review(state, Rating.Easy).IntervalDays);
}
