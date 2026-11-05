using Microsoft.EntityFrameworkCore;
using Ticketing.Infrastructure.Core;
using Ticketing.Internal.Domain;

namespace Ticketing.Internal.Infrastructure;

internal sealed class TicketingDbContext : BaseDbContext
{
    public const string SchemaName = "ticketing";

    public TicketingDbContext(DbContextOptions<TicketingDbContext> options)
        : base(options, SchemaName)
    {
    }

    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TicketType>(entity =>
        {
            entity.ToTable("TicketTypes", SchemaName);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Price).HasPrecision(18, 0).IsRequired();
            entity.Property(x => x.Capacity).IsRequired();
            entity.Property(x => x.QuantityAvailable).IsRequired();
            entity.Property(x => x.MaxPerOrder).IsRequired();
            entity.Property(x => x.Version).IsConcurrencyToken();

            entity.HasIndex(x => x.EventId);
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.ToTable("Reservations", SchemaName);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 0).IsRequired();
            entity.Property(x => x.Quantity).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(30).IsRequired();
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ExpiresAtUtc).IsRequired();

            entity.HasIndex(x => new { x.CustomerId, x.IdempotencyKey }).IsUnique();
            entity.HasIndex(x => x.ExpiresAtUtc);
            entity.HasIndex(x => x.EventId);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Tickets", SchemaName);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TicketCode).HasMaxLength(50).IsRequired();
            entity.Property(x => x.EventTitle).HasMaxLength(200).IsRequired();
            entity.Property(x => x.TicketTypeName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(20).IsRequired();
            entity.Property(x => x.QrPayload).HasMaxLength(500).IsRequired();
            entity.Property(x => x.UsedByStaffId).HasMaxLength(100);

            entity.HasIndex(x => new { x.OrderId, x.Sequence }).IsUnique();
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.EventId);
        });
    }
}

