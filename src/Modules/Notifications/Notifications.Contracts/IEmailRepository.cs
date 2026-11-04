namespace Ticketing.Modules.Notifications.Contracts;

/// <summary>
/// DTO representing an email history record for contract purposes.
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
    int RetryCount
);

/// <summary>
/// Repository interface for email history operations (Contract layer).
/// </summary>
public interface IEmailRepository
{
    Task AddAsync(EmailHistoryDto record, CancellationToken cancellationToken = default);
    Task<EmailHistoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmailHistoryDto>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task UpdateAsync(EmailHistoryDto record, CancellationToken cancellationToken = default);
}