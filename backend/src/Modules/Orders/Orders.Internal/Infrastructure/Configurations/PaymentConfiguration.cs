using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Internal.Domain;

namespace Orders.Internal.Infrastructure.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", t =>
        {
            t.HasCheckConstraint("ck_payments_status", "\"Status\" IN ('Pending', 'Succeeded', 'Failed')");
        });

        builder.HasKey(x => x.Id);

        // FK nội bộ tới Order
        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.TxnRef)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 0);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ProviderTransactionNo)
            .HasMaxLength(200);

        builder.Property(x => x.RawPayload)
            .HasColumnType("jsonb");

        // Indexes
        builder.HasIndex(x => x.TxnRef)
            .IsUnique()
            .HasDatabaseName("ux_payments_txnref");

        builder.HasIndex(x => x.OrderId)
            .HasDatabaseName("ix_payments_order");
    }
}

