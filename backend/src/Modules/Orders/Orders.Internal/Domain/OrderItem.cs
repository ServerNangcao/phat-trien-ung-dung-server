using Ticketing.SharedKernel;

namespace Orders.Internal.Domain;

internal sealed class OrderItem : BaseEntity<Guid>
{
    public Guid OrderId { get; private set; }

    /// <summary>
    /// Tham chiếu hạng vé trong Ticketing module, NO FK cross-schema.
    /// </summary>
    public Guid TicketTypeId { get; private set; }

    /// <summary>
    /// Snapshot tên hạng vé tại thời điểm mua.
    /// </summary>
    public string TicketTypeName { get; private set; } = string.Empty;

    /// <summary>
    /// Snapshot đơn giá tại thời điểm mua.
    /// </summary>
    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    private OrderItem() { }

    internal static OrderItem Create(
        Guid orderId,
        Guid ticketTypeId,
        string ticketTypeName,
        decimal unitPrice,
        int quantity) => new()
    {
        Id = Guid.CreateVersion7(),
        OrderId = orderId,
        TicketTypeId = ticketTypeId,
        TicketTypeName = ticketTypeName,
        UnitPrice = unitPrice,
        Quantity = quantity,
        CreatedAt = DateTime.UtcNow
    };
}

