using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ticketing.Infrastructure.Core.Outbox;
using Ticketing.Infrastructure.Core.Persistence;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.Modules.Orders.Infrastructure.Outbox;

namespace Ticketing.Modules.Orders.Infrastructure;

public sealed class OrdersDbContext : BaseDbContext
{
    public override string Schema => "orders";

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<RefundRequest> RefundRequests => Set<RefundRequest>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<Order>(builder =>
        {
            builder.HasKey(o => o.Id);
            builder.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)");
            builder.Property(o => o.Status).HasConversion<string>();
            builder.Property(o => o.CustomerId).IsRequired();
            builder.Property(o => o.ReservationId).IsRequired();
            builder.Property(o => o.CreatedAt).IsRequired();
            builder.Property(o => o.UpdatedAt).IsRequired();
            builder.Ignore(o => o.DomainEvents);

            builder.OwnsMany(o => o.Items, itemBuilder =>
            {
                itemBuilder.Property(i => i.TicketTypeId).IsRequired();
                itemBuilder.Property(i => i.Quantity).IsRequired();
                itemBuilder.Property(i => i.Price).HasColumnType("decimal(18,2)").IsRequired();
            });

            builder.HasIndex(o => o.CustomerId);
            builder.HasIndex(o => o.ReservationId).IsUnique();

            // Postgres xmin as a concurrency token: two concurrent IPNs for the same order
            // cannot both pass the Pending check and commit; the loser retries via VNPay.
            builder.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<RefundRequest>(builder =>
        {
            builder.HasKey(r => r.Id);
            builder.Property(r => r.OrderId).IsRequired();
            builder.Property(r => r.Reason).IsRequired().HasMaxLength(500);
            builder.Property(r => r.CreatedAt).IsRequired();
            builder.Property(r => r.Status).HasConversion<string>();

            builder.HasOne<Order>()
                .WithMany()
                .HasForeignKey(r => r.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(r => r.Status);
        });

        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable("outbox_messages");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Type).IsRequired().HasMaxLength(500);
            builder.Property(m => m.Content).IsRequired();
            builder.Property(m => m.CreatedAt).IsRequired();
            builder.HasIndex(m => m.ProcessedAt);
            builder.Property(m => m.LastAttemptAt);
        });
    }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        // The order state change and its outbox rows commit in one transaction: either both
        // land or neither does, so an integration event can never be lost or orphaned.
        // The InMemory test provider supports no-op transactions only via a relational check.
        var transaction = Database.IsRelational() && Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            await AppendIntegrationEventsToOutboxAsync(cancellationToken);
            transaction?.Commit();
            return result;
        }
        catch
        {
            transaction?.Rollback();
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private async Task AppendIntegrationEventsToOutboxAsync(CancellationToken cancellationToken)
    {
        var entries = ChangeTracker.Entries<Order>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .ToList();

        if (entries.Count == 0)
            return;

        foreach (var entry in entries)
        {
            var order = entry.Entity;
            foreach (var domainEvent in order.DomainEvents.ToList())
            {
                if (IntegrationEventMapper.Map(domainEvent, order) is { } integrationEvent)
                {
                    // Serialize as object: with the interface-typed variable STJ would emit
                    // the (empty) marker interface metadata instead of the event's properties.
                    await Set<OutboxMessage>().AddAsync(new OutboxMessage
                    {
                        Type = integrationEvent.GetType().FullName!,
                        Content = JsonSerializer.Serialize((object)integrationEvent, JsonOptions)
                    }, cancellationToken);
                }
            }

            order.GetAndClearDomainEvents();
        }

        await base.SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);
    }
}
