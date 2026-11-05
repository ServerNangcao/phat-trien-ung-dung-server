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
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Name = "VIP",
            Price = 500000,
            Capacity = 100,
            QuantityAvailable = 100,
            MaxPerOrder = 5,
            Version = 0
        };
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
        Assert.Equal(98, updated.QuantityAvailable);

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
        var ticketType = new TicketType
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Name = "Standard",
            Price = 200000,
            Capacity = 2,
            QuantityAvailable = 2,
            MaxPerOrder = 5,
            Version = 0
        };
        db.TicketTypes.Add(ticketType);
        await db.SaveChangesAsync();

        // Mua 2 vé thành công
        await service.ReserveAsync(Guid.NewGuid(), eventId, ticketType.Id, 2, Guid.NewGuid().ToString());

        // Mua tiếp khi hết vé -> văng lỗi
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

        var ticket = new Ticket
        {
            Id = ticketId,
            EventId = eventId,
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            TicketTypeId = Guid.NewGuid(),
            Sequence = 1,
            TicketCode = "TK-12345678",
            EventTitle = "Concert 2026",
            TicketTypeName = "VIP",
            Status = "Valid",
            QrPayload = qr
        };
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

