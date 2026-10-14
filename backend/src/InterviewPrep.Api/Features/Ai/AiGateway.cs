using InterviewPrep.Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace InterviewPrep.Api.Features.Ai;

/// <summary>Either the parsed value, or the problem to answer with.</summary>
public sealed class AiOutcome<T>
{
    public T? Value { get; private init; }
    public int StatusCode { get; private init; }
    public string Title { get; private init; } = "";
    public string Detail { get; private init; } = "";
    public TimeSpan? RetryAfter { get; private init; }

    public bool Succeeded => StatusCode == 0;

    public static AiOutcome<T> Ok(T value) => new() { Value = value };

    public static AiOutcome<T> Fail(int status, string title, string detail, TimeSpan? retryAfter = null) =>
        new() { StatusCode = status, Title = title, Detail = detail, RetryAfter = retryAfter };

    /// <summary>The failure as a ProblemDetails response; adds Retry-After when the provider said how long to wait.</summary>
    public ProblemHttpResult ToProblem(HttpContext http)
    {
        var extensions = new Dictionary<string, object?>();
        if (RetryAfter is { } wait)
        {
            var seconds = (int)Math.Ceiling(wait.TotalSeconds);
            http.Response.Headers.RetryAfter = seconds.ToString();
            extensions["retryAfter"] = seconds;
        }

        return TypedResults.Problem(statusCode: StatusCode, title: Title, detail: Detail, extensions: extensions);
    }
}

/// <summary>
/// The one door to the provider. Refuses when AI is off or the daily cap is used up, records every call that
/// reaches the provider (also failed ones, they cost money), retries invalid output once and maps provider
/// failures to HTTP problems.
/// </summary>
public sealed class AiGateway(AppDbContext db, IAiService ai, TimeProvider time, IOptions<AiOptions> options)
{
    private const int MaxAttempts = 2; // the first call, plus one retry for invalid output

    /// <param name="parse">Returns null when the model's JSON does not satisfy the schema.</param>
    /// <param name="dayStartUtc">When the user's current day began; the daily cap counts from here.</param>
    public async Task<AiOutcome<T>> RunAsync<T>(
        AiRequest request, Func<string, T?> parse, DateTime dayStartUtc, CancellationToken ct) where T : class
    {
        if (!ai.IsConfigured)
        {
            return AiOutcome<T>.Fail(StatusCodes.Status501NotImplemented, "AI is not configured",
                "No Anthropic API key is configured, so AI features are unavailable.");
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var used = await db.AiCalls.CountAsync(c => c.CalledAt >= dayStartUtc, ct);
            if (used >= options.Value.DailyCap)
            {
                return AiOutcome<T>.Fail(StatusCodes.Status429TooManyRequests, "Daily AI limit reached",
                    $"The daily limit of {options.Value.DailyCap} AI calls is used up. It resets with your next day.");
            }

            // Saved before the call, so a call that times out or fails still counts.
            db.AiCalls.Add(new AiCall { CalledAt = time.GetUtcNow().UtcDateTime, Feature = request.Feature });
            await db.SaveChangesAsync(ct);

            string json;
            try
            {
                json = await ai.CompleteJsonAsync(request, ct);
            }
            catch (AiProviderException e)
            {
                return AiOutcome<T>.Fail(StatusCodes.Status503ServiceUnavailable, "AI provider unavailable",
                    e.Failure switch
                    {
                        AiFailure.Timeout => "The AI provider did not answer in time. Try again.",
                        AiFailure.RateLimited => "The AI provider is rate limiting requests. Try again shortly.",
                        _ => "The AI provider returned an error. Try again."
                    },
                    e.RetryAfter);
            }

            if (parse(json) is { } value)
                return AiOutcome<T>.Ok(value);
        }

        return AiOutcome<T>.Fail(StatusCodes.Status502BadGateway, "Invalid AI output",
            "The AI returned an answer that could not be understood, twice. Try again.");
    }
}
