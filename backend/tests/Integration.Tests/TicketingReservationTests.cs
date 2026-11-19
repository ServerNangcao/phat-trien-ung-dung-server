using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketing.Contracts;
using Ticketing.Internal.Application;
using Ticketing.Internal.Domain;
using Ticketing.Internal.Infrastructure;
using Xunit;

namespace Integration.Tests;

public class TicketingReservationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TicketingDbContext> _options;
    private const string QrKey = "unit-test-qr-signing-key-32-bytes-minimum";

    public TicketingReservationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<TicketingDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = new TicketingDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Reserve_Deducts_Stock_And_Sets_10_Minutes_Expiry()
    {
        using var db = new TicketingDbContext(_options);
        var service = new TicketingService(db, new QrSigner(QrKey), NullLogger<TicketingService>.Instance);

        var eventId = Guid.NewGuid();
        var ticketType = TicketType.Create(eventId, "VIP", 500000, 100, 5);
        db.TicketTypes.Add(ticketType);
        await db.SaveChangesAsync();

        var customerId = Guid.NewGuid();
        var idempotencyKey = Guid.NewGuid().ToString();

        // Thực hiện đặt 2 vé
        var reservation = await service.ReserveAsync(customerId, eventId, ticketType.Id, 2, idempotencyKey);

        Assert.Equal("Active", reservation.Status);
        Assert.Equal(2, reservation.Quantity);
        Assert.True(reservation.ExpiresAtUtc > DateTime.UtcNow.AddMinutes(9));

        // Kiểm tra tồn kho đã giảm còn 98
        var updated = await db.TicketTypes.FindAsync(ticketType.Id);
        Assert.NotNull(updated);
        Assert.Equal(98, updated.AvailableQuantity);

        // Gửi lại cùng idempotency key -> trả về cùng reservation
        var duplicate = await service.ReserveAsync(customerId, eventId, ticketType.Id, 2, idempotencyKey);
        Assert.Equal(reservation.Id, duplicate.Id);
    }

    [Fact]
    public async Task OverSelling_Is_Blocked_When_Insufficient_Stock()
    {
        using var db = new TicketingDbContext(_options);
        var service = new TicketingService(db, new QrSigner(QrKey), NullLogger<TicketingService>.Instance);

        var eventId = Guid.NewGuid();
        var ticketType = TicketType.Create(eventId, "Standard", 200000, 2, 5);
        db.TicketTypes.Add(ticketType);
        await db.SaveChangesAsync();

        // Mua 2 vé thành công
        await service.ReserveAsync(Guid.NewGuid(), eventId, ticketType.Id, 2, Guid.NewGuid().ToString());

        // Mua tiếp khi hết vé -> ném InvalidOperationException (chặn over-selling)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReserveAsync(Guid.NewGuid(), eventId, ticketType.Id, 1, Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task RedeemTicket_Once_Succeeds_And_SecondScan_Returns_AlreadyUsed()
    {
        using var db = new TicketingDbContext(_options);
        var signer = new QrSigner(QrKey);
        var service = new TicketingService(db, signer, NullLogger<TicketingService>.Instance);

        var eventId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var qr = signer.Sign(ticketId, eventId);

        var ticketType = TicketType.Create(eventId, "VIP", 500000, 100, 5);
        db.TicketTypes.Add(ticketType);

        var ticket = Ticket.Issue(
            Guid.NewGuid(),
            ticketType.Id,
            eventId,
            Guid.NewGuid(),
            1,
            "TK-12345678",
            qr,
            "customer@test.com");

        // Gán Id khớp với QR đã ký để test
        typeof(Ticket).GetProperty(nameof(Ticket.Id))!.SetValue(ticket, ticketId);

        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        // Quét lần 1: Hợp lệ
        var scan1 = await service.RedeemTicketAsync(qr, eventId, "staff-01");
        Assert.Equal(RedeemTicketStatus.Valid, scan1.Status);

        // Quét lần 2: Đã sử dụng (chặn quay vòng vé)
        var scan2 = await service.RedeemTicketAsync(qr, eventId, "staff-02");
        Assert.Equal(RedeemTicketStatus.AlreadyUsed, scan2.Status);
    }
}
