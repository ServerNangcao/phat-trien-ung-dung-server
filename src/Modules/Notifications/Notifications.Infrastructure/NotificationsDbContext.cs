using Microsoft.EntityFrameworkCore;
using Ticketing.Infrastructure.Core.Outbox;
using Ticketing.Infrastructure.Core.Persistence;
using Ticketing.Modules.Notifications.Domain.Entities;

namespace Ticketing.Modules.Notifications.Infrastructure;

public sealed class NotificationsDbContext : BaseDbContext
{
    public override string Schema => "notifications";

    public DbSet<EmailHistory> EmailHistories => Set<EmailHistory>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<EmailHistory>(builder =>
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.OrderId).IsRequired();
            builder.Property(e => e.To).IsRequired().HasMaxLength(255);
            builder.Property(e => e.Subject).IsRequired().HasMaxLength(500);
            builder.Property(e => e.Body).IsRequired();
            builder.Property(e => e.Status).HasConversion<string>();
            builder.Property(e => e.SentAt);
            builder.Property(e => e.CreatedAt).IsRequired();
            builder.Property(e => e.RetryCount).IsRequired();
            builder.Property(e => e.LastError).HasMaxLength(1000);

            builder.HasIndex(e => e.OrderId);
            builder.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<InboxMessage>(builder =>
        {
            builder.ToTable("inbox_messages");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.EventId).IsRequired();
            builder.Property(m => m.EventType).IsRequired().HasMaxLength(500);
            builder.Property(m => m.Payload).IsRequired();
            builder.Property(m => m.ReceivedAt).IsRequired();

            builder.HasIndex(m => m.EventId).IsUnique();
        });
    }
}
