using Ticketing.Modules.Notifications.Application.Data;
using Ticketing.Modules.Notifications.Application.Repositories;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Tests.Notifications.TestSupport;

/// <summary>
/// In-memory email history store. Add/Update stage only; Commit() (driven by
/// FakeUnitOfWork) persists the staged rows, mirroring the real unit-of-work contract.
/// </summary>
public sealed class FakeEmailRepository : IEmailRepository
{
    private readonly List<EmailHistory> _staged = new();

    public List<EmailHistory> Store { get; } = new();
    public List<EmailHistory> Updated { get; } = new();

    public Task AddAsync(EmailHistory email, CancellationToken cancellationToken = default)
    {
        _staged.Add(email);
        return Task.CompletedTask;
    }

    public Task<EmailHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Store.FirstOrDefault(e => e.Id == id));

    public Task<IReadOnlyList<EmailHistory>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EmailHistory>>(Store.Where(e => e.OrderId == orderId).ToList());

    public Task UpdateAsync(EmailHistory email, CancellationToken cancellationToken = default)
    {
        Updated.Add(email);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EmailHistory>> GetRetryableAsync(DateTime now, int limit, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EmailHistory>>(Store
            .Where(e => (e.Status == EmailStatus.Failed && e.RetryCount < 5 && e.NextRetryAt != null && e.NextRetryAt <= now)
                     || (e.Status == EmailStatus.Pending && e.CreatedAt <= now.AddSeconds(-30)))
            .Take(limit)
            .ToList());

    public void Commit()
    {
        Store.AddRange(_staged);
        _staged.Clear();
    }
}

public sealed class FakeUnitOfWork(FakeEmailRepository repository) : IUnitOfWork
{
    public int CommitCount { get; private set; }

    public Task<Result> CommitAsync(CancellationToken cancellationToken = default)
    {
        CommitCount++;
        repository.Commit();
        return Task.FromResult(Result.Success());
    }
}

/// <summary>Inbox double: first sight of an EventId is new, repeats are duplicates.</summary>
public sealed class FakeInbox : IInbox
{
    private readonly HashSet<Guid> _seen = new();

    public Task<bool> TryStageAsync(Guid eventId, string eventType, string payload, CancellationToken cancellationToken = default)
        => Task.FromResult(_seen.Add(eventId));
}
