using Ticketing.SharedKernel;

namespace Ticketing.Internal.Domain;

internal sealed class Ticket : BaseEntity<Guid>
{
    /// <summary>
    /// Tham chiếu tới đơn hàng trong module Orders (không có FK cross-schema).
    /// </summary>
    public Guid OrderId { get; private set; }

    public Guid TicketTypeId { get; private set; }

    /// <summary>
    /// Lưu sẵn EventId để check-in không cần join chéo schema.
    /// </summary>
    public Guid EventId { get; private set; }

    /// <summary>
    /// Số thứ tự vé trong (OrderId, TicketTypeId).
    /// </summary>
    public int Sequence { get; private set; }

    public string Code { get; private set; } = string.Empty;

    /// <summary>
    /// Chữ ký HMAC-SHA256 để chống làm giả mã QR.
    /// </summary>
    public string QrSignature { get; private set; } = string.Empty;

    public string Status { get; private set; } = TicketStatus.Valid;
    public DateTime? UsedAt { get; private set; }

    /// <summary>
    /// Tham chiếu nhân viên soát vé trong Identity (không có FK cross-schema).
    /// </summary>
    public Guid? UsedByStaffId { get; private set; }

    /// <summary>
    /// Snapshot email khách từ OrderPaid event để gửi mail không cần join Identity.
    /// </summary>
    public string CustomerEmail { get; private set; } = string.Empty;

    public DateTime IssuedAt { get; private set; }

    // Navigation property (FK internal)
    public TicketType TicketType { get; private set; } = null!;

    private Ticket() { }

    public static Ticket Issue(
        Guid orderId,
        Guid ticketTypeId,
        Guid eventId,
        int sequence,
        string code,
        string qrSignature,
        string customerEmail) => new()
    {
        Id = Guid.CreateVersion7(),
        OrderId = orderId,
        TicketTypeId = ticketTypeId,
        EventId = eventId,
        Sequence = sequence,
        Code = code,
        QrSignature = qrSignature,
        Status = TicketStatus.Valid,
        CustomerEmail = customerEmail,
        IssuedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };

    public void MarkUsed(Guid staffId)
    {
        Status = TicketStatus.Used;
        UsedAt = DateTime.UtcNow;
        UsedByStaffId = staffId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status = TicketStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }
}

