using System.Security.Claims;
using Events.Internal.Api;
using Events.Internal.Domain;
using Events.Internal.Infrastructure;
using Events.Internal.Infrastructure.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketing.Internal.Api;
using Ticketing.Internal.Application;
using Ticketing.Internal.Domain;
using Ticketing.Internal.Infrastructure;
using Xunit;

namespace Integration.Tests;

public sealed class EventsModuleApiAndTicketsTests : IDisposable
{
    private readonly SqliteConnection _eventsConnection;
    private readonly SqliteConnection _ticketingConnection;
    private readonly EventsDbContext _eventsDb;
    private readonly TicketingDbContext _ticketingDb;

    private static readonly Guid CategoryMusicId = new("20000000-0000-0000-0000-000000000001");

    public EventsModuleApiAndTicketsTests()
    {
        _eventsConnection = new SqliteConnection("DataSource=:memory:");
        _eventsConnection.Open();
        var eventsOptions = new DbContextOptionsBuilder<EventsDbContext>()
            .UseSqlite(_eventsConnection)
            .Options;
        _eventsDb = new EventsDbContext(eventsOptions);
        _eventsDb.Database.EnsureCreated();

        _ticketingConnection = new SqliteConnection("DataSource=:memory:");
        _ticketingConnection.Open();
        var ticketingOptions = new DbContextOptionsBuilder<TicketingDbContext>()
            .UseSqlite(_ticketingConnection)
            .Options;
        _ticketingDb = new TicketingDbContext(ticketingOptions);
        _ticketingDb.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _eventsDb.Dispose();
        _ticketingDb.Dispose();
        _eventsConnection.Dispose();
        _ticketingConnection.Dispose();
    }

    [Fact]
    public async Task GetEventsByIdsAsync_ReturnsEventDetailsWithDirectCoverUrl_WhenEventsExist()
    {
        // Arrange
        var venue = Venue.Create("The Global City", "Đỗ Xuân Hợp", "TP. Hồ Chí Minh", capacity: 5000);
        _eventsDb.Venues.Add(venue);
        await _eventsDb.SaveChangesAsync();

        var startsAt = new DateTime(2026, 12, 12, 19, 30, 0, DateTimeKind.Utc);
        var ev = Event.Create(
            Guid.NewGuid(),
            CategoryMusicId,
            venue.Id,
            title: "MIDNIGHT JAZZ — Chạm vào đêm",
            slug: "midnight-jazz-test",
            description: "Đêm nhạc jazz",
            startsAt: startsAt,
            endsAt: startsAt.AddHours(3));
        ev.SetCoverImage("https://cdn.sukienbanve.id.vn/midnight-jazz.jpg");

        _eventsDb.Events.Add(ev);
        await _eventsDb.SaveChangesAsync();
        _eventsDb.ChangeTracker.Clear();

        var api = new EventsModuleApi(_eventsDb);

        // Act
        var map = await api.GetEventsByIdsAsync([ev.Id, ev.Id, Guid.NewGuid()]);
        var single = await api.GetEventByIdAsync(ev.Id);

        // Assert
        Assert.Single(map);
        var dto = Assert.Contains(ev.Id, map);
        Assert.Equal("MIDNIGHT JAZZ — Chạm vào đêm", dto.Title);
        Assert.Equal("https://cdn.sukienbanve.id.vn/midnight-jazz.jpg", dto.CoverImageUrl);
        Assert.Equal(startsAt, dto.StartsAt);
        Assert.Equal("The Global City", dto.VenueName);
        Assert.Equal("TP. Hồ Chí Minh", dto.VenueCity);
        Assert.Equal(5000, dto.VenueCapacity);

        Assert.NotNull(single);
        Assert.Equal(dto, single);
    }

    [Fact]
    public async Task GetEventsByIdsAsync_ResolvesObjectKeyViaStorage_AndFallsBackToNullWhenStorageThrows()
    {
        // Arrange
        var venue = Venue.Create("Nhà hát Thành phố", "07 Công Trường Lam Sơn", "TP. Hồ Chí Minh", capacity: 800);
        _eventsDb.Venues.Add(venue);
        await _eventsDb.SaveChangesAsync();

        var ev = Event.Create(
            Guid.NewGuid(),
            CategoryMusicId,
            venue.Id,
            title: "Symphony Night",
            slug: "symphony-night-test",
            description: null,
            startsAt: DateTime.UtcNow.AddDays(10),
            endsAt: DateTime.UtcNow.AddDays(10).AddHours(2));
        ev.SetCoverImage("events/symphony/cover.jpg");

        _eventsDb.Events.Add(ev);
        await _eventsDb.SaveChangesAsync();
        _eventsDb.ChangeTracker.Clear();

        var workingApi = new EventsModuleApi(_eventsDb, new FakeR2StorageService("https://r2.example.test/signed-cover.jpg"));
        var failingApi = new EventsModuleApi(_eventsDb, new FailingR2StorageService(), NullLogger<EventsModuleApi>.Instance);

        // Act
        var resolved = await workingApi.GetEventByIdAsync(ev.Id);
        var fallback = await failingApi.GetEventByIdAsync(ev.Id);

        // Assert
        Assert.NotNull(resolved);
        Assert.Equal("https://r2.example.test/signed-cover.jpg", resolved.CoverImageUrl);

        Assert.NotNull(fallback);
        Assert.Equal("Symphony Night", fallback.Title);
        Assert.Null(fallback.CoverImageUrl);
    }

    [Fact]
    public async Task GetEventsByIdsAsync_ReturnsEmptyDictionary_WhenInputIsEmpty()
    {
        var api = new EventsModuleApi(_eventsDb);

        var map = await api.GetEventsByIdsAsync([]);

        Assert.Empty(map);
    }

    [Fact]
    public async Task TicketingService_PopulatesEventTitle_ForMyTicketsAndOrderTickets()
    {
        // Arrange
        var venue = Venue.Create("Công viên Yên Sở", "Hoàng Mai", "Hà Nội", capacity: 8000);
        _eventsDb.Venues.Add(venue);
        await _eventsDb.SaveChangesAsync();

        var ev = Event.Create(
            Guid.NewGuid(),
            CategoryMusicId,
            venue.Id,
            title: "The Sound of Spring",
            slug: "sound-of-spring-test",
            description: "Lễ hội âm nhạc",
            startsAt: new DateTime(2026, 12, 21, 16, 0, 0, DateTimeKind.Utc),
            endsAt: new DateTime(2026, 12, 21, 20, 0, 0, DateTimeKind.Utc));
        _eventsDb.Events.Add(ev);
        await _eventsDb.SaveChangesAsync();
        _eventsDb.ChangeTracker.Clear();

        var ticketType = TicketType.Create(ev.Id, "Regular", 450_000m, 100, 4);
        _ticketingDb.TicketTypes.Add(ticketType);
        await _ticketingDb.SaveChangesAsync();

        var signingKey = $"{Guid.NewGuid():N}{Guid.NewGuid():N}";
        var eventsApi = new EventsModuleApi(_eventsDb);
        var ticketingService = new TicketingService(
            _ticketingDb,
            new QrSigner(signingKey),
            NullLogger<TicketingService>.Instance,
            eventsApi);

        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        await ticketingService.IssueTicketsForOrderAsync(
            orderId,
            ev.Id,
            customerId,
            "buyer@example.test",
            "Người Mua",
            ev.Title,
            ticketType.Id,
            ticketType.Name,
            ticketType.Price,
            quantity: 2);

        // Act
        var myTickets = await ticketingService.GetMyTicketsAsync(customerId);
        var orderTickets = await ticketingService.GetTicketsByOrderIdsAsync([orderId]);

        // Assert
        Assert.Equal(2, myTickets.Count);
        Assert.All(myTickets, t =>
        {
            Assert.Equal("The Sound of Spring", t.EventTitle);
            Assert.Equal("Regular", t.TicketTypeName);
        });

        Assert.Equal(2, orderTickets.Count);
        Assert.All(orderTickets, t => Assert.Equal("The Sound of Spring", t.EventTitle));
    }

    [Fact]
    public async Task CreateTicketTypeAsync_RejectsWhenTotalQuantityExceedsVenueCapacity()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var venue = Venue.Create("Complex 01", "Tây Hồ", "Hà Nội", capacity: 350);
        _eventsDb.Venues.Add(venue);
        await _eventsDb.SaveChangesAsync();

        var ev = Event.Create(
            organizerId,
            CategoryMusicId,
            venue.Id,
            title: "Acoustic Night",
            slug: "acoustic-night-capacity",
            description: "Đêm nhạc giới hạn chỗ",
            startsAt: DateTime.UtcNow.AddDays(10),
            endsAt: DateTime.UtcNow.AddDays(10).AddHours(3));
        _eventsDb.Events.Add(ev);
        await _eventsDb.SaveChangesAsync();

        var eventsApi = new EventsModuleApi(_eventsDb);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()),
                new Claim(ClaimTypes.Role, "Organizer")
            ], "TestAuth"))
        };

        // Act 1: 200 <= 350 -> 201 Created
        var firstResult = await TicketingModule.CreateTicketTypeAsync(
            context,
            ev.Id,
            new CreateTicketTypeRequest("Standard", 250_000m, 200, 4),
            _ticketingDb,
            eventsApi,
            CancellationToken.None);

        var firstStatus = Assert.IsAssignableFrom<IStatusCodeHttpResult>(firstResult);
        Assert.Equal(StatusCodes.Status201Created, firstStatus.StatusCode);

        // Act 2: 200 + 200 = 400 > 350 -> 400 BadRequest
        var overResult = await TicketingModule.CreateTicketTypeAsync(
            context,
            ev.Id,
            new CreateTicketTypeRequest("VIP", 500_000m, 200, 4),
            _ticketingDb,
            eventsApi,
            CancellationToken.None);

        var overStatus = Assert.IsAssignableFrom<IStatusCodeHttpResult>(overResult);
        Assert.Equal(StatusCodes.Status400BadRequest, overStatus.StatusCode);

        var count = await _ticketingDb.TicketTypes.CountAsync(t => t.EventId == ev.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CreateTicketTypeAsync_RejectsWhenEventIsCancelledOrRejected()
    {
        // Arrange
        var organizerId = Guid.NewGuid();
        var venue = Venue.Create("Complex 01", "Tây Hồ", "Hà Nội", capacity: 350);
        _eventsDb.Venues.Add(venue);
        await _eventsDb.SaveChangesAsync();

        var cancelledEvent = Event.Create(
            organizerId,
            CategoryMusicId,
            venue.Id,
            title: "Cancelled Event",
            slug: "cancelled-event-ticket-type",
            description: "Sự kiện đã hủy",
            startsAt: DateTime.UtcNow.AddDays(10),
            endsAt: DateTime.UtcNow.AddDays(10).AddHours(3));
        cancelledEvent.Cancel("Hủy bởi Admin");

        var rejectedEvent = Event.Create(
            organizerId,
            CategoryMusicId,
            venue.Id,
            title: "Rejected Event",
            slug: "rejected-event-ticket-type",
            description: "Sự kiện bị từ chối",
            startsAt: DateTime.UtcNow.AddDays(12),
            endsAt: DateTime.UtcNow.AddDays(12).AddHours(3));
        rejectedEvent.SubmitForApproval();
        rejectedEvent.Reject("Không đạt yêu cầu");

        _eventsDb.Events.AddRange(cancelledEvent, rejectedEvent);
        await _eventsDb.SaveChangesAsync();

        var eventsApi = new EventsModuleApi(_eventsDb);
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, organizerId.ToString()),
                new Claim(ClaimTypes.Role, "Organizer")
            ], "TestAuth"))
        };

        // Act
        var cancelledResult = await TicketingModule.CreateTicketTypeAsync(
            context,
            cancelledEvent.Id,
            new CreateTicketTypeRequest("Standard", 200_000m, 50, 4),
            _ticketingDb,
            eventsApi,
            CancellationToken.None);

        var rejectedResult = await TicketingModule.CreateTicketTypeAsync(
            context,
            rejectedEvent.Id,
            new CreateTicketTypeRequest("Standard", 200_000m, 50, 4),
            _ticketingDb,
            eventsApi,
            CancellationToken.None);

        // Assert
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsAssignableFrom<IStatusCodeHttpResult>(cancelledResult).StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, Assert.IsAssignableFrom<IStatusCodeHttpResult>(rejectedResult).StatusCode);
        Assert.Equal(0, await _ticketingDb.TicketTypes.CountAsync(t => t.EventId == cancelledEvent.Id || t.EventId == rejectedEvent.Id));
    }

    private sealed class FakeR2StorageService(string presignedUrl) : IR2StorageService
    {
        public Task<string> GeneratePresignedPutUrlAsync(string objectKey, string contentType, int expiresInMinutes = 15, CancellationToken cancellationToken = default) =>
            Task.FromResult(presignedUrl);

        public Task<string> GeneratePresignedGetUrlAsync(string objectKey, int expiresInMinutes = 60, CancellationToken cancellationToken = default) =>
            Task.FromResult(presignedUrl);

        public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FailingR2StorageService : IR2StorageService
    {
        public Task<string> GeneratePresignedPutUrlAsync(string objectKey, string contentType, int expiresInMinutes = 15, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Storage not configured.");

        public Task<string> GeneratePresignedGetUrlAsync(string objectKey, int expiresInMinutes = 60, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Storage not configured.");

        public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Storage not configured.");
    }
}
