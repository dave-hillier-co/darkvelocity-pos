using System.Collections.Concurrent;
using DarkVelocity.Shared.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.Shared.Infrastructure.Events;

/// <summary>
/// In-memory implementation of the event bus for development and testing.
/// In production, this would be replaced with Kafka or RabbitMQ implementation.
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;
    private readonly ConcurrentQueue<IIntegrationEvent> _eventLog = new();

    public InMemoryEventBus(IServiceProvider serviceProvider, ILogger<InMemoryEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        _logger.LogInformation(
            "Publishing event {EventType} with ID {EventId}",
            @event.EventType,
            @event.EventId);

        _eventLog.Enqueue(@event);

        using var scope = _serviceProvider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<TEvent>>();

        foreach (var handler in handlers)
        {
            try
            {
                _logger.LogDebug(
                    "Dispatching event {EventType} to handler {HandlerType}",
                    @event.EventType,
                    handler.GetType().Name);

                await handler.HandleAsync(@event, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error handling event {EventType} in handler {HandlerType}",
                    @event.EventType,
                    handler.GetType().Name);
            }
        }
    }

    public async Task PublishAllAsync(IEnumerable<IIntegrationEvent> events, CancellationToken cancellationToken = default)
    {
        foreach (var @event in events)
        {
            await PublishAsync((dynamic)@event, cancellationToken);
        }
    }

    /// <summary>
    /// Gets all events that have been published (for testing/debugging).
    /// </summary>
    public IReadOnlyCollection<IIntegrationEvent> GetEventLog() => _eventLog.ToArray();

    /// <summary>
    /// Clears the event log (for testing).
    /// </summary>
    public void ClearEventLog()
    {
        while (_eventLog.TryDequeue(out _)) { }
    }
}
