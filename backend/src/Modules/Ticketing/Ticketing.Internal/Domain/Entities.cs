using Ticketing.SharedKernel;

namespace Ticketing.Internal.Domain;

internal sealed class TicketType : BaseEntity<Guid>
{
    public TicketType()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }

    public new Guid Id
    {
        get => base.Id;
        set => base.Id = value;
    }

    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Capacity { get; set; }
    public int QuantityAvailable { get; set; }
    public int MaxPerOrder { get; set; }
    public uint Version { get; set; }
}

internal sealed class Reservation : BaseEntity<Guid>
{
    public Reservation()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }

    public new Guid Id
    {
        get => base.Id;
        set => base.Id = value;
    }

    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }
    public Guid CustomerId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string Status { get; set; } = "Active";
    public string IdempotencyKey { get; set; } = string.Empty;
}

internal sealed class Ticket : BaseEntity<Guid>
{
    public Ticket()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
    }

    public new Guid Id
    {
        get => base.Id;
        set => base.Id = value;
    }

    public Guid EventId { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid TicketTypeId { get; set; }
    public int Sequence { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string EventTitle { get; set; } = string.Empty;
    public string TicketTypeName { get; set; } = string.Empty;
    public string Status { get; set; } = "Valid";
    public string QrPayload { get; set; } = string.Empty;
    public DateTime? UsedAtUtc { get; set; }
    public string? UsedByStaffId { get; set; }
}
