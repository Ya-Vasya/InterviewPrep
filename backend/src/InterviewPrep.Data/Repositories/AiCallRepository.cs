using InterviewPrep.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace InterviewPrep.Data.Repositories;

public sealed class AiCallRepository(AppDbContext db) : IAiCallRepository
{
    public Task<int> CountSinceAsync(DateTime sinceUtc, CancellationToken ct) =>
        db.AiCalls.CountAsync(c => c.CalledAt >= sinceUtc, ct);

    public void Add(AiCall call) => db.AiCalls.Add(call);
}
