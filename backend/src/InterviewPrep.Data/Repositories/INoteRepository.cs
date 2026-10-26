using InterviewPrep.Data.Models;

namespace InterviewPrep.Data.Repositories;

public interface INoteRepository
{
    Task<Note?> GetByQuestionIdAsync(Guid questionId, CancellationToken ct);

    void Add(Note note);

    /// <summary>Marks a note loaded earlier as changed (the context does not track by default).</summary>
    void Update(Note note);
}
