using Ticketing.SharedKernel;

namespace Orders.Internal.Domain;

internal sealed class RefundRequest : BaseEntity<Guid>
{
    public Guid OrderId { get; private set; }
    public Guid PaymentId { get; private set; }
    public decimal Amount { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string Status { get; private set; } = RefundStatus.Pending;

    /// <summary>
    /// Tham chiếu Admin xử lý trong Identity module, NO FK cross-schema.
    /// </summary>
    public Guid? ProcessedBy { get; private set; }

    public DateTime? ProcessedAt { get; private set; }

    private RefundRequest() { }

    public static RefundRequest Create(
        Guid orderId,
        Guid paymentId,
        decimal amount,
        string reason) => new()
    {
        Id = Guid.CreateVersion7(),
        OrderId = orderId,
        PaymentId = paymentId,
        Amount = amount,
        Reason = reason,
        Status = RefundStatus.Pending,
        CreatedAt = DateTime.UtcNow
    };

    public void Approve(Guid adminUserId)
    {
        Status = RefundStatus.Approved;
        ProcessedBy = adminUserId;
        ProcessedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(Guid adminUserId)
    {
        Status = RefundStatus.Rejected;
        ProcessedBy = adminUserId;
        ProcessedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkRefunded()
    {
        Status = RefundStatus.Refunded;
        UpdatedAt = DateTime.UtcNow;
    }
}

