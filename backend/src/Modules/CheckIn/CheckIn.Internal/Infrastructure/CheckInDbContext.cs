using Microsoft.EntityFrameworkCore;
using Ticketing.Infrastructure.Core;
using CheckIn.Internal.Domain;

namespace CheckIn.Internal.Infrastructure;

internal sealed class CheckInDbContext : BaseDbContext
{
    public const string SchemaName = "checkin";

    public CheckInDbContext(DbContextOptions<CheckInDbContext> options)
        : base(options, SchemaName)
    {
    }

    public DbSet<StaffAssignment> StaffAssignments => Set<StaffAssignment>();
    public DbSet<CheckInLog> CheckInLogs => Set<CheckInLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StaffAssignment>(entity =>
        {
            entity.ToTable("StaffAssignments", SchemaName);
            entity.HasKey(x => new { x.EventId, x.StaffId });
            entity.Property(x => x.StaffId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.AssignedAtUtc).IsRequired();
        });

        modelBuilder.Entity<CheckInLog>(entity =>
        {
            entity.ToTable("CheckInLogs", SchemaName);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StaffId).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Gate).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ScannedAtUtc).IsRequired();

            entity.HasIndex(x => new { x.EventId, x.ScannedAtUtc });
            entity.HasIndex(x => x.TicketId);
        });
    }
}

