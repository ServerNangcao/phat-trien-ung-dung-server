using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Application.Ports;

/// <summary>
/// Consumer-side port for the Ticketing module (TV4). The HTTP adapter maps Ticketing's
/// public API onto these models so Orders depends on nothing but this contract.
/// </summary>
public interface ITicketingModuleApi
{
    /// <summary>
    /// Fetch a ticket reservation with its authoritative items and prices.
    /// </summary>
    Task<Result<ReservationDto>> GetReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// List tickets issued for an event (used to build the organizer attendee list, FR-22).
    /// </summary>
    Task<Result<IReadOnlyList<TicketDto>>> GetTicketsByEventAsync(Guid eventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Release a reservation back to stock (FR-16 cancel order). Best-effort from the
    /// caller's perspective: if it fails, the reservation's 10-minute TTL releases the
    /// stock anyway.
    /// </summary>
    Task<Result> CancelReservationAsync(Guid reservationId, CancellationToken cancellationToken = default);
}

public sealed record ReservationItemDto(Guid TicketTypeId, string TicketTypeName, int Quantity, decimal UnitPrice);

public sealed record ReservationDto(Guid ReservationId, Guid CustomerId, Guid EventId,
    IReadOnlyList<ReservationItemDto> Items, DateTime ExpiresAtUtc, bool IsActive, string CustomerEmail = "");

public sealed record TicketDto(Guid TicketId, Guid OrderId, Guid CustomerId, Guid TicketTypeId,
    string TicketTypeName, string Status, DateTime IssuedAtUtc);
