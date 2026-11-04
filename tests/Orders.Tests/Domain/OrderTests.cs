using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.Modules.Orders.Domain.Events;
using Ticketing.Modules.Orders.Domain.ValueObjects;
using Ticketing.SharedKernel;

namespace Ticketing.Tests.Orders.Domain;

public class OrderTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ReservationId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static IReadOnlyList<OrderItem> Items() => new[]
    {
        OrderItem.Create(Guid.NewGuid(), 2, 125_000m).Value,
        OrderItem.Create(Guid.NewGuid(), 1, 50_000m).Value
    };

    [Fact]
    public void Create_WithValidInput_SucceedsAndRaisesOrderCreated()
    {
        var result = Order.Create(CustomerId, ReservationId, 300_000m, Items(), Now);

        result.IsSuccess.Should().BeTrue();
        var order = result.Value;
        order.Status.Should().Be(OrderStatus.Pending);
        order.TotalAmount.Should().Be(300_000m);
        order.ReservationId.Should().Be(ReservationId);
        order.DomainEvents.Should().ContainSingle(e => e is OrderCreated);
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, false, true)]
    public void Create_WithInvalidInput_FailsValidation(
        bool validCustomer, bool validReservation, bool emptyItems, bool negativeTotal)
    {
        var customer = validCustomer ? CustomerId : Guid.Empty;
        var reservation = validReservation ? ReservationId : Guid.Empty;
        var items = emptyItems ? Array.Empty<OrderItem>() : Items();
        var total = negativeTotal ? -1m : 300_000m;

        var result = Order.Create(customer, reservation, total, items, Now);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_WithTotalMismatchingItems_Fails()
    {
        var result = Order.Create(CustomerId, ReservationId, 999_999m, Items(), Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("does not match the sum of item subtotals");
    }

    [Fact]
    public void MarkAsPaid_FromPending_SetsStatusAndRaisesEvent()
    {
        var order = Order.Create(CustomerId, ReservationId, 300_000m, Items(), Now).Value;

        order.MarkAsPaid(Now.AddMinutes(5));

        order.Status.Should().Be(OrderStatus.Paid);
        order.DomainEvents.Should().ContainSingle(e => e is OrderPaid);
    }

    [Fact]
    public void MarkAsPaid_FromNonPending_Throws()
    {
        var order = Order.Create(CustomerId, ReservationId, 300_000m, Items(), Now).Value;
        order.MarkAsPaid(Now.AddMinutes(5));

        var act = () => order.MarkAsPaid(Now.AddMinutes(6));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsPaidAfterExpiry_SetsStatusAndRaisesEventWithReason()
    {
        var order = Order.Create(CustomerId, ReservationId, 300_000m, Items(), Now).Value;

        order.MarkAsPaidAfterExpiry(Now.AddMinutes(20), "late payment");

        order.Status.Should().Be(OrderStatus.PaidAfterExpiry);
        order.DomainEvents.Should().ContainSingle(e => e is OrderPaidAfterExpiry);
        order.DomainEvents.OfType<OrderPaidAfterExpiry>().Single().Reason.Should().Be("late payment");
    }

    [Fact]
    public void RequestRefund_FromPaid_SetsStatusAndRaisesEvent()
    {
        var order = Order.Create(CustomerId, ReservationId, 300_000m, Items(), Now).Value;
        order.MarkAsPaid(Now.AddMinutes(5));

        order.RequestRefund("changed plans", Now.AddMinutes(10));

        order.Status.Should().Be(OrderStatus.RefundRequested);
        order.DomainEvents.Should().ContainSingle(e => e is OrderRefundRequested);
    }

    [Fact]
    public void RequestRefund_FromPending_Throws()
    {
        var order = Order.Create(CustomerId, ReservationId, 300_000m, Items(), Now).Value;

        var act = () => order.RequestRefund("changed plans", Now.AddMinutes(1));

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RequestRefund_WithEmptyReason_Throws()
    {
        var order = Order.Create(CustomerId, ReservationId, 300_000m, Items(), Now).Value;
        order.MarkAsPaid(Now.AddMinutes(5));

        var act = () => order.RequestRefund("  ", Now.AddMinutes(10));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsRefunded_FromRefundRequested_SetsStatusAndRaisesEvent()
    {
        var order = Order.Create(CustomerId, ReservationId, 300_000m, Items(), Now).Value;
        order.MarkAsPaid(Now.AddMinutes(5));
        order.RequestRefund("changed plans", Now.AddMinutes(10));

        order.MarkAsRefunded(Now.AddMinutes(20));

        order.Status.Should().Be(OrderStatus.Refunded);
        order.DomainEvents.Should().ContainSingle(e => e is OrderRefunded);
    }

    [Fact]
    public void MarkRefundRejected_FromRefundRequested_ReturnsToPaid()
    {
        var order = Order.Create(CustomerId, ReservationId, 300_000m, Items(), Now).Value;
        order.MarkAsPaid(Now.AddMinutes(5));
        order.RequestRefund("changed plans", Now.AddMinutes(10));

        order.MarkRefundRejected(Now.AddMinutes(20));

        order.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public void Cancel_FromPending_SetsCancelled()
    {
        var order = Order.Create(CustomerId, ReservationId, 300_000m, Items(), Now).Value;

        order.Cancel(Now.AddMinutes(1));

        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_FromPaid_Throws()
    {
        var order = Order.Create(CustomerId, ReservationId, 300_000m, Items(), Now).Value;
        order.MarkAsPaid(Now.AddMinutes(5));

        var act = () => order.Cancel(Now.AddMinutes(10));

        act.Should().Throw<InvalidOperationException>();
    }
}
