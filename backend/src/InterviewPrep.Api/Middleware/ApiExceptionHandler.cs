using InterviewPrep.Infrastructure.Ai;
using InterviewPrep.Services.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace InterviewPrep.Api.Middleware;

/// <summary>
/// Turns the typed exceptions the services throw into ProblemDetails responses, so every error body has the same
/// shape. Anything else is not handled here and becomes the default 500 problem.
/// </summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var problem = exception switch
        {
            ValidationException e => new HttpValidationProblemDetails(e.Errors.ToDictionary(x => x.Key, x => x.Value))
            {
                Status = StatusCodes.Status400BadRequest
            },
            NotFoundException => new ProblemDetails { Status = StatusCodes.Status404NotFound },
            ConflictException e => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict, Title = e.Title, Detail = e.Message
            },
            AiNotConfiguredException e => new ProblemDetails
            {
                Status = StatusCodes.Status501NotImplemented, Title = "AI is not configured", Detail = e.Message
            },
            AiDailyCapReachedException e => new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests, Title = "Daily AI limit reached", Detail = e.Message
            },
            AiInvalidOutputException e => new ProblemDetails
            {
                Status = StatusCodes.Status502BadGateway, Title = "Invalid AI output", Detail = e.Message
            },
            AiProviderException e => ProviderProblem(http, e),
            _ => null
        };

        if (problem is null)
            return false;

        http.Response.StatusCode = problem.Status!.Value;

        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    /// <summary>A provider failure; adds Retry-After when the provider said how long to wait.</summary>
    private static ProblemDetails ProviderProblem(HttpContext http, AiProviderException e)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "AI provider unavailable",
            Detail = e.Failure switch
            {
                AiFailure.Timeout => "The AI provider did not answer in time. Try again.",
                AiFailure.RateLimited => "The AI provider is rate limiting requests. Try again shortly.",
                _ => "The AI provider returned an error. Try again."
            }
        };

        if (e.RetryAfter is { } wait)
        {
            var seconds = (int)Math.Ceiling(wait.TotalSeconds);
            http.Response.Headers.RetryAfter = seconds.ToString();
            problem.Extensions["retryAfter"] = seconds;
        }

        return problem;
    }
}
