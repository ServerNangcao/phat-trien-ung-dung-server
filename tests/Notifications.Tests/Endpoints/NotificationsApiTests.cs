using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Ticketing.Modules.Notifications.Contracts.IntegrationEvents;
using Ticketing.Tests.Notifications.Fixtures;

namespace Ticketing.Tests.Notifications.Endpoints;

public class NotificationsApiTests
{
    private static TicketIssuedIntegrationEvent ValidEvent(Guid? eventId = null) => new()
    {
        EventId = eventId ?? Guid.NewGuid(),
        TicketId = Guid.NewGuid(),
        OrderId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        CustomerEmail = "fan@example.com",
        EventName = "Rock Fest 2026",
        EventDate = "2026-12-31",
        TicketType = "VIP",
        QrCodeUrl = ""
    };

    [Fact]
    public async Task Inbound_FirstDelivery_AcceptsAndProcesses()
    {
        using var factory = new NotificationsApiFactory();
        var client = factory.CreateClient();
        var @event = ValidEvent();

        var response = await client.PostAsJsonAsync("/api/integration-events/ticket-issued", @event);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        // The send runs in a background scope; wait briefly for it to land.
        for (var i = 0; i < 50 && factory.Email.SendCallCount == 0; i++)
            await Task.Delay(100);
        factory.Email.SendCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Inbound_RedeliveredEvent_NeverSendsTwice()
    {
        using var factory = new NotificationsApiFactory();
        var client = factory.CreateClient();
        var @event = ValidEvent();

        var first = await client.PostAsJsonAsync("/api/integration-events/ticket-issued", @event);
        for (var i = 0; i < 50 && factory.Email.SendCallCount == 0; i++)
            await Task.Delay(100);
        var second = await client.PostAsJsonAsync("/api/integration-events/ticket-issued", @event);

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("duplicated").GetBoolean().Should().BeTrue();
        factory.Email.SendCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Inbound_ThenHistory_ShowsExactlyOneRecord()
    {
        using var factory = new NotificationsApiFactory();
        var client = factory.CreateClient();
        var @event = ValidEvent();

        await client.PostAsJsonAsync("/api/integration-events/ticket-issued", @event);
        for (var i = 0; i < 50 && factory.Email.SendCallCount == 0; i++)
            await Task.Delay(100);

        var history = await client.GetAsync($"/api/notifications/order/{@event.OrderId}");

        history.StatusCode.Should().Be(HttpStatusCode.OK);
        var records = await history.Content.ReadFromJsonAsync<JsonElement>();
        records.GetArrayLength().Should().Be(1);
        records[0].GetProperty("status").GetString().Should().Be("Sent");
        records[0].GetProperty("body").GetString().Should().Contain("data:image/png;base64,"); // QR fallback
    }

    [Fact]
    public async Task Send_WhenSmtpFails_RecordsFailureWithRetrySchedule()
    {
        using var factory = new NotificationsApiFactory();
        var client = factory.CreateClient();
        factory.Email.ThrowOnSend = new Exception("SMTP unavailable");
        var orderId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/notifications/send", new
        {
            orderId,
            to = "fan@example.com",
            subject = "Hello",
            body = "Body"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Failed");

        var history = await client.GetAsync($"/api/notifications/order/{orderId}");
        var records = await history.Content.ReadFromJsonAsync<JsonElement>();
        records[0].GetProperty("status").GetString().Should().Be("Failed");
        records[0].GetProperty("retryCount").GetInt32().Should().Be(1);
        records[0].GetProperty("nextRetryAt").GetString().Should().NotBeNull();
        records[0].GetProperty("lastError").GetString().Should().Contain("SMTP unavailable");
    }

    [Fact]
    public async Task Send_WithInvalidEmail_Returns400WithoutRecord()
    {
        using var factory = new NotificationsApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/notifications/send", new
        {
            orderId = Guid.NewGuid(),
            to = "not-an-email",
            subject = "Hello",
            body = "Body"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        factory.Email.SendCallCount.Should().Be(0);
    }

    [Fact]
    public async Task LatePaymentEvent_SendsApologyEmailWithRefundConfirmation()
    {
        using var factory = new NotificationsApiFactory();
        var client = factory.CreateClient();
        var @event = new OrderPaidAfterExpiryIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            TotalAmount = 250_000m,
            PaidAt = DateTime.UtcNow,
            Reason = "Payment received after the reservation expired (ADR-0003).",
            CustomerEmail = "fan@example.com"
        };

        var response = await client.PostAsJsonAsync("/api/integration-events/order-paid-after-expiry", @event);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        for (var i = 0; i < 50 && factory.Email.SendCallCount == 0; i++)
            await Task.Delay(100);
        factory.Email.SendCallCount.Should().Be(1);
        factory.Email.Sent[^1].Subject.Should().Contain(@event.OrderId.ToString()).And.Contain("expired");

        var history = await client.GetAsync($"/api/notifications/order/{@event.OrderId}");
        var records = await history.Content.ReadFromJsonAsync<JsonElement>();
        records.GetArrayLength().Should().Be(1);
        records[0].GetProperty("status").GetString().Should().Be("Sent");
        records[0].GetProperty("body").GetString().Should().Contain("250,000 VND");
    }

    [Fact]
    public async Task LatePaymentEvent_Redelivered_NeverSendsTwice()
    {
        using var factory = new NotificationsApiFactory();
        var client = factory.CreateClient();
        var @event = new OrderPaidAfterExpiryIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            TotalAmount = 250_000m,
            PaidAt = DateTime.UtcNow,
            Reason = "ADR-0003",
            CustomerEmail = "fan@example.com"
        };

        var first = await client.PostAsJsonAsync("/api/integration-events/order-paid-after-expiry", @event);
        for (var i = 0; i < 50 && factory.Email.SendCallCount == 0; i++)
            await Task.Delay(100);
        var second = await client.PostAsJsonAsync("/api/integration-events/order-paid-after-expiry", @event);

        first.StatusCode.Should().Be(HttpStatusCode.Accepted);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("duplicated").GetBoolean().Should().BeTrue();
        factory.Email.SendCallCount.Should().Be(1);
    }

    [Fact]
    public async Task LatePaymentEvent_WithoutCustomerEmail_ConsumedWithoutEmail()
    {
        using var factory = new NotificationsApiFactory();
        var client = factory.CreateClient();
        var @event = new OrderPaidAfterExpiryIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            TotalAmount = 250_000m,
            PaidAt = DateTime.UtcNow,
            Reason = "ADR-0003",
            CustomerEmail = ""
        };

        var response = await client.PostAsJsonAsync("/api/integration-events/order-paid-after-expiry", @event);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var history = await client.GetAsync($"/api/notifications/order/{@event.OrderId}");
        var records = await history.Content.ReadFromJsonAsync<JsonElement>();
        records.GetArrayLength().Should().Be(0);
        factory.Email.SendCallCount.Should().Be(0);
    }

    [Fact]
    public async Task RefundProcessedEvent_SendsConfirmationEmail()
    {
        using var factory = new NotificationsApiFactory();
        var client = factory.CreateClient();
        var @event = new RefundProcessedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            TotalAmount = 180_000m,
            RefundedAt = DateTime.UtcNow,
            CustomerEmail = "fan@example.com"
        };

        var response = await client.PostAsJsonAsync("/api/integration-events/refund-processed", @event);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        for (var i = 0; i < 50 && factory.Email.SendCallCount == 0; i++)
            await Task.Delay(100);
        factory.Email.SendCallCount.Should().Be(1);
        factory.Email.Sent[^1].Subject.Should().Contain("refund").And.Contain("processed");

        var history = await client.GetAsync($"/api/notifications/order/{@event.OrderId}");
        var records = await history.Content.ReadFromJsonAsync<JsonElement>();
        records.GetArrayLength().Should().Be(1);
        records[0].GetProperty("status").GetString().Should().Be("Sent");
        records[0].GetProperty("body").GetString().Should().Contain("180,000 VND");
    }
}
