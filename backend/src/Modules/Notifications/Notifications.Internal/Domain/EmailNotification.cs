using Ticketing.SharedKernel;

namespace Notifications.Internal.Domain;

internal sealed class EmailNotification : BaseEntity<Guid>
{
    /// <summary>
    /// Tham chiếu đơn hàng trong Orders module, NO FK cross-schema. Unique 1-1.
    /// </summary>
    public Guid OrderId { get; private set; }

    /// <summary>
    /// Email người nhận (chép sẵn từ TicketIssuedIntegrationEvent, không gọi sang Identity).
    /// </summary>
    public string Recipient { get; private set; } = string.Empty;

    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string Status { get; private set; } = EmailNotificationStatus.Pending;
    public int AttemptCount { get; private set; }
    public DateTime? NextRetryAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? SentAt { get; private set; }

    private EmailNotification() { }

    public static EmailNotification Create(
        Guid orderId,
        string recipient,
        string subject,
        string body) => new()
    {
        Id = Guid.CreateVersion7(),
        OrderId = orderId,
        Recipient = recipient,
        Subject = subject,
        Body = body,
        Status = EmailNotificationStatus.Pending,
        AttemptCount = 0,
        CreatedAt = DateTime.UtcNow
    };

    public void MarkSent()
    {
        Status = EmailNotificationStatus.Sent;
        SentAt = DateTime.UtcNow;
        LastError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string error, DateTime? nextRetryAt = null)
    {
        Status = EmailNotificationStatus.Failed;
        AttemptCount++;
        LastError = error;
        NextRetryAt = nextRetryAt;
        UpdatedAt = DateTime.UtcNow;
    }
}

