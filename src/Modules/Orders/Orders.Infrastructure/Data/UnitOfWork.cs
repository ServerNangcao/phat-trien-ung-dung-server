using Ticketing.Modules.Orders.Application.Data;

namespace Ticketing.Modules.Orders.Infrastructure.Data;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly OrdersDbContext _dbContext;

    public UnitOfWork(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        // OrdersDbContext.SaveChangesAsync appends the mapped integration events to the
        // outbox inside the same transaction as the aggregate state change.
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
