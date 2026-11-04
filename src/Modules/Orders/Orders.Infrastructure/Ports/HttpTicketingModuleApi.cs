using System.Net.Http.Json;
using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Orders.Infrastructure.Ports;

/// <summary>
/// HTTP adapter for the Ticketing module's public API (TV4). The routes below are the
/// consumer-driven contract Orders relies on; the Ticketing module owns their implementation.
/// </summary>
public sealed class HttpTicketingModuleApi : ITicketingModuleApi
{
    private readonly HttpClient _httpClient;

    public HttpTicketingModuleApi(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result<ReservationDto>> GetReservationAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        try
        {
            var reservation = await _httpClient.GetFromJsonAsync<ReservationDto>(
                $"api/ticketing/reservations/{reservationId}", cancellationToken);

            if (reservation is null)
                return Result<ReservationDto>.Failure(Error.NotFound("Reservation not found."));

            // A malformed Ticketing response must not NRE downstream; normalize at the boundary.
            return Result<ReservationDto>.Success(reservation.Items is null
                ? reservation with { Items = Array.Empty<ReservationItemDto>() }
                : reservation);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Result<ReservationDto>.Failure(Error.Internal($"Ticketing module unreachable: {ex.Message}"));
        }
    }

    public async Task<Result<IReadOnlyList<TicketDto>>> GetTicketsByEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        try
        {
            var tickets = await _httpClient.GetFromJsonAsync<IReadOnlyList<TicketDto>>(
                $"api/ticketing/events/{eventId}/tickets", cancellationToken);

            return Result<IReadOnlyList<TicketDto>>.Success(tickets ?? Array.Empty<TicketDto>());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Result<IReadOnlyList<TicketDto>>.Failure(Error.Internal($"Ticketing module unreachable: {ex.Message}"));
        }
    }
}
