using Microsoft.AspNetCore.Mvc;

namespace InterviewPrep.Api.Controllers;

public sealed record HealthResponse(string Status);

[ApiController]
[Route("api/health")]
[Tags("Health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    [EndpointName("Health")]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK, "application/json")]
    public IActionResult Get() => Ok(new HealthResponse("ok"));
}
