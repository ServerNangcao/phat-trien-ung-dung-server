using Ticketing.SharedKernel;

namespace CheckIn.Internal.Domain;

internal sealed class CheckInLog : BaseEntity<Guid>
{
    /// <summary>
    /// Tham chiếu vé trong Ticketing module, NO FK cross-schema. NULL khi không quét được mã QR.
    /// </summary>
    public Guid? TicketId { get; private set; }

    /// <summary>
    /// Tham chiếu sự kiện trong Events module, NO FK cross-schema.
    /// </summary>
    public Guid EventId { get; private set; }

    /// <summary>
    /// Tham chiếu nhân viên soát vé trong Identity module, NO FK cross-schema.
    /// </summary>
    public Guid StaffId { get; private set; }

    public string Result { get; private set; } = CheckInResult.Valid;
    public DateTime ScannedAt { get; private set; }
    public string? DeviceInfo { get; private set; }

    private CheckInLog() { }

    public static CheckInLog Create(
        Guid? ticketId,
        Guid eventId,
        Guid staffId,
        string result,
        string? deviceInfo = null) => new()
    {
        Id = Guid.CreateVersion7(),
        TicketId = ticketId,
        EventId = eventId,
        StaffId = staffId,
        Result = result,
        DeviceInfo = deviceInfo,
        ScannedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };
}

