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
}
