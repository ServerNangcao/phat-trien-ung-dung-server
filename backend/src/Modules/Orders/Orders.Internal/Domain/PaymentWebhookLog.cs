using Ticketing.SharedKernel;

namespace Orders.Internal.Domain;

internal sealed class PaymentWebhookLog : BaseEntity<Guid>
{
    public string Provider { get; private set; } = string.Empty;
    public string TxnRef { get; private set; } = string.Empty;

    /// <summary>
    /// Nguyên văn payload webhook để phục vụ đối soát, lưu dưới dạng JSONB.
    /// </summary>
    public string Payload { get; private set; } = string.Empty;

    public bool SignatureValid { get; private set; }
    public DateTime ReceivedAt { get; private set; }

    private PaymentWebhookLog() { }

    public static PaymentWebhookLog Create(
        string provider,
        string txnRef,
        string payload,
        bool signatureValid) => new()
    {
        Id = Guid.CreateVersion7(),
        Provider = provider,
        TxnRef = txnRef,
        Payload = payload,
        SignatureValid = signatureValid,
        ReceivedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };
}

