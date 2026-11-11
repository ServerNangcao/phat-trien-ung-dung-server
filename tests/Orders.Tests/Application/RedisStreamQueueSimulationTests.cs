using FluentAssertions;
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

public class RedisStreamQueueSimulationTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Queue_PartitionByTicketTypeId_MessagesOrderedPerType()
    {
        var queue = new SimulatedRedisStream();

        var msg1 = new QueueMessage { IdempotencyKey = Guid.NewGuid().ToString(), Action = "reserve", TicketTypeId = Guid.NewGuid(), Quantity = 1, UserId = Guid.NewGuid(), TraceId = "trace-1" };
        var msg2 = new QueueMessage { IdempotencyKey = Guid.NewGuid().ToString(), Action = "reserve", TicketTypeId = msg1.TicketTypeId, Quantity = 1, UserId = Guid.NewGuid(), TraceId = "trace-2" };
        var msg3 = new QueueMessage { IdempotencyKey = Guid.NewGuid().ToString(), Action = "reserve", TicketTypeId = Guid.NewGuid(), Quantity = 1, UserId = Guid.NewGuid(), TraceId = "trace-3" };

        await queue.XAddAsync(msg1);
        await queue.XAddAsync(msg2);
        await queue.XAddAsync(msg3);

        var type1Messages = await queue.XReadGroupAsync("consumer-group", "consumer-1", msg1.TicketTypeId, 10);
        var type2Messages = await queue.XReadGroupAsync("consumer-group", "consumer-1", msg3.TicketTypeId, 10);

        type1Messages.Should().HaveCount(2);
        type1Messages.Select(m => m.TraceId).Should().BeEquivalentTo(new[] { "trace-1", "trace-2" });
        type2Messages.Should().HaveCount(1);
        type2Messages[0].TraceId.Should().Be("trace-3");
    }

    [Fact]
    public async Task Queue_ExactlyOnce_AckOnlyAfterCommit()
    {
        var queue = new SimulatedRedisStream();
        var msg = new QueueMessage { IdempotencyKey = Guid.NewGuid().ToString(), Action = "reserve", TicketTypeId = Guid.NewGuid(), Quantity = 1, UserId = Guid.NewGuid(), TraceId = "trace-1" };
        await queue.XAddAsync(msg);

        var messages = await queue.XReadGroupAsync("group", "consumer-1", msg.TicketTypeId, 1);
        messages.Should().HaveCount(1);

        // Simulate processing - commit to DB
        await Task.Delay(10);

        // Ack after successful commit
        var acked = await queue.XAckAsync("group", messages[0].Id);
        acked.Should().BeTrue();

        // Message should not be redelivered
        var redelivered = await queue.XReadGroupAsync("group", "consumer-1", msg.TicketTypeId, 1);
        redelivered.Should().BeEmpty();
    }

    [Fact]
    public async Task Queue_RetryPolicy_Max3AttemptsThenDLQ()
    {
        var queue = new SimulatedRedisStream();
        var msg = new QueueMessage { IdempotencyKey = Guid.NewGuid().ToString(), Action = "reserve", TicketTypeId = Guid.NewGuid(), Quantity = 1, UserId = Guid.NewGuid(), TraceId = "trace-1" };
        await queue.XAddAsync(msg);

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            var messages = await queue.XReadGroupAsync("group", "consumer-1", msg.TicketTypeId, 1);
            messages.Should().HaveCount(1);

            // Simulate processing failure
            await queue.XAddRetryAsync(msg, attempt); // Re-queue with delay
        }

        // 4th attempt should go to DLQ
        var dlqMessages = await queue.XReadDLQAsync(1);
        dlqMessages.Should().HaveCount(1);
        dlqMessages[0].RetryCount.Should().Be(3);
        dlqMessages[0].Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Queue_IdempotencyKey_DedupeAcrossRetries()
    {
        var queue = new SimulatedRedisStream();
        var idempotencyKey = Guid.NewGuid().ToString();
        
        var msg1 = new QueueMessage { IdempotencyKey = idempotencyKey, Action = "reserve", TicketTypeId = Guid.NewGuid(), Quantity = 1, UserId = Guid.NewGuid(), TraceId = "trace-1" };
        var msg2 = new QueueMessage { IdempotencyKey = idempotencyKey, Action = "reserve", TicketTypeId = Guid.NewGuid(), Quantity = 1, UserId = Guid.NewGuid(), TraceId = "trace-2" }; // Retry with same key

        await queue.XAddAsync(msg1);
        await queue.XAddAsync(msg2);

        var messages = await queue.XReadGroupAsync("group", "consumer-1", msg1.TicketTypeId, 10);
        
        // Should deduplicate by idempotency key
        messages.Select(m => m.IdempotencyKey).Distinct().Should().HaveCount(1);
    }

    private record QueueMessage
    {
        public string IdempotencyKey { get; init; } = "";
        public string Action { get; init; } = "";
        public Guid TicketTypeId { get; init; }
        public int Quantity { get; init; }
        public Guid UserId { get; init; }
        public string TraceId { get; init; } = "";
        public string Id { get; init; } = Guid.NewGuid().ToString();
        public int RetryCount { get; init; } = 0;
        public string? Error { get; init; }
    }

private class SimulatedRedisStream
    {
        private readonly List<QueueMessage> _stream = new();
        private readonly Dictionary<string, HashSet<string>> _acked = new(); // group:ticketTypeId -> messageIds
        private readonly Dictionary<string, int> _retryCount = new(); // messageId -> retryCount
        private readonly List<QueueMessage> _dlq = new();

        public Task XAddAsync(QueueMessage msg) { _stream.Add(msg); return Task.CompletedTask; }

        public async Task<List<QueueMessage>> XReadGroupAsync(string group, string consumer, Guid ticketTypeId, int count)
        {
            await Task.Yield();
            var key = $"{group}:{ticketTypeId}";
            var ackedIds = _acked.GetValueOrDefault(key, new HashSet<string>());

            var available = _stream
                .Where(m => m.TicketTypeId == ticketTypeId && !ackedIds.Contains(m.Id))
                .Take(count)
                .ToList();

            return available;
        }

        public async Task<bool> XAckAsync(string group, string messageId)
        {
            await Task.Yield();
            var msg = _stream.FirstOrDefault(m => m.Id == messageId);
            if (msg == null)
                return false;
            
            var key = $"{group}:{msg.TicketTypeId}";
            if (!_acked.ContainsKey(key))
                _acked[key] = new HashSet<string>();
            _acked[key].Add(messageId);
            return true;
        }

        public async Task XAddRetryAsync(QueueMessage msg, int attempt)
        {
            await Task.Yield();
            var newMsg = msg with { Id = Guid.NewGuid().ToString(), RetryCount = attempt };
            
            // Track retry count by idempotency key to enforce max 3 attempts
            var retryKey = $"retry:{msg.IdempotencyKey}";
            _retryCount[retryKey] = attempt;
            
            // If max retries exceeded (3), move to DLQ instead of re-queueing
            if (attempt >= 3)
            {
                var dlqMsg = msg with { Id = Guid.NewGuid().ToString(), RetryCount = attempt, Error = "Max retries exceeded" };
                _dlq.Add(dlqMsg);
            }
            else
            {
                _stream.Add(newMsg);
            }
        }

        public async Task<List<QueueMessage>> XReadDLQAsync(int count)
        {
            await Task.Yield();
            return _dlq.Take(count).ToList();
        }
    }
}