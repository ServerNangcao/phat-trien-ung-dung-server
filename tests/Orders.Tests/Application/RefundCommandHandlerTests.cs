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

public class RefundCommandHandlerTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ReservationId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static (RequestRefundCommandHandler Handler, OrdersDbContext Db) CreateRequestHandler(FakeTicketingModuleApi ticketing)
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new OrdersDbContext(options);
        var repo = new OrderRepository(db);
        var handler = new RequestRefundCommandHandler(repo, new UnitOfWork(db), new FixedClock(Now));
        return (handler, db);
    }

    private static (ProcessRefundCommandHandler Handler, OrdersDbContext Db) CreateProcessHandler(FakeTicketingModuleApi ticketing)
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new OrdersDbContext(options);
        var repo = new OrderRepository(db);
        var handler = new ProcessRefundCommandHandler(repo, new UnitOfWork(db), new FixedClock(Now));
        return (handler, db);
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

    private static async Task<Order> SeedCancelledOrder(OrdersDbContext db)
    {
        var order = Order.Create(CustomerId, ReservationId, EventId, 250_000m,
            new[] { OrderItem.Create(Guid.NewGuid(), 2, 125_000m).Value }, Now, "fan@example.com").Value;
        order.Cancel(Now.AddMinutes(1));
        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();
        return order;
    }

    private static async Task<(Order Order, RefundRequest Refund)> SeedRefundRequestedOrder(OrdersDbContext db)
    {
        var order = await SeedPaidOrder(db);
        order.RequestRefund("changed plans", Now.AddMinutes(10));
        var refund = RefundRequest.Create(order.Id, "changed plans", Now.AddMinutes(10));
        await db.RefundRequests.AddAsync(refund);
        await db.SaveChangesAsync();
        return (order, refund);
    }

    private static async Task<(Order Order, RefundRequest Refund)> SeedRefundedOrder(OrdersDbContext db)
    {
        var (order, refund) = await SeedRefundRequestedOrder(db);
        order.MarkAsRefunded(Now.AddMinutes(20));
        refund.MarkAsProcessed(Now.AddMinutes(20), "Approved");
        await db.SaveChangesAsync();
        return (order, refund);
    }

    private static async Task<(Order Order, RefundRequest Refund)> SeedPaidAfterExpiryOrder(OrdersDbContext db)
    {
        var order = Order.Create(CustomerId, ReservationId, EventId, 250_000m,
            new[] { OrderItem.Create(Guid.NewGuid(), 2, 125_000m).Value }, Now, "fan@example.com").Value;
        order.MarkAsPaidAfterExpiry(Now.AddMinutes(20), "late payment");
        var refund = RefundRequest.Create(order.Id, "Late payment after reservation expiry (ADR-0003)", Now.AddMinutes(20));
        await db.Orders.AddAsync(order);
        await db.RefundRequests.AddAsync(refund);
        await db.SaveChangesAsync();
        return (order, refund);
    }

    // --- RequestRefundCommandHandler tests ---

    [Fact]
    public async Task RequestRefund_FromPaid_CreatesRefundRequestAndCommits()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateRequestHandler(ticketing);
        var order = await SeedPaidOrder(db);

        var result = await handler.Handle(new RequestRefundCommand(order.Id, "changed plans"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var fresh = await db.Orders.FirstAsync(o => o.Id == order.Id);
        fresh.Status.Should().Be(OrderStatus.RefundRequested);
        var refund = await db.RefundRequests.SingleAsync(r => r.OrderId == order.Id);
        refund.Reason.Should().Be("changed plans");
        refund.Status.Should().Be(RefundStatus.Pending);
    }

    [Fact]
    public async Task RequestRefund_FromPending_RejectsWithValidationError()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateRequestHandler(ticketing);
        var order = Order.Create(CustomerId, ReservationId, EventId, 250_000m,
            new[] { OrderItem.Create(Guid.NewGuid(), 2, 125_000m).Value }, Now, "fan@example.com").Value;
        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();

        var result = await handler.Handle(new RequestRefundCommand(order.Id, "changed plans"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("Only paid orders can request a refund");
    }

    [Fact]
    public async Task RequestRefund_FromRefundRequested_Rejects()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateRequestHandler(ticketing);
        var (order, _) = await SeedRefundRequestedOrder(db);

        var result = await handler.Handle(new RequestRefundCommand(order.Id, "changed plans"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("Only paid orders can request a refund");
    }

    [Fact]
    public async Task RequestRefund_FromCancelled_Rejects()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateRequestHandler(ticketing);
        var order = await SeedCancelledOrder(db);

        var result = await handler.Handle(new RequestRefundCommand(order.Id, "changed plans"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
    }

    [Fact]
    public async Task RequestRefund_WithEmptyReason_Rejects()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateRequestHandler(ticketing);
        var order = await SeedPaidOrder(db);

        var result = await handler.Handle(new RequestRefundCommand(order.Id, "  "), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("reason");
    }

    [Fact]
    public async Task RequestRefund_NotFoundOrder_ReturnsNotFound()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, _) = CreateRequestHandler(ticketing);

        var result = await handler.Handle(new RequestRefundCommand(Guid.NewGuid(), "reason"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NotFound");
    }

    // --- ProcessRefundCommandHandler tests ---

    [Fact]
    public async Task ProcessRefund_Approve_FromRefundRequested_MarksOrderRefundedAndProcessesRequest()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateProcessHandler(ticketing);
        var (order, refund) = await SeedRefundRequestedOrder(db);

        var result = await handler.Handle(new ProcessRefundCommand(refund.Id, true, "Approved"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var freshOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
        freshOrder.Status.Should().Be(OrderStatus.Refunded);
        var freshRefund = await db.RefundRequests.FirstAsync(r => r.Id == refund.Id);
        freshRefund.Status.Should().Be(RefundStatus.Processed);
        freshRefund.ProcessedNote.Should().Be("Approved");
    }

    [Fact]
    public async Task ProcessRefund_Approve_FromPaidAfterExpiry_MarksOrderRefundedAndProcessesRequest()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateProcessHandler(ticketing);
        var (order, refund) = await SeedPaidAfterExpiryOrder(db);

        var result = await handler.Handle(new ProcessRefundCommand(refund.Id, true, "Late payment auto-refund"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var freshOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
        freshOrder.Status.Should().Be(OrderStatus.Refunded);
        var freshRefund = await db.RefundRequests.FirstAsync(r => r.Id == refund.Id);
        freshRefund.Status.Should().Be(RefundStatus.Processed);
        freshRefund.ProcessedNote.Should().Be("Late payment auto-refund");
    }

    [Fact]
    public async Task ProcessRefund_Reject_FromRefundRequested_ReturnsOrderToPaid()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateProcessHandler(ticketing);
        var (order, refund) = await SeedRefundRequestedOrder(db);

        var result = await handler.Handle(new ProcessRefundCommand(refund.Id, false, "Insufficient evidence"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var freshOrder = await db.Orders.FirstAsync(o => o.Id == order.Id);
        freshOrder.Status.Should().Be(OrderStatus.Paid);
        var freshRefund = await db.RefundRequests.FirstAsync(r => r.Id == refund.Id);
        freshRefund.Status.Should().Be(RefundStatus.Rejected);
        freshRefund.ProcessedNote.Should().Be("Insufficient evidence");
    }

    [Fact]
    public async Task ProcessRefund_Reject_FromPaidAfterExpiry_Rejects()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateProcessHandler(ticketing);
        var (order, refund) = await SeedPaidAfterExpiryOrder(db);

        var result = await handler.Handle(new ProcessRefundCommand(refund.Id, false), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("Cannot reject refund");
    }

    [Fact]
    public async Task ProcessRefund_Approve_FromRefunded_Rejects()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateProcessHandler(ticketing);
        var (order, refund) = await SeedRefundedOrder(db);

        var result = await handler.Handle(new ProcessRefundCommand(refund.Id, true), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
        result.Error.Message.Should().Contain("not pending");
    }

    [Fact]
    public async Task ProcessRefund_NotFoundRefundRequest_ReturnsNotFound()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, _) = CreateProcessHandler(ticketing);

        var result = await handler.Handle(new ProcessRefundCommand(Guid.NewGuid(), true), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NotFound");
    }

    [Fact]
    public async Task ProcessRefund_AlreadyProcessedRefund_Rejects()
    {
        var ticketing = new FakeTicketingModuleApi();
        var (handler, db) = CreateProcessHandler(ticketing);
        var (order, refund) = await SeedRefundedOrder(db);

        var result = await handler.Handle(new ProcessRefundCommand(refund.Id, true), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ValidationError");
    }
}