namespace Ticketing.Internal.Domain;

internal sealed class ReservationItem
{
    public Guid ReservationId { get; private set; }
    public Guid TicketTypeId { get; private set; }
    public int Quantity { get; private set; }

    public TicketType TicketType { get; private set; } = null!;

    private ReservationItem() { }

    internal static ReservationItem Create(Guid reservationId, Guid ticketTypeId, int quantity) => new()
    {
        ReservationId = reservationId,
        TicketTypeId = ticketTypeId,
        Quantity = quantity
    };
}

