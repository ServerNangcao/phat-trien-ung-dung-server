using Microsoft.EntityFrameworkCore;
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

[Collection("CompleteOrderPaymentCommandHandlerTests")]
public class CompleteOrderPaymentCommandHandlerTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ReservationId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static OrdersDbContext CreateDb() => new(new DbContextOptionsBuilder<OrdersDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options);

    private static (CompleteOrderPaymentCommandHandler Handler, OrdersDbContext Db) CreateHandler(FakeTicketingModuleApi ticketing)
    {
        var db = CreateDb();
        var repo = new OrderRepository(db);
        var handler = new CompleteOrderPaymentCommandHandler(repo, new UnitOfWork(db), ticketing, new FixedClock(Now));
        return (handler, db);
    }

    [Fact]
    public async Task Handle_FromPending_ActiveReservation_MarksPaidAndCommits()
    {
        var ticketing = new FakeTicketingModuleApi();
        ticketing.AddReservation(new ReservationDto(
            ReservationId, CustomerId, EventId,
            new[] { new ReservationItemDto(Guid.NewGuid(), "VIP", 2, 125_000m) },
            Now.AddMinutes(10), true, "fan@example.com"));
        var (handler, db) = CreateHandler(ticketing);
        var order = await SeedPendingOrder(db, 250_000m);

        var result = await handler.Handle(new CompleteOrderPaymentCommand(order.Id, 250_000m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Paid");
        var fresh = await db.Orders.FirstAsync(o => o.Id == order.Id);
        fresh.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task Handle_AmountMismatch_RejectsAndKeepsPending()
    {
        var ticketing = new FakeTicketingModuleApi();
        ticketing.AddReservation(new ReservationDto(
            ReservationId, CustomerId, EventId,
            new[] { new ReservationItemDto(Guid.NewGuid(), "VIP", 2, 125_000m) },
            Now.AddMinutes(10), true, "fan@example.com"));
        var (handler, db) = CreateHandler(ticketing);
        var order = await SeedPendingOrder(db, 250_000m);

        var result = await handler.Handle(new CompleteOrderPaymentCommand(order.Id, 999_999m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("Amount mismatch");
        var fresh = await db.Orders.FirstAsync(o => o.Id == order.Id);
        fresh.Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Handle_AlreadyPaid_IsIdempotentAndReturnsCurrentStatus()
    {
        var ticketing = new FakeTicketingModuleApi();
        ticketing.AddReservation(new ReservationDto(
            ReservationId, CustomerId, EventId,
            new[] { new ReservationItemDto(Guid.NewGuid(), "VIP", 2, 125_000m) },
            Now.AddMinutes(10), true, "fan@example.com"));
        var (handler, db) = CreateHandler(ticketing);
        var order = await SeedOrder(db, OrderStatus.Paid);

        var result = await handler.Handle(new CompleteOrderPaymentCommand(order.Id, 250_000m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Paid");
    }

    [Fact]
    public async Task Handle_AlreadyRefunded_IsIdempotentAndReturnsCurrentStatus()
    {
        var ticketing = new FakeTicketingModuleApi();
        ticketing.AddReservation(new ReservationDto(
            ReservationId, CustomerId, EventId,
            new[] { new ReservationItemDto(Guid.NewGuid(), "VIP", 2, 125_000m) },
            Now.AddMinutes(10), true, "fan@example.com"));
        var (handler, db) = CreateHandler(ticketing);
        var order = await SeedOrder(db, OrderStatus.Refunded);

        var result = await handler.Handle(new CompleteOrderPaymentCommand(order.Id, 250_000m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Refunded");
    }

    [Fact]
    public async Task Handle_AlreadyCancelled_IsIdempotentAndReturnsCurrentStatus()
    {
        var ticketing = new FakeTicketingModuleApi();
        ticketing.AddReservation(new ReservationDto(
            ReservationId, CustomerId, EventId,
            new[] { new ReservationItemDto(Guid.NewGuid(), "VIP", 2, 125_000m) },
            Now.AddMinutes(10), true, "fan@example.com"));
        var (handler, db) = CreateHandler(ticketing);
        var order = await SeedOrder(db, OrderStatus.Cancelled);

        var result = await handler.Handle(new CompleteOrderPaymentCommand(order.Id, 250_000m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task Handle_ExpiredReservation_MarksPaidAfterExpiryAndCreatesAutoRefund()
    {
        var ticketing = new FakeTicketingModuleApi();
        ticketing.AddReservation(new ReservationDto(
            ReservationId, CustomerId, EventId,
            new[] { new ReservationItemDto(Guid.NewGuid(), "VIP", 2, 125_000m) },
            Now.AddMinutes(-5), false, "fan@example.com"));
        var (handler, db) = CreateHandler(ticketing);
        var order = await SeedPendingOrder(db, 250_000m);

        var result = await handler.Handle(new CompleteOrderPaymentCommand(order.Id, 250_000m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("PaidAfterExpiry");
        var fresh = await db.Orders.FirstAsync(o => o.Id == order.Id);
        fresh.Status.Should().Be(OrderStatus.PaidAfterExpiry);
        var refund = await db.RefundRequests.SingleAsync(r => r.OrderId == order.Id);
        refund.Status.Should().Be(RefundStatus.Pending);
        refund.Reason.Should().Contain("ADR-0003");
    }

    [Fact]
    public async Task Handle_NotFoundOrder_ReturnsNotFound()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, _) = CreateHandler(ticketing);

        var result = await handler.Handle(new CompleteOrderPaymentCommand(Guid.NewGuid(), 250_000m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task Handle_TicketingOutage_ReturnsTransientFailureForRetry()
    {
        var ticketing = new FakeTicketingModuleApi { SimulateOutage = true };
        ticketing.AddReservation(new ReservationDto(
            ReservationId, CustomerId, EventId,
            new[] { new ReservationItemDto(Guid.NewGuid(), "VIP", 2, 125_000m) },
            Now.AddMinutes(10), true, "fan@example.com"));
        var (handler, db) = CreateHandler(ticketing);
        var order = await SeedPendingOrder(db, 250_000m);

        var result = await handler.Handle(new CompleteOrderPaymentCommand(order.Id, 250_000m), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("InternalError");
        result.Error.Message.Should().Contain("unreachable");
        var fresh = await db.Orders.FirstAsync(o => o.Id == order.Id);
        fresh.Status.Should().Be(OrderStatus.Pending);
    }

    private static async Task<Order> SeedPendingOrder(OrdersDbContext db, decimal total)
    {
        var order = Order.Create(CustomerId, ReservationId, EventId, total,
            new[] { OrderItem.Create(Guid.NewGuid(), 2, 125_000m).Value }, Now, "fan@example.com").Value;
        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();
        return order;
    }

    private static async Task<Order> SeedOrder(OrdersDbContext db, OrderStatus status)
    {
        var order = Order.Create(CustomerId, ReservationId, EventId, 250_000m,
            new[] { OrderItem.Create(Guid.NewGuid(), 2, 125_000m).Value }, Now, "fan@example.com").Value;
        switch (status)
        {
            case OrderStatus.Paid:
                order.MarkAsPaid(Now.AddMinutes(5));
                break;
            case OrderStatus.RefundRequested:
                order.MarkAsPaid(Now.AddMinutes(5));
                order.RequestRefund("changed plans", Now.AddMinutes(10));
                break;
            case OrderStatus.Refunded:
                order.MarkAsPaid(Now.AddMinutes(5));
                order.RequestRefund("changed plans", Now.AddMinutes(10));
                order.MarkAsRefunded(Now.AddMinutes(20));
                break;
            case OrderStatus.Cancelled:
                order.Cancel(Now.AddMinutes(1));
                break;
            case OrderStatus.PaidAfterExpiry:
                order.MarkAsPaidAfterExpiry(Now.AddMinutes(20), "late payment");
                break;
        }
        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();
        return order;
    }
}