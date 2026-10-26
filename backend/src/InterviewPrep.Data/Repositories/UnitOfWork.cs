using InterviewPrep.Data.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InterviewPrep.Data.Repositories;

public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UniqueConstraintViolationException(e);
        }
    }

    public void DiscardChanges() => db.ChangeTracker.Clear();
}
