using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Internal.Domain;

namespace Ticketing.Internal.Infrastructure.Configurations;

internal sealed class ReservationItemConfiguration : IEntityTypeConfiguration<ReservationItem>
{
    public void Configure(EntityTypeBuilder<ReservationItem> builder)
    {
        builder.ToTable("ReservationItems", t =>
        {
            t.HasCheckConstraint("ck_reservationitems_quantity", "\"Quantity\" > 0");
        });

        builder.HasKey(x => new { x.ReservationId, x.TicketTypeId });

        builder.HasOne<Reservation>()
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TicketType)
            .WithMany()
            .HasForeignKey(x => x.TicketTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Quantity)
            .IsRequired();
    }
}

