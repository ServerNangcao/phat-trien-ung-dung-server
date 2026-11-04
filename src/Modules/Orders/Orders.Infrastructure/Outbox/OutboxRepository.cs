using Microsoft.EntityFrameworkCore;
using Ticketing.Infrastructure.Core.Outbox;

namespace Ticketing.Modules.Orders.Infrastructure.Outbox;

/// <summary>
/// Outbox read/write side for the orders schema. Writes ride the ambient unit of work;
/// the module-local delivery service and TV1's central worker both read this table
/// (the worker issues its own FOR UPDATE SKIP LOCKED query for multi-worker safety).
/// </summary>
public sealed class OutboxRepository : IOutbox
{
    private readonly OrdersDbContext _dbContext;

    public OutboxRepository(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _dbContext.Set<OutboxMessage>().AddAsync(message, cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        // Rows past the retry ceiling are dead-lettered: they stay visible in the table
        // for operator inspection but no longer block the delivery pipeline.
        return await _dbContext.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await _dbContext.Set<OutboxMessage>().FindAsync([id], cancellationToken);
        if (message is not null)
        {
            message.ProcessedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        var message = await _dbContext.Set<OutboxMessage>().FindAsync([id], cancellationToken);
        if (message is not null)
        {
            message.Error = error;
            message.RetryCount++;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
