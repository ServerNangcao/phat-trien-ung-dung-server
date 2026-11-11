using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Application.Data;

/// <summary>
/// Commit boundary for the Notifications module. Persists staged changes atomically —
/// e.g. an inbox row together with its Pending email history row. Returns Failure only
/// for a concurrent-duplicate commit (unique constraint); transient failures throw so
/// the delivering outbox worker retries instead of the event being dropped.
/// </summary>
public interface IUnitOfWork
{
    Task<Result> CommitAsync(CancellationToken cancellationToken = default);
}
