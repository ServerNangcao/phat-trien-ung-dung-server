using Ticketing.Modules.Notifications.Domain.Entities;

namespace Ticketing.Modules.Notifications.Application.Repositories;

/// <summary>
/// Repository port for the EmailHistory aggregate.
/// Lives in the Application layer so the domain never depends on persistence.
/// </summary>
public interface IEmailRepository
{
    /// <summary>Persists a new email history record.</summary>
    Task AddAsync(EmailHistory email, CancellationToken cancellationToken = default);

    Task<EmailHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailHistory>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Persists state changes on a tracked (or detached) email history entity.</summary>
    Task UpdateAsync(EmailHistory email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Emails due for a retry attempt: failed ones whose backoff elapsed (below the retry
    /// ceiling), plus Pending ones abandoned by a crash between persist and send.
    /// </summary>
    Task<IReadOnlyList<EmailHistory>> GetRetryableAsync(DateTime now, int limit, CancellationToken cancellationToken = default);
}
