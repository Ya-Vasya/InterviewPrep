using InterviewPrep.Services;
using InterviewPrep.Services.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace InterviewPrep.Api.Controllers;

[ApiController]
[Route("api")]
[Tags("Questions")]
public class QuestionsController(IQuestionService questions) : ControllerBase
{
    [HttpGet("topics")]
    [EndpointName("GetTopics")]
    [ProducesResponseType<IReadOnlyList<TopicSummary>>(StatusCodes.Status200OK, "application/json")]
    public async Task<IActionResult> GetTopics(CancellationToken ct) =>
        Ok(await questions.GetTopicsAsync(ct));

    [HttpGet("questions")]
    [EndpointName("GetQuestions")]
    [ProducesResponseType<PagedResponse<QuestionResponse>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> GetQuestions(
        [FromQuery] string? topic, [FromQuery] string? difficulty, [FromQuery] string? tag,
        [FromQuery] int? limit, [FromQuery] int? offset, CancellationToken ct) =>
        Ok(await questions.GetQuestionsAsync(topic, difficulty, tag, limit, offset, ct));

    [HttpGet("questions/{id:guid}")]
    [EndpointName("GetQuestion")]
    [ProducesResponseType<QuestionResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetQuestion(Guid id, CancellationToken ct) =>
        Ok(await questions.GetQuestionAsync(id, ct));

    [HttpPost("questions")]
    [EndpointName("CreateQuestion")]
    [ProducesResponseType<QuestionResponse>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [Consumes("application/json")]
    public async Task<IActionResult> CreateQuestion([FromBody] CreateQuestionRequest request, CancellationToken ct)
    {
        var question = await questions.CreateQuestionAsync(request, ct);

        return Created($"/api/questions/{question.Id}", question);
    }
}
