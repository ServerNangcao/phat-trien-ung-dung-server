using Ticketing.SharedKernel;

namespace Orders.Internal.Domain;

internal sealed class Payment : BaseEntity<Guid>
{
    public Guid OrderId { get; private set; }
    public string Provider { get; private set; } = string.Empty;

    /// <summary>
    /// Mã tham chiếu gửi cổng thanh toán, UNIQUE để đảm bảo idempotent.
    /// </summary>
    public string TxnRef { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }
    public string Status { get; private set; } = PaymentStatus.Pending;
    public string? ProviderTransactionNo { get; private set; }

    /// <summary>
    /// Payload phản hồi thô từ cổng thanh toán, lưu dưới dạng JSONB.
    /// </summary>
    public string? RawPayload { get; private set; }

    public DateTime? PaidAt { get; private set; }

    private Payment() { }

    public static Payment Create(
        Guid orderId,
        string provider,
        string txnRef,
        decimal amount) => new()
    {
        Id = Guid.CreateVersion7(),
        OrderId = orderId,
        Provider = provider,
        TxnRef = txnRef,
        Amount = amount,
        Status = PaymentStatus.Pending,
        CreatedAt = DateTime.UtcNow
    };

    public void MarkSucceeded(string? providerTransactionNo, string? rawPayload)
    {
        Status = PaymentStatus.Succeeded;
        ProviderTransactionNo = providerTransactionNo;
        RawPayload = rawPayload;
        PaidAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string? rawPayload)
    {
        Status = PaymentStatus.Failed;
        RawPayload = rawPayload;
        UpdatedAt = DateTime.UtcNow;
    }
}

