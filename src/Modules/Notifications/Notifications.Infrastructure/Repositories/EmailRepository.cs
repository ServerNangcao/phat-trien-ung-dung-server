using Microsoft.EntityFrameworkCore;
using Ticketing.Modules.Notifications.Contracts;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Infrastructure.Repositories;

public sealed class EmailRepository : IEmailRepository
{
    private readonly NotificationsDbContext _dbContext;

    public EmailRepository(NotificationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(EmailHistoryDto record, CancellationToken cancellationToken = default)
    {
        var result = EmailHistory.Create(record.OrderId, record.To, record.Subject, record.Body, record.CreatedAt);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Message);
        }

        var entity = result.Value;
        if (record.Status == "Sent")
        {
            entity.MarkSent(record.SentAt ?? DateTime.UtcNow);
        }
        else if (record.Status == "Failed")
        {
            entity.MarkFailed();
        }

        await _dbContext.EmailHistories.AddAsync(entity, cancellationToken);
    }

    public async Task<EmailHistoryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.EmailHistories.FindAsync([id], cancellationToken);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<IReadOnlyList<EmailHistoryDto>> ListByOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.EmailHistories
            .AsNoTracking()
            .Where(e => e.OrderId == orderId)
            .OrderByDescending(e => e.SentAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    public async Task UpdateAsync(EmailHistoryDto record, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.EmailHistories.FindAsync([record.Id], cancellationToken);
        if (entity is not null)
        {
            if (record.Status == "Sent")
            {
                entity.MarkSent(record.SentAt ?? DateTime.UtcNow);
            }
            else if (record.Status == "Failed")
            {
                entity.MarkFailed();
            }
        }
        await Task.CompletedTask;
    }

    private static EmailHistoryDto MapToDto(EmailHistory entity)
    {
        return new EmailHistoryDto(
            entity.Id,
            entity.OrderId,
            entity.To,
            entity.Subject,
            entity.Body,
            entity.Status.ToString(),
            entity.SentAt,
            entity.CreatedAt,
            entity.RetryCount
        );
    }
}