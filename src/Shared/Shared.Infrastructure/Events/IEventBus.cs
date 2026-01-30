using DarkVelocity.Shared.Contracts.Events;

namespace DarkVelocity.Shared.Infrastructure.Events;

/// <summary>
/// Interface for publishing integration events to the event bus.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all subscribed handlers.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;

    /// <summary>
    /// Publishes multiple events in order.
    /// </summary>
    Task PublishAllAsync(IEnumerable<IIntegrationEvent> events, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for handling integration events.
/// </summary>
/// <typeparam name="TEvent">The type of event to handle.</typeparam>
public interface IEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    /// <summary>
    /// Handles the event asynchronously.
    /// </summary>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

/// <summary>
/// Marker interface for event handlers to enable discovery.
/// </summary>
public interface IEventHandler { }
