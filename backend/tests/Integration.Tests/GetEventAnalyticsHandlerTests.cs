using System.Text.Json;
using CheckIn.Contracts;
using Events.Internal.Domain;
using Events.Internal.Features.Reports;
using Events.Internal.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Contracts;
using Ticketing.Contracts;
using Ticketing.SharedKernel;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// Nhiệm vụ 3 — TV3: Bộ kiểm thử độc lập cho GetEventAnalyticsHandler (FR-23 Analytics).
/// Kiểm thử các kịch bản bảo mật Fail-Closed, Cache Miss/Hit, Fallback và Validation.
/// </summary>
public class GetEventAnalyticsHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly EventsDbContext _db;
    private static readonly Guid CategoryId = new("20000000-0000-0000-0000-000000000001");

    public GetEventAnalyticsHandlerTests()
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

    private async Task<Event> SeedEventAsync(Guid organizerId)
    {
        var venue = Venue.Create("Hội trường Thống Nhất", "Nam Kỳ Khởi Nghĩa", "TP. Hồ Chí Minh", 1000);
        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();

        var @event = Event.Create(
            organizerId,
            CategoryId,
            venue.Id,
            "Tech Summit 2026",
            $"tech-summit-{Guid.NewGuid():N}"[..24],
            "Hội nghị công nghệ",
            DateTime.UtcNow.AddDays(5),
            DateTime.UtcNow.AddDays(6));

        _db.Events.Add(@event);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        return @event;
    }

    // ── Kịch bản 1: Non-admin với organizerId == null → Forbidden (Fail-Closed) ─
    [Fact]
    public async Task HandleAsync_NonAdmin_With_Null_OrganizerId_Returns_Forbidden()
    {
        var ownerId = Guid.NewGuid();
        var @event = await SeedEventAsync(ownerId);

        var ordersApi = new MockOrdersApi();
        var checkInApi = new MockCheckInApi();
        var ticketingApi = new MockTicketingApi();
        var cache = new InMemoryDistributedCache();

        var handler = new GetEventAnalyticsHandler(
            _db,
            ordersApi,
            checkInApi,
            ticketingApi,
            cache,
            NullLogger<GetEventAnalyticsHandler>.Instance);

        // Gọi với organizerId = null và isAdmin = false
        var result = await handler.HandleAsync(@event.Id, organizerId: null, isAdmin: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(0, ordersApi.CallCount); // Không gọi sang các module khác
    }

    // ── Kịch bản 2: Non-owner (organizerId != event.OrganizerId) → Forbidden ─
    [Fact]
    public async Task HandleAsync_NonOwner_Returns_Forbidden()
    {
        var ownerId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var @event = await SeedEventAsync(ownerId);

        var ordersApi = new MockOrdersApi();
        var checkInApi = new MockCheckInApi();
        var ticketingApi = new MockTicketingApi();
        var cache = new InMemoryDistributedCache();

        var handler = new GetEventAnalyticsHandler(
            _db,
            ordersApi,
            checkInApi,
            ticketingApi,
            cache,
            NullLogger<GetEventAnalyticsHandler>.Instance);

        var result = await handler.HandleAsync(@event.Id, organizerId: otherUser, isAdmin: false);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(0, ordersApi.CallCount);
    }

    // ── Kịch bản 3: Owner hợp lệ (Cache Miss) → Gọi 3 module, tính đúng số liệu, ghi cache ─
    [Fact]
    public async Task HandleAsync_Owner_CacheMiss_Fetches_From_Modules_And_Populates_Cache()
    {
        var ownerId = Guid.NewGuid();
        var @event = await SeedEventAsync(ownerId);

        var vipId = Guid.NewGuid();
        var standardId = Guid.NewGuid();

        var ordersApi = new MockOrdersApi(new EventRevenueSummaryDto(
            EventId: @event.Id,
            TotalRevenue: 20_000_000m,
            TotalOrdersCount: 40,
            TotalTicketsSold: 50,
            TicketTypesSales:
            [
                new TicketTypeSalesDto(vipId, "VIP", 20, 10_000_000m),
                new TicketTypeSalesDto(standardId, "Standard", 30, 10_000_000m)
            ]));

        var checkInApi = new MockCheckInApi(new EventCheckInSummaryDto(
            EventId: @event.Id,
            TotalCheckedIn: 40,
            LastCheckInAt: DateTime.UtcNow));

        var ticketingApi = new MockTicketingApi(
        [
            new TicketTypeSummaryDto(vipId, @event.Id, "VIP", 500_000m, 50, 20, 30),
            new TicketTypeSummaryDto(standardId, @event.Id, "Standard", 350_000m, 100, 30, 70)
        ]);

        var cache = new InMemoryDistributedCache();

        var handler = new GetEventAnalyticsHandler(
            _db,
            ordersApi,
            checkInApi,
            ticketingApi,
            cache,
            NullLogger<GetEventAnalyticsHandler>.Instance);

        var result = await handler.HandleAsync(@event.Id, organizerId: ownerId, isAdmin: false);

        Assert.True(result.IsSuccess);
        var data = result.Value;
        Assert.Equal(20_000_000m, data.TotalRevenue);
        Assert.Equal(50, data.TotalTicketsSold);
        Assert.Equal(40, data.CheckedInAttendees);
        Assert.Equal(80.0, data.AttendanceRatePercentage); // 40 / 50 = 80%
        Assert.Equal(2, data.Tiers.Count);

        // Kiểm tra đã gọi đủ 3 module
        Assert.Equal(1, ordersApi.CallCount);
        Assert.Equal(1, checkInApi.CallCount);
        Assert.Equal(1, ticketingApi.CallCount);

        // Kiểm tra đã ghi cache
        Assert.True(cache.SetCount > 0);

        // Kiểm tra bảng EventStatistics đã được lưu trong DB
        _db.ChangeTracker.Clear();
        var stats = await _db.EventStatistics.AsNoTracking().FirstOrDefaultAsync(s => s.EventId == @event.Id);
        Assert.NotNull(stats);
        Assert.Equal(20_000_000m, stats.TotalRevenue);
        Assert.Equal(50, stats.TotalTicketsSold);
        Assert.Equal(40, stats.TotalCheckedIn);
    }

    // ── Kịch bản 4: Cache Hit → Trả về từ cache, không gọi 3 module API ─
    [Fact]
    public async Task HandleAsync_CacheHit_Returns_Cached_Data_Without_Calling_Modules()
    {
        var ownerId = Guid.NewGuid();
        var @event = await SeedEventAsync(ownerId);

        var ordersApi = new MockOrdersApi();
        var checkInApi = new MockCheckInApi();
        var ticketingApi = new MockTicketingApi();
        var cache = new InMemoryDistributedCache();

        // Nạp sẵn dữ liệu vào cache
        var cachedResponse = new OrganizerEventAnalyticsResponse(
            EventId: @event.Id,
            EventTitle: @event.Title,
            Status: @event.Status,
            TotalRevenue: 99_999_999m,
            TotalOrdersCount: 99,
            TotalTicketsSold: 100,
            CheckedInAttendees: 90,
            AttendanceRatePercentage: 90.0,
            Tiers: [],
            CalculatedAt: DateTime.UtcNow);

        var cacheKey = $"analytics:events:{@event.Id}";
        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(cachedResponse));

        var handler = new GetEventAnalyticsHandler(
            _db,
            ordersApi,
            checkInApi,
            ticketingApi,
            cache,
            NullLogger<GetEventAnalyticsHandler>.Instance);

        var result = await handler.HandleAsync(@event.Id, organizerId: ownerId, isAdmin: false);

        Assert.True(result.IsSuccess);
        Assert.Equal(99_999_999m, result.Value.TotalRevenue);
        Assert.Equal(90, result.Value.CheckedInAttendees);

        // Xác nhận 3 module KHÔNG bị gọi nhờ Cache Hit
        Assert.Equal(0, ordersApi.CallCount);
        Assert.Equal(0, checkInApi.CallCount);
        Assert.Equal(0, ticketingApi.CallCount);
    }

    // ── Kịch bản 5: Sự kiện không tồn tại → Trả về NotFound ──────
    [Fact]
    public async Task HandleAsync_Event_Not_Found_Returns_NotFound()
    {
        var nonExistentId = Guid.NewGuid();
        var ordersApi = new MockOrdersApi();
        var checkInApi = new MockCheckInApi();
        var ticketingApi = new MockTicketingApi();
        var cache = new InMemoryDistributedCache();

        var handler = new GetEventAnalyticsHandler(
            _db,
            ordersApi,
            checkInApi,
            ticketingApi,
            cache,
            NullLogger<GetEventAnalyticsHandler>.Instance);

        var result = await handler.HandleAsync(nonExistentId, organizerId: Guid.NewGuid(), isAdmin: true);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.NotFound, result.ErrorCode);
    }
}

// ── In-Memory Mock Cache & APIs ──────────────────────────────────
internal sealed class InMemoryDistributedCache : IDistributedCache
{
    private readonly Dictionary<string, byte[]> _storage = new();
    public int SetCount { get; private set; }

    public byte[]? Get(string key) => _storage.TryGetValue(key, out var bytes) ? bytes : null;
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
    {
        SetCount++;
        _storage[key] = value;
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }

    public void Refresh(string key) { }
    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    public void Remove(string key) => _storage.Remove(key);
    public Task RemoveAsync(string key, CancellationToken token = default) { Remove(key); return Task.CompletedTask; }
}

internal sealed class MockOrdersApi(EventRevenueSummaryDto? summary = null) : IOrdersModuleApi
{
    public int CallCount { get; private set; }

    public Task<EventRevenueSummaryDto> GetEventRevenueSummaryAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(summary ?? new EventRevenueSummaryDto(eventId, 0, 0, 0, []));
    }

    public Task<IReadOnlyDictionary<Guid, decimal>> GetTotalRevenuesByEventIdsAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, decimal>>(new Dictionary<Guid, decimal>());
}

internal sealed class MockCheckInApi(EventCheckInSummaryDto? summary = null) : ICheckInModuleApi
{
    public int CallCount { get; private set; }

    public Task<EventCheckInSummaryDto> GetEventCheckInSummaryAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(summary ?? new EventCheckInSummaryDto(eventId, 0, null));
    }

    public Task<int> GetCheckedInCountAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        Task.FromResult(summary?.TotalCheckedIn ?? 0);

    public Task<IReadOnlyDictionary<Guid, int>> GetCheckedInCountsByEventIdsAsync(IEnumerable<Guid> eventIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

    public Task<CheckInResultDto> CheckInAsync(CheckInRequestDto request, string staffId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new CheckInResultDto("Valid", Guid.NewGuid(), "VIP", DateTime.UtcNow, "Valid"));

    public Task AssignStaffAsync(Guid eventId, string staffId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

internal sealed class MockTicketingApi(IReadOnlyList<TicketTypeSummaryDto>? ticketTypes = null) : ITicketingModuleApi
{
    public int CallCount { get; private set; }

    public Task<IReadOnlyList<TicketTypeSummaryDto>> GetTicketTypesByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(ticketTypes ?? []);
    }

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
        Task.FromResult((ticketTypes ?? []).Sum(t => t.SoldQuantity));
}
