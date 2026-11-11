using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ticketing.Modules.Orders.Application.Data;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Infrastructure.Data;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly OrdersDbContext _dbContext;

    public UnitOfWork(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // OrdersDbContext.SaveChangesAsync appends the mapped integration events to the
            // outbox inside the same transaction as the aggregate state change.
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Two creates racing on one reservation (or two IPNs on one order): the loser
            // reports a duplicate; callers recover idempotently instead of surfacing a 500.
            return Result.Failure(Error.Conflict("A duplicate record was committed concurrently."));
        }
    }
}
