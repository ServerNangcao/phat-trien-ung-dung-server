using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Internal.Domain;

namespace Orders.Internal.Infrastructure.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("ck_orders_status",
                "\"Status\" IN ('Pending', 'Paid', 'Expired', 'Cancelled', 'PaidAfterExpiry', 'Refunded')");
        });

        builder.HasKey(x => x.Id);

        // Cross-schema references, NO FK
        builder.Property(x => x.CustomerId)
            .IsRequired();

        builder.Property(x => x.EventId)
            .IsRequired();

        builder.Property(x => x.ReservationId)
            .IsRequired();

        builder.Property(x => x.CustomerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.CustomerEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.TotalAmount)
            .HasPrecision(18, 0);

        builder.Property(x => x.Currency)
            .IsRequired()
            .HasMaxLength(3)
            .HasDefaultValue("VND");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        // 1-N quan hệ nội bộ với OrderItems
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Indexes
        builder.HasIndex(x => x.ReservationId)
            .IsUnique()
            .HasDatabaseName("ux_orders_reservation");

        builder.HasIndex(x => new { x.CustomerId, x.CreatedAt })
            .HasDatabaseName("ix_orders_customer");

        builder.HasIndex(x => new { x.EventId, x.Status })
            .HasDatabaseName("ix_orders_event_status");
    }
}

