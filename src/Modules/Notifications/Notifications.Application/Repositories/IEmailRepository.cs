using Ticketing.Modules.Notifications.Domain.Entities;

namespace Ticketing.Modules.Notifications.Application.Repositories;

/// <summary>
/// Repository port for the EmailHistory aggregate. Mutating methods stage changes only;
/// persistence happens through <see cref="Data.IUnitOfWork.CommitAsync"/> so related
/// rows (inbox + email history) commit atomically.
/// </summary>
public interface IEmailRepository
{
    /// <summary>Stages a new email history record; persisted on the next CommitAsync.</summary>
    Task AddAsync(EmailHistory email, CancellationToken cancellationToken = default);

    /// <summary>Loads a tracked email so state changes can be committed through the unit of work.</summary>
    Task<EmailHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmailHistory>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken = default);

    /// <summary>Stages state changes on a tracked (or detached) email; persisted on the next CommitAsync.</summary>
    Task UpdateAsync(EmailHistory email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Emails due for a retry attempt: failed ones whose backoff elapsed (below the retry
    /// ceiling), plus Pending ones abandoned by a crash between persist and send.
    /// </summary>
    Task<IReadOnlyList<EmailHistory>> GetRetryableAsync(DateTime now, int limit, CancellationToken cancellationToken = default);
}
