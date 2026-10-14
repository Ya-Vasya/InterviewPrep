using InterviewPrep.Api.Common;
using InterviewPrep.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace InterviewPrep.Api.Features.Notes;

public static class NoteEndpoints
{
    public static IEndpointRouteBuilder MapNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/questions/{questionId:guid}/note").WithTags("Notes");

        group.MapGet("", GetNote).WithName(nameof(GetNote));
        group.MapPut("", SaveNote).WithName(nameof(SaveNote));

        return app;
    }

    /// <summary>404 when the question has no note yet (or does not exist).</summary>
    private static async Task<Results<Ok<NoteResponse>, NotFound>> GetNote(
        Guid questionId, AppDbContext db, CancellationToken ct)
    {
        var note = await db.Notes.FirstOrDefaultAsync(n => n.QuestionId == questionId, ct);

        return note is null ? TypedResults.NotFound() : TypedResults.Ok(NoteResponse.From(note));
    }

    /// <summary>
    /// Saves the question's note, replacing the existing one. Asking the user before replacing is the client's
    /// job; the server keeps no history.
    /// </summary>
    private static async Task<Results<Ok<NoteResponse>, ValidationProblem, NotFound>> SaveNote(
        Guid questionId, SaveNoteRequest request, AppDbContext db, TimeProvider time, CancellationToken ct)
    {
        if (!await db.Questions.AnyAsync(q => q.Id == questionId, ct))
            return TypedResults.NotFound();

        var errors = new ValidationErrors();

        if (string.IsNullOrWhiteSpace(request.Text))
            errors.Add("text", "Text is required.");
        else if (request.Text.Length > NoteLimits.MaxTextLength)
            errors.Add("text", $"Text must be at most {NoteLimits.MaxTextLength} characters.");

        if (errors.Any)
            return errors.ToProblem();

        var now = time.GetUtcNow().UtcDateTime;
        var note = await db.Notes.FirstOrDefaultAsync(n => n.QuestionId == questionId, ct);

        if (note is null)
        {
            note = new Note { QuestionId = questionId, Text = request.Text!, UpdatedAt = now };
            db.Notes.Add(note);
        }
        else
        {
            note.Text = request.Text!;
            note.UpdatedAt = now;
            db.Notes.Update(note);
        }

        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(NoteResponse.From(note));
    }
}
