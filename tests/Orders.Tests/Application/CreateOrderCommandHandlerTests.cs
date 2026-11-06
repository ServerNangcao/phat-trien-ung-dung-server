using Microsoft.EntityFrameworkCore;
using Ticketing.Modules.Orders.Application.Commands;
using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.Modules.Orders.Infrastructure;
using Ticketing.Modules.Orders.Infrastructure.Data;
using Ticketing.Modules.Orders.Infrastructure.Repositories;
using Ticketing.Tests.Orders.TestSupport;
using Ticketing.SharedKernel;

namespace Ticketing.Tests.Orders.Application;

public class CreateOrderCommandHandlerTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static ReservationDto ValidReservation(Guid? reservationId = null, bool isActive = true) => new(
        reservationId ?? Guid.NewGuid(),
        CustomerId,
        Guid.NewGuid(),
        new[]
        {
            new ReservationItemDto(Guid.NewGuid(), "VIP", 2, 125_000m),
            new ReservationItemDto(Guid.NewGuid(), "Standard", 1, 50_000m)
        },
        Now.AddMinutes(10),
        isActive);

    private static (CreateOrderCommandHandler Handler, OrdersDbContext Db) CreateHandler(
        FakeTicketingModuleApi ticketing)
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new OrdersDbContext(options);
        var handler = new CreateOrderCommandHandler(
            new OrderRepository(db), new DiscountCodeRepository(db), new UnitOfWork(db), ticketing, new FixedClock(Now));
        return (handler, db);
    }

    [Fact]
    public async Task Handle_WithActiveReservation_CreatesOrderWithComputedTotal()
    {
        var ticketing = new FakeTicketingModuleApi();
        var reservation = ValidReservation();
        ticketing.AddReservation(reservation);
        var (handler, db) = CreateHandler(ticketing);

        var result = await handler.Handle(new CreateOrderCommand(CustomerId, reservation.ReservationId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var order = await db.Orders.Include(o => o.Items).SingleAsync(o => o.Id == result.Value.OrderId);
        order.TotalAmount.Should().Be(300_000m); // 2 × 125,000 + 1 × 50,000 — from the reservation, not the client
        order.ReservationId.Should().Be(reservation.ReservationId);
        order.Status.Should().Be(Ticketing.Modules.Orders.Domain.Entities.OrderStatus.Pending);
        order.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_SameReservationTwice_ReturnsSameOrderIdWithoutDuplicate()
    {
        var ticketing = new FakeTicketingModuleApi();
        var reservation = ValidReservation();
        ticketing.AddReservation(reservation);
        var (handler, db) = CreateHandler(ticketing);

        var first = await handler.Handle(new CreateOrderCommand(CustomerId, reservation.ReservationId), CancellationToken.None);
        var second = await handler.Handle(new CreateOrderCommand(CustomerId, reservation.ReservationId), CancellationToken.None);

        first.Value.OrderId.Should().Be(second.Value.OrderId);
        db.Orders.Count(o => o.ReservationId == reservation.ReservationId).Should().Be(1);
    }

    [Fact]
    public async Task Handle_ExpiredReservation_Fails()
    {
        var ticketing = new FakeTicketingModuleApi();
        var reservation = ValidReservation(isActive: false);
        ticketing.AddReservation(reservation);
        var (handler, _) = CreateHandler(ticketing);

        var result = await handler.Handle(new CreateOrderCommand(CustomerId, reservation.ReservationId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
    }

    [Fact]
    public async Task Handle_CustomerMismatch_Fails()
    {
        var ticketing = new FakeTicketingModuleApi();
        var reservation = ValidReservation();
        ticketing.AddReservation(reservation);
        var (handler, _) = CreateHandler(ticketing);

        var result = await handler.Handle(new CreateOrderCommand(Guid.NewGuid(), reservation.ReservationId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("different customer");
    }

    [Fact]
    public async Task Handle_TicketingUnreachable_PropagatesFailure()
    {
        var (handler, _) = CreateHandler(new FakeTicketingModuleApi()); // nothing registered

        var result = await handler.Handle(new CreateOrderCommand(CustomerId, Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task Handle_WithDiscountCode_ReducesTotalAndRecordsRedemptionInOneCommit()
    {
        var ticketing = new FakeTicketingModuleApi();
        var reservation = ValidReservation(); // 300,000 items total
        ticketing.AddReservation(reservation);
        var (handler, db) = CreateHandler(ticketing);
        var code = DiscountCode.Create("P10", DiscountCode.DiscountType.Percentage, 10m, null, null).Value;
        db.DiscountCodes.Add(code);
        await db.SaveChangesAsync();

        var result = await handler.Handle(new CreateOrderCommand(CustomerId, reservation.ReservationId, "p10"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DiscountAmount.Should().Be(30_000m);
        result.Value.TotalAmount.Should().Be(270_000m);
        var order = await db.Orders.Include(o => o.Items).SingleAsync(o => o.Id == result.Value.OrderId);
        order.DiscountAmount.Should().Be(30_000m);
        order.TotalAmount.Should().Be(270_000m); // payable total the gateway must confirm
        db.DiscountCodes.Entry(code).Reload();
        code.TimesUsed.Should().Be(1); // redemption counted in the same commit as the order
    }

    [Fact]
    public async Task Handle_WithUnknownDiscountCode_FailsWithoutCreatingOrder()
    {
        var ticketing = new FakeTicketingModuleApi();
        var reservation = ValidReservation();
        ticketing.AddReservation(reservation);
        var (handler, db) = CreateHandler(ticketing);

        var result = await handler.Handle(new CreateOrderCommand(CustomerId, reservation.ReservationId, "NOPE"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("does not exist");
        db.Orders.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithExhaustedDiscountCode_Fails()
    {
        var ticketing = new FakeTicketingModuleApi();
        var reservation = ValidReservation();
        ticketing.AddReservation(reservation);
        var (handler, db) = CreateHandler(ticketing);
        var code = DiscountCode.Create("ONCE", DiscountCode.DiscountType.Percentage, 10m, 1, null).Value;
        code.RecordRedemption();
        db.DiscountCodes.Add(code);
        await db.SaveChangesAsync();

        var result = await handler.Handle(new CreateOrderCommand(CustomerId, reservation.ReservationId, "ONCE"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("redemption limit");
    }
}
