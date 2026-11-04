using Microsoft.EntityFrameworkCore;
using Ticketing.Infrastructure.Core.Persistence;
using Ticketing.Modules.Notifications.Domain.Entities;
using Ticketing.Modules.Notifications.Domain.ValueObjects;

namespace Ticketing.Modules.Notifications.Infrastructure;

public sealed class NotificationsDbContext : BaseDbContext
{
    public override string Schema => "notifications";

    public DbSet<EmailHistory> EmailHistories => Set<EmailHistory>();

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

            builder.HasIndex(e => e.OrderId);
            builder.HasIndex(e => e.To);
            builder.HasIndex(e => e.Status);
        });
    }
}