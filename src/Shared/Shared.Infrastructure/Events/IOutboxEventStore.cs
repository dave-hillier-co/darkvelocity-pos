using System.Text.Json;
using DarkVelocity.Shared.Contracts.Events;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Shared.Infrastructure.Events;

/// <summary>
/// Interface for storing events in the outbox for reliable delivery.
/// </summary>
public interface IOutboxEventStore
{
    /// <summary>
    /// Adds an event to the outbox for later processing.
    /// </summary>
    Task AddAsync<TEvent>(TEvent @event, Guid? correlationId = null, Guid? aggregateId = null, string? aggregateType = null)
        where TEvent : IIntegrationEvent;

    /// <summary>
    /// Adds multiple events to the outbox.
    /// </summary>
    Task AddRangeAsync(IEnumerable<IIntegrationEvent> events, Guid? correlationId = null);
}

/// <summary>
/// EF Core implementation of the outbox event store.
/// </summary>
/// <typeparam name="TDbContext">The DbContext type that contains the OutboxEvent DbSet.</typeparam>
public class OutboxEventStore<TDbContext> : IOutboxEventStore where TDbContext : DbContext
{
    private readonly TDbContext _dbContext;

    public OutboxEventStore(TDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync<TEvent>(TEvent @event, Guid? correlationId = null, Guid? aggregateId = null, string? aggregateType = null)
        where TEvent : IIntegrationEvent
    {
        var outboxEvent = new OutboxEvent
        {
            Id = @event.EventId,
            EventType = @event.EventType,
            Payload = JsonSerializer.Serialize(@event, @event.GetType()),
            EventClrType = @event.GetType().AssemblyQualifiedName!,
            OccurredAt = @event.OccurredAt,
            CorrelationId = correlationId,
            AggregateId = aggregateId,
            AggregateType = aggregateType
        };

        _dbContext.Set<OutboxEvent>().Add(outboxEvent);
        await Task.CompletedTask; // Will be saved with the main transaction
    }

    public async Task AddRangeAsync(IEnumerable<IIntegrationEvent> events, Guid? correlationId = null)
    {
        foreach (var @event in events)
        {
            await AddAsync((dynamic)@event, correlationId);
        }
    }
}
