using Microsoft.EntityFrameworkCore;
using Ticketing.Modules.Orders.Application.Repositories;
using Ticketing.Modules.Orders.Domain.Entities;

namespace Ticketing.Modules.Orders.Infrastructure.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _dbContext;

    public OrderRepository(OrdersDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _dbContext.Orders.AddAsync(order, cancellationToken);
    }

    /// <summary>
    /// Loads a tracked order so callers can mutate the aggregate and commit through the unit of work.
    /// </summary>
    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<Order?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.ReservationId == reservationId, cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> ListByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> ListByEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Where(o => o.EventId == eventId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> ListByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
            return Array.Empty<Order>();

        return await _dbContext.Orders
            .AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddRefundRequestAsync(RefundRequest refundRequest, CancellationToken cancellationToken = default)
    {
        await _dbContext.RefundRequests.AddAsync(refundRequest, cancellationToken);
    }

    public async Task<RefundRequest?> GetRefundRequestByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RefundRequests.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyList<RefundRequest>> ListRefundRequestsAsync(RefundStatus? status, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RefundRequests
            .AsNoTracking()
            .Where(r => status == null || r.Status == status)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
