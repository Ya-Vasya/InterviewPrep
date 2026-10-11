using InterviewPrep.Api.Common;
using InterviewPrep.Api.Data;
using InterviewPrep.Api.Features.Questions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InterviewPrep.Api.Features.Study;

public static class StudyEndpoints
{
    private const int DefaultQueueLimit = 50;
    private const int MaxQueueLimit = 500;
    private const int SqliteConstraintError = 19;

    public static IEndpointRouteBuilder MapStudyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/study").WithTags("Study");

        group.MapGet("/summary", GetSummary);
        group.MapGet("/queue", GetQueue);
        group.MapPost("/reviews", PostReview);
        group.MapDelete("/reviews/{reviewId:guid}", UndoReview);
        group.MapPost("/cards/{cardId:guid}/suspend", (Guid cardId, AppDbContext db, CancellationToken ct) =>
            SetSuspended(cardId, true, db, ct));
        group.MapPost("/cards/{cardId:guid}/unsuspend", (Guid cardId, AppDbContext db, CancellationToken ct) =>
            SetSuspended(cardId, false, db, ct));

        return app;
    }

    // What is left of today's limits.
    private sealed record Allowance(int NewRemaining, int ReviewsRemaining);

    private static async Task<Allowance> GetAllowanceAsync(
        AppDbContext db, StudyOptions options, DateTime dayStartUtc, CancellationToken ct)
    {
        var newToday = await db.Cards.CountAsync(c => c.CreatedAt >= dayStartUtc, ct);
        // A review that created its card is a new-card review, already counted above.
        var reviewsToday = await db.ReviewLogs.CountAsync(r => r.RatedAt >= dayStartUtc && !r.CardCreated, ct);

        return new Allowance(
            Math.Max(0, options.NewCardsPerDay - newToday),
            Math.Max(0, options.ReviewsPerDay - reviewsToday));
    }

    private static async Task<IResult> GetSummary(
        string? tz, AppDbContext db, TimeProvider time, IOptions<StudyOptions> options, CancellationToken ct)
    {
        if (!StudyDay.TryGetZone(tz, out var zone))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tz"] = ["Unknown time zone."] });

        var now = time.GetUtcNow().UtcDateTime;
        var allowance = await GetAllowanceAsync(db, options.Value, StudyDay.StartOfDayUtc(now, zone), ct);

        var dueByTopic = await db.Cards
            .Where(c => !c.Suspended && c.DueAt <= now)
            .GroupBy(c => c.Question.Topic)
            .Select(g => new { Topic = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Topic, x => x.Count, ct);

        var nextDueByTopic = await db.Cards
            .Where(c => !c.Suspended && c.DueAt > now)
            .GroupBy(c => c.Question.Topic)
            .Select(g => new { Topic = g.Key, Next = g.Min(c => c.DueAt) })
            .ToDictionaryAsync(x => x.Topic, x => x.Next, ct);

        var newByTopic = await db.Questions
            .Where(q => !db.Cards.Any(c => c.QuestionId == q.Id))
            .GroupBy(q => q.Topic)
            .Select(g => new { Topic = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Topic, x => x.Count, ct);

        // Always return the full fixed set, including topics with nothing to study.
        var topics = Enum.GetValues<Topic>()
            .Select(t => new TopicStudySummary(
                t,
                t.DisplayName(),
                Math.Min(dueByTopic.GetValueOrDefault(t), allowance.ReviewsRemaining),
                Math.Min(newByTopic.GetValueOrDefault(t), allowance.NewRemaining),
                nextDueByTopic.TryGetValue(t, out var next) ? next : null))
            .ToList();

        return Results.Ok(new StudySummaryResponse(topics, allowance.NewRemaining, allowance.ReviewsRemaining));
    }

    /// <summary>A pure read: it never creates cards, so abandoning a session leaves no trace.</summary>
    private static async Task<IResult> GetQueue(
        string[]? topic, int? limit, string? tz,
        AppDbContext db, IScheduler scheduler, TimeProvider time, IOptions<StudyOptions> options,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        var topics = new List<Topic>();
        foreach (var name in topic ?? [])
        {
            if (EnumParsing.TryParseName<Topic>(name, out var parsed))
                topics.Add(parsed);
            else
                errors["topic"] = [$"Topic must be one of: {EnumParsing.AllowedValues<Topic>()}."];
        }

        if (limit is < 1 or > MaxQueueLimit)
            errors["limit"] = [$"Limit must be between 1 and {MaxQueueLimit}."];

        if (!StudyDay.TryGetZone(tz, out var zone))
            errors["tz"] = ["Unknown time zone."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var now = time.GetUtcNow().UtcDateTime;
        var allowance = await GetAllowanceAsync(db, options.Value, StudyDay.StartOfDayUtc(now, zone), ct);
        var remaining = limit ?? DefaultQueueLimit;

        var dueQuery = db.Cards.Include(c => c.Question).Where(c => !c.Suspended && c.DueAt <= now);
        if (topics.Count > 0)
            dueQuery = dueQuery.Where(c => topics.Contains(c.Question.Topic));

        var dueCards = await dueQuery
            .OrderBy(c => c.DueAt).ThenBy(c => c.CreatedAt)
            .Take(Math.Min(remaining, allowance.ReviewsRemaining))
            .ToListAsync(ct);

        remaining -= dueCards.Count;

        var newQuery = db.Questions.Where(q => !db.Cards.Any(c => c.QuestionId == q.Id));
        if (topics.Count > 0)
            newQuery = newQuery.Where(q => topics.Contains(q.Topic));

        var newQuestions = await newQuery
            .OrderBy(q => q.Difficulty).ThenBy(q => q.Topic).ThenBy(q => q.Text).ThenBy(q => q.Id)
            .Take(Math.Min(remaining, allowance.NewRemaining))
            .ToListAsync(ct);

        var newState = scheduler.NewCard();

        var items = dueCards
            .Select(c => ToItem(c.Question, c.Id, c.ToState(), isNew: false, scheduler))
            .Concat(newQuestions.Select(q => ToItem(q, null, newState, isNew: true, scheduler)))
            .ToList();

        return Results.Ok(items);
    }

    private static StudyQueueItem ToItem(Question q, Guid? cardId, CardState state, bool isNew, IScheduler scheduler) =>
        new(q.Id, cardId, q.Topic, q.Difficulty, q.Text, q.Answer, q.Tags, isNew, IntervalsFor(state, scheduler));

    /// <summary>Intervals do not depend on the clock, so this is also correct for a replayed review.</summary>
    private static RatingIntervals IntervalsFor(CardState state, IScheduler scheduler) =>
        new(
            scheduler.Review(state, Rating.Again).IntervalDays,
            scheduler.Review(state, Rating.Hard).IntervalDays,
            scheduler.Review(state, Rating.Good).IntervalDays,
            scheduler.Review(state, Rating.Easy).IntervalDays);

    private static async Task<IResult> PostReview(
        ReviewRequest request, AppDbContext db, IScheduler scheduler, TimeProvider time, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var rating = default(Rating);

        if (request.QuestionId is null || request.QuestionId == Guid.Empty)
            errors["questionId"] = ["Question id is required."];

        if (request.ReviewId is null || request.ReviewId == Guid.Empty)
            errors["reviewId"] = ["Review id is required."];

        if (string.IsNullOrWhiteSpace(request.Rating))
            errors["rating"] = ["Rating is required."];
        else if (!EnumParsing.TryParseName(request.Rating, out rating))
            errors["rating"] = [$"Rating must be one of: {EnumParsing.AllowedValues<Rating>()}."];

        if (errors.Count == 0 && !await db.Questions.AnyAsync(q => q.Id == request.QuestionId, ct))
            errors["questionId"] = ["No question with this id exists."];

        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var questionId = request.QuestionId!.Value;
        var reviewId = request.ReviewId!.Value;

        // Two attempts: the first can lose a race (same reviewId, or two first ratings of one question
        // creating the card). The unique indexes reject the loser, and the retry then finds the winner's rows.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var existing = await db.ReviewLogs.Include(r => r.Card).FirstOrDefaultAsync(r => r.ReviewId == reviewId, ct);
            if (existing is not null)
            {
                return existing.Card.QuestionId == questionId && existing.Rating == rating
                    ? Results.Ok(new ReviewResponse(
                        reviewId, existing.CardId, existing.ResultDueAt, existing.ResultIntervalDays,
                        IntervalsFor(scheduler.Review(existing.Before, existing.Rating), scheduler)))
                    : Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Review id already used",
                        detail: "This review id was already recorded for a different question or rating.");
            }

            var card = await db.Cards.AsTracking().FirstOrDefaultAsync(c => c.QuestionId == questionId, ct);
            var created = card is null;
            var now = time.GetUtcNow().UtcDateTime;

            var before = card?.ToState() ?? scheduler.NewCard();
            if (card is null)
            {
                card = new Card { QuestionId = questionId, CreatedAt = now };
                card.Apply(before);
                db.Cards.Add(card);
            }

            var after = scheduler.Review(before, rating);
            card.Apply(after);

            db.ReviewLogs.Add(new ReviewLog
            {
                ReviewId = reviewId,
                CardId = card.Id,
                RatedAt = now,
                Rating = rating,
                Source = ReviewSource.Self,
                CardCreated = created,
                Before = before,
                ResultDueAt = after.DueAt,
                ResultIntervalDays = after.IntervalDays
            });

            try
            {
                // One SaveChanges is one transaction: the card is created together with its first review.
                await db.SaveChangesAsync(ct);
                return Results.Ok(new ReviewResponse(reviewId, card.Id, after.DueAt, after.IntervalDays, IntervalsFor(after, scheduler)));
            }
            catch (DbUpdateException e) when (e.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintError })
            {
                db.ChangeTracker.Clear();
            }
        }

        return Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Review conflict",
            detail: "The review could not be recorded because of a concurrent change. Try again.");
    }

    /// <summary>Undo: restores the card from the snapshot and deletes the review. Only the card's latest review can be undone.</summary>
    private static async Task<IResult> UndoReview(Guid reviewId, AppDbContext db, CancellationToken ct)
    {
        var log = await db.ReviewLogs.AsTracking().Include(r => r.Card).FirstOrDefaultAsync(r => r.ReviewId == reviewId, ct);
        if (log is null)
            return Results.NotFound();

        if (await db.ReviewLogs.AnyAsync(r => r.CardId == log.CardId && r.Id > log.Id, ct))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Review cannot be undone",
                detail: "Only the latest review of a card can be undone.");
        }

        if (log.CardCreated)
        {
            // The question goes back to being a new card. The log entry is removed with the card.
            db.Cards.Remove(log.Card);
        }
        else
        {
            log.Card.Apply(log.Before);
            db.ReviewLogs.Remove(log);
        }

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetSuspended(Guid cardId, bool suspended, AppDbContext db, CancellationToken ct)
    {
        var changed = await db.Cards
            .Where(c => c.Id == cardId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Suspended, suspended), ct);

        return changed == 0 ? Results.NotFound() : Results.NoContent();
    }
}
