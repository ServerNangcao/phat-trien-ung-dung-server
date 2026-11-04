using Microsoft.EntityFrameworkCore;
using Ticketing.Modules.Notifications.Application.Repositories;
using Ticketing.Modules.Notifications.Application.Services;
using Ticketing.Modules.Notifications.Domain.Entities;

namespace Ticketing.Modules.Notifications.Infrastructure.Repositories;

public sealed class EmailRepository : IEmailRepository
{
    private readonly NotificationsDbContext _dbContext;

    public EmailRepository(NotificationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(EmailHistory email, CancellationToken cancellationToken = default)
    {
        await _dbContext.EmailHistories.AddAsync(email, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<EmailHistory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.EmailHistories.FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyList<EmailHistory>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.EmailHistories
            .AsNoTracking()
            .Where(e => e.OrderId == orderId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities;
    }

    public async Task UpdateAsync(EmailHistory email, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Entry(email).State == EntityState.Detached)
            _dbContext.EmailHistories.Update(email);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EmailHistory>> GetRetryableAsync(DateTime now, int limit, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.EmailHistories
            .Where(e => (e.Status == EmailStatus.Failed
                            && e.RetryCount < EmailRetryProcessor.MaxRetries
                            && e.NextRetryAt != null && e.NextRetryAt <= now)
                     || (e.Status == EmailStatus.Pending && e.CreatedAt <= now.AddSeconds(-30)))
            .OrderBy(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return entities;
    }
}
