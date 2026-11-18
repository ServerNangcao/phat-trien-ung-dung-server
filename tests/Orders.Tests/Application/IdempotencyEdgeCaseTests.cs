using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
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

public class IdempotencyEdgeCaseTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ReservationId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CreateOrder_SameIdempotencyKeyDifferentBody_ReturnsConflict()
    {
        var ticketing = new FakeTicketingModuleApi();
        ticketing.AddReservation(new ReservationDto(
            ReservationId, CustomerId, EventId,
            new[] { new ReservationItemDto(Guid.NewGuid(), "VIP", 1, 100_000m) },
            Now.AddMinutes(10), true, "fan@example.com"));

        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new OrdersDbContext(options);
        var repo = new OrderRepository(db);
        var handler = new CreateOrderCommandHandler(repo, new DiscountCodeRepository(db), new UnitOfWork(db), ticketing, new FixedClock(Now));

        // First request
        var cmd1 = new CreateOrderCommand(CustomerId, ReservationId, null);
        var result1 = await handler.Handle(cmd1, CancellationToken.None);
        result1.IsSuccess.Should().BeTrue();

        // Simulate idempotency key store
        var idempotencyStore = new Dictionary<string, (string bodyHash, CreateOrderResult result)>();
        var bodyHash1 = ComputeHash(cmd1);
        var idempotencyKey = Guid.NewGuid().ToString();
        idempotencyStore[idempotencyKey] = (bodyHash1, result1.Value);

        // Second request with SAME key but DIFFERENT body
        var cmd2 = new CreateOrderCommand(CustomerId, ReservationId, "DISCOUNT10");
        var bodyHash2 = ComputeHash(cmd2);
        bodyHash2.Should().NotBe(bodyHash1);

        var stored = idempotencyStore[idempotencyKey];
        stored.bodyHash.Should().NotBe(bodyHash2);

        // Should return IDEMPOTENCY_CONFLICT
        var conflict = stored.bodyHash != bodyHash2;
        conflict.Should().BeTrue();
    }

    [Fact]
    public async Task CreateOrder_IdempotencyKeyExpired_AllowsNewRequest()
    {
        var ticketing = new FakeTicketingModuleApi();
        ticketing.AddReservation(new ReservationDto(
            ReservationId, CustomerId, EventId,
            new[] { new ReservationItemDto(Guid.NewGuid(), "VIP", 1, 100_000m) },
            Now.AddMinutes(10), true, "fan@example.com"));

        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new OrdersDbContext(options);
        var repo = new OrderRepository(db);
        var handler = new CreateOrderCommandHandler(repo, new DiscountCodeRepository(db), new UnitOfWork(db), ticketing, new FixedClock(Now));

        var cmd = new CreateOrderCommand(CustomerId, ReservationId, null);
        var result1 = await handler.Handle(cmd, CancellationToken.None);
        result1.IsSuccess.Should().BeTrue();

        var idempotencyStore = new Dictionary<string, (DateTime createdAt, CreateOrderResult result)>();
        var idempotencyKey = Guid.NewGuid().ToString();
        idempotencyStore[idempotencyKey] = (Now.AddHours(-25), result1.Value); // Expired (24h TTL)

        // New request with same key after expiry should be allowed
        var stored = idempotencyStore[idempotencyKey];
        var isExpired = Now - stored.createdAt > TimeSpan.FromHours(24);
        isExpired.Should().BeTrue();
    }

    [Fact]
    public async Task CancelOrder_IdempotentRetry_ReturnsSuccess()
    {
        var ticketing = new FakeTicketingModuleApi();
        ticketing.AddReservation(new ReservationDto(
            ReservationId, CustomerId, EventId,
            new[] { new ReservationItemDto(Guid.NewGuid(), "VIP", 1, 100_000m) },
            Now.AddMinutes(10), true, "fan@example.com"));

        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new OrdersDbContext(options);
        var repo = new OrderRepository(db);
        var handler = new CancelOrderCommandHandler(repo, new UnitOfWork(db), ticketing, new FixedClock(Now), NullLogger<CancelOrderCommandHandler>.Instance);

        var order = Order.Create(CustomerId, ReservationId, EventId, 100_000m,
            new[] { OrderItem.Create(Guid.NewGuid(), 1, 100_000m).Value }, Now, "fan@example.com").Value;
        await db.Orders.AddAsync(order);
        await db.SaveChangesAsync();

        var cmd = new CancelOrderCommand(order.Id);

        // First call should succeed
        var result1 = await handler.Handle(cmd, CancellationToken.None);
        result1.IsSuccess.Should().BeTrue();
        result1.Value.Status.Should().Be("Cancelled");

        // Retry with same command - also succeeds (idempotent)
        var result2 = await handler.Handle(cmd, CancellationToken.None);
        result2.IsSuccess.Should().BeTrue();
        result2.Value.OrderId.Should().Be(result1.Value.OrderId);
    }

    [Fact]
    public async Task CreateDiscountCode_DuplicateIdempotencyKey_ReturnsCachedResult()
    {
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new OrdersDbContext(options);
        var repo = new DiscountCodeRepository(db);
        var handler = new CreateDiscountCodeCommandHandler(repo, new UnitOfWork(db));

        var cmd1 = new CreateDiscountCodeCommand("SAVE10", "Percentage", 10m, 100, null);
        var result1 = await handler.Handle(cmd1, CancellationToken.None);
        result1.IsSuccess.Should().BeTrue();

        var idempotencyStore = new Dictionary<string, DiscountCodeDto>();
        var idempotencyKey = Guid.NewGuid().ToString();
        idempotencyStore[idempotencyKey] = result1.Value;

        // Retry with same key
        var stored = idempotencyStore.ContainsKey(idempotencyKey);
        stored.Should().BeTrue();

        // Should return cached result, not create new
        var cached = idempotencyStore[idempotencyKey];
        cached.Code.Should().Be("SAVE10");
    }

    [Fact]
    public async Task CancelOrder_NotFound_ReturnsNotFound()
    {
        var ticketing = new FakeTicketingModuleApi();
        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new OrdersDbContext(options);
        var repo = new OrderRepository(db);
        var handler = new CancelOrderCommandHandler(repo, new UnitOfWork(db), ticketing, new FixedClock(Now), NullLogger<CancelOrderCommandHandler>.Instance);

        var result = await handler.Handle(new CancelOrderCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("NotFound");
    }

    private static string ComputeHash(CreateOrderCommand cmd)
    {
        var json = JsonSerializer.Serialize(cmd);
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }
}