using InterviewPrep.Data.Models;

namespace InterviewPrep.Data.Repositories;

public interface IAiCallRepository
{
    /// <summary>Provider calls recorded at or after <paramref name="sinceUtc"/>.</summary>
    Task<int> CountSinceAsync(DateTime sinceUtc, CancellationToken ct);

    void Add(AiCall call);
}
