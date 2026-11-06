using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Ticketing.Modules.Notifications.Infrastructure;
using Ticketing.Modules.Notifications.Infrastructure.Inbox;

namespace Ticketing.Tests.Notifications.Repositories;

public class InboxRepositoryTests
{
    [Fact]
    public async Task TryStage_FirstDelivery_ReturnsTrue()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new NotificationsDbContext(options);
        var repository = new InboxRepository(db);

        var isNew = await repository.TryStageAsync(Guid.NewGuid(), "TicketIssued", "{}");
        await db.SaveChangesAsync(); // the caller's unit of work persists the staged row

        isNew.Should().BeTrue();
    }

    [Fact]
    public async Task TryStage_SameEventIdTwice_SecondIsDuplicateAfterCommit()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new NotificationsDbContext(options);
        var repository = new InboxRepository(db);
        var eventId = Guid.NewGuid();

        var first = await repository.TryStageAsync(eventId, "TicketIssued", "{}");
        await db.SaveChangesAsync();
        var second = await repository.TryStageAsync(eventId, "TicketIssued", "{}");

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public async Task TryStage_DifferentEventIds_AreAllNew()
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new NotificationsDbContext(options);
        var repository = new InboxRepository(db);

        var first = await repository.TryStageAsync(Guid.NewGuid(), "TicketIssued", "{}");
        await db.SaveChangesAsync();
        var second = await repository.TryStageAsync(Guid.NewGuid(), "TicketIssued", "{}");

        first.Should().BeTrue();
        second.Should().BeTrue();
    }
}
