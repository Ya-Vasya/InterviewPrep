using InterviewPrep.Data.Models;
using InterviewPrep.Data.Models.Enums;
using InterviewPrep.Data.Repositories;
using InterviewPrep.Infrastructure.Options;
using InterviewPrep.Services.Dtos;
using InterviewPrep.Services.Helpers;
using InterviewPrep.Services.Prompts;
using Microsoft.Extensions.Options;

namespace InterviewPrep.Services;

public sealed class AnswerCheckService(
    IQuestionRepository questions,
    IAnswerAttemptRepository attempts,
    IAiUsageService ai,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    IOptions<PagingOptions> paging) : IAnswerCheckService
{
    public async Task<CheckAnswerResponse> CheckAsync(CheckAnswerRequest request, string? timeZone, CancellationToken ct)
    {
        var errors = new ValidationErrors();

        if (request.QuestionId is null || request.QuestionId == Guid.Empty)
            errors.Add("questionId", "Question id is required.");

        if (string.IsNullOrWhiteSpace(request.Answer))
            errors.Add("answer", "Answer is required.");
        else if (request.Answer.Length > CheckAnswerLimits.MaxAnswerLength)
            errors.Add("answer", $"Answer must be at most {CheckAnswerLimits.MaxAnswerLength} characters.");

        var zone = errors.RequireZone(timeZone);

        var question = errors.Any ? null : await questions.GetByIdAsync(request.QuestionId!.Value, ct);

        if (!errors.Any && question is null)
            errors.Add("questionId", "No question with this id exists.");

        errors.ThrowIfAny();

        var now = time.GetUtcNow().UtcDateTime;
        var check = await ai.RunAsync(
            CheckAnswerPrompt.Build(question!.Text, question.Answer, request.Answer!),
            CheckAnswerPrompt.Parse,
            StudyDay.StartOfDayUtc(now, zone),
            ct);

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
        attempts.Add(attempt);
        await unitOfWork.SaveChangesAsync(ct);

        return new CheckAnswerResponse(
            attempt.Id, check.Verdict, check.Score, check.Covered, check.Missing, check.Feedback,
            check.Verdict.SuggestedRating());
    }

    public async Task<PagedResponse<AnswerAttemptResponse>> GetAttemptsAsync(
        Guid? questionId, int? limit, int? offset, CancellationToken ct)
    {
        var errors = new ValidationErrors();

        if (questionId is null || questionId == Guid.Empty)
            errors.Add("questionId", "Question id is required.");

        Paging.TryResolve(limit, offset, paging.Value, errors, out var take, out var skip);
        errors.ThrowIfAny();

        var (items, total) = await attempts.GetPageByQuestionAsync(questionId!.Value, skip, take, ct);

        return new PagedResponse<AnswerAttemptResponse>(items.Select(a => a.ToResponse()).ToList(), total, take, skip);
    }

    public async Task<ExplainMistakesResponse> ExplainMistakesAsync(
        ExplainMistakesRequest request, string? timeZone, CancellationToken ct)
    {
        var errors = new ValidationErrors();

        if (request.AttemptId is null || request.AttemptId == Guid.Empty)
            errors.Add("attemptId", "Attempt id is required.");

        var zone = errors.RequireZone(timeZone);

        var attempt = errors.Any ? null : await attempts.GetWithQuestionAsync(request.AttemptId!.Value, ct);

        if (!errors.Any)
        {
            if (attempt is null)
                errors.Add("attemptId", "No answer attempt with this id exists.");
            else if (attempt.Verdict == Verdict.Correct)
                errors.Add("attemptId", "Only partial or incorrect attempts can be explained.");
        }

        errors.ThrowIfAny();

        var explanation = await ai.RunAsync(
            ExplainMistakesPrompt.Build(attempt!.Question.Text, attempt.Question.Answer, attempt),
            ExplainMistakesPrompt.Parse,
            StudyDay.StartOfDayUtc(time.GetUtcNow().UtcDateTime, zone),
            ct);

        return new ExplainMistakesResponse(attempt.Id, explanation.Markdown);
    }
}
