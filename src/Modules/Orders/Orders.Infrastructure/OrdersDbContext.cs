using Microsoft.EntityFrameworkCore;
using Ticketing.Infrastructure.Core.Persistence;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.Modules.Orders.Domain.ValueObjects;

namespace Ticketing.Modules.Orders.Infrastructure;

public sealed class OrdersDbContext : BaseDbContext
{
    public override string Schema => "orders";

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<RefundRequest> RefundRequests => Set<RefundRequest>();

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
            builder.Property(o => o.CreatedAt).IsRequired();
            builder.Property(o => o.UpdatedAt).IsRequired();

            builder.OwnsMany(o => o.Items, itemBuilder =>
            {
                itemBuilder.Property(i => i.TicketTypeId).IsRequired();
                itemBuilder.Property(i => i.Quantity).IsRequired();
                itemBuilder.Property(i => i.Price).HasColumnType("decimal(18,2)").IsRequired();
            });

            builder.HasIndex(o => o.CustomerId);
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
        });
    }
}