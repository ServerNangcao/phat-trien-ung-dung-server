using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Internal.Domain;

namespace Ticketing.Internal.Infrastructure.Configurations;

internal sealed class TicketTypeConfiguration : IEntityTypeConfiguration<TicketType>
{
    public void Configure(EntityTypeBuilder<TicketType> builder)
    {
        builder.ToTable("TicketTypes", t =>
        {
            t.HasCheckConstraint("ck_tickettypes_inventory",
                "\"ReservedQuantity\" >= 0 AND \"SoldQuantity\" >= 0 " +
                "AND \"ReservedQuantity\" + \"SoldQuantity\" <= \"TotalQuantity\"");
            t.HasCheckConstraint("ck_tickettypes_price", "\"Price\" >= 0");
        });

        builder.HasKey(x => x.Id);

        // EventId — cross-schema reference tới Events, NO FK
        builder.Property(x => x.EventId)
            .IsRequired();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Price)
            .HasPrecision(18, 0);

        builder.Property(x => x.TotalQuantity)
            .IsRequired();

        builder.Property(x => x.ReservedQuantity)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.SoldQuantity)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.MaxPerOrder)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // xmin concurrency token (PostgreSQL optimistic concurrency)
        builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();

        builder.HasIndex(x => x.EventId);
    }
}
