using Ticketing.SharedKernel;

namespace Ticketing.Internal.Domain;

internal sealed class TicketType : BaseEntity<Guid>
{
    /// <summary>
    /// Tham chiếu tới sự kiện trong module Events (không có FK cross-schema).
    /// </summary>
    public Guid EventId { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int TotalQuantity { get; private set; }
    public int ReservedQuantity { get; private set; }
    public int SoldQuantity { get; private set; }
    public int MaxPerOrder { get; private set; }
    public DateTime? SaleStartsAt { get; private set; }
    public DateTime? SaleEndsAt { get; private set; }
    public bool IsActive { get; private set; } = true;

    public int AvailableQuantity => Math.Max(0, TotalQuantity - ReservedQuantity - SoldQuantity);

    private TicketType() { }

    public static TicketType Create(
        Guid eventId,
        string name,
        decimal price,
        int totalQuantity,
        int maxPerOrder,
        DateTime? saleStartsAt = null,
        DateTime? saleEndsAt = null) => new()
    {
        Id = Guid.CreateVersion7(),
        EventId = eventId,
        Name = name,
        Price = price,
        TotalQuantity = totalQuantity,
        ReservedQuantity = 0,
        SoldQuantity = 0,
        MaxPerOrder = maxPerOrder,
        SaleStartsAt = saleStartsAt,
        SaleEndsAt = saleEndsAt,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    public void UpdatePrice(decimal newPrice)
    {
        Price = newPrice;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}

