using InterviewPrep.Data.Models;
using InterviewPrep.Data.Repositories;
using InterviewPrep.Services.Dtos;
using InterviewPrep.Services.Exceptions;
using InterviewPrep.Services.Helpers;
using InterviewPrep.Services.Rules;

namespace InterviewPrep.Services;

public sealed class NoteService(
    INoteRepository notes, IQuestionRepository questions, IUnitOfWork unitOfWork, TimeProvider time) : INoteService
{
    public async Task<NoteResponse> GetNoteAsync(Guid questionId, CancellationToken ct) =>
        (await notes.GetByQuestionIdAsync(questionId, ct) ?? throw new NotFoundException()).ToResponse();

    public async Task<NoteResponse> SaveNoteAsync(Guid questionId, SaveNoteRequest request, CancellationToken ct)
    {
        if (!await questions.ExistsAsync(questionId, ct))
            throw new NotFoundException();

        var errors = new ValidationErrors();

        if (string.IsNullOrWhiteSpace(request.Text))
            errors.Add("text", "Text is required.");
        else if (request.Text.Length > NoteLimits.MaxTextLength)
            errors.Add("text", $"Text must be at most {NoteLimits.MaxTextLength} characters.");

        errors.ThrowIfAny();

        var now = time.GetUtcNow().UtcDateTime;
        var note = await notes.GetByQuestionIdAsync(questionId, ct);

        if (note is null)
        {
            note = new Note { QuestionId = questionId, Text = request.Text!, UpdatedAt = now };
            notes.Add(note);
        }
        else
        {
            note.Text = request.Text!;
            note.UpdatedAt = now;
            notes.Update(note);
        }

        await unitOfWork.SaveChangesAsync(ct);

        return note.ToResponse();
    }
}
