using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Ticketing.Modules.Notifications.Infrastructure;
using Ticketing.Modules.Notifications.Infrastructure.Inbox;

namespace Ticketing.Tests.Notifications.Repositories;

public class InboxRepositoryEdgeCaseTests
{
    [Fact]
    public async Task TryStage_SameEventIdDifferentEventType_TreatsAsDuplicate()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new NotificationsDbContext(options);
        var repository = new InboxRepository(db);
        var eventId = Guid.NewGuid();

        var first = await repository.TryStageAsync(eventId, "TicketIssued", "{}");
        await db.SaveChangesAsync();
        var second = await repository.TryStageAsync(eventId, "RefundProcessed", "{}");

        first.Should().BeTrue();
        second.Should().BeFalse(); // Same EventId = duplicate regardless of type
    }

    [Fact]
    public async Task TryStage_ConcurrentSameEventId_FirstWins()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db1 = new NotificationsDbContext(options);
        var db2 = new NotificationsDbContext(options);
        var repo1 = new InboxRepository(db1);
        var repo2 = new InboxRepository(db2);
        var eventId = Guid.NewGuid();

        var first1 = await repo1.TryStageAsync(eventId, "TicketIssued", "{}");
        var first2 = await repo2.TryStageAsync(eventId, "TicketIssued", "{}");
        await db1.SaveChangesAsync();

        var second2 = await repo2.TryStageAsync(eventId, "TicketIssued", "{}");

        first1.Should().BeTrue();
        first2.Should().BeTrue(); // Both see it as new before commit
        second2.Should().BeFalse(); // After first commits, second sees duplicate
    }

    [Fact]
    public async Task TryStage_AfterRollback_AllowsRetry()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new NotificationsDbContext(options);
        var repository = new InboxRepository(db);
        var eventId = Guid.NewGuid();

        await repository.TryStageAsync(eventId, "TicketIssued", "{}");
        // Don't save - simulates rollback

        var retry = await repository.TryStageAsync(eventId, "TicketIssued", "{}");

        retry.Should().BeTrue(); // In-memory doesn't track uncommitted, so it's new
    }

    [Fact]
    public async Task TryStage_EmptyPayload_Accepted()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new NotificationsDbContext(options);
        var repository = new InboxRepository(db);

        var isNew = await repository.TryStageAsync(Guid.NewGuid(), "TicketIssued", "");
        await db.SaveChangesAsync();

        isNew.Should().BeTrue();
    }

    [Fact]
    public async Task TryStage_LargePayload_Accepted()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new NotificationsDbContext(options);
        var repository = new InboxRepository(db);
        var largePayload = new string('X', 100_000); // 100KB

        var isNew = await repository.TryStageAsync(Guid.NewGuid(), "TicketIssued", largePayload);
        await db.SaveChangesAsync();

        isNew.Should().BeTrue();
    }
}