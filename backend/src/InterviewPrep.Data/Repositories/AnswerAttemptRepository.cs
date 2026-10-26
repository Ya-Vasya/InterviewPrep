using InterviewPrep.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace InterviewPrep.Data.Repositories;

public sealed class AnswerAttemptRepository(AppDbContext db) : IAnswerAttemptRepository
{
    public Task<AnswerAttempt?> GetWithQuestionAsync(Guid id, CancellationToken ct) =>
        db.AnswerAttempts.Include(a => a.Question).FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<AnswerAttempt?> GetForQuestionAsync(Guid id, Guid questionId, CancellationToken ct) =>
        db.AnswerAttempts.FirstOrDefaultAsync(a => a.Id == id && a.QuestionId == questionId, ct);

    public async Task<(IReadOnlyList<AnswerAttempt> Items, int Total)> GetPageByQuestionAsync(
        Guid questionId, int skip, int take, CancellationToken ct)
    {
        var query = db.AnswerAttempts.Where(a => a.QuestionId == questionId);
        var total = await query.CountAsync(ct);

        // Id is the final tie-breaker so pages never overlap or skip rows with equal timestamps.
        var items = await query
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        return (items, total);
    }

    public void Add(AnswerAttempt attempt) => db.AnswerAttempts.Add(attempt);
}
