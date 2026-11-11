using Microsoft.EntityFrameworkCore;
using Orders.Internal.Domain;
using Ticketing.Infrastructure.Core;

namespace Orders.Internal.Infrastructure;

internal sealed class OrdersDbContext : BaseDbContext
{
    public const string SchemaName = "orders";

    public OrdersDbContext(DbContextOptions<OrdersDbContext> options)
        : base(options, SchemaName)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentWebhookLog> PaymentWebhookLogs => Set<PaymentWebhookLog>();
    public DbSet<RefundRequest> RefundRequests => Set<RefundRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
    }
}

