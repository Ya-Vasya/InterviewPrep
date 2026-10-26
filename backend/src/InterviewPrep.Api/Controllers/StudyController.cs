using InterviewPrep.Services;
using InterviewPrep.Services.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace InterviewPrep.Api.Controllers;

[ApiController]
[Route("api/study")]
[Tags("Study")]
public class StudyController(IStudyService study) : ControllerBase
{
    [HttpGet("summary")]
    [EndpointName("GetStudySummary")]
    [ProducesResponseType<StudySummaryResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> GetSummary([FromQuery] string? tz, CancellationToken ct) =>
        Ok(await study.GetSummaryAsync(tz, ct));

    /// <summary>A pure read: it never creates cards, so abandoning a session leaves no trace.</summary>
    [HttpGet("queue")]
    [EndpointName("GetStudyQueue")]
    [ProducesResponseType<IReadOnlyList<StudyQueueItem>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> GetQueue(
        [FromQuery] string[]? topic, [FromQuery] int? limit, [FromQuery] string? tz, CancellationToken ct) =>
        Ok(await study.GetQueueAsync(topic, limit, tz, ct));

    [HttpPost("reviews")]
    [EndpointName("PostReview")]
    [ProducesResponseType<ReviewResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [Consumes("application/json")]
    public async Task<IActionResult> PostReview([FromBody] ReviewRequest request, CancellationToken ct) =>
        Ok(await study.RecordReviewAsync(request, ct));

    /// <summary>Undo: only the card's latest review can be undone.</summary>
    [HttpDelete("reviews/{reviewId:guid}")]
    [EndpointName("UndoReview")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> UndoReview(Guid reviewId, CancellationToken ct)
    {
        await study.UndoReviewAsync(reviewId, ct);

        return NoContent();
    }

    [HttpPost("cards/{cardId:guid}/suspend")]
    [EndpointName("SuspendCard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> SuspendCard(Guid cardId, CancellationToken ct)
    {
        await study.SetSuspendedAsync(cardId, true, ct);

        return NoContent();
    }

    [HttpPost("cards/{cardId:guid}/unsuspend")]
    [EndpointName("UnsuspendCard")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> UnsuspendCard(Guid cardId, CancellationToken ct)
    {
        await study.SetSuspendedAsync(cardId, false, ct);

        return NoContent();
    }
}
