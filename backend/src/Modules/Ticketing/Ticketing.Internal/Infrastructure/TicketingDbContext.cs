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
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TicketingDbContext).Assembly);
    }
}

