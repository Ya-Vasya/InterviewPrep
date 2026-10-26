using InterviewPrep.Services.Dtos;

namespace InterviewPrep.Services;

public interface IAnswerCheckService
{
    /// <summary>
    /// Has the AI grade the user's answer and stores the attempt. Never rates a card: the verdict only carries a
    /// suggested rating.
    /// </summary>
    /// <exception cref="Exceptions.ValidationException">The request breaks a rule or the question does not exist.</exception>
    Task<CheckAnswerResponse> CheckAsync(CheckAnswerRequest request, string? timeZone, CancellationToken ct);

    /// <summary>The attempts for one question, newest first.</summary>
    /// <exception cref="Exceptions.ValidationException">The question id is missing or paging is out of range.</exception>
    Task<PagedResponse<AnswerAttemptResponse>> GetAttemptsAsync(
        Guid? questionId, int? limit, int? offset, CancellationToken ct);

    /// <summary>One-shot: nothing is stored. Saving the explanation as a note is a separate call.</summary>
    /// <exception cref="Exceptions.ValidationException">
    /// The attempt id is missing, unknown or for a correct answer, or the time zone is unknown.
    /// </exception>
    Task<ExplainMistakesResponse> ExplainMistakesAsync(ExplainMistakesRequest request, string? timeZone, CancellationToken ct);
}
