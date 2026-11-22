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
/// Bước 4 — TV3: Kiểm thử toàn diện vòng đời sự kiện theo state machine:
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
        _db.ChangeTracker.Clear();
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

    // ── TC-LC-01 ────────────────────────────────────────────────
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

        _db.ChangeTracker.Clear();
        var saved = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == result.Value);
        Assert.NotNull(saved);
        Assert.Equal(EventStatus.Draft, saved.Status);
        Assert.Equal(organizerId, saved.OrganizerId);
    }

    // ── TC-LC-02 ────────────────────────────────────────────────
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

        _db.ChangeTracker.Clear();
        var updatedInDraft = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == @event.Id);
        Assert.NotNull(updatedInDraft);
        Assert.Equal("Tiêu đề đã sửa", updatedInDraft.Title);

        // Chuyển sang OnSale (Draft → PendingApproval → OnSale)
        var statusHandler = new ChangeEventStatusHandler(_db);
        await statusHandler.SubmitForApprovalAsync(@event.Id, organizerId);
        await statusHandler.ApproveAsync(@event.Id);

        _db.ChangeTracker.Clear();

        // Cập nhật khi OnSale → phải trả Conflict
        var onSaleResult = await updateHandler.HandleAsync(@event.Id, organizerId, req);
        Assert.False(onSaleResult.IsSuccess);
        Assert.Equal(ErrorCodes.Conflict, onSaleResult.ErrorCode);
    }

    // ── TC-LC-03 ────────────────────────────────────────────────
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

        _db.ChangeTracker.Clear();
        var updated = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == @event.Id);
        Assert.NotNull(updated);
        Assert.Equal(EventStatus.PendingApproval, updated.Status);
    }

    // ── TC-LC-04a ───────────────────────────────────────────────
    [Fact]
    public async Task Approve_Sets_Status_To_OnSale()
    {
        var venue = await SeedVenueAsync("Cung Văn hóa Hữu Nghị");
        var organizerId = Guid.NewGuid();
        var handler = new ChangeEventStatusHandler(_db);

        var @event = MakeEvent(organizerId, CategoryId, venue.Id, "approve");
        @event.SubmitForApproval();
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var approveResult = await handler.ApproveAsync(@event.Id);
        Assert.True(approveResult.IsSuccess);

        _db.ChangeTracker.Clear();
        var approvedEvent = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == @event.Id);
        Assert.NotNull(approvedEvent);
        Assert.Equal(EventStatus.OnSale, approvedEvent.Status);
    }

    // ── TC-LC-04b ───────────────────────────────────────────────
    [Fact]
    public async Task Reject_Sets_Status_To_Rejected_With_Reason()
    {
        var venue = await SeedVenueAsync("Nhà thi đấu Hoàng Mai");
        var organizerId = Guid.NewGuid();
        var handler = new ChangeEventStatusHandler(_db);

        var @event = MakeEvent(organizerId, CategoryId, venue.Id, "reject");
        @event.SubmitForApproval();
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var rejectResult = await handler.RejectAsync(@event.Id, "Nội dung vi phạm chính sách.");
        Assert.True(rejectResult.IsSuccess);

        _db.ChangeTracker.Clear();
        var rejectedEvent = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == @event.Id);
        Assert.NotNull(rejectedEvent);
        Assert.Equal(EventStatus.Rejected, rejectedEvent.Status);
        Assert.Equal("Nội dung vi phạm chính sách.", rejectedEvent.RejectReason);
    }

    // ── TC-LC-05a ───────────────────────────────────────────────
    [Fact]
    public async Task CancelEvent_InDraft_Succeeds()
    {
        var venue = await SeedVenueAsync("Phòng Hội thảo B");
        var organizerId = Guid.NewGuid();
        var handler = new ChangeEventStatusHandler(_db);

        var @event = MakeEvent(organizerId, CategoryId, venue.Id, "cancel-draft");
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var cancelResult = await handler.CancelAsync(@event.Id, organizerId, isAdmin: false, reason: "Hủy từ nháp");
        Assert.True(cancelResult.IsSuccess);

        _db.ChangeTracker.Clear();
        var cancelledEvent = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == @event.Id);
        Assert.NotNull(cancelledEvent);
        Assert.Equal(EventStatus.Cancelled, cancelledEvent.Status);
    }

    // ── TC-LC-05b ───────────────────────────────────────────────
    [Fact]
    public async Task CancelEvent_When_Ended_Returns_Conflict()
    {
        var venue = await SeedVenueAsync("Phòng Hội nghị Quốc tế");
        var organizerId = Guid.NewGuid();
        var handler = new ChangeEventStatusHandler(_db);

        var @event = MakeEvent(organizerId, CategoryId, venue.Id, "ended-cancel");
        @event.SubmitForApproval();
        @event.Approve();
        @event.End();
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var failedCancel = await handler.CancelAsync(@event.Id, organizerId, isAdmin: false, reason: "Thử hủy sự kiện đã kết thúc");
        Assert.False(failedCancel.IsSuccess);
        Assert.Equal(ErrorCodes.Conflict, failedCancel.ErrorCode);
    }

    // ── TC-LC-06 ────────────────────────────────────────────────
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
        var result = await handler.CancelAsync(@event.Id, intruderId, isAdmin: false, reason: "Cố tình hủy");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.Forbidden, result.ErrorCode);

        _db.ChangeTracker.Clear();
        var unchanged = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == @event.Id);
        Assert.NotNull(unchanged);
        Assert.Equal(EventStatus.Draft, unchanged.Status);
    }

    // ── Bổ sung: EndEvent từ OnSale thành công ───────────────────
    [Fact]
    public async Task EndEvent_From_OnSale_Succeeds()
    {
        var venue = await SeedVenueAsync("Sân khấu Cầu Giấy");
        var organizerId = Guid.NewGuid();
        var handler = new ChangeEventStatusHandler(_db);

        var @event = MakeEvent(organizerId, CategoryId, venue.Id, "end-onsale");
        @event.SubmitForApproval();
        @event.Approve();
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var endResult = await handler.EndAsync(@event.Id);
        Assert.True(endResult.IsSuccess);

        _db.ChangeTracker.Clear();
        var ended = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == @event.Id);
        Assert.NotNull(ended);
        Assert.Equal(EventStatus.Ended, ended.Status);
    }

    // ── Bổ sung: EndEvent từ Draft trả về Conflict ───────────────
    [Fact]
    public async Task EndEvent_From_Draft_Returns_Conflict()
    {
        var venue = await SeedVenueAsync("Sân khấu Đống Đa");
        var organizerId = Guid.NewGuid();
        var handler = new ChangeEventStatusHandler(_db);

        var @event = MakeEvent(organizerId, CategoryId, venue.Id, "end-draft");
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var endResult = await handler.EndAsync(@event.Id);
        Assert.False(endResult.IsSuccess);
        Assert.Equal(ErrorCodes.Conflict, endResult.ErrorCode);
    }

    // ── Bổ sung: CancelEvent khi đang OnSale thành công ──────────
    [Fact]
    public async Task CancelEvent_In_OnSale_Succeeds()
    {
        var venue = await SeedVenueAsync("Sân Quần Ngựa");
        var organizerId = Guid.NewGuid();
        var handler = new ChangeEventStatusHandler(_db);

        var @event = MakeEvent(organizerId, CategoryId, venue.Id, "cancel-onsale");
        @event.SubmitForApproval();
        @event.Approve();
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var cancelResult = await handler.CancelAsync(@event.Id, organizerId, isAdmin: false, reason: "Bão lớn hủy show");
        Assert.True(cancelResult.IsSuccess);

        _db.ChangeTracker.Clear();
        var cancelled = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == @event.Id);
        Assert.NotNull(cancelled);
        Assert.Equal(EventStatus.Cancelled, cancelled.Status);
        Assert.Equal("Bão lớn hủy show", cancelled.RejectReason);
    }

    // ── Bổ sung: CancelEvent bởi Admin trên sự kiện của người khác ─
    [Fact]
    public async Task CancelEvent_By_Admin_On_Others_Event_Succeeds()
    {
        var venue = await SeedVenueAsync("Sân Hàng Đẫy");
        var organizerId = Guid.NewGuid();
        var handler = new ChangeEventStatusHandler(_db);

        var @event = MakeEvent(organizerId, CategoryId, venue.Id, "admin-cancel");
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        // Admin hủy (không truyền organizerId hoặc truyền id khác)
        var cancelResult = await handler.CancelAsync(@event.Id, organizerId: null, isAdmin: true, reason: "Vi phạm quy định nền tảng");
        Assert.True(cancelResult.IsSuccess);

        _db.ChangeTracker.Clear();
        var cancelled = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.Id == @event.Id);
        Assert.NotNull(cancelled);
        Assert.Equal(EventStatus.Cancelled, cancelled.Status);
        Assert.Equal("Vi phạm quy định nền tảng", cancelled.RejectReason);
    }

    // ── Bổ sung: Chặn duyệt nhảy cóc từ Draft ────────────────────
    [Fact]
    public async Task ApproveEvent_From_Draft_Returns_Conflict()
    {
        var venue = await SeedVenueAsync("Nhà hát Tuổi Trẻ");
        var organizerId = Guid.NewGuid();
        var handler = new ChangeEventStatusHandler(_db);

        var @event = MakeEvent(organizerId, CategoryId, venue.Id, "approve-draft");
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var approveResult = await handler.ApproveAsync(@event.Id);
        Assert.False(approveResult.IsSuccess);
        Assert.Equal(ErrorCodes.Conflict, approveResult.ErrorCode);
    }

    // ── Bổ sung: Chặn non-owner nộp duyệt sự kiện ─────────────────
    [Fact]
    public async Task SubmitForApproval_By_NonOwner_Returns_Forbidden()
    {
        var venue = await SeedVenueAsync("Trung tâm Triển lãm");
        var ownerId = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var handler = new ChangeEventStatusHandler(_db);

        var @event = MakeEvent(ownerId, CategoryId, venue.Id, "submit-nonowner");
        _db.Events.Add(@event);
        await _db.SaveChangesAsync();

        var result = await handler.SubmitForApprovalAsync(@event.Id, otherUser);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.Forbidden, result.ErrorCode);
    }
}
