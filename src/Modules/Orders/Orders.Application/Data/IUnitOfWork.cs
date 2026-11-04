namespace Ticketing.Modules.Orders.Application.Data;

/// <summary>
/// Commit boundary for the Orders module. Persists aggregate changes and, in the same
/// transaction, writes the mapped integration events to the outbox.
/// </summary>
public interface IUnitOfWork
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
