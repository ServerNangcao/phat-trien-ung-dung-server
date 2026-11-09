using Ticketing.SharedKernel;

namespace Orders.Internal.Domain;

internal sealed class Order : BaseEntity<Guid>
{
    private readonly List<OrderItem> _items = [];

    /// <summary>
    /// Tham chiếu khách hàng trong Identity module, NO FK cross-schema.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Tham chiếu sự kiện trong Events module, NO FK cross-schema.
    /// </summary>
    public Guid EventId { get; private set; }

    /// <summary>
    /// Tham chiếu giữ vé trong Ticketing module, NO FK cross-schema (1 Reservation chỉ tạo 1 Order).
    /// </summary>
    public Guid ReservationId { get; private set; }

    /// <summary>
    /// Snapshot tên khách hàng để xuất báo cáo người tham dự và gửi mail.
    /// </summary>
    public string CustomerName { get; private set; } = string.Empty;

    /// <summary>
    /// Snapshot email khách hàng để gửi mail không cần join Identity.
    /// </summary>
    public string CustomerEmail { get; private set; } = string.Empty;

    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "VND";
    public string Status { get; private set; } = OrderStatus.Pending;
    public DateTime? PaidAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { }

    public static Order Create(
        Guid customerId,
        Guid eventId,
        Guid reservationId,
        string customerName,
        string customerEmail,
        decimal totalAmount,
        string currency = "VND") => new()
    {
        Id = Guid.CreateVersion7(),
        CustomerId = customerId,
        EventId = eventId,
        ReservationId = reservationId,
        CustomerName = customerName,
        CustomerEmail = customerEmail,
        TotalAmount = totalAmount,
        Currency = currency,
        Status = OrderStatus.Pending,
        CreatedAt = DateTime.UtcNow
    };

    public void AddItem(Guid ticketTypeId, string ticketTypeName, decimal unitPrice, int quantity)
    {
        _items.Add(OrderItem.Create(Id, ticketTypeId, ticketTypeName, unitPrice, quantity));
    }

    public void MarkPaid()
    {
        Status = OrderStatus.Paid;
        PaidAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkExpired()
    {
        Status = OrderStatus.Expired;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = OrderStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkPaidAfterExpiry()
    {
        Status = OrderStatus.PaidAfterExpiry;
        PaidAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkRefunded()
    {
        Status = OrderStatus.Refunded;
        UpdatedAt = DateTime.UtcNow;
    }
}

