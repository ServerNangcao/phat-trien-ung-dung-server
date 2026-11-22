using Events.Internal.Domain;
using Events.Internal.Features.Events.ChangeEventStatus;
using Events.Internal.Features.Events.CreateEvent;
using Events.Internal.Features.Events.UpdateEvent;
using Events.Internal.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Ticketing.SharedKernel;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// Bước 4 — TV3: Kiểm thử vòng đời sự kiện theo state machine.
/// Draft → PendingApproval → OnSale | Rejected → Cancelled | Ended
/// </summary>
public class EventsLifecycleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly EventsDbContext _db;

    private static readonly Guid CategoryId = new("20000000-0000-0000-0000-000000000001");

    public EventsLifecycleTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<EventsDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new EventsDbContext(options);
        // EnsureCreated áp dụng HasData → CategoryId đã được seed sẵn
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Helper: tạo Venue và lưu DB ──────────────────────────────
    private async Task<Venue> SeedVenueAsync(string name = "Sân vận động Quốc gia")
    {
        var venue = Venue.Create(name, "Đường Lê Duẩn", "Hà Nội", 50_000);
        _db.Venues.Add(venue);
        await _db.SaveChangesAsync();
        return venue;
    }

    // ── Helper: tạo Event trực tiếp qua Domain ───────────────────
    private static Event MakeEvent(Guid organizerId, Guid categoryId, Guid venueId, string suffix = "")
    {
        var ts = suffix.Length > 0 ? suffix : Guid.NewGuid().ToString("N")[..8];
        return Event.Create(
            organizerId,
            categoryId,
            venueId,
            title: $"Test Event {ts}",
            slug: $"test-event-{ts}",
            description: "Mô tả kiểm thử",
            startsAt: DateTime.UtcNow.AddDays(5),
            endsAt: DateTime.UtcNow.AddDays(6));
    }

    // ── Test 1 ──────────────────────────────────────────────────
    /// <summary>
    /// TC-LC-01: Tạo sự kiện qua CreateEventHandler → trạng thái phải là Draft.
    /// </summary>
    [Fact]
    public async Task CreateEvent_Sets_Status_To_Draft()
    {
        var venue = await SeedVenueAsync();
        var organizerId = Guid.NewGuid();

        var handler = new CreateEventHandler(
            _db,
            new Events.Internal.Features.Events.CreateEvent.CreateEventValidator());

        var request = new CreateEventRequest(
            CategoryId: CategoryId,
            VenueId: venue.Id,
            Title: "Hòa nhạc mùa Thu",
            Description: "Chương trình nghệ thuật đặc sắc",
            StartsAt: DateTime.UtcNow.AddDays(10),
            EndsAt: DateTime.UtcNow.AddDays(11));

        var result = await handler.HandleAsync(organizerId, request);

        Assert.True(result.IsSuccess);

        var saved = await _db.Events.FindAsync(result.Value);
        Assert.NotNull(saved);
        Assert.Equal(EventStatus.Draft, saved.Status);
        Assert.Equal(organizerId, saved.OrganizerId);
    }

    // ── Test 2 ──────────────────────────────────────────────────
    /// <summary>
    /// TC-LC-02: UpdateEvent thành công khi Draft, trả Conflict khi OnSale.
    /// </summary>
    [Fact]
    public async Task UpdateEvent_InDraft_Succeeds_InOnSale_Fails()
    {
        var venue = await SeedVenueAsync("Nhà hát Lớn");
        var organizerId = Guid.NewGuid();

        var @event = MakeEvent(organizerId, CategoryId, venue.Id);
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var updateHandler = new UpdateEventHandler(
            _db,
            new Events.Internal.Features.Events.UpdateEvent.UpdateEventValidator());

        // Cập nhật khi Draft → phải thành công
        var req = new UpdateEventRequest(
            CategoryId: null,
            VenueId: null,
            Title: "Tiêu đề đã sửa",
            Description: null,
            StartsAt: null,
            EndsAt: null);

        var draftResult = await updateHandler.HandleAsync(@event.Id, organizerId, req);
        Assert.True(draftResult.IsSuccess);

        // Chuyển sang OnSale (Draft → PendingApproval → OnSale)
        @event.SubmitForApproval();
        @event.Approve();
        await _db.SaveChangesAsync();

        // Cập nhật khi OnSale → phải trả Conflict
        var onSaleResult = await updateHandler.HandleAsync(@event.Id, organizerId, req);
        Assert.False(onSaleResult.IsSuccess);
        Assert.Equal(ErrorCodes.Conflict, onSaleResult.ErrorCode);
    }

    // ── Test 3 ──────────────────────────────────────────────────
    /// <summary>
    /// TC-LC-03: SubmitForApproval → trạng thái chuyển sang PendingApproval.
    /// </summary>
    [Fact]
    public async Task SubmitForApproval_Changes_Status_To_PendingApproval()
    {
        var venue = await SeedVenueAsync("Trung tâm Hội nghị");
        var organizerId = Guid.NewGuid();

        var @event = MakeEvent(organizerId, CategoryId, venue.Id);
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var handler = new ChangeEventStatusHandler(_db);
        var result = await handler.SubmitForApprovalAsync(@event.Id, organizerId);

        Assert.True(result.IsSuccess);

        var updated = await _db.Events.FindAsync(@event.Id);
        Assert.NotNull(updated);
        Assert.Equal(EventStatus.PendingApproval, updated.Status);
    }

    // ── Test 4 ──────────────────────────────────────────────────
    /// <summary>
    /// TC-LC-04a: Admin duyệt → OnSale.
    /// TC-LC-04b: Admin từ chối → Rejected với lý do được lưu.
    /// </summary>
    [Fact]
    public async Task Approve_Sets_OnSale_And_Reject_Sets_Rejected_With_Reason()
    {
        var venue = await SeedVenueAsync("Cung Văn hóa Hữu Nghị");
        var organizerId = Guid.NewGuid();
        var handler = new ChangeEventStatusHandler(_db);

        // --- 4a: Approve ---
        var eventA = MakeEvent(organizerId, CategoryId, venue.Id, "approve");
        eventA.SubmitForApproval();
        _db.Events.Add(eventA);
        await _db.SaveChangesAsync();

        var approveResult = await handler.ApproveAsync(eventA.Id);
        Assert.True(approveResult.IsSuccess);

        var approvedEvent = await _db.Events.FindAsync(eventA.Id);
        Assert.NotNull(approvedEvent);
        Assert.Equal(EventStatus.OnSale, approvedEvent.Status);

        // --- 4b: Reject ---
        var eventB = MakeEvent(organizerId, CategoryId, venue.Id, "reject");
        eventB.SubmitForApproval();
        _db.Events.Add(eventB);
        await _db.SaveChangesAsync();

        var rejectResult = await handler.RejectAsync(eventB.Id, "Nội dung vi phạm chính sách.");
        Assert.True(rejectResult.IsSuccess);

        var rejectedEvent = await _db.Events.FindAsync(eventB.Id);
        Assert.NotNull(rejectedEvent);
        Assert.Equal(EventStatus.Rejected, rejectedEvent.Status);
        Assert.Equal("Nội dung vi phạm chính sách.", rejectedEvent.RejectReason);
    }

    // ── Test 5 ──────────────────────────────────────────────────
    /// <summary>
    /// TC-LC-05: Hủy sự kiện hợp lệ → Cancelled.
    ///           Hủy sự kiện đã Ended → Conflict.
    /// </summary>
    [Fact]
    public async Task CancelEvent_Succeeds_Blocks_On_Ended()
    {
        var venue = await SeedVenueAsync("Phòng Hội thảo B");
        var organizerId = Guid.NewGuid();
        var handler = new ChangeEventStatusHandler(_db);

        // --- Cancel hợp lệ (Draft → Cancel) ---
        var eventToCancel = MakeEvent(organizerId, CategoryId, venue.Id, "cancel");
        _db.Events.Add(eventToCancel);
        await _db.SaveChangesAsync();

        var cancelResult = await handler.CancelAsync(eventToCancel.Id, organizerId, isAdmin: false, reason: "Lý do hủy");
        Assert.True(cancelResult.IsSuccess);

        var cancelledEvent = await _db.Events.FindAsync(eventToCancel.Id);
        Assert.NotNull(cancelledEvent);
        Assert.Equal(EventStatus.Cancelled, cancelledEvent.Status);

        // --- Cancel sự kiện đã Ended → Conflict ---
        var eventToEnd = MakeEvent(organizerId, CategoryId, venue.Id, "end-then-cancel");
        eventToEnd.SubmitForApproval();
        eventToEnd.Approve();
        eventToEnd.End();
        _db.Events.Add(eventToEnd);
        await _db.SaveChangesAsync();

        var failedCancel = await handler.CancelAsync(eventToEnd.Id, organizerId, isAdmin: false, reason: "Thử hủy");
        Assert.False(failedCancel.IsSuccess);
        Assert.Equal(ErrorCodes.Conflict, failedCancel.ErrorCode);
    }

    // ── Test 6 ──────────────────────────────────────────────────
    /// <summary>
    /// TC-LC-06: CancelAsync từ Organizer khác (không phải chủ sự kiện) + isAdmin=false → Forbidden.
    /// </summary>
    [Fact]
    public async Task CancelAsync_Forbids_Non_Owner_Without_Admin()
    {
        var venue = await SeedVenueAsync("Sân khấu ngoài trời");
        var ownerId = Guid.NewGuid();
        var intruderId = Guid.NewGuid();

        var @event = MakeEvent(ownerId, CategoryId, venue.Id, "forbidden");
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var handler = new ChangeEventStatusHandler(_db);

        // Người lạ cố hủy, không phải admin → Forbidden
        var result = await handler.CancelAsync(@event.Id, intruderId, isAdmin: false, reason: "Cố tình hủy");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.Forbidden, result.ErrorCode);

        // Xác nhận event vẫn ở Draft (không bị thay đổi)
        var unchanged = await _db.Events.FindAsync(@event.Id);
        Assert.NotNull(unchanged);
        Assert.Equal(EventStatus.Draft, unchanged.Status);
    }
}
