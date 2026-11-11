using CheckIn.Internal.Domain;
using Microsoft.EntityFrameworkCore;
using Ticketing.Infrastructure.Core;

namespace CheckIn.Internal.Infrastructure;

internal sealed class CheckInDbContext : BaseDbContext
{
    public const string SchemaName = "checkin";

    public CheckInDbContext(DbContextOptions<CheckInDbContext> options)
        : base(options, SchemaName)
    {
    }

    public DbSet<CheckInLog> CheckInLogs => Set<CheckInLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CheckInDbContext).Assembly);
    }
}

