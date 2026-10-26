using InterviewPrep.Services;
using InterviewPrep.Services.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace InterviewPrep.Api.Controllers;

[ApiController]
[Route("api/questions/{questionId:guid}/note")]
[Tags("Notes")]
public class NotesController(INoteService notes) : ControllerBase
{
    /// <summary>404 when the question has no note yet (or does not exist).</summary>
    [HttpGet]
    [EndpointName("GetNote")]
    [ProducesResponseType<NoteResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetNote(Guid questionId, CancellationToken ct) =>
        Ok(await notes.GetNoteAsync(questionId, ct));

    [HttpPut]
    [EndpointName("SaveNote")]
    [ProducesResponseType<NoteResponse>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<HttpValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [Consumes("application/json")]
    public async Task<IActionResult> SaveNote(Guid questionId, [FromBody] SaveNoteRequest request, CancellationToken ct) =>
        Ok(await notes.SaveNoteAsync(questionId, request, ct));
}
