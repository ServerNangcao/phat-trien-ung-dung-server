using Ticketing.Modules.Orders.Domain.Entities;

namespace Ticketing.Modules.Orders.Application.Repositories;

/// <summary>
/// Repository port for the Order aggregate and its refund requests.
/// Lives in the Application layer so the domain never depends on persistence.
/// </summary>
public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load a tracked order so state changes can be committed through the unit of work.
    /// </summary>
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Order?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> ListByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> ListByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task AddRefundRequestAsync(RefundRequest refundRequest, CancellationToken cancellationToken = default);

    Task<RefundRequest?> GetRefundRequestByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RefundRequest>> ListRefundRequestsAsync(RefundStatus? status, CancellationToken cancellationToken = default);
}
