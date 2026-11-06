using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Ticketing.Modules.Orders.Application.Commands;
using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.Modules.Orders.Domain.ValueObjects;
using Ticketing.Modules.Orders.Infrastructure;
using Ticketing.Modules.Orders.Infrastructure.Data;
using Ticketing.Modules.Orders.Infrastructure.Repositories;
using Ticketing.Tests.Orders.TestSupport;
using Ticketing.SharedKernel;

namespace Ticketing.Tests.Orders.Application;

public class CancelAndDiscountCommandHandlerTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ReservationId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static (CancelOrderCommandHandler Handler, OrdersDbContext Db) CreateCancelHandler(FakeTicketingModuleApi ticketing)
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new OrdersDbContext(options);
        var repo = new OrderRepository(db);
        var handler = new CancelOrderCommandHandler(repo, new UnitOfWork(db), ticketing, new FixedClock(Now), NullLogger<CancelOrderCommandHandler>.Instance);
        return (handler, db);
    }

    private static (CreateDiscountCodeCommandHandler Handler, OrdersDbContext Db) CreateDiscountHandler()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new OrdersDbContext(options);
        var repo = new DiscountCodeRepository(db);
        var handler = new CreateDiscountCodeCommandHandler(repo, new UnitOfWork(db));
        return (handler, db);
    }

    private static async Task<Order> SeedPendingOrder(OrdersDbContext db)
    {
        var order = Order.Create(CustomerId, ReservationId, EventId, 250_000m,
            new[] { OrderItem.Create(Guid.NewGuid(), 2, 125_000m).Value }, Now, "fan@example.com").Value;
        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();
        return order;
    }

    private static async Task<Order> SeedPaidOrder(OrdersDbContext db)
    {
        var order = Order.Create(CustomerId, ReservationId, EventId, 250_000m,
            new[] { OrderItem.Create(Guid.NewGuid(), 2, 125_000m).Value }, Now, "fan@example.com").Value;
        order.MarkAsPaid(Now.AddMinutes(5));
        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();
        return order;
    }

    // --- CancelOrderCommandHandler tests ---

    [Fact]
    public async Task Cancel_PendingOrder_MarksCancelledAndReleasesReservation()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateCancelHandler(ticketing);
        var order = await SeedPendingOrder(db);

        var result = await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Cancelled");
        var fresh = await db.Orders.FirstAsync(o => o.Id == order.Id);
        fresh.Status.Should().Be(OrderStatus.Cancelled);
        ticketing.CancelledReservations.Should().Contain(ReservationId);
    }

    [Fact]
    public async Task Cancel_PaidOrder_RejectsWithValidationError()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateCancelHandler(ticketing);
        var order = await SeedPaidOrder(db);

        var result = await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("Only pending orders can be cancelled");
    }

    [Fact]
    public async Task Cancel_RefundRequestedOrder_Rejects()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateCancelHandler(ticketing);
        var order = await SeedPaidOrder(db);
        order.RequestRefund("changed plans", Now.AddMinutes(10));
        await db.SaveChangesAsync();

        var result = await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
    }

    [Fact]
    public async Task Cancel_AlreadyCancelled_Rejects()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateCancelHandler(ticketing);
        var order = await SeedPendingOrder(db);
        order.Cancel(Now.AddMinutes(1));
        await db.SaveChangesAsync();

        var result = await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
    }

    [Fact]
    public async Task Cancel_NotFoundOrder_ReturnsNotFound()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, _) = CreateCancelHandler(ticketing);

        var result = await handler.Handle(new CancelOrderCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task Cancel_WhenReservationReleaseFails_StillCancelsOrder()
    {
        var ticketing = new FakeTicketingModuleApi { SimulateCancelFailure = true };
        var (handler, db) = CreateCancelHandler(ticketing);
        var order = await SeedPendingOrder(db);

        var result = await handler.Handle(new CancelOrderCommand(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var fresh = await db.Orders.FirstAsync(o => o.Id == order.Id);
        fresh.Status.Should().Be(OrderStatus.Cancelled);
    }

    // --- CreateDiscountCodeCommandHandler tests ---

    [Fact]
    public async Task CreateDiscountCode_ValidPercentage_CreatesCodeAndCommits()
    {
        var (handler, db) = CreateDiscountHandler();

        var result = await handler.Handle(new CreateDiscountCodeCommand("SUMMER10", "Percentage", 10m, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("SUMMER10");
        result.Value.Type.Should().Be("Percentage");
        result.Value.Value.Should().Be(10m);
        result.Value.MaxRedemptions.Should().BeNull();
        result.Value.TimesUsed.Should().Be(0);
        result.Value.IsActive.Should().BeTrue();
        var stored = await db.DiscountCodes.SingleAsync(c => c.Code == "SUMMER10");
        stored.Value.Should().Be(10m);
    }

    [Fact]
    public async Task CreateDiscountCode_ValidFixedAmount_CreatesCodeAndCommits()
    {
        var (handler, db) = CreateDiscountHandler();

        var result = await handler.Handle(new CreateDiscountCodeCommand("FLAT50K", "FixedVnd", 50_000m, 100, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("FLAT50K");
        result.Value.Type.Should().Be("FixedVnd");
        result.Value.Value.Should().Be(50_000m);
        result.Value.MaxRedemptions.Should().Be(100);
    }

    [Fact]
    public async Task CreateDiscountCode_NormalizesCodeToUppercase()
    {
        var (handler, db) = CreateDiscountHandler();

        var result = await handler.Handle(new CreateDiscountCodeCommand("  summer26  ", "Percentage", 10m, null, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Code.Should().Be("SUMMER26");
    }

    [Fact]
    public async Task CreateDiscountCode_DuplicateCode_ReturnsConflict()
    {
        var (handler, db) = CreateDiscountHandler();
        await handler.Handle(new CreateDiscountCodeCommand("DUPLICATE", "Percentage", 10m, null, null), CancellationToken.None);

        var result = await handler.Handle(new CreateDiscountCodeCommand("DUPLICATE", "Percentage", 15m, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Conflict");
        result.Error.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task CreateDiscountCode_InvalidType_ReturnsValidationError()
    {
        var (handler, db) = CreateDiscountHandler();

        var result = await handler.Handle(new CreateDiscountCodeCommand("INVALID", "BadType", 10m, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("invalid");
    }

    [Fact]
    public async Task CreateDiscountCode_PercentageOutsideRange_ReturnsValidationError()
    {
        var (handler, db) = CreateDiscountHandler();

        var result = await handler.Handle(new CreateDiscountCodeCommand("OVER100", "Percentage", 150m, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("0 and 100");
    }

    [Fact]
    public async Task CreateDiscountCode_FixedAmountZeroOrNegative_ReturnsValidationError()
    {
        var (handler, db) = CreateDiscountHandler();

        var result = await handler.Handle(new CreateDiscountCodeCommand("ZERO", "FixedVnd", 0m, null, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("greater than zero");
    }

    [Fact]
    public async Task CreateDiscountCode_MaxRedemptionsBelowOne_ReturnsValidationError()
    {
        var (handler, db) = CreateDiscountHandler();

        var result = await handler.Handle(new CreateDiscountCodeCommand("LIMIT", "Percentage", 10m, 0, null), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("at least 1");
    }

    [Fact]
    public async Task CreateDiscountCode_WithExpiration_SetsExpiresAtUtc()
    {
        var (handler, db) = CreateDiscountHandler();
        var expires = Now.AddDays(30);

        var result = await handler.Handle(new CreateDiscountCodeCommand("EXPIRES", "Percentage", 10m, null, expires), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ExpiresAtUtc.Should().Be(expires);
    }
}