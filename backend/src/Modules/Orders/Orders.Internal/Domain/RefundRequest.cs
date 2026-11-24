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
        if (Status != RefundStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot approve refund request from status '{Status}'. Only '{RefundStatus.Pending}' requests can be approved.");
        }

        Status = RefundStatus.Approved;
        ProcessedBy = adminUserId;
        ProcessedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reject(Guid adminUserId)
    {
        if (Status != RefundStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot reject refund request from status '{Status}'. Only '{RefundStatus.Pending}' requests can be rejected.");
        }

        Status = RefundStatus.Rejected;
        ProcessedBy = adminUserId;
        ProcessedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkRefunded()
    {
        if (Status != RefundStatus.Approved)
        {
            throw new InvalidOperationException($"Cannot mark refund request as refunded from status '{Status}'. Only '{RefundStatus.Approved}' requests can be marked as refunded.");
        }

        Status = RefundStatus.Refunded;
        UpdatedAt = DateTime.UtcNow;
    }
}

