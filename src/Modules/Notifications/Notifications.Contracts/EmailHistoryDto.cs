namespace Ticketing.Modules.Notifications.Contracts;

/// <summary>
/// DTO representing an email history record for API responses.
/// </summary>
public record EmailHistoryDto(
    Guid Id,
    Guid OrderId,
    string To,
    string Subject,
    string Body,
    string Status,
    DateTime? SentAt,
    DateTime CreatedAt,
    int RetryCount,
    DateTime? NextRetryAt,
    string? LastError
);
