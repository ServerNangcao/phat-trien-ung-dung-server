using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Application.Data;

/// <summary>
/// Commit boundary for the Orders module. Persists aggregate changes and, in the same
/// transaction, writes the mapped integration events to the outbox. Returns Failure only
/// for a concurrent-duplicate commit (unique constraint, e.g. two creates racing on one
/// reservation); transient failures throw so HTTP callers return 500 and VNPay retries.
/// </summary>
public interface IUnitOfWork
{
    Task<Result> CommitAsync(CancellationToken cancellationToken = default);
}
