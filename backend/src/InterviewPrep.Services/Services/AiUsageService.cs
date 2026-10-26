using InterviewPrep.Data.Models;
using InterviewPrep.Data.Repositories;
using InterviewPrep.Infrastructure.Ai;
using InterviewPrep.Infrastructure.Options;
using InterviewPrep.Services.Dtos;
using InterviewPrep.Services.Exceptions;
using InterviewPrep.Services.Helpers;
using Microsoft.Extensions.Options;

namespace InterviewPrep.Services;

public sealed class AiUsageService(
    IAiCallRepository calls, IUnitOfWork unitOfWork, IAiProvider ai, TimeProvider time, IOptions<AiOptions> options)
    : IAiUsageService
{
    private const int MaxAttempts = 2; // the first call, plus one retry for invalid output

    public async Task<AiStatusResponse> GetStatusAsync(string? timeZone, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var zone = errors.RequireZone(timeZone);
        errors.ThrowIfAny();

        var dayStart = StudyDay.StartOfDayUtc(time.GetUtcNow().UtcDateTime, zone);
        var used = await calls.CountSinceAsync(dayStart, ct);

        return new AiStatusResponse(ai.IsConfigured, used, options.Value.DailyCap);
    }

    public async Task<T> RunAsync<T>(
        AiRequest request, Func<string, T?> parse, DateTime dayStartUtc, CancellationToken ct) where T : class
    {
        if (!ai.IsConfigured)
            throw new AiNotConfiguredException();

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var used = await calls.CountSinceAsync(dayStartUtc, ct);
            if (used >= options.Value.DailyCap)
                throw new AiDailyCapReachedException(options.Value.DailyCap);

            // Saved before the call, so a call that times out or fails still counts.
            calls.Add(new AiCall { CalledAt = time.GetUtcNow().UtcDateTime, Feature = request.Feature });
            await unitOfWork.SaveChangesAsync(ct);

            var json = await ai.CompleteJsonAsync(request, ct);

            if (parse(json) is { } value)
                return value;
        }

        throw new AiInvalidOutputException();
    }
}
