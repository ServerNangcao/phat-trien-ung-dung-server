using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Internal.Domain;

namespace Orders.Internal.Infrastructure.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", t =>
        {
            t.HasCheckConstraint("ck_orderitems_quantity", "\"Quantity\" > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrderId)
            .IsRequired();

        // Cross-schema reference tới Ticketing, NO FK
        builder.Property(x => x.TicketTypeId)
            .IsRequired();

        builder.Property(x => x.TicketTypeName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.UnitPrice)
            .HasPrecision(18, 0);

        builder.Property(x => x.Quantity)
            .IsRequired();
    }
}

