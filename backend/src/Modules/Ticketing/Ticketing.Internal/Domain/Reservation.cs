using Ticketing.SharedKernel;

namespace Ticketing.Internal.Domain;

internal sealed class Reservation : BaseEntity<Guid>
{
    private readonly List<ReservationItem> _items = [];

    /// <summary>
    /// Tham chiếu tới khách hàng trong module Identity (không có FK cross-schema).
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Tham chiếu tới sự kiện trong module Events (không có FK cross-schema).
    /// </summary>
    public Guid EventId { get; private set; }

    public string Status { get; private set; } = ReservationStatus.Active;
    public DateTime ExpiresAt { get; private set; }

    public IReadOnlyCollection<ReservationItem> Items => _items.AsReadOnly();

    private Reservation() { }

    public static Reservation Create(
        Guid customerId,
        Guid eventId,
        DateTime expiresAt) => new()
    {
        Id = Guid.CreateVersion7(),
        CustomerId = customerId,
        EventId = eventId,
        Status = ReservationStatus.Active,
        ExpiresAt = expiresAt,
        CreatedAt = DateTime.UtcNow
    };

    public void AddItem(Guid ticketTypeId, int quantity)
    {
        _items.Add(ReservationItem.Create(Id, ticketTypeId, quantity));
    }

    public void MarkPaymentPending()
    {
        Status = ReservationStatus.PaymentPending;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Confirm()
    {
        Status = ReservationStatus.Confirmed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Release()
    {
        Status = ReservationStatus.Released;
        UpdatedAt = DateTime.UtcNow;
    }
}

