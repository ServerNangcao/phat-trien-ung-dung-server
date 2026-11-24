using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketing.Contracts;
using Ticketing.Internal.Api;
using Ticketing.Internal.Application;
using Ticketing.Internal.Domain;
using Ticketing.Internal.Infrastructure;
using Xunit;

namespace Integration.Tests;

public class TicketingMyTicketsTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TicketingDbContext> _options;
    private const string QrKey = "unit-test-qr-signing-key-32-bytes-minimum";

    public TicketingMyTicketsTests()
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

    private static DefaultHttpContext CreateHttpContext(string? sub = null, string? role = null)
    {
        var context = new DefaultHttpContext();
        var claims = new List<Claim>();

        if (!string.IsNullOrWhiteSpace(sub))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
            claims.Add(new Claim("sub", sub));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (claims.Count > 0)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        }
        else
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
        }

        return context;
    }

    [Fact]
    public async Task AC01_GetMyTicketsAsync_WithoutClaims_Returns401Unauthorized()
    {
        using var db = new TicketingDbContext(_options);
        var service = new TicketingService(db, new QrSigner(QrKey), NullLogger<TicketingService>.Instance);
        var context = CreateHttpContext(); // No sub claim

        var result = await TicketingModule.GetMyTicketsAsync(context, service, CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task AC02_AC03_GetMyTicketsAsync_WithCustomerAClaims_ReturnsOnlyCustomerATickets()
    {
        using var db = new TicketingDbContext(_options);
        var service = new TicketingService(db, new QrSigner(QrKey), NullLogger<TicketingService>.Instance);

        var eventId = Guid.NewGuid();
        var ticketType = TicketType.Create(eventId, "VIP", 500000, 100, 5);
        db.TicketTypes.Add(ticketType);
        await db.SaveChangesAsync();

        var customerA = Guid.NewGuid();
        var customerB = Guid.NewGuid();

        // Seed ticket for Customer A
        var ticketA = Ticket.Issue(
            Guid.NewGuid(), ticketType.Id, eventId, customerA, 1,
            "TK-A-001", "qr-signature-a", "customera@example.com");
        db.Tickets.Add(ticketA);

        // Seed ticket for Customer B
        var ticketB = Ticket.Issue(
            Guid.NewGuid(), ticketType.Id, eventId, customerB, 1,
            "TK-B-001", "qr-signature-b", "customerb@example.com");
        db.Tickets.Add(ticketB);

        await db.SaveChangesAsync();

        var contextA = CreateHttpContext(customerA.ToString());

        var result = await TicketingModule.GetMyTicketsAsync(contextA, service, CancellationToken.None);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        Assert.NotNull(valueResult.Value);

        var returnedList = ((System.Collections.IEnumerable)valueResult.Value).Cast<dynamic>().ToList();
        Assert.Single(returnedList);

        // Chắc chắn vé trả về là của Customer A, không chứa vé của Customer B
        Assert.Equal(ticketA.Id, (Guid)returnedList[0].id);
        Assert.Equal("customera@example.com", (string)returnedList[0].holderName);
        Assert.DoesNotContain(returnedList, x => (Guid)x.id == ticketB.Id);
    }

    [Fact]
    public async Task AC04_GetMyTicketsAsync_CustomerWithoutTickets_Returns200WithEmptyList()
    {
        using var db = new TicketingDbContext(_options);
        var service = new TicketingService(db, new QrSigner(QrKey), NullLogger<TicketingService>.Instance);

        var customerC = Guid.NewGuid();
        var contextC = CreateHttpContext(customerC.ToString());

        var result = await TicketingModule.GetMyTicketsAsync(contextC, service, CancellationToken.None);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        Assert.NotNull(valueResult.Value);
        var returnedList = ((System.Collections.IEnumerable)valueResult.Value).Cast<dynamic>().ToList();
        Assert.Empty(returnedList);
    }

    [Fact]
    public async Task AC05_GetTicketQrImageAsync_OtherCustomerOrStaff_Returns403Forbidden()
    {
        using var db = new TicketingDbContext(_options);
        var eventId = Guid.NewGuid();
        var ticketType = TicketType.Create(eventId, "VIP", 500000, 100, 5);
        db.TicketTypes.Add(ticketType);

        var customerA = Guid.NewGuid();
        var ticketA = Ticket.Issue(
            Guid.NewGuid(), ticketType.Id, eventId, customerA, 1,
            "TK-A-002", "valid-qr-a", "customera@example.com");
        db.Tickets.Add(ticketA);
        await db.SaveChangesAsync();

        // 1. Customer B gọi lấy QR của Customer A -> 403 Forbidden
        var customerB = Guid.NewGuid();
        var contextB = CreateHttpContext(customerB.ToString());
        var resultB = await TicketingModule.GetTicketQrImageAsync(contextB, ticketA.Id, db, CancellationToken.None);
        Assert.IsType<ForbidHttpResult>(resultB);

        // 2. Staff (không phải chủ vé) gọi lấy QR của Customer A -> 403 Forbidden
        var staffId = Guid.NewGuid();
        var contextStaff = CreateHttpContext(staffId.ToString(), role: "Staff");
        var resultStaff = await TicketingModule.GetTicketQrImageAsync(contextStaff, ticketA.Id, db, CancellationToken.None);
        Assert.IsType<ForbidHttpResult>(resultStaff);
    }

    [Fact]
    public async Task GetTicketQrImageAsync_OwnerAndAdmin_Returns200WithPngImage()
    {
        using var db = new TicketingDbContext(_options);
        var eventId = Guid.NewGuid();
        var ticketType = TicketType.Create(eventId, "VIP", 500000, 100, 5);
        db.TicketTypes.Add(ticketType);

        var customerA = Guid.NewGuid();
        var signer = new QrSigner(QrKey);
        var ticketId = Guid.NewGuid();
        var qrSignature = signer.Sign(ticketId, eventId);

        var ticketA = Ticket.Issue(
            Guid.NewGuid(), ticketType.Id, eventId, customerA, 1,
            "TK-A-003", qrSignature, "customera@example.com", ticketId);
        db.Tickets.Add(ticketA);
        await db.SaveChangesAsync();

        // 1. Owner gọi
        var contextA = CreateHttpContext(customerA.ToString());
        var resultA = await TicketingModule.GetTicketQrImageAsync(contextA, ticketA.Id, db, CancellationToken.None);
        var fileResultA = Assert.IsType<FileContentHttpResult>(resultA);
        Assert.Equal("image/png", fileResultA.ContentType);
        Assert.False(fileResultA.FileContents.IsEmpty);

        // 2. Admin gọi
        var adminId = Guid.NewGuid();
        var contextAdmin = CreateHttpContext(adminId.ToString(), role: "Admin");
        var resultAdmin = await TicketingModule.GetTicketQrImageAsync(contextAdmin, ticketA.Id, db, CancellationToken.None);
        var fileResultAdmin = Assert.IsType<FileContentHttpResult>(resultAdmin);
        Assert.Equal("image/png", fileResultAdmin.ContentType);
        Assert.False(fileResultAdmin.FileContents.IsEmpty);
    }

    [Fact]
    public async Task GetTicketQrImageAsync_WithoutClaims_Returns401Unauthorized()
    {
        using var db = new TicketingDbContext(_options);
        var context = CreateHttpContext(); // No sub claim

        var result = await TicketingModule.GetTicketQrImageAsync(context, Guid.NewGuid(), db, CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task GetTicketQrImageAsync_CancelledTicket_Returns409Conflict()
    {
        using var db = new TicketingDbContext(_options);
        var eventId = Guid.NewGuid();
        var ticketType = TicketType.Create(eventId, "VIP", 500000, 100, 5);
        db.TicketTypes.Add(ticketType);

        var customerA = Guid.NewGuid();
        var ticketA = Ticket.Issue(
            Guid.NewGuid(), ticketType.Id, eventId, customerA, 1,
            "TK-A-004", "qr-sig-cancelled", "customera@example.com");
        ticketA.Cancel();
        db.Tickets.Add(ticketA);
        await db.SaveChangesAsync();

        var contextA = CreateHttpContext(customerA.ToString());
        var result = await TicketingModule.GetTicketQrImageAsync(contextA, ticketA.Id, db, CancellationToken.None);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, statusResult.StatusCode);
        var valueResult = Assert.IsAssignableFrom<IValueHttpResult>(result);
        Assert.NotNull(valueResult.Value);
    }

    [Fact]
    public async Task CancelTicketsForOrderAsync_CancelsValidTicketsAndRestoresSoldQuantity()
    {
        using var db = new TicketingDbContext(_options);
        var service = new TicketingService(db, new QrSigner(QrKey), NullLogger<TicketingService>.Instance);

        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var ticketType = TicketType.Create(eventId, "Standard", 200000, 50, 4);
        db.TicketTypes.Add(ticketType);
        await db.SaveChangesAsync();

        await service.IssueTicketsForOrderAsync(
            orderId, eventId, customerId, "buyer@example.com", "Buyer",
            "Concert", ticketType.Id, "Standard", 200000, 2, CancellationToken.None);

        var before = await db.TicketTypes.AsNoTracking().FirstAsync(t => t.Id == ticketType.Id);
        Assert.Equal(2, before.SoldQuantity);

        var cancelledCount = await service.CancelTicketsForOrderAsync(orderId, CancellationToken.None);

        Assert.Equal(2, cancelledCount);
        var afterType = await db.TicketTypes.AsNoTracking().FirstAsync(t => t.Id == ticketType.Id);
        Assert.Equal(0, afterType.SoldQuantity);
        var tickets = await db.Tickets.AsNoTracking().Where(t => t.OrderId == orderId).ToListAsync();
        Assert.All(tickets, t => Assert.Equal(TicketStatus.Cancelled, t.Status));
    }

    [Fact]
    public async Task CancelTicketsForOrderAsync_ThrowsInvalidOperationException_WhenAnyTicketAlreadyUsed()
    {
        using var db = new TicketingDbContext(_options);
        var service = new TicketingService(db, new QrSigner(QrKey), NullLogger<TicketingService>.Instance);

        var eventId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var ticketType = TicketType.Create(eventId, "VIP", 500000, 20, 4);
        db.TicketTypes.Add(ticketType);
        await db.SaveChangesAsync();

        var issued = await service.IssueTicketsForOrderAsync(
            orderId, eventId, customerId, "buyer@example.com", "Buyer",
            "Concert", ticketType.Id, "VIP", 500000, 2, CancellationToken.None);

        var firstTicket = await db.Tickets.FirstAsync(t => t.Id == issued[0].Id);
        firstTicket.MarkUsed(Guid.NewGuid());
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CancelTicketsForOrderAsync(orderId, CancellationToken.None));
    }
}
