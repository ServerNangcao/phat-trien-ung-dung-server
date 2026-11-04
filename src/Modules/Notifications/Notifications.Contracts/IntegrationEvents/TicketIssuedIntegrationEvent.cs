using MediatR;

namespace Ticketing.Modules.Notifications.Contracts.IntegrationEvents;

/// <summary>
/// Integration event published by the Ticketing module when a ticket is issued.
/// Notifications module subscribes to this to send the email with QR code.
/// </summary>
public sealed record TicketIssuedIntegrationEvent : INotification
{
    public Guid TicketId { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerEmail { get; init; } = string.Empty;
    public string EventName { get; init; } = string.Empty;
    public string EventDate { get; init; } = string.Empty;
    public string TicketType { get; init; } = string.Empty;
    public string QrCodeUrl { get; init; } = string.Empty;
    public Guid? ReservationId { get; init; }
}