using InterviewPrep.Data.Models;

namespace InterviewPrep.Data.Repositories;

public interface IAnswerAttemptRepository
{
    /// <summary>The attempt with its question loaded, or null.</summary>
    Task<AnswerAttempt?> GetWithQuestionAsync(Guid id, CancellationToken ct);

    /// <summary>The attempt, only if it belongs to <paramref name="questionId"/>.</summary>
    Task<AnswerAttempt?> GetForQuestionAsync(Guid id, Guid questionId, CancellationToken ct);

    /// <summary>One page of the question's attempts, newest first, plus the total.</summary>
    Task<(IReadOnlyList<AnswerAttempt> Items, int Total)> GetPageByQuestionAsync(
        Guid questionId, int skip, int take, CancellationToken ct);

    void Add(AnswerAttempt attempt);
}
