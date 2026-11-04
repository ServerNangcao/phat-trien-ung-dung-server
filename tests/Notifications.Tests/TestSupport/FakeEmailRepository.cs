using Ticketing.Modules.Notifications.Application.Repositories;
using Ticketing.Modules.Notifications.Domain.Entities;

namespace Ticketing.Tests.Notifications.TestSupport;

/// <summary>In-memory email history store for handler tests.</summary>
public sealed class FakeEmailRepository : IEmailRepository
{
    private readonly List<EmailHistory> _store = new();

    public List<EmailHistory> Added { get; } = new();
    public List<EmailHistory> Updated { get; } = new();

    public Task AddAsync(EmailHistory email, CancellationToken cancellationToken = default)
    {
        Added.Add(email);
        _store.Add(email);
        return Task.CompletedTask;
    }

    public Task<EmailHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_store.FirstOrDefault(e => e.Id == id));

    public Task<IReadOnlyList<EmailHistory>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EmailHistory>>(_store.Where(e => e.OrderId == orderId).ToList());

    public Task UpdateAsync(EmailHistory email, CancellationToken cancellationToken = default)
    {
        Updated.Add(email);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EmailHistory>> GetRetryableAsync(DateTime now, int limit, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EmailHistory>>(_store
            .Where(e => (e.Status == EmailStatus.Failed && e.RetryCount < 5 && e.NextRetryAt != null && e.NextRetryAt <= now)
                     || (e.Status == EmailStatus.Pending && e.CreatedAt <= now.AddSeconds(-30)))
            .Take(limit)
            .ToList());
}
