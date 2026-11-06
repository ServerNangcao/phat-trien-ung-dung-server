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
        isActive,
        "fan@example.com");

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
    public async Task Ipn_WithTamperedAmount_RespondsRspCode04AndKeepsOrderPending()
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

        // Permanent condition: VNPay's documented RspCode 04 (invalid amount) with HTTP 200
        // stops the retry loop; the 400 previously caused endless retries.
        ipn.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ipn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rspCode").GetString().Should().Be("04");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Ipn_WithInvalidSignature_RespondsRspCode97()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var orderId = Guid.NewGuid();
        var query = VnPayTestHelper.BuildIpnQuery(orderId, 100_000m) + "&vnp_Amount=999"; // breaks the signature

        var ipn = await client.PostAsync($"/api/payments/vnpay/ipn?{query}", null);

        ipn.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ipn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rspCode").GetString().Should().Be("97");
    }

    [Fact]
    public async Task Ipn_WithFailedTransactionStatus_DoesNotMarkPaid()
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

        // ResponseCode 00 with a non-success TransactionStatus (e.g. suspected fraud) is
        // NOT a completed payment: no state change, success ack so VNPay stops retrying.
        var query = VnPayTestHelper.BuildIpnQuery(orderId, total, transactionStatus: "05");
        var ipn = await client.PostAsync($"/api/payments/vnpay/ipn?{query}", null);

        ipn.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Ipn_WhenTicketingUnreachable_RespondsRspCode99ForRetry()
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

        // The Ticketing module is down (transient outage): the handler must not guess
        // between Paid and PaidAfterExpiry. RspCode 99 tells VNPay to retry the IPN later.
        factory.Ticketing.SimulateOutage = true;

        var ipn = await client.PostAsync($"/api/payments/vnpay/ipn?{VnPayTestHelper.BuildIpnQuery(orderId, total)}", null);

        ipn.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ipn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("rspCode").GetString().Should().Be("99");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Pending);
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

        // The refund-notification payload carries a dedupe EventId and the customer email.
        var latePayment = outbox.Single(m => m.Type.Contains(nameof(OrderPaidAfterExpiryIntegrationEvent)));
        var latePaymentJson = JsonDocument.Parse(latePayment.Content).RootElement;
        latePaymentJson.GetProperty("eventId").GetGuid().Should().NotBe(Guid.Empty);
        latePaymentJson.GetProperty("customerEmail").GetString().Should().Be("fan@example.com");
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

        var processed = await client.PostAsJsonAsync($"/api/orders/refunds/{refundId}/process", new { approve = true, adminNote = "Approved after verification" });

        processed.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Refunded);
        var refund = await db.RefundRequests.FirstAsync(r => r.Id == refundId);
        refund.Status.Should().Be(RefundStatus.Processed);
        refund.ProcessedNote.Should().Be("Approved after verification"); // admin audit note persisted
        var outbox = await db.OutboxMessages.ToListAsync();
        outbox.Should().Contain(m => m.Type.Contains(nameof(RefundProcessedIntegrationEvent)));
        var refundEvent = outbox.Single(m => m.Type.Contains(nameof(RefundProcessedIntegrationEvent)));
        var refundEventJson = JsonDocument.Parse(refundEvent.Content).RootElement;
        refundEventJson.GetProperty("eventId").GetGuid().Should().NotBe(Guid.Empty);
        refundEventJson.GetProperty("customerEmail").GetString().Should().Be("fan@example.com");
    }

    [Fact]
    public async Task RefundFlow_PaidAfterExpiry_AutoRefundApprovedByAdmin_CompletesRefund()
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
        factory.Ticketing.AddReservation(reservation with { IsActive = false });
        await client.PostAsync($"/api/payments/vnpay/ipn?{VnPayTestHelper.BuildIpnQuery(orderId, created.GetProperty("totalAmount").GetDecimal())}", null);

        Guid refundId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            refundId = (await db.RefundRequests.AsNoTracking().SingleAsync(r => r.OrderId == orderId)).Id;
        }

        var processed = await client.PostAsJsonAsync($"/api/orders/refunds/{refundId}/process", new { approve = true, adminNote = "Late payment auto-refund" });

        processed.StatusCode.Should().Be(HttpStatusCode.OK);
        using (var freshScope = factory.Services.CreateScope())
        {
            var freshDb = freshScope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            (await freshDb.Orders.AsNoTracking().FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Refunded);
            (await freshDb.RefundRequests.AsNoTracking().FirstAsync(r => r.Id == refundId)).Status.Should().Be(RefundStatus.Processed);
        }
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

    [Fact]
    public async Task GetEventSalesSummary_CountsPaidOrdersOnlyWithPerTypeBreakdown()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var paidReservation = ValidReservation(); // 2 × VIP @125,000 = 250,000
        var pendingReservation = ValidReservation();
        var refundedReservation = ValidReservation();
        var otherEventReservation = ValidReservation();
        foreach (var r in new[] { paidReservation, pendingReservation, refundedReservation, otherEventReservation })
            factory.Ticketing.AddReservation(r);

        var paid = await CreateAndPayAsync(client, paidReservation);
        var pending = await CreateOrderAsync(client, pendingReservation); // stays Pending → excluded
        var refunded = await CreateAndPayAsync(client, refundedReservation); // refunded money is not revenue
        await CreateAndPayAsync(client, otherEventReservation); // different event → excluded

        await client.PostAsJsonAsync($"/api/orders/{refunded.GetProperty("orderId").GetGuid()}/refund", new { reason = "changed mind" });
        var refunds = await client.GetAsync("/api/orders/refunds?status=Pending");
        var refundList = await refunds.Content.ReadFromJsonAsync<JsonElement>();
        var refundId = refundList[0].GetProperty("id").GetGuid();
        await client.PostAsJsonAsync($"/api/orders/refunds/{refundId}/process", new { approve = true, adminNote = "ok" });

        var eventId = paidReservation.EventId;
        var response = await client.GetAsync($"/api/orders/events/{eventId}/sales-summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<JsonElement>();
        summary.GetProperty("eventId").GetGuid().Should().Be(eventId);
        summary.GetProperty("totalRevenue").GetDecimal().Should().Be(250_000m);
        summary.GetProperty("paidOrderCount").GetInt32().Should().Be(1);
        var byType = summary.GetProperty("byTicketType");
        byType.GetArrayLength().Should().Be(1);
        byType[0].GetProperty("ticketTypeId").GetGuid().Should().Be(paidReservation.Items[0].TicketTypeId);
        byType[0].GetProperty("ticketTypeName").GetString().Should().Be("VIP");
        byType[0].GetProperty("ticketsSold").GetInt32().Should().Be(2);
        byType[0].GetProperty("revenue").GetDecimal().Should().Be(250_000m);
        pending.GetProperty("orderId").GetGuid().Should().NotBe(Guid.Empty); // keep pending order referenced
    }

    private static async Task<JsonElement> CreateOrderAsync(HttpClient client, ReservationDto reservation)
    {
        var response = await client.PostAsJsonAsync("/api/orders", new { customerId = CustomerId, reservationId = reservation.ReservationId });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<JsonElement> CreateAndPayAsync(HttpClient client, ReservationDto reservation)
    {
        var order = await CreateOrderAsync(client, reservation);
        var ipn = await client.PostAsync(
            $"/api/payments/vnpay/ipn?{VnPayTestHelper.BuildIpnQuery(order.GetProperty("orderId").GetGuid(), order.GetProperty("totalAmount").GetDecimal())}", null);
        ipn.StatusCode.Should().Be(HttpStatusCode.OK);
        return order;
    }

    [Fact]
    public async Task ExportEventAttendees_ReturnsCsvOfPaidOrdersOnly()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var paidReservation = ValidReservation();
        var pendingReservation = ValidReservation();
        factory.Ticketing.AddReservation(paidReservation);
        factory.Ticketing.AddReservation(pendingReservation);

        var paid = await CreateAndPayAsync(client, paidReservation);
        await CreateOrderAsync(client, pendingReservation);

        var eventId = paidReservation.EventId;
        var ticketId = Guid.NewGuid();
        factory.Ticketing.SetTickets(eventId, new[]
        {
            new TicketDto(ticketId, paid.GetProperty("orderId").GetGuid(), CustomerId, Guid.NewGuid(), "VIP", "Issued", Now.AddMinutes(6)),
            new TicketDto(Guid.NewGuid(), Guid.NewGuid(), CustomerId, Guid.NewGuid(), "VIP", "Issued", Now) // unknown order → filtered out
        });

        var response = await client.GetAsync($"/api/orders/events/{eventId}/attendees/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileName.Should().NotBeNullOrWhiteSpace();
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().StartWith("TicketId,OrderId,CustomerId,TicketTypeId,TicketType,Status,IssuedAtUtc");
        csv.Should().Contain(ticketId.ToString());
        csv.Should().Contain("VIP");
        csv.Should().NotContain(paid.GetProperty("orderId").GetGuid() + ",,"); // no empty-customer artifact rows
    }

    [Fact]
    public async Task ExportEventOrders_ReturnsCsvWithEveryOrderState()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var paidReservation = ValidReservation();
        var pendingReservation = ValidReservation() with { EventId = paidReservation.EventId };
        factory.Ticketing.AddReservation(paidReservation);
        factory.Ticketing.AddReservation(pendingReservation);

        var paid = await CreateAndPayAsync(client, paidReservation);
        var pending = await CreateOrderAsync(client, pendingReservation);
        var eventId = paidReservation.EventId;

        var response = await client.GetAsync($"/api/orders/events/{eventId}/orders/export");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var csv = await response.Content.ReadAsStringAsync();
        csv.Should().StartWith("OrderId,CustomerId,ReservationId,TotalAmount,Status,CreatedAt,Items");
        csv.Should().Contain(paid.GetProperty("orderId").GetGuid().ToString()).And.Contain("Paid");
        csv.Should().Contain(pending.GetProperty("orderId").GetGuid().ToString()).And.Contain("Pending");
        csv.Should().Contain("VIP x2");
    }

    [Fact]
    public async Task DiscountFlow_CreateCodeThenOrder_ReducesTotalAndPaymentUrlAmount()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation(); // 2 × 125,000 = 250,000
        factory.Ticketing.AddReservation(reservation);

        var created = await client.PostAsJsonAsync("/api/orders/discounts",
            new { code = "event10", type = "Percentage", value = 10m, maxRedemptions = (int?)null, expiresAtUtc = (DateTime?)null });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        var code = await created.Content.ReadFromJsonAsync<JsonElement>();
        code.GetProperty("code").GetString().Should().Be("EVENT10"); // stored normalized

        var duplicated = await client.PostAsJsonAsync("/api/orders/discounts",
            new { code = "event10", type = "Percentage", value = 10m, maxRedemptions = (int?)null, expiresAtUtc = (DateTime?)null });
        duplicated.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var order = await CreateOrderWithDiscountAsync(client, reservation, "event10");

        order.GetProperty("discountAmount").GetDecimal().Should().Be(25_000m);
        order.GetProperty("totalAmount").GetDecimal().Should().Be(225_000m);
        order.GetProperty("paymentUrl").GetString()!.Should().Contain("vnp_Amount=22500000"); // gateway sees the payable total

        var unknownReservation = ValidReservation();
        factory.Ticketing.AddReservation(unknownReservation);
        var unknown = await client.PostAsJsonAsync("/api/orders", new { customerId = CustomerId, reservationId = unknownReservation.ReservationId, discountCode = "GHOST" });
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task MoMoIpn_WithValidSignature_MarksPaidAndIsIdempotent()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);
        var order = await CreateOrderAsync(client, reservation);
        var orderId = order.GetProperty("orderId").GetGuid();

        var ipn = MoMoIpnPayload(orderId, order.GetProperty("totalAmount").GetDecimal());
        var first = await client.PostAsJsonAsync("/api/payments/momo/ipn", ipn);
        var second = await client.PostAsJsonAsync("/api/payments/momo/ipn", ipn);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        first.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resultCode").GetInt32().Should().Be(0);
        second.StatusCode.Should().Be(HttpStatusCode.OK); // replay: still acknowledged, no state change

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public async Task MoMoIpn_WithTamperedSignature_Returns400AndKeepsOrderPending()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);
        var order = await CreateOrderAsync(client, reservation);
        var orderId = order.GetProperty("orderId").GetGuid();

        var ipn = MoMoIpnPayload(orderId, order.GetProperty("totalAmount").GetDecimal());
        ipn["amount"] = "1"; // tamper after signing

        var response = await client.PostAsJsonAsync("/api/payments/momo/ipn", ipn);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task MoMoIpn_WithFailedResultCode_DoesNotChangeOrderState()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);
        var order = await CreateOrderAsync(client, reservation);
        var orderId = order.GetProperty("orderId").GetGuid();

        var ipn = MoMoIpnPayload(orderId, order.GetProperty("totalAmount").GetDecimal(), resultCode: "7002");

        var response = await client.PostAsJsonAsync("/api/payments/momo/ipn", ipn);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Pending);
    }

    [Fact]
    public async Task Cancel_PendingOrder_MarksCancelledAndReleasesReservation()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);
        var order = await CreateOrderAsync(client, reservation);
        var orderId = order.GetProperty("orderId").GetGuid();

        var cancelled = await client.PostAsJsonAsync($"/api/orders/{orderId}/cancel", new { });

        cancelled.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cancelled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("Cancelled");
        factory.Ticketing.CancelledReservations.Should().Contain(reservation.ReservationId);

        // A late IPN for the cancelled order replays idempotently without resurrecting it.
        var ipn = await client.PostAsync($"/api/payments/vnpay/ipn?{VnPayTestHelper.BuildIpnQuery(orderId, order.GetProperty("totalAmount").GetDecimal())}", null);
        ipn.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_PaidOrder_Returns400()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);
        var order = await CreateAndPayAsync(client, reservation);
        var orderId = order.GetProperty("orderId").GetGuid();

        var response = await client.PostAsJsonAsync($"/api/orders/{orderId}/cancel", new { });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancel_WhenReservationReleaseFails_StillCancelsOrder()
    {
        using var factory = new OrdersApiFactory();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);
        factory.Ticketing.SimulateCancelFailure = true;
        var order = await CreateOrderAsync(client, reservation);
        var orderId = order.GetProperty("orderId").GetGuid();

        var response = await client.PostAsJsonAsync($"/api/orders/{orderId}/cancel", new { });

        response.StatusCode.Should().Be(HttpStatusCode.OK); // cancellation must not fail on release outage
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Cancelled);
    }

    private static Dictionary<string, string?> MoMoIpnPayload(Guid orderId, decimal amount, string resultCode = "0")
    {
        var ipn = new Dictionary<string, string?>
        {
            ["partnerCode"] = MoMoTestHelper.PartnerCode,
            ["orderId"] = orderId.ToString(),
            ["requestId"] = $"req-{Guid.NewGuid():N}",
            ["amount"] = amount.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
            ["transId"] = "998877",
            ["payType"] = "web",
            ["orderInfo"] = $"Ticketing order {orderId}",
            ["message"] = "Success",
            ["responseTime"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
            ["resultCode"] = resultCode,
            ["extraData"] = "",
            ["signature"] = ""
        };
        ipn["signature"] = MoMoTestHelper.SignIpn(ipn);
        return ipn;
    }

    private static async Task<JsonElement> CreateOrderWithDiscountAsync(HttpClient client, ReservationDto reservation, string code)
    {
        var response = await client.PostAsJsonAsync("/api/orders", new { customerId = CustomerId, reservationId = reservation.ReservationId, discountCode = code });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
