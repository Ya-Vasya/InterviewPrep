using InterviewPrep.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace InterviewPrep.Data.Repositories;

public sealed class NoteRepository(AppDbContext db) : INoteRepository
{
    public Task<Note?> GetByQuestionIdAsync(Guid questionId, CancellationToken ct) =>
        db.Notes.FirstOrDefaultAsync(n => n.QuestionId == questionId, ct);

    public void Add(Note note) => db.Notes.Add(note);

    public void Update(Note note) => db.Notes.Update(note);
}
