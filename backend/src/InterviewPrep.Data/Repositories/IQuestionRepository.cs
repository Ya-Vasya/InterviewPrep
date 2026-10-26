using InterviewPrep.Data.Models;
using InterviewPrep.Data.Models.Enums;

namespace InterviewPrep.Data.Repositories;

public interface IQuestionRepository
{
    Task<Question?> GetByIdAsync(Guid id, CancellationToken ct);

    Task<bool> ExistsAsync(Guid id, CancellationToken ct);

    /// <summary>Number of questions per topic. Topics without questions are absent.</summary>
    Task<IReadOnlyDictionary<Topic, int>> CountByTopicAsync(CancellationToken ct);

    /// <summary>One page of the matches in a stable order (Id is the final tie-breaker), plus the total number of matches.</summary>
    /// <param name="tag">Already normalized; null or empty means any tag.</param>
    Task<(IReadOnlyList<Question> Items, int Total)> SearchAsync(
        Topic? topic, Difficulty? difficulty, string? tag, int skip, int take, CancellationToken ct);

    Task<IReadOnlyList<Question>> GetByTopicsAsync(IReadOnlyCollection<Topic> topics, CancellationToken ct);

    void Add(Question question);

    void AddRange(IEnumerable<Question> questions);
}
