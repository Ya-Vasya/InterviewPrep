using InterviewPrep.Api.Common;
using InterviewPrep.Api.Data;
using InterviewPrep.Api.Features.Study;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InterviewPrep.Api.Features.Ai;

public static class AiEndpoints
{
    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ai").WithTags("AI");

        group.MapGet("/status", GetStatus).WithName("GetAiStatus");
        group.MapPost("/answers/check", CheckAnswer).WithName("CheckAnswer");
        group.MapGet("/answers/attempts", GetAttempts).WithName("GetAnswerAttempts");
        group.MapPost("/answers/explain", ExplainMistakes).WithName("ExplainMistakes");

        return app;
    }

    private static async Task<Results<Ok<AiStatusResponse>, ValidationProblem>> GetStatus(
        string? tz, AppDbContext db, IAiService ai, TimeProvider time, IOptions<AiOptions> options, CancellationToken ct)
    {
        if (!StudyDay.TryGetZone(tz, out var zone))
        {
            var errors = new ValidationErrors();
            errors.Add("tz", "Unknown time zone.");
            return errors.ToProblem();
        }

        var dayStart = StudyDay.StartOfDayUtc(time.GetUtcNow().UtcDateTime, zone);
        var used = await db.AiCalls.CountAsync(c => c.CalledAt >= dayStart, ct);

        return TypedResults.Ok(new AiStatusResponse(ai.IsConfigured, used, options.Value.DailyCap));
    }

    /// <summary>The attempts for one question, newest first.</summary>
    private static async Task<Results<Ok<PagedResponse<AnswerAttemptResponse>>, ValidationProblem>> GetAttempts(
        Guid? questionId, int? limit, int? offset, AppDbContext db, CancellationToken ct)
    {
        var errors = new ValidationErrors();

        if (questionId is null || questionId == Guid.Empty)
            errors.Add("questionId", "Question id is required.");

        Paging.TryResolve(limit, offset, errors, out var take, out var skip);

        if (errors.Any)
            return errors.ToProblem();

        var query = db.AnswerAttempts.Where(a => a.QuestionId == questionId);
        var total = await query.CountAsync(ct);

        // Id is the final tie-breaker so pages never overlap or skip rows with equal timestamps.
        var attempts = await query
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Skip(skip).Take(take)
            .ToListAsync(ct);

        return TypedResults.Ok(new PagedResponse<AnswerAttemptResponse>(
            attempts.Select(AnswerAttemptResponse.From).ToList(), total, take, skip));
    }

    /// <summary>One-shot: nothing is stored. Saving the explanation as a note is a separate call.</summary>
    private static async Task<Results<Ok<ExplainMistakesResponse>, ValidationProblem, ProblemHttpResult>> ExplainMistakes(
        ExplainMistakesRequest request, string? tz,
        AppDbContext db, AiGateway gateway, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        var errors = new ValidationErrors();

        if (request.AttemptId is null || request.AttemptId == Guid.Empty)
            errors.Add("attemptId", "Attempt id is required.");

        if (!StudyDay.TryGetZone(tz, out var zone))
            errors.Add("tz", "Unknown time zone.");

        var attempt = errors.Any
            ? null
            : await db.AnswerAttempts.Include(a => a.Question).FirstOrDefaultAsync(a => a.Id == request.AttemptId, ct);

        if (!errors.Any)
        {
            if (attempt is null)
                errors.Add("attemptId", "No answer attempt with this id exists.");
            else if (attempt.Verdict == Verdict.Correct)
                errors.Add("attemptId", "Only partial or incorrect attempts can be explained.");
        }

        if (errors.Any)
            return errors.ToProblem();

        var outcome = await gateway.RunAsync(
            ExplainMistakesPrompt.Build(attempt!.Question.Text, attempt.Question.Answer, attempt),
            ExplainMistakesPrompt.Parse,
            StudyDay.StartOfDayUtc(time.GetUtcNow().UtcDateTime, zone),
            ct);

        if (!outcome.Succeeded)
            return outcome.ToProblem(http);

        return TypedResults.Ok(new ExplainMistakesResponse(attempt.Id, outcome.Value!.Markdown));
    }

    private static async Task<Results<Ok<CheckAnswerResponse>, ValidationProblem, ProblemHttpResult>> CheckAnswer(
        CheckAnswerRequest request, string? tz,
        AppDbContext db, AiGateway gateway, TimeProvider time, HttpContext http, CancellationToken ct)
    {
        var errors = new ValidationErrors();

        if (request.QuestionId is null || request.QuestionId == Guid.Empty)
            errors.Add("questionId", "Question id is required.");

        if (string.IsNullOrWhiteSpace(request.Answer))
            errors.Add("answer", "Answer is required.");
        else if (request.Answer.Length > CheckAnswerLimits.MaxAnswerLength)
            errors.Add("answer", $"Answer must be at most {CheckAnswerLimits.MaxAnswerLength} characters.");

        if (!StudyDay.TryGetZone(tz, out var zone))
            errors.Add("tz", "Unknown time zone.");

        var question = errors.Any
            ? null
            : await db.Questions.FirstOrDefaultAsync(q => q.Id == request.QuestionId, ct);

        if (!errors.Any && question is null)
            errors.Add("questionId", "No question with this id exists.");

        if (errors.Any)
            return errors.ToProblem();

        var now = time.GetUtcNow().UtcDateTime;
        var outcome = await gateway.RunAsync(
            CheckAnswerPrompt.Build(question!.Text, question.Answer, request.Answer!),
            CheckAnswerPrompt.Parse,
            StudyDay.StartOfDayUtc(now, zone),
            ct);

        if (!outcome.Succeeded)
            return outcome.ToProblem(http);

        var check = outcome.Value!;
        var attempt = new AnswerAttempt
        {
            QuestionId = question.Id,
            Answer = request.Answer!,
            Verdict = check.Verdict,
            Score = check.Score,
            Covered = check.Covered,
            Missing = check.Missing,
            Feedback = check.Feedback,
            CreatedAt = now
        };
        db.AnswerAttempts.Add(attempt);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(new CheckAnswerResponse(
            attempt.Id, check.Verdict, check.Score, check.Covered, check.Missing, check.Feedback,
            check.Verdict.SuggestedRating()));
    }
}
