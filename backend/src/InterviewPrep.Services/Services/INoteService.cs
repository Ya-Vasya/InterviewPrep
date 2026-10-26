using InterviewPrep.Services.Dtos;

namespace InterviewPrep.Services;

public interface INoteService
{
    /// <exception cref="Exceptions.NotFoundException">The question has no note yet (or does not exist).</exception>
    Task<NoteResponse> GetNoteAsync(Guid questionId, CancellationToken ct);

    /// <summary>
    /// Saves the question's note, replacing the existing one. Asking the user before replacing is the client's
    /// job; the server keeps no history.
    /// </summary>
    /// <exception cref="Exceptions.NotFoundException">The question does not exist.</exception>
    /// <exception cref="Exceptions.ValidationException">The text is missing or too long.</exception>
    Task<NoteResponse> SaveNoteAsync(Guid questionId, SaveNoteRequest request, CancellationToken ct);
}
