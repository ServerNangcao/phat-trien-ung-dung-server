using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ticketing.SharedKernel;

namespace Ticketing.Infrastructure.Core.Outbox;

/// <summary>
/// Cấu hình cho Outbox Worker.
/// </summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Khoảng thời gian giữa mỗi lần quét (giây).</summary>
    public int IntervalSeconds { get; set; } = 5;

    /// <summary>Số event tối đa lấy mỗi lần quét.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Số ngày giữ event đã xử lý trước khi dọn.</summary>
    public int CleanupAfterDays { get; set; } = 7;
}

/// <summary>
/// Interface cho handler nhận IntegrationEvent từ Outbox.
/// Mỗi module đăng ký handler cho từng loại event quan tâm.
/// </summary>
public interface IIntegrationEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Background service quét OutboxMessages từ tất cả module DbContext đã đăng ký,
/// deserialize event và giao cho handler.
/// 
/// Sử dụng SELECT ... FOR UPDATE SKIP LOCKED (Postgres) để nhiều instance
/// không xử lý trùng event.
/// </summary>
public sealed class OutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxWorker> _logger;

    /// <summary>
    /// Registry: EventType name → (Event CLR type, Handler CLR type).
    /// Đăng ký bởi mỗi module khi AddXxxModule.
    /// </summary>
    private static readonly Dictionary<string, List<EventHandlerRegistration>> EventHandlerRegistry = new();

    public OutboxWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxOptions> options,
        ILogger<OutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Đăng ký mapping từ event type name sang CLR types.
    /// Gọi bởi extension method RegisterIntegrationEventHandler trong ModuleExtensions.
    /// </summary>
    public static void RegisterHandler(string eventTypeName, Type eventType, Type handlerType)
    {
        if (!EventHandlerRegistry.TryGetValue(eventTypeName, out var handlers))
        {
            handlers = [];
            EventHandlerRegistry[eventTypeName] = handlers;
        }

        handlers.Add(new EventHandlerRegistration(eventType, handlerType));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxWorker started. Interval: {Interval}s, BatchSize: {BatchSize}",
            _options.IntervalSeconds, _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OutboxWorker encountered an error");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.IntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("OutboxWorker stopped");
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContextTypes = scope.ServiceProvider.GetServices<BaseDbContextMarker>();

        foreach (var marker in dbContextTypes)
        {
            if (scope.ServiceProvider.GetService(marker.DbContextType) is not BaseDbContext dbContext)
            {
                continue;
            }

            await ProcessOutboxForContextAsync(dbContext, scope.ServiceProvider, cancellationToken);
        }
    }

    private async Task ProcessOutboxForContextAsync(
        BaseDbContext dbContext,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        // Dùng raw SQL với FOR UPDATE SKIP LOCKED cho Postgres
        // để nhiều instance không xử lý trùng event
        var sql = $"""
            SELECT * FROM "{dbContext.Schema}"."OutboxMessages"
            WHERE "ProcessedAt" IS NULL
            ORDER BY "CreatedAt"
            LIMIT {_options.BatchSize}
            FOR UPDATE SKIP LOCKED
            """;

        var messages = await dbContext.OutboxMessages
            .FromSqlRaw(sql)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return;
        }

        _logger.LogDebug("Processing {Count} outbox messages from schema '{Schema}'",
            messages.Count, dbContext.Schema);

        foreach (var message in messages)
        {
            try
            {
                await DispatchEventAsync(message, serviceProvider, cancellationToken);
                message.ProcessedAt = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {MessageId} (EventType: {EventType})",
                    message.Id, message.EventType);
                message.Error = ex.Message[..Math.Min(ex.Message.Length, 2000)];
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task DispatchEventAsync(
        OutboxMessage message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        if (!EventHandlerRegistry.TryGetValue(message.EventType, out var registrations))
        {
            _logger.LogWarning("No handler registered for event type '{EventType}'", message.EventType);
            return;
        }

        foreach (var reg in registrations)
        {
            var @event = JsonSerializer.Deserialize(message.Payload, reg.EventType)
                ?? throw new InvalidOperationException(
                    $"Failed to deserialize event of type '{message.EventType}'");

            var handler = serviceProvider.GetRequiredService(reg.HandlerType);

            var handleMethod = reg.HandlerType
                .GetMethod(nameof(IIntegrationEventHandler<IntegrationEvent>.HandleAsync))
                ?? throw new InvalidOperationException(
                    $"Handler '{reg.HandlerType.Name}' does not have HandleAsync method");

            await (Task)handleMethod.Invoke(handler, [@event, cancellationToken])!;
        }
    }
}

/// <summary>
/// Registration record cho event handler.
/// </summary>
public sealed record EventHandlerRegistration(Type EventType, Type HandlerType);

/// <summary>
/// Marker service để OutboxWorker tìm được tất cả BaseDbContext đã đăng ký.
/// Mỗi module đăng ký một instance khi AddXxxModule.
/// </summary>
public sealed class BaseDbContextMarker
{
    public Type DbContextType { get; }

    public BaseDbContextMarker(Type dbContextType)
    {
        DbContextType = dbContextType;
    }
}
