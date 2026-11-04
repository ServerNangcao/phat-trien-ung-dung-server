using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ticketing.Infrastructure.Core.EventBus;

public sealed class EventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventBus> _logger;
    private readonly HashSet<Type> _registeredTypes = new();
    private readonly List<(Type EventType, Delegate Handler)> _handlers = new();

    public EventBus(IServiceProvider serviceProvider, ILogger<EventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void Register<TEvent, THandler>()
        where TEvent : class
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        _registeredTypes.Add(typeof(TEvent));
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class
    {
        var eventType = typeof(TEvent);
        var handlerType = typeof(IIntegrationEventHandler<TEvent>);

        var handlers = _serviceProvider.GetServices(handlerType);
        foreach (var handler in handlers)
        {
            if (handler is null) continue;
            try
            {
                await ((IIntegrationEventHandler<TEvent>)handler).HandleAsync(@event, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle event {EventType}", eventType.Name);
            }
        }
    }

    public async Task PublishAsync(Type eventType, object @event, CancellationToken cancellationToken = default)
    {
        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
        var handlers = _serviceProvider.GetServices(handlerType);
        foreach (var handler in handlers)
        {
            if (handler is null) continue;
            try
            {
                var method = handlerType.GetMethod("HandleAsync")!;
                var task = (Task)method.Invoke(handler, new object[] { @event, cancellationToken })!;
                await task;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle event {EventType}", eventType.Name);
            }
        }
    }
}