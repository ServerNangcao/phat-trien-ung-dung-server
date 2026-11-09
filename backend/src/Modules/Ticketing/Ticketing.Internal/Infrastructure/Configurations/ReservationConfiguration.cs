using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ticketing.Internal.Domain;

namespace Ticketing.Internal.Infrastructure.Configurations;

internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations", t =>
        {
            t.HasCheckConstraint("ck_reservations_status",
                "\"Status\" IN ('Active', 'PaymentPending', 'Confirmed', 'Released')");
        });

        builder.HasKey(x => x.Id);

        // Cross-schema references, NO FK
        builder.Property(x => x.CustomerId)
            .IsRequired();

        builder.Property(x => x.EventId)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ExpiresAt)
            .IsRequired();

        // 1-N quan hệ nội bộ với ReservationItems
        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.ReservationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Items)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Partial index cho job quét vé quá hạn
        builder.HasIndex(x => x.ExpiresAt)
            .HasDatabaseName("ix_reservations_expiry")
            .HasFilter("\"Status\" IN ('Active', 'PaymentPending')");
    }
}

