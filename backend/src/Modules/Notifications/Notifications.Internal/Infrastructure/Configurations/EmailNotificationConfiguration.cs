using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Internal.Domain;

namespace Notifications.Internal.Infrastructure.Configurations;

internal sealed class EmailNotificationConfiguration : IEntityTypeConfiguration<EmailNotification>
{
    public void Configure(EntityTypeBuilder<EmailNotification> builder)
    {
        builder.ToTable("EmailNotifications", t =>
        {
            t.HasCheckConstraint("ck_email_status", "\"Status\" IN ('Pending', 'Sent', 'Failed')");
        });

        builder.HasKey(x => x.Id);

        // Cross-schema reference tới Orders, NO FK
        builder.Property(x => x.OrderId)
            .IsRequired();

        builder.Property(x => x.Recipient)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Subject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Body)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.LastError)
            .HasMaxLength(4000);

        // Indexes
        builder.HasIndex(x => x.OrderId)
            .IsUnique()
            .HasDatabaseName("ux_email_order");

        builder.HasIndex(x => x.NextRetryAt)
            .HasDatabaseName("ix_email_retry")
            .HasFilter("\"Status\" IN ('Pending', 'Failed')");
    }
}

