using DarkVelocity.Orleans.Grains.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Orleans.Grains.Subscribers;

/// <summary>
/// Implicit stream subscriber that handles alert events for notifications.
///
/// This subscriber enables:
/// - Email notifications for critical alerts
/// - SMS/push notifications for urgent alerts
/// - Slack/Teams integration for team notifications
/// - Dashboard real-time updates
/// - External monitoring system integration
/// </summary>
[ImplicitStreamSubscription(StreamConstants.AlertStreamNamespace)]
public class AlertNotificationSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<AlertNotificationSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    public AlertNotificationSubscriberGrain(ILogger<AlertNotificationSubscriberGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.AlertStreamNamespace, this.GetPrimaryKeyString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        _subscription = await stream.SubscribeAsync(this);

        _logger.LogInformation(
            "AlertNotificationSubscriber activated for organization {OrgId}",
            this.GetPrimaryKeyString());

        await base.OnActivateAsync(cancellationToken);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        if (_subscription != null)
        {
            await _subscription.UnsubscribeAsync();
        }

        await base.OnDeactivateAsync(reason, cancellationToken);
    }

    public async Task OnNextAsync(IStreamEvent item, StreamSequenceToken? token = null)
    {
        try
        {
            switch (item)
            {
                case AlertTriggeredEvent alertEvent:
                    await HandleAlertTriggeredAsync(alertEvent);
                    break;

                case ReorderPointBreachedEvent reorderEvent:
                    await HandleReorderPointBreachedAsync(reorderEvent);
                    break;

                case StockDepletedEvent depletedEvent:
                    await HandleStockDepletedAsync(depletedEvent);
                    break;

                default:
                    _logger.LogDebug("Ignoring unhandled event type: {EventType}", item.GetType().Name);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling alert event {EventType}: {EventId}", item.GetType().Name, item.EventId);
        }
    }

    public Task OnCompletedAsync()
    {
        _logger.LogInformation("Alert event stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in alert event stream");
        return Task.CompletedTask;
    }

    private Task HandleAlertTriggeredAsync(AlertTriggeredEvent evt)
    {
        _logger.LogInformation(
            "[ALERT] {Severity} - {AlertType}: {Title}",
            evt.Severity,
            evt.AlertType,
            evt.Title);

        // Route to appropriate notification channels based on severity
        switch (evt.Severity.ToUpperInvariant())
        {
            case "CRITICAL":
                // In production, this would:
                // 1. Send immediate SMS/push to on-call managers
                // 2. Create incident in monitoring system (PagerDuty, OpsGenie)
                // 3. Post to #critical-alerts Slack channel
                _logger.LogCritical(
                    "CRITICAL ALERT at site {SiteId}: {Title} - {Message}",
                    evt.SiteId,
                    evt.Title,
                    evt.Message);
                break;

            case "HIGH":
                // In production, this would:
                // 1. Send push notification to site managers
                // 2. Email relevant team leads
                // 3. Post to #alerts Slack channel
                _logger.LogWarning(
                    "HIGH PRIORITY ALERT at site {SiteId}: {Title}",
                    evt.SiteId,
                    evt.Title);
                break;

            case "MEDIUM":
            case "LOW":
                // In production, this would:
                // 1. Queue for daily digest email
                // 2. Display on dashboard
                _logger.LogInformation(
                    "{Severity} alert at site {SiteId}: {Title}",
                    evt.Severity,
                    evt.SiteId,
                    evt.Title);
                break;
        }

        // Log metadata for debugging
        if (evt.Metadata.Count > 0)
        {
            _logger.LogDebug(
                "Alert metadata: {Metadata}",
                string.Join(", ", evt.Metadata.Select(kv => $"{kv.Key}={kv.Value}")));
        }

        return Task.CompletedTask;
    }

    private Task HandleReorderPointBreachedAsync(ReorderPointBreachedEvent evt)
    {
        _logger.LogWarning(
            "[REORDER NEEDED] {IngredientName} at site {SiteId}: On hand {OnHand}, Reorder point {ReorderPoint}, Suggested order qty {OrderQty}",
            evt.IngredientName,
            evt.SiteId,
            evt.QuantityOnHand,
            evt.ReorderPoint,
            evt.QuantityToOrder);

        // In production, this would:
        // 1. Add to procurement queue
        // 2. Notify inventory manager
        // 3. Auto-generate purchase order if configured

        return Task.CompletedTask;
    }

    private Task HandleStockDepletedAsync(StockDepletedEvent evt)
    {
        _logger.LogError(
            "[STOCK DEPLETED] {IngredientName} at site {SiteId} depleted at {DepletedAt}",
            evt.IngredientName,
            evt.SiteId,
            evt.DepletedAt);

        // In production, this would:
        // 1. Send URGENT notification to site manager
        // 2. Update menu availability in POS
        // 3. Notify kitchen display
        // 4. Create emergency procurement request

        return Task.CompletedTask;
    }
}
