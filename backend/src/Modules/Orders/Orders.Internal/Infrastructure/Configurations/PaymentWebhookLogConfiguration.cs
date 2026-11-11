using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Internal.Domain;

namespace Orders.Internal.Infrastructure.Configurations;

internal sealed class PaymentWebhookLogConfiguration : IEntityTypeConfiguration<PaymentWebhookLog>
{
    public void Configure(EntityTypeBuilder<PaymentWebhookLog> builder)
    {
        builder.ToTable("PaymentWebhookLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.TxnRef)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.SignatureValid)
            .IsRequired();

        builder.Property(x => x.ReceivedAt)
            .IsRequired();

        builder.HasIndex(x => new { x.Provider, x.TxnRef })
            .HasDatabaseName("ix_webhooklogs_txnref");
    }
}

