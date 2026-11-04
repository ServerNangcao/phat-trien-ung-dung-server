using MediatR;

namespace Ticketing.Modules.Notifications.Contracts.IntegrationEvents;

/// <summary>
/// Integration event published by the Ticketing module (TV4) when a ticket is issued.
/// The Notifications module subscribes via the Inbox pattern to send the ticket email
/// with the embedded QR code. EventId drives inbox dedupe so a redelivered event
/// never triggers a second email.
/// </summary>
public sealed record TicketIssuedIntegrationEvent : INotification
{
    public Guid EventId { get; init; }
    public Guid TicketId { get; init; }
    public Guid OrderId { get; init; }
    public Guid CustomerId { get; init; }
    public string CustomerEmail { get; init; } = string.Empty;
    public string EventName { get; init; } = string.Empty;
    public string EventDate { get; init; } = string.Empty;
    public string TicketType { get; init; } = string.Empty;
    public string QrCodeUrl { get; init; } = string.Empty;
    public Guid? ReservationId { get; init; }

    /// <summary>
    /// Order total for the email invoice section. Zero means the publisher did not
    /// supply pricing (contract before the invoice extension) and the section is omitted.
    /// </summary>
    public decimal TotalAmount { get; init; }

    /// <summary>Per-ticket-type pricing for the invoice rows.</summary>
    public IReadOnlyList<TicketItemDto> Items { get; init; } = Array.Empty<TicketItemDto>();
}

public sealed record TicketItemDto(string TicketTypeName, int Quantity, decimal UnitPrice);
