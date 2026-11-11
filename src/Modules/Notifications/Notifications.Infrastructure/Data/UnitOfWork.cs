using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ticketing.Modules.Notifications.Application.Data;
using Ticketing.SharedKernel;

namespace Ticketing.Modules.Notifications.Infrastructure.Data;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly NotificationsDbContext _dbContext;

    public UnitOfWork(NotificationsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result> CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A concurrent delivery committed the same inbox EventId first: this one is a
            // duplicate. Transient failures are NOT mapped — they propagate so the
            // delivering outbox worker retries.
            return Result.Failure(Error.Conflict("A duplicate record was committed concurrently."));
        }
    }
}
