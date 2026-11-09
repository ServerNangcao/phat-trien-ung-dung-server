using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Internal.Domain;

namespace Ticketing.Internal.Infrastructure.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets", t =>
        {
            t.HasCheckConstraint("ck_tickets_status", "\"Status\" IN ('Valid', 'Used', 'Cancelled')");
        });

        builder.HasKey(x => x.Id);

        // Cross-schema reference tới Orders, NO FK
        builder.Property(x => x.OrderId)
            .IsRequired();

        // FK nội bộ tới TicketType
        builder.HasOne(x => x.TicketType)
            .WithMany()
            .HasForeignKey(x => x.TicketTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Lưu EventId để check-in không cần join chéo schema, NO FK
        builder.Property(x => x.EventId)
            .IsRequired();

        builder.Property(x => x.Sequence)
            .IsRequired();

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.QrSignature)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CustomerEmail)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.IssuedAt)
            .IsRequired();

        // Cross-schema reference tới Identity, NO FK
        builder.Property(x => x.UsedByStaffId);

        // Indexes
        builder.HasIndex(x => new { x.OrderId, x.TicketTypeId, x.Sequence })
            .IsUnique()
            .HasDatabaseName("ux_tickets_order_type_seq");

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("ux_tickets_code");

        builder.HasIndex(x => new { x.EventId, x.Status })
            .HasDatabaseName("ix_tickets_event_status");

        builder.HasIndex(x => x.OrderId)
            .HasDatabaseName("ix_tickets_order");
    }
}

