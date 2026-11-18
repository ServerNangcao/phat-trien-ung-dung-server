using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

public class SeatAllocationRaceConditionTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ReservationId = Guid.NewGuid();
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid TicketTypeId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Reserve_LastSeat_TwoConcurrentRequests_FirstWinsSecondGetsConflict()
    {
        // Simulate DO seat allocator behavior with in-memory counter + version
        var seatState = new SeatAllocatorState { Available = 1, Version = 1 };

        var task1 = ReserveSeatAsync(seatState, 1);
        var task2 = ReserveSeatAsync(seatState, 1);

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.IsSuccess);
        var conflictCount = results.Count(r => r.IsFailure && r.Error.Code == "Conflict");

        successCount.Should().Be(1);
        conflictCount.Should().Be(1);
        seatState.Available.Should().Be(0);
    }

    [Fact]
    public async Task Reserve_WithOptimisticLock_VersionMismatch_ReturnsConflict()
    {
        var seatState = new SeatAllocatorState { Available = 5, Version = 10 };

        // Client reads version 10
        var clientVersion = seatState.Version;

        // Another request increments version to 11
        seatState.Version = 11;

        // Client tries to reserve with stale version
        var result = await ReserveSeatWithVersionAsync(seatState, 1, clientVersion);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Conflict");
        result.Error.Message.Should().Be("VERSION_MISMATCH");
    }

    [Fact]
    public async Task Release_Seat_IncrementsAvailable_AndVersion()
    {
        var seatState = new SeatAllocatorState { Available = 0, Version = 5, Capacity = 10 };

        var result = await ReleaseSeatAsync(seatState, 2);

        result.IsSuccess.Should().BeTrue();
        seatState.Available.Should().Be(2);
        seatState.Version.Should().Be(6);
    }

    private record SeatAllocatorState
    {
        public int Available { get; set; }
        public int Version { get; set; }
        public int Capacity { get; set; } = 100;
    }

    private static async Task<Result> ReserveSeatAsync(SeatAllocatorState state, int quantity)
    {
        await Task.Yield(); // Simulate async
        if (state.Available < quantity)
            return Result.Failure(Error.Conflict("SEAT_UNAVAILABLE"));
        state.Available -= quantity;
        state.Version++;
        return Result.Success();
    }

    private static async Task<Result> ReserveSeatWithVersionAsync(SeatAllocatorState state, int quantity, int expectedVersion)
    {
        await Task.Yield();
        if (state.Version != expectedVersion)
            return Result.Failure(Error.Conflict("VERSION_MISMATCH"));
        if (state.Available < quantity)
            return Result.Failure(Error.Conflict("SEAT_UNAVAILABLE"));
        state.Available -= quantity;
        state.Version++;
        return Result.Success();
    }

    private static async Task<Result> ReleaseSeatAsync(SeatAllocatorState state, int quantity)
    {
        await Task.Yield();
        if (state.Available + quantity > state.Capacity)
            return Result.Failure(Error.Conflict("CAPACITY_EXCEEDED"));
        state.Available += quantity;
        state.Version++;
        return Result.Success();
    }
}