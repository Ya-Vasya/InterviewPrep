using InterviewPrep.Data.Models;
using InterviewPrep.Data.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace InterviewPrep.Data.Repositories;

public sealed class StudyRepository(AppDbContext db) : IStudyRepository
{
    public Task<int> CountCardsCreatedSinceAsync(DateTime sinceUtc, CancellationToken ct) =>
        db.Cards.CountAsync(c => c.CreatedAt >= sinceUtc, ct);

    public Task<int> CountReviewsSinceAsync(DateTime sinceUtc, CancellationToken ct) =>
        db.ReviewLogs.CountAsync(r => r.RatedAt >= sinceUtc && !r.CardCreated, ct);

    public async Task<IReadOnlyDictionary<Topic, int>> CountDueByTopicAsync(DateTime nowUtc, CancellationToken ct) =>
        await db.Cards
            .Where(c => !c.Suspended && c.DueAt <= nowUtc)
            .GroupBy(c => c.Question.Topic)
            .Select(g => new { Topic = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Topic, x => x.Count, ct);

    public async Task<IReadOnlyDictionary<Topic, DateTime>> GetNextDueByTopicAsync(DateTime nowUtc, CancellationToken ct) =>
        await db.Cards
            .Where(c => !c.Suspended && c.DueAt > nowUtc)
            .GroupBy(c => c.Question.Topic)
            .Select(g => new { Topic = g.Key, Next = g.Min(c => c.DueAt) })
            .ToDictionaryAsync(x => x.Topic, x => x.Next, ct);

    public async Task<IReadOnlyDictionary<Topic, int>> CountNewByTopicAsync(CancellationToken ct) =>
        await db.Questions
            .Where(q => !db.Cards.Any(c => c.QuestionId == q.Id))
            .GroupBy(q => q.Topic)
            .Select(g => new { Topic = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Topic, x => x.Count, ct);

    public async Task<IReadOnlyList<Card>> GetDueCardsAsync(
        IReadOnlyCollection<Topic> topics, DateTime nowUtc, int take, CancellationToken ct)
    {
        var query = db.Cards.Include(c => c.Question).Where(c => !c.Suspended && c.DueAt <= nowUtc);
        if (topics.Count > 0)
            query = query.Where(c => topics.Contains(c.Question.Topic));

        return await query
            .OrderBy(c => c.DueAt).ThenBy(c => c.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Question>> GetNewQuestionsAsync(
        IReadOnlyCollection<Topic> topics, int take, CancellationToken ct)
    {
        var query = db.Questions.Where(q => !db.Cards.Any(c => c.QuestionId == q.Id));
        if (topics.Count > 0)
            query = query.Where(q => topics.Contains(q.Topic));

        return await query
            .OrderBy(q => q.Difficulty).ThenBy(q => q.Topic).ThenBy(q => q.Text).ThenBy(q => q.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<ReviewLog?> GetReviewAsync(Guid reviewId, CancellationToken ct) =>
        db.ReviewLogs.Include(r => r.Card).FirstOrDefaultAsync(r => r.ReviewId == reviewId, ct);

    public Task<ReviewLog?> GetReviewForUndoAsync(Guid reviewId, CancellationToken ct) =>
        db.ReviewLogs.AsTracking().Include(r => r.Card).FirstOrDefaultAsync(r => r.ReviewId == reviewId, ct);

    public Task<bool> HasLaterReviewAsync(Guid cardId, long reviewLogId, CancellationToken ct) =>
        db.ReviewLogs.AnyAsync(r => r.CardId == cardId && r.Id > reviewLogId, ct);

    public Task<Card?> GetCardForUpdateAsync(Guid questionId, CancellationToken ct) =>
        db.Cards.AsTracking().FirstOrDefaultAsync(c => c.QuestionId == questionId, ct);

    public Task<int> SetSuspendedAsync(Guid cardId, bool suspended, CancellationToken ct) =>
        db.Cards
            .Where(c => c.Id == cardId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Suspended, suspended), ct);

    public void AddCard(Card card) => db.Cards.Add(card);

    public void AddReview(ReviewLog review) => db.ReviewLogs.Add(review);

    public void RemoveCard(Card card) => db.Cards.Remove(card);

    public void RemoveReview(ReviewLog review) => db.ReviewLogs.Remove(review);
}
