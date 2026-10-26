namespace InterviewPrep.Data.Repositories;

/// <summary>
/// Saves everything the repositories added, changed or removed in one transaction, so a use case that touches
/// several entities (a review creates a card and a log entry) either happens completely or not at all.
/// </summary>
public interface IUnitOfWork
{
    /// <exception cref="Exceptions.UniqueConstraintViolationException">A unique index rejected the save.</exception>
    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>Forgets every pending change, e.g. before retrying after a rejected save.</summary>
    void DiscardChanges();
}
