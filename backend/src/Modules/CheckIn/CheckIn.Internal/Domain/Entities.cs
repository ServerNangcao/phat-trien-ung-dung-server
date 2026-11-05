using Ticketing.SharedKernel;

namespace CheckIn.Internal.Domain;

internal sealed class StaffAssignment : BaseEntity<Guid>
{
    public StaffAssignment()
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
    public string StaffId { get; set; } = string.Empty;
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
}

internal sealed class CheckInLog : BaseEntity<Guid>
{
    public CheckInLog()
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
    public Guid? TicketId { get; set; }
    public string StaffId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Gate { get; set; } = "Cổng chính";
    public DateTime ScannedAtUtc { get; set; } = DateTime.UtcNow;
}
