using Events.Internal.Domain;
using Microsoft.EntityFrameworkCore;
using Ticketing.Infrastructure.Core;

namespace Events.Internal.Infrastructure;

internal sealed class EventsDbContext : BaseDbContext
{
    public const string SchemaName = "events";

    public EventsDbContext(DbContextOptions<EventsDbContext> options)
        : base(options, SchemaName)
    {
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventStaff> EventStaff => Set<EventStaff>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder); // applies schema + Outbox/Inbox

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventsDbContext).Assembly);

        SeedCategories(modelBuilder);
    }

    private static void SeedCategories(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>().HasData(
            new
            {
                Id = new Guid("20000000-0000-0000-0000-000000000001"),
                Name = "Âm nhạc",
                Slug = "am-nhac",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = (DateTime?)null
            },
            new
            {
                Id = new Guid("20000000-0000-0000-0000-000000000002"),
                Name = "Công nghệ",
                Slug = "cong-nghe",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = (DateTime?)null
            },
            new
            {
                Id = new Guid("20000000-0000-0000-0000-000000000003"),
                Name = "Thể thao",
                Slug = "the-thao",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = (DateTime?)null
            },
            new
            {
                Id = new Guid("20000000-0000-0000-0000-000000000004"),
                Name = "Hội thảo & Workshop",
                Slug = "hoi-thao-workshop",
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = (DateTime?)null
            }
        );
    }
}
