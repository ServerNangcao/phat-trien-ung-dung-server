using Microsoft.EntityFrameworkCore;
using Notifications.Internal.Domain;
using Ticketing.Infrastructure.Core;

namespace Notifications.Internal.Infrastructure;

internal sealed class NotificationsDbContext : BaseDbContext
{
    public const string SchemaName = "notifications";

    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options, SchemaName)
    {
    }

    public DbSet<EmailNotification> EmailNotifications => Set<EmailNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}
