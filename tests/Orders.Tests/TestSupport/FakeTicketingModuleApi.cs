using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.SharedKernel;

namespace Ticketing.Tests.Orders.TestSupport;

/// <summary>
/// In-memory stand-in for the Ticketing module (TV4). Tests register the reservations
/// and tickets they need; calls that miss registration return NotFound.
/// </summary>
public sealed class FakeTicketingModuleApi : ITicketingModuleApi
{
    private readonly Dictionary<Guid, ReservationDto> _reservations = new();
    private readonly Dictionary<Guid, IReadOnlyList<TicketDto>> _ticketsByEvent = new();

    public int ReservationLookups { get; private set; }

    /// <summary>When set, every lookup fails with a transient internal error (module outage).</summary>
    public bool SimulateOutage { get; set; }

    public void AddReservation(ReservationDto reservation) => _reservations[reservation.ReservationId] = reservation;

    public void SetTickets(Guid eventId, IReadOnlyList<TicketDto> tickets) => _ticketsByEvent[eventId] = tickets;

    public void Reset()
    {
        _reservations.Clear();
        _ticketsByEvent.Clear();
        ReservationLookups = 0;
        SimulateOutage = false;
    }

    public Task<Result<ReservationDto>> GetReservationAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        ReservationLookups++;
        if (SimulateOutage)
            return Task.FromResult(Result<ReservationDto>.Failure(Error.Internal("Ticketing module unreachable: connection refused")));

        return Task.FromResult(_reservations.TryGetValue(reservationId, out var reservation)
            ? Result<ReservationDto>.Success(reservation)
            : Result<ReservationDto>.Failure(Error.NotFound("Reservation not found.")));
    }

    public Task<Result<IReadOnlyList<TicketDto>>> GetTicketsByEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_ticketsByEvent.TryGetValue(eventId, out var tickets)
            ? Result<IReadOnlyList<TicketDto>>.Success(tickets)
            : Result<IReadOnlyList<TicketDto>>.Failure(Error.NotFound("No tickets for event.")));
    }
}
