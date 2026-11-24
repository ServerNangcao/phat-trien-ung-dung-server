using Notifications.Internal.Domain;
using Orders.Internal.Domain;
using Xunit;

namespace Integration.Tests;

public class OrderAndPaymentDomainTests
{
    [Fact]
    public void Order_AddItem_ThrowsArgumentOutOfRangeException_WhenQuantityZeroOrNegative()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Alice", "alice@example.com", 100m);

        Assert.Throws<ArgumentOutOfRangeException>(() => order.AddItem(Guid.NewGuid(), "VIP", 100m, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => order.AddItem(Guid.NewGuid(), "VIP", 100m, -1));
    }

    [Fact]
    public void Order_AddItem_ThrowsArgumentOutOfRangeException_WhenUnitPriceNegative()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Alice", "alice@example.com", 100m);

        Assert.Throws<ArgumentOutOfRangeException>(() => order.AddItem(Guid.NewGuid(), "VIP", -10m, 2));
    }

    [Fact]
    public void Order_AddItem_Succeeds_WhenValid()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Alice", "alice@example.com", 200m);
        var typeId = Guid.NewGuid();

        order.AddItem(typeId, "Standard", 100m, 2);

        Assert.Single(order.Items);
        var item = order.Items.First();
        Assert.Equal(typeId, item.TicketTypeId);
        Assert.Equal("Standard", item.TicketTypeName);
        Assert.Equal(100m, item.UnitPrice);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public void Order_MarkPaid_ThrowsInvalidOperationException_WhenNotPending()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Alice", "alice@example.com", 100m);
        order.Cancel();

        var ex = Assert.Throws<InvalidOperationException>(() => order.MarkPaid());
        Assert.Contains(OrderStatus.Cancelled, ex.Message);
    }

    [Fact]
    public void Order_MarkPaid_Succeeds_WhenPending()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Alice", "alice@example.com", 100m);
        order.MarkPaid();

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.NotNull(order.PaidAt);
    }

    [Fact]
    public void Order_MarkExpired_ThrowsInvalidOperationException_WhenNotPending()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Alice", "alice@example.com", 100m);
        order.MarkPaid();

        Assert.Throws<InvalidOperationException>(() => order.MarkExpired());
    }

    [Fact]
    public void Order_Cancel_ThrowsInvalidOperationException_WhenNotPending()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Alice", "alice@example.com", 100m);
        order.MarkPaid();

        Assert.Throws<InvalidOperationException>(() => order.Cancel());
    }

    [Fact]
    public void Order_MarkPaidAfterExpiry_ThrowsInvalidOperationException_WhenNotExpired()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Alice", "alice@example.com", 100m);

        Assert.Throws<InvalidOperationException>(() => order.MarkPaidAfterExpiry());
    }

    [Fact]
    public void Order_MarkPaidAfterExpiry_Succeeds_WhenExpired()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Alice", "alice@example.com", 100m);
        order.MarkExpired();
        order.MarkPaidAfterExpiry();

        Assert.Equal(OrderStatus.PaidAfterExpiry, order.Status);
        Assert.NotNull(order.PaidAt);
    }

    [Fact]
    public void Order_MarkRefunded_ThrowsInvalidOperationException_WhenNotPaidOrPaidAfterExpiry()
    {
        var order = Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Alice", "alice@example.com", 100m);

        Assert.Throws<InvalidOperationException>(() => order.MarkRefunded());
    }

    [Fact]
    public void Order_MarkRefunded_Succeeds_WhenPaidOrPaidAfterExpiry()
    {
        var order1 = Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Alice", "alice@example.com", 100m);
        order1.MarkPaid();
        order1.MarkRefunded();
        Assert.Equal(OrderStatus.Refunded, order1.Status);

        var order2 = Order.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Bob", "bob@example.com", 100m);
        order2.MarkExpired();
        order2.MarkPaidAfterExpiry();
        order2.MarkRefunded();
        Assert.Equal(OrderStatus.Refunded, order2.Status);
    }

    [Fact]
    public void Payment_MarkSucceeded_ThrowsInvalidOperationException_WhenNotPending()
    {
        var payment = Payment.Create(Guid.NewGuid(), "VNPAY", "TXN123", 100m);
        payment.MarkFailed("cancelled");

        Assert.Throws<InvalidOperationException>(() => payment.MarkSucceeded("VN123", "{}"));
    }

    [Fact]
    public void Payment_MarkSucceeded_Succeeds_WhenPending()
    {
        var payment = Payment.Create(Guid.NewGuid(), "VNPAY", "TXN123", 100m);
        payment.MarkSucceeded("VN123", "{}");

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal("VN123", payment.ProviderTransactionNo);
        Assert.NotNull(payment.PaidAt);
    }

    [Fact]
    public void Payment_MarkFailed_ThrowsInvalidOperationException_WhenNotPending()
    {
        var payment = Payment.Create(Guid.NewGuid(), "VNPAY", "TXN123", 100m);
        payment.MarkSucceeded("VN123", "{}");

        Assert.Throws<InvalidOperationException>(() => payment.MarkFailed("{}"));
    }

    [Fact]
    public void Payment_MarkFailed_Succeeds_WhenPending()
    {
        var payment = Payment.Create(Guid.NewGuid(), "VNPAY", "TXN123", 100m);
        payment.MarkFailed("error_payload");

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("error_payload", payment.RawPayload);
    }

    [Fact]
    public void EmailNotification_MarkSent_IncrementsAttemptCount_AndClearsNextRetryAt()
    {
        var email = EmailNotification.Create(Guid.NewGuid(), "alice@example.com", "Ticket Confirmation", "Enjoy!");
        email.MarkFailed("SMTP timeout", DateTime.UtcNow.AddMinutes(5));

        Assert.Equal(1, email.AttemptCount);
        Assert.NotNull(email.NextRetryAt);

        email.MarkSent();

        Assert.Equal(EmailNotificationStatus.Sent, email.Status);
        Assert.Equal(2, email.AttemptCount);
        Assert.Null(email.NextRetryAt);
        Assert.Null(email.LastError);
        Assert.NotNull(email.SentAt);
    }
}
