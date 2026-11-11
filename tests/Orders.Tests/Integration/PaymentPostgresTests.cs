using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ticketing.Modules.Orders.Application.Ports;
using Ticketing.Modules.Orders.Contracts.IntegrationEvents;
using Ticketing.Modules.Orders.Domain.Entities;
using Ticketing.Modules.Orders.Infrastructure;
using Ticketing.Tests.Orders.Fixtures;
using Ticketing.Tests.Orders.TestSupport;

namespace Ticketing.Tests.Orders.Integration;

/// <summary>
/// Payment and refund flows against real PostgreSQL (Testcontainers). These run in CI
/// with Docker; on machines without Docker they skip via DockerRequiredFact.
/// </summary>
public class PaymentPostgresTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public PaymentPostgresTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private static ReservationDto ValidReservation() => new(
        Guid.NewGuid(),
        CustomerId,
        Guid.NewGuid(),
        new[] { new ReservationItemDto(Guid.NewGuid(), "VIP", 2, 125_000m) },
        DateTime.UtcNow.AddMinutes(10), // the app under test uses the real clock
        true);

    [DockerRequiredFact]
    public async Task CreateAndPay_PersistsOrderAndOutboxInPostgres()
    {
        await _fixture.ResetAsync();
        using var factory = new PostgresOrdersFactory(_fixture.ConnectionString);
        await factory.EnsureDatabaseCreatedAsync();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);

        var created = await (await client.PostAsJsonAsync("/api/orders", new
        {
            customerId = CustomerId,
            reservationId = reservation.ReservationId
        })).Content.ReadFromJsonAsync<JsonElement>();
        var orderId = created.GetProperty("orderId").GetGuid();

        var ipn = await client.PostAsync($"/api/payments/vnpay/ipn?{VnPayTestHelper.BuildIpnQuery(orderId, created.GetProperty("totalAmount").GetDecimal())}", null);

        ipn.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Paid);
        (await db.OutboxMessages.CountAsync(m => m.Type.Contains(nameof(OrderPaidIntegrationEvent)))).Should().Be(1);
    }

    [DockerRequiredFact]
    public async Task SameReservation_SecondCreate_IsRejectedByUniqueIndexOrHandler()
    {
        await _fixture.ResetAsync();
        using var factory = new PostgresOrdersFactory(_fixture.ConnectionString);
        await factory.EnsureDatabaseCreatedAsync();
        var client = factory.CreateClient();
        var reservation = ValidReservation();
        factory.Ticketing.AddReservation(reservation);

        var first = await client.PostAsJsonAsync("/api/orders", new { customerId = CustomerId, reservationId = reservation.ReservationId });
        var second = await client.PostAsJsonAsync("/api/orders", new { customerId = CustomerId, reservationId = reservation.ReservationId });

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetGuid();
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetGuid();
        firstId.Should().Be(secondId); // idempotent: the handler short-circuits before the unique index is exercised
        using var scope = factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Orders.CountAsync())
            .Should().Be(1);
    }

    [DockerRequiredFact]
    public async Task ExpiredReservation_Payment_CreatesRefundRequestRowInPostgres()
    {
        await _fixture.ResetAsync();
        using var factory = new PostgresOrdersFactory(_fixture.ConnectionString);
        await factory.EnsureDatabaseCreatedAsync();
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

        var ipn = await client.PostAsync($"/api/payments/vnpay/ipn?{VnPayTestHelper.BuildIpnQuery(orderId, created.GetProperty("totalAmount").GetDecimal())}", null);

        ipn.StatusCode.Should().Be(HttpStatusCode.OK);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        (await db.Orders.FirstAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.PaidAfterExpiry);
        (await db.RefundRequests.SingleAsync(r => r.OrderId == orderId)).Status.Should().Be(RefundStatus.Pending);
    }
}
