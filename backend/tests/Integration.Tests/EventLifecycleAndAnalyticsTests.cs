using CheckIn.Contracts;
using Events.Internal.Domain;
using Events.Internal.Features.Events.ChangeEventStatus;
using Events.Internal.Features.Events.ListEvents;
using Events.Internal.Features.Reports;
using Events.Internal.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Contracts;
using Ticketing.Contracts;
using Xunit;

namespace Integration.Tests;

public class EventLifecycleAndAnalyticsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly EventsDbContext _db;

    public EventLifecycleAndAnalyticsTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<EventsDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new EventsDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Event_Cancel_Sets_Status_To_Cancelled_And_Records_Reason()
    {
        var @event = Event.Create(
            organizerId: Guid.NewGuid(),
            categoryId: new Guid("20000000-0000-0000-0000-000000000001"),
            venueId: Guid.NewGuid(),
            title: "Live Concert 2026",
            slug: "live-concert-2026",
            description: "Music festival",
            startsAt: DateTime.UtcNow.AddDays(10),
            endsAt: DateTime.UtcNow.AddDays(11));

        @event.Approve(); // Chuyển sang OnSale
        Assert.Equal(EventStatus.OnSale, @event.Status);

        @event.Cancel("Do thời tiết xấu");

        Assert.Equal(EventStatus.Cancelled, @event.Status);
        Assert.Equal("Do thời tiết xấu", @event.RejectReason);
    }

    [Fact]
    public void Event_Cancel_Throws_When_Already_Ended_Or_Rejected()
    {
        var @event = Event.Create(
            organizerId: Guid.NewGuid(),
            categoryId: new Guid("20000000-0000-0000-0000-000000000001"),
            venueId: Guid.NewGuid(),
            title: "Live Concert Ended",
            slug: "live-concert-ended",
            description: "Music festival",
            startsAt: DateTime.UtcNow.AddDays(10),
            endsAt: DateTime.UtcNow.AddDays(11));

        @event.Approve();
        @event.End();
        Assert.Equal(EventStatus.Ended, @event.Status);

        Assert.Throws<InvalidOperationException>(() => @event.Cancel("Thử hủy"));
    }

    [Fact]
    public async Task CancelAsync_Allows_Organizer_To_Cancel_Own_Event()
    {
        var organizerId = Guid.NewGuid();
        var venue = Venue.Create("Sân vận động Mỹ Đình", "Đường Lê Đức Thọ", "Hà Nội", 40000);
        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();

        var @event = Event.Create(
            organizerId: organizerId,
            categoryId: new Guid("20000000-0000-0000-0000-000000000001"),
            venueId: venue.Id,
            title: "Rock Fest",
            slug: "rock-fest",
            description: "Rock band",
            startsAt: DateTime.UtcNow.AddDays(5),
            endsAt: DateTime.UtcNow.AddDays(6));

        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var handler = new ChangeEventStatusHandler(_db);

        // Organizer hợp lệ hủy
        var result = await handler.CancelAsync(@event.Id, organizerId, isAdmin: false, reason: "Bận lịch");
        Assert.True(result.IsSuccess);

        var updated = await _db.Events.FindAsync(@event.Id);
        Assert.NotNull(updated);
        Assert.Equal(EventStatus.Cancelled, updated.Status);
        Assert.Equal("Bận lịch", updated.RejectReason);
    }

    [Fact]
    public async Task CancelAsync_Forbids_Non_Owner_Organizer()
    {
        var ownerId = Guid.NewGuid();
        var otherOrganizerId = Guid.NewGuid();

        var venue = Venue.Create("Nhà thi đấu Phú Thọ", "Lý Thường Kiệt", "TP. Hồ Chí Minh", 5000);
        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();

        var @event = Event.Create(
            organizerId: ownerId,
            categoryId: new Guid("20000000-0000-0000-0000-000000000001"),
            venueId: venue.Id,
            title: "EDM Night",
            slug: "edm-night",
            description: "EDM",
            startsAt: DateTime.UtcNow.AddDays(2),
            endsAt: DateTime.UtcNow.AddDays(3));

        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var handler = new ChangeEventStatusHandler(_db);
        var result = await handler.CancelAsync(@event.Id, otherOrganizerId, isAdmin: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(Ticketing.SharedKernel.ErrorCodes.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task CancelAsync_By_Admin_Succeeds_Even_When_Not_Owner()
    {
        var ownerId = Guid.NewGuid();
        var venue = Venue.Create("Sân Quần Ngựa", "Văn Cao", "Hà Nội", 8000);
        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();

        var @event = Event.Create(
            organizerId: ownerId,
            categoryId: new Guid("20000000-0000-0000-0000-000000000001"),
            venueId: venue.Id,
            title: "Indie Pop Night",
            slug: "indie-pop-night",
            description: "Indie artists",
            startsAt: DateTime.UtcNow.AddDays(7),
            endsAt: DateTime.UtcNow.AddDays(8));

        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var handler = new ChangeEventStatusHandler(_db);
        // Admin hủy (không truyền organizerId hoặc truyền id khác)
        var result = await handler.CancelAsync(@event.Id, organizerId: null, isAdmin: true, reason: "Vi phạm chính sách sàn");

        Assert.True(result.IsSuccess);

        var updated = await _db.Events.FindAsync(@event.Id);
        Assert.NotNull(updated);
        Assert.Equal(EventStatus.Cancelled, updated.Status);
    }

    [Fact]
    public async Task CancelAsync_Forbids_When_Neither_Admin_Nor_OrganizerId()
    {
        var ownerId = Guid.NewGuid();
        var venue = Venue.Create("Hội trường Thống Nhất", "Nam Kỳ Khởi Nghĩa", "TP. Hồ Chí Minh", 1500);
        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();

        var @event = Event.Create(
            organizerId: ownerId,
            categoryId: new Guid("20000000-0000-0000-0000-000000000001"),
            venueId: venue.Id,
            title: "Acoustic Night",
            slug: "acoustic-night",
            description: "Acoustic",
            startsAt: DateTime.UtcNow.AddDays(4),
            endsAt: DateTime.UtcNow.AddDays(5));

        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var handler = new ChangeEventStatusHandler(_db);
        // Không phải admin và không có organizerId
        var result = await handler.CancelAsync(@event.Id, organizerId: null, isAdmin: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(Ticketing.SharedKernel.ErrorCodes.Forbidden, result.ErrorCode);
    }

    [Fact]
    public async Task CancelAsync_Conflict_When_Event_Is_Rejected()
    {
        var organizerId = Guid.NewGuid();
        var venue = Venue.Create("Nhà văn hóa Thanh Niên", "Phạm Ngọc Thạch", "TP. Hồ Chí Minh", 2000);
        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();

        var @event = Event.Create(
            organizerId: organizerId,
            categoryId: new Guid("20000000-0000-0000-0000-000000000001"),
            venueId: venue.Id,
            title: "Workshop Tech",
            slug: "workshop-tech",
            description: "Tech talks",
            startsAt: DateTime.UtcNow.AddDays(3),
            endsAt: DateTime.UtcNow.AddDays(4));

        @event.SubmitForApproval();
        @event.Reject("Thiếu giấy phép");
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var handler = new ChangeEventStatusHandler(_db);
        var result = await handler.CancelAsync(@event.Id, organizerId, isAdmin: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(Ticketing.SharedKernel.ErrorCodes.Conflict, result.ErrorCode);
    }

    [Fact]
    public async Task ListOrganizerEvents_Returns_All_Statuses_For_Organizer()
    {
        var organizerId = Guid.NewGuid();
        var venue = Venue.Create("Trung tâm hội nghị", "Hà Nội", "Hà Nội", 1000);
        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();

        var catId = new Guid("20000000-0000-0000-0000-000000000001");

        var event1 = Event.Create(organizerId, catId, venue.Id, "Event Draft", "event-draft", null, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2));
        var event2 = Event.Create(organizerId, catId, venue.Id, "Event OnSale", "event-onsale", null, DateTime.UtcNow.AddDays(3), DateTime.UtcNow.AddDays(4));
        event2.Approve();
        var event3 = Event.Create(organizerId, catId, venue.Id, "Event Cancelled", "event-cancelled", null, DateTime.UtcNow.AddDays(5), DateTime.UtcNow.AddDays(6));
        event3.Cancel("Hủy sớm");

        _db.Events.AddRange(event1, event2, event3);
        await _db.SaveChangesAsync();

        var handler = new ListOrganizerEventsHandler(_db);
        var result = await handler.HandleAsync(new ListOrganizerEventsQuery(organizerId));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TotalCount);
        Assert.Contains(result.Value.Items, e => e.Status == EventStatus.Draft);
        Assert.Contains(result.Value.Items, e => e.Status == EventStatus.OnSale);
        Assert.Contains(result.Value.Items, e => e.Status == EventStatus.Cancelled);
    }

    [Fact]
    public async Task GetEventAnalytics_Aggregates_Revenue_TicketsSold_And_CheckInCount()
    {
        var organizerId = Guid.NewGuid();
        var venue = Venue.Create("Nhà hát lớn", "Tràng Tiền", "Hà Nội", 500);
        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();

        var catId = new Guid("20000000-0000-0000-0000-000000000001");
        var @event = Event.Create(organizerId, catId, venue.Id, "Gala Âm Nhạc", "gala-am-nhac", null, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2));
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var vipTypeId = Guid.NewGuid();
        var stdTypeId = Guid.NewGuid();

        // Giả lập Module Orders trả về doanh thu
        var mockOrdersApi = new TestOrdersModuleApi(new EventRevenueSummaryDto(
            EventId: @event.Id,
            TotalRevenue: 15_000_000m,
            TotalOrdersCount: 25,
            TotalTicketsSold: 30,
            TicketTypesSales:
            [
                new TicketTypeSalesDto(vipTypeId, "VIP", 10, 10_000_000m),
                new TicketTypeSalesDto(stdTypeId, "Standard", 20, 5_000_000m)
            ]));

        // Giả lập Module CheckIn trả về 24 lượt soát vé thành công
        var mockCheckInApi = new TestCheckInModuleApi(new EventCheckInSummaryDto(
            EventId: @event.Id,
            TotalCheckedIn: 24,
            LastCheckInAt: DateTime.UtcNow));

        // Giả lập Module Ticketing trả về danh sách hạng vé
        var mockTicketingApi = new TestTicketingModuleApi(
        [
            new TicketTypeSummaryDto(vipTypeId, @event.Id, "VIP", 1_000_000m, 50, 10, 40),
            new TicketTypeSummaryDto(stdTypeId, @event.Id, "Standard", 250_000m, 200, 20, 180)
        ]);

        var handler = new GetEventAnalyticsHandler(
            _db,
            mockOrdersApi,
            mockCheckInApi,
            mockTicketingApi,
            cache: null,
            logger: NullLogger<GetEventAnalyticsHandler>.Instance);

        var result = await handler.HandleAsync(@event.Id, organizerId, isAdmin: false);

        Assert.True(result.IsSuccess);
        var data = result.Value;
        Assert.Equal(15_000_000m, data.TotalRevenue);
        Assert.Equal(30, data.TotalTicketsSold);
        Assert.Equal(24, data.CheckedInAttendees);
        Assert.Equal(80.0, data.AttendanceRatePercentage); // 24 / 30 = 80.0%
        Assert.Equal(2, data.Tiers.Count);

        // Kiểm tra EventStatistics được lưu vào DB
        var savedStats = await _db.EventStatistics.FirstOrDefaultAsync(s => s.EventId == @event.Id);
        Assert.NotNull(savedStats);
        Assert.Equal(15_000_000m, savedStats.TotalRevenue);
        Assert.Equal(30, savedStats.TotalTicketsSold);
        Assert.Equal(24, savedStats.TotalCheckedIn);
    }

    [Fact]
    public async Task CancelAsync_By_Admin_Removes_OnSale_Event_From_Public_ListEvents()
    {
        var venue = Venue.Create("Nhà hát Hòa Bình", "3 Tháng 2", "TP. Hồ Chí Minh", 2500);
        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();

        var @event = Event.Create(
            organizerId: Guid.NewGuid(),
            categoryId: new Guid("20000000-0000-0000-0000-000000000001"),
            venueId: venue.Id,
            title: "Public Concert To Cancel",
            slug: "public-concert-to-cancel",
            description: "Will be cancelled by admin",
            startsAt: DateTime.UtcNow.AddDays(12),
            endsAt: DateTime.UtcNow.AddDays(13));
        @event.Approve();

        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var listHandler = new ListEventsHandler(_db);
        var beforeCancel = await listHandler.HandleAsync(new ListEventsQuery(PageSize: 100));
        Assert.True(beforeCancel.IsSuccess);
        Assert.Contains(beforeCancel.Value!.Items, item => item.Id == @event.Id);

        var statusHandler = new ChangeEventStatusHandler(_db);
        var cancelResult = await statusHandler.CancelAsync(@event.Id, organizerId: null, isAdmin: true, reason: "Hủy khẩn cấp");
        Assert.True(cancelResult.IsSuccess);

        var afterCancel = await listHandler.HandleAsync(new ListEventsQuery(PageSize: 100));
        Assert.True(afterCancel.IsSuccess);
        Assert.DoesNotContain(afterCancel.Value!.Items, item => item.Id == @event.Id);
    }
}

// ── Test Implementations cho liên lạc liên module ──────────────
internal sealed class TestOrdersModuleApi(EventRevenueSummaryDto summary) : IOrdersModuleApi
{
    public Task<EventRevenueSummaryDto> GetEventRevenueSummaryAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(summary);

    public Task<IReadOnlyDictionary<Guid, decimal>> GetTotalRevenuesByEventIdsAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(new Dictionary<Guid, decimal> { [summary.EventId] = summary.TotalRevenue });
}

internal sealed class TestCheckInModuleApi(EventCheckInSummaryDto summary) : ICheckInModuleApi
{
    public Task<int> GetCheckedInCountAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(summary.TotalCheckedIn);

    public Task<CheckInResultDto> CheckInAsync(CheckInRequestDto request, string staffId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CheckInResultDto("Valid", Guid.NewGuid(), "VIP", DateTime.UtcNow, "Valid"));

    public Task AssignStaffAsync(Guid eventId, string staffId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<EventCheckInSummaryDto> GetEventCheckInSummaryAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(summary);

    public Task<IReadOnlyDictionary<Guid, int>> GetCheckedInCountsByEventIdsAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int> { [summary.EventId] = summary.TotalCheckedIn });
}

internal sealed class TestTicketingModuleApi(IReadOnlyList<TicketTypeSummaryDto> tiers) : ITicketingModuleApi
{
    public Task<IReadOnlyList<TicketTypeDto>> GetTypesAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TicketTypeDto>>([]);

    public Task<ReservationDto> ReserveAsync(Guid customerId, Guid eventId, Guid ticketTypeId, int quantity, string idempotencyKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(new ReservationDto(Guid.NewGuid(), eventId, ticketTypeId, customerId, quantity, 0, DateTime.UtcNow.AddMinutes(10), "Confirmed"));

    public Task<ReservationDto?> GetReservationAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<ReservationDto?>(null);

    public Task MarkPaymentPendingAsync(Guid id, Guid customerId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ReleaseAsync(Guid id, Guid customerId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<TicketDto>> GetMyTicketsAsync(Guid customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TicketDto>>([]);

    public Task<IReadOnlyList<TicketDto>> IssueTicketsForOrderAsync(Guid orderId, Guid eventId, Guid customerId, string customerEmail, string customerName, string eventTitle, Guid ticketTypeId, string ticketTypeName, decimal unitPrice, int quantity, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TicketDto>>([]);

    public Task<RedeemTicketResult> RedeemTicketAsync(string qrPayload, Guid eventId, string staffId, CancellationToken cancellationToken = default) =>
        Task.FromResult(RedeemTicketResult.Valid(Guid.NewGuid(), qrPayload, "VIP"));

    public Task<IReadOnlyList<TicketDto>> GetTicketsByOrderIdsAsync(IEnumerable<Guid> orderIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TicketDto>>([]);

    public Task<int> GetTotalIssuedTicketsCountAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(tiers.Sum(t => t.SoldQuantity));

    public Task<IReadOnlyList<TicketTypeSummaryDto>> GetTicketTypesByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(tiers);

    public Task<int> CancelTicketsForOrderAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}
