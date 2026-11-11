using Microsoft.EntityFrameworkCore;

namespace Ticketing.Infrastructure.Core.Persistence;

public abstract class BaseDbContext : DbContext
{
    protected BaseDbContext(DbContextOptions options) : base(options)
    {
    }

    public abstract string Schema { get; }
}