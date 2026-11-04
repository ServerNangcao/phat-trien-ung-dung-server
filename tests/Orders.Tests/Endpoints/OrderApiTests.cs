using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.Modules.Orders.Infrastructure;
using Ticketing.Modules.Orders.Contracts.IntegrationEvents;
using Ticketing.Tests.Orders.Fixtures;
using Ticketing.Tests.Orders.TestSupport;

namespace Ticketing.Tests.Orders.Endpoints;

public class OrderApiTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static ReservationDto ValidReservation(bool isActive = true) => new(
        Guid.NewGuid(),
        CustomerId,
        Guid.NewGuid(),
        new[] { new ReservationItemDto(Guid.NewGuid(), "VIP", 2, 125_000m) },
        DateTime.UtcNow.AddMinutes(10), // the app under test uses the real clock
        isActive);

    [Fact]
    public async Task CreateOrder_ReturnsOrderIdAndSignedPaymentUrl()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = CustomerId,
            reservationId = reservation.ReservationId
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("orderId").GetGuid().Should().NotBe(Guid.Empty);
        var paymentUrl = body.GetProperty("paymentUrl").GetString()!;
        paymentUrl.Should().Contain("vnp_SecureHash=");
        paymentUrl.Should().Contain("vnp_Amount=25000000"); // 2 × 125,000 × 100
        body.GetProperty("status").GetString().Should().Be("Pending");
    }

    [Fact]
    public async Task CreateOrder_SameReservationTwice_ReturnsSameOrderId()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);

        var first = await client.PostAsJsonAsync("/api/orders", new { customerId = CustomerId, reservationId = reservation.ReservationId });
        var second = await client.PostAsJsonAsync("/api/orders", new { customerId = CustomerId, reservationId = reservation.ReservationId });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetGuid();
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetGuid();
        firstId.Should().Be(secondId);
    }

    [Fact]
    public async Task CreateOrder_ExpiredReservation_Returns400()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation(isActive: false);
        factory.Ticketing.AddReservation(reservation);

        var response = await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = CustomerId,
            reservationId = reservation.ReservationId
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ipn_WithValidSignature_MarksPaidAndWritesOutbox()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);

        var created = await (await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = CustomerId,
            reservationId = reservation.ReservationId
        })).Content.ReadFromJsonAsync<JsonElement>();
        var orderId = created.GetProperty("orderId").GetGuid();
        var total = created.GetProperty("totalAmount").GetDecimal();

        var ipn = await client.PostAsync($"/api/payments/vnpay/ipn?{VnPayTestHelper.BuildIpnQuery(orderId, total)}", null);

        ipn.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var order = await db.Orders.Include(o => o.Items).FirstAsync(o => o.Id == orderId);
        order.Status.Should().Be(OrderStatus.Paid);

        var outbox = await db.OutboxMessages.Where(m => m.ProcessedAt == null).ToListAsync();
        outbox.Should().ContainSingle();
        outbox[0].Type.Should().Contain(nameof(OrderPaidIntegrationEvent));
        outbox[0].Content.Should().Contain(orderId.ToString()).And.Contain(reservation.ReservationId.ToString());
    }

    [Fact]
    public async Task Ipn_ReplayedDelivery_IsIdempotent()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);
        var created = await (await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = CustomerId,
            reservationId = reservation.ReservationId
        })).Content.ReadFromJsonAsync<JsonElement>();
        var orderId = created.GetProperty("orderId").GetGuid();
        var total = created.GetProperty("totalAmount").GetDecimal();
        var query = VnPayTestHelper.BuildIpnQuery(orderId, total);

        await client.PostAsync($"/api/payments/vnpay/ipn?{query}", null);
        var replay = await client.PostAsync($"/api/payments/vnpay/ipn?{query}", null);

        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.OutboxMessages.CountAsync()).Should().Be(1);
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task Ipn_WithTamperedAmount_Returns400AndKeepsOrderPending()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);
        var created = await (await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = CustomerId,
            reservationId = reservation.ReservationId
        })).Content.ReadFromJsonAsync<JsonElement>();
        var orderId = created.GetProperty("orderId").GetGuid();
        var total = created.GetProperty("totalAmount").GetDecimal();

        var ipn = await client.PostAsync($"/api/payments/vnpay/ipn?{VnPayTestHelper.BuildIpnQuery(orderId, total, tamperAmount: true)}", null);

        ipn.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Ipn_WithInvalidSignature_Returns400()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var orderId = Guid.NewGuid();
        var query = VnPayTestHelper.BuildIpnQuery(orderId, 100_000m) + "&vnp_Amount=999"; // breaks the signature

        var ipn = await client.PostAsync($"/api/payments/vnpay/ipn?{query}", null);

        ipn.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Ipn_AfterReservationExpired_MarksPaidAfterExpiryWithAutoRefund()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);
        var created = await (await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = CustomerId,
            reservationId = reservation.ReservationId
        })).Content.ReadFromJsonAsync<JsonElement>();
        var orderId = created.GetProperty("orderId").GetGuid();
        var total = created.GetProperty("totalAmount").GetDecimal();

        // The reservation expires before the IPN arrives.
        factory.Ticketing.AddReservation(reservation with { IsActive = false });

        var ipn = await client.PostAsync($"/api/payments/vnpay/ipn?{VnPayTestHelper.BuildIpnQuery(orderId, total)}", null);

        ipn.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.PaidAfterExpiry);

        var refund = await db.RefundRequests.SingleAsync(r => r.OrderId == orderId);
        refund.Status.Should().Be(RefundStatus.Pending);

        var outbox = await db.OutboxMessages.ToListAsync();
        outbox.Should().ContainSingle(m => m.Type.Contains(nameof(OrderPaidAfterExpiryIntegrationEvent)));
        outbox.Should().NotContain(m => m.Type.Contains(nameof(OrderPaidIntegrationEvent))); // tickets must never be issued
    }

    [Fact]
    public async Task RefundFlow_RequestThenApprove_RefundsOrderAndWritesOutbox()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);
        var created = await (await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = CustomerId,
            reservationId = reservation.ReservationId
        })).Content.ReadFromJsonAsync<JsonElement>();
        var orderId = created.GetProperty("orderId").GetGuid();
        await client.PostAsync($"/api/payments/vnpay/ipn?{VnPayTestHelper.BuildIpnQuery(orderId, created.GetProperty("totalAmount").GetDecimal())}", null);

        var requested = await client.PostAsJsonAsync($"/api/orders/{orderId}/refund", new { reason = "changed plans" });
        requested.StatusCode.Should().Be(HttpStatusCode.OK);
        var refundId = (await requested.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refundRequestId").GetGuid();

        var processed = await client.PostAsJsonAsync($"/api/orders/refunds/{refundId}/process", new { approve = true });

        processed.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Refunded);
        (await db.RefundRequests.FirstAsync(r => r.Id == refundId)).Status.Should().Be(RefundStatus.Processed);
        var outbox = await db.OutboxMessages.ToListAsync();
        outbox.Should().Contain(m => m.Type.Contains(nameof(RefundProcessedIntegrationEvent)));
    }

    [Fact]
    public async Task RefundFlow_RejectedRequest_ReturnsOrderToPaid()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);
        var created = await (await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = CustomerId,
            reservationId = reservation.ReservationId
        })).Content.ReadFromJsonAsync<JsonElement>();
        var orderId = created.GetProperty("orderId").GetGuid();
        await client.PostAsync($"/api/payments/vnpay/ipn?{VnPayTestHelper.BuildIpnQuery(orderId, created.GetProperty("totalAmount").GetDecimal())}", null);

        var requested = await client.PostAsJsonAsync($"/api/orders/{orderId}/refund", new { reason = "changed plans" });
        var refundId = (await requested.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refundRequestId").GetGuid();

        var processed = await client.PostAsJsonAsync($"/api/orders/refunds/{refundId}/process", new { approve = false });

        processed.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Paid);
        (await db.RefundRequests.FirstAsync(r => r.Id == refundId)).Status.Should().Be(RefundStatus.Rejected);
    }

    [Fact]
    public async Task GetCustomerOrders_ReturnsCustomerOrdersWithItems()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation1 = ValidReservation();
        var reservation2 = ValidReservation();
        factory.Ticketing.AddReservation(reservation1);
        factory.Ticketing.AddReservation(reservation2);
        await client.PostAsJsonAsync("/api/orders", new { customerId = CustomerId, reservationId = reservation1.ReservationId });
        await client.PostAsJsonAsync("/api/orders", new { customerId = CustomerId, reservationId = reservation2.ReservationId });

        var list = await client.GetAsync($"/api/orders/customer/{CustomerId}");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        var orders = await list.Content.ReadFromJsonAsync<JsonElement>();
        orders.GetArrayLength().Should().Be(2);

        var detail = await client.GetAsync($"/api/orders/{orders[0].GetProperty("orderId").GetGuid()}");
        detail.StatusCode.Should().Be(HttpStatusCode.OK);
        (await detail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task GetEventAttendees_ReturnsOnlyTicketsOfPaidOrders()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var paidReservation = ValidReservation();
        var pendingReservation = ValidReservation();
        factory.Ticketing.AddReservation(paidReservation);
        factory.Ticketing.AddReservation(pendingReservation);

        var paid = await (await client.PostAsJsonAsync("/api/orders", new { customerId = CustomerId, reservationId = paidReservation.ReservationId })).Content.ReadFromJsonAsync<JsonElement>();
        await client.PostAsJsonAsync("/api/orders", new { customerId = CustomerId, reservationId = pendingReservation.ReservationId });
        await client.PostAsync($"/api/payments/vnpay/ipn?{VnPayTestHelper.BuildIpnQuery(paid.GetProperty("orderId").GetGuid(), paid.GetProperty("totalAmount").GetDecimal())}", null);

        var eventId = paidReservation.EventId;
        factory.Ticketing.SetTickets(eventId, new[]
        {
            new TicketDto(Guid.NewGuid(), paid.GetProperty("orderId").GetGuid(), CustomerId, Guid.NewGuid(), "VIP", "Issued", Now.AddMinutes(6)),
            new TicketDto(Guid.NewGuid(), Guid.NewGuid(), CustomerId, Guid.NewGuid(), "VIP", "Issued", Now) // unknown order → filtered out
        });

        var response = await client.GetAsync($"/api/orders/events/{eventId}/attendees");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var attendees = await response.Content.ReadFromJsonAsync<JsonElement>();
        attendees.GetArrayLength().Should().Be(1);
        attendees[0].GetProperty("orderId").GetGuid().Should().Be(paid.GetProperty("orderId").GetGuid());
    }
}
