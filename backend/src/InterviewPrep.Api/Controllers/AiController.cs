using InterviewPrep.Services;
using InterviewPrep.Services.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace InterviewPrep.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Tags("AI")]
public class AiController(
    IAiUsageService usage, IAnswerCheckService answers, IQuestionGenerationService generation) : ControllerBase
{
    [HttpGet("status")]
    [EndpointName("GetAiStatus")]
    [ProducesResponseType<AiStatusResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> GetStatus([FromQuery] string? tz, CancellationToken ct) =>
        Ok(await usage.GetStatusAsync(tz, ct));

    [HttpPost("answers/check")]
    [EndpointName("CheckAnswer")]
    [ProducesResponseType<CheckAnswerResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status501NotImplemented, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    [Consumes("application/json")]
    public async Task<IActionResult> CheckAnswer(
        [FromBody] CheckAnswerRequest request, [FromQuery] string? tz, CancellationToken ct) =>
        Ok(await answers.CheckAsync(request, tz, ct));

    /// <summary>The attempts for one question, newest first.</summary>
    [HttpGet("answers/attempts")]
    [EndpointName("GetAnswerAttempts")]
    [ProducesResponseType<PagedResponse<AnswerAttemptResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> GetAttempts(
        [FromQuery] Guid? questionId, [FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct) =>
        Ok(await answers.GetAttemptsAsync(questionId, limit, offset, ct));

    /// <summary>One-shot: nothing is stored. Saving the explanation as a note is a separate call.</summary>
    [HttpPost("answers/explain")]
    [EndpointName("ExplainMistakes")]
    [ProducesResponseType<ExplainMistakesResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status501NotImplemented, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    [Consumes("application/json")]
    public async Task<IActionResult> ExplainMistakes(
        [FromBody] ExplainMistakesRequest request, [FromQuery] string? tz, CancellationToken ct) =>
        Ok(await answers.ExplainMistakesAsync(request, tz, ct));

    /// <summary>One counted provider call. Nothing is stored; the user reviews the Drafts and accepts them separately.</summary>
    [HttpPost("questions/generate")]
    [EndpointName("GenerateQuestions")]
    [ProducesResponseType<GenerateQuestionsResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status501NotImplemented, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    [Consumes("application/json")]
    public async Task<IActionResult> GenerateQuestions(
        [FromBody] GenerateQuestionsRequest request, [FromQuery] string? tz, CancellationToken ct) =>
        Ok(await generation.GenerateAsync(request, tz, ct));

    /// <summary>Saves the valid Drafts as AI-sourced questions and reports the others by position.</summary>
    [HttpPost("questions/accept")]
    [EndpointName("AcceptQuestionDrafts")]
    [ProducesResponseType<AcceptDraftsResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [Consumes("application/json")]
    public async Task<IActionResult> AcceptDrafts([FromBody] AcceptDraftsRequest request, CancellationToken ct) =>
        Ok(await generation.AcceptAsync(request, ct));
}
