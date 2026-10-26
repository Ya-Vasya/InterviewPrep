using InterviewPrep.Data.Models;
using InterviewPrep.Data.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace InterviewPrep.Data.Repositories;

public sealed class QuestionRepository(AppDbContext db) : IQuestionRepository
{
    public Task<Question?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Questions.FirstOrDefaultAsync(q => q.Id == id, ct);

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) =>
        db.Questions.AnyAsync(q => q.Id == id, ct);

    public async Task<IReadOnlyDictionary<Topic, int>> CountByTopicAsync(CancellationToken ct) =>
        await db.Questions
            .GroupBy(q => q.Topic)
            .Select(g => new { Topic = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Topic, x => x.Count, ct);

    public async Task<(IReadOnlyList<Question> Items, int Total)> SearchAsync(
        Topic? topic, Difficulty? difficulty, string? tag, int skip, int take, CancellationToken ct)
    {
        var query = db.Questions.AsQueryable();

        if (topic is { } t)
            query = query.Where(q => q.Topic == t);

        if (difficulty is { } d)
            query = query.Where(q => q.Difficulty == d);

        if (!string.IsNullOrEmpty(tag))
            query = query.Where(q => q.Tags.Contains(tag));

        var total = await query.CountAsync(ct);

        // Id is the final tie-breaker so pages never overlap or skip rows when texts are equal.
        var items = await query
            .OrderBy(q => q.Topic).ThenBy(q => q.Difficulty).ThenBy(q => q.Text).ThenBy(q => q.Id)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<Question>> GetByTopicsAsync(IReadOnlyCollection<Topic> topics, CancellationToken ct) =>
        await db.Questions.Where(q => topics.Contains(q.Topic)).ToListAsync(ct);

    public void Add(Question question) => db.Questions.Add(question);

    public void AddRange(IEnumerable<Question> questions) => db.Questions.AddRange(questions);
}
