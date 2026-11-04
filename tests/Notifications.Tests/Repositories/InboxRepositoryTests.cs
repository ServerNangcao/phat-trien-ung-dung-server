using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Ticketing.Modules.Notifications.Infrastructure;
using Ticketing.Modules.Notifications.Infrastructure.Inbox;

namespace Ticketing.Tests.Notifications.Repositories;

public class InboxRepositoryTests
{
    private static InboxRepository CreateRepository(out NotificationsDbContext db)
    {
        var options = new DbContextOptionsBuilder<NotificationsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        db = new NotificationsDbContext(options);
        return new InboxRepository(db);
    }

    [Fact]
    public async Task TryRecord_FirstDelivery_ReturnsTrue()
    {
        var repository = CreateRepository(out _);

        var isNew = await repository.TryRecordAsync(Guid.NewGuid(), "TicketIssued", "{}");

        isNew.Should().BeTrue();
    }

    [Fact]
    public async Task TryRecord_SameEventIdTwice_SecondIsDuplicate()
    {
        var repository = CreateRepository(out _);
        var eventId = Guid.NewGuid();

        var first = await repository.TryRecordAsync(eventId, "TicketIssued", "{}");
        var second = await repository.TryRecordAsync(eventId, "TicketIssued", "{}");

        first.Should().BeTrue();
        second.Should().BeFalse();
    }

    [Fact]
    public async Task TryRecord_DifferentEventIds_AreAllNew()
    {
        var repository = CreateRepository(out _);

        var first = await repository.TryRecordAsync(Guid.NewGuid(), "TicketIssued", "{}");
        var second = await repository.TryRecordAsync(Guid.NewGuid(), "TicketIssued", "{}");

        first.Should().BeTrue();
        second.Should().BeTrue();
    }
}
