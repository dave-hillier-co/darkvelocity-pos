using System.Text.Json;
using DarkVelocity.Shared.Contracts.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.Shared.Infrastructure.Events;

/// <summary>
/// Background service that processes outbox events and publishes them to the event bus.
/// </summary>
public class OutboxProcessor<TDbContext> : BackgroundService where TDbContext : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor<TDbContext>> _logger;
    private readonly TimeSpan _pollingInterval;
    private readonly int _batchSize;

    public OutboxProcessor(
        IServiceProvider serviceProvider,
        ILogger<OutboxProcessor<TDbContext>> logger,
        TimeSpan? pollingInterval = null,
        int batchSize = 100)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _pollingInterval = pollingInterval ?? TimeSpan.FromSeconds(5);
        _batchSize = batchSize;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started for {DbContext}", typeof(TDbContext).Name);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox events");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var outboxEvents = await dbContext.Set<OutboxEvent>()
            .Where(e => e.ProcessedAt == null && e.RetryCount < 5)
            .OrderBy(e => e.OccurredAt)
            .Take(_batchSize)
            .ToListAsync(cancellationToken);

        if (outboxEvents.Count == 0)
            return;

        _logger.LogDebug("Processing {Count} outbox events", outboxEvents.Count);

        foreach (var outboxEvent in outboxEvents)
        {
            try
            {
                var eventType = Type.GetType(outboxEvent.EventClrType);
                if (eventType == null)
                {
                    _logger.LogWarning(
                        "Could not find event type {EventClrType} for outbox event {EventId}",
                        outboxEvent.EventClrType,
                        outboxEvent.Id);
                    outboxEvent.LastError = $"Event type not found: {outboxEvent.EventClrType}";
                    outboxEvent.RetryCount++;
                    continue;
                }

                var @event = JsonSerializer.Deserialize(outboxEvent.Payload, eventType) as IIntegrationEvent;
                if (@event == null)
                {
                    _logger.LogWarning(
                        "Could not deserialize outbox event {EventId}",
                        outboxEvent.Id);
                    outboxEvent.LastError = "Failed to deserialize event";
                    outboxEvent.RetryCount++;
                    continue;
                }

                await eventBus.PublishAsync((dynamic)@event, cancellationToken);

                outboxEvent.ProcessedAt = DateTime.UtcNow;
                _logger.LogDebug(
                    "Successfully processed outbox event {EventId} of type {EventType}",
                    outboxEvent.Id,
                    outboxEvent.EventType);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing outbox event {EventId}",
                    outboxEvent.Id);
                outboxEvent.LastError = ex.Message;
                outboxEvent.RetryCount++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
