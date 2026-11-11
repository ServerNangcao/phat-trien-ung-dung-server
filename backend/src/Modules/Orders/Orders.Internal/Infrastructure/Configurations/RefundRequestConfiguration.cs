using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Internal.Domain;

namespace Orders.Internal.Infrastructure.Configurations;

internal sealed class RefundRequestConfiguration : IEntityTypeConfiguration<RefundRequest>
{
    public void Configure(EntityTypeBuilder<RefundRequest> builder)
    {
        builder.ToTable("RefundRequests", t =>
        {
            t.HasCheckConstraint("ck_refundrequests_status",
                "\"Status\" IN ('Pending', 'Approved', 'Rejected', 'Refunded')");
        });

        builder.HasKey(x => x.Id);

        // FK nội bộ tới Order
        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK nội bộ tới Payment
        builder.HasOne<Payment>()
            .WithMany()
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 0);

        builder.Property(x => x.Reason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        // ProcessedBy — cross-schema reference tới Identity, NO FK
        builder.Property(x => x.ProcessedBy);

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_refunds_status");
    }
}

