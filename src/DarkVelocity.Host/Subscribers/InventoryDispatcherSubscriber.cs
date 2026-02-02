using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Subscribes to inventory-events stream and routes consumption derived events to InventoryGrain.
/// This dispatcher handles the routing complexity of compound grain keys (org:site:ingredientId).
///
/// Listens to: inventory-events stream (InventoryConsumptionDerivedEvent, InventoryConsumptionReversalDerivedEvent)
/// Routes to: InventoryGrain (keyed by org:site:ingredientId)
/// </summary>
[ImplicitStreamSubscription(StreamConstants.InventoryStreamNamespace)]
public class InventoryDispatcherSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<InventoryDispatcherSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;
    private IAsyncStream<IStreamEvent>? _alertStream;

    public InventoryDispatcherSubscriberGrain(ILogger<InventoryDispatcherSubscriberGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.InventoryStreamNamespace, this.GetPrimaryKeyString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        _subscription = await stream.SubscribeAsync(this);

        // Get alert stream for publishing stock shortage alerts
        var alertStreamId = StreamId.Create(StreamConstants.AlertStreamNamespace, this.GetPrimaryKeyString());
        _alertStream = streamProvider.GetStream<IStreamEvent>(alertStreamId);

        _logger.LogInformation(
            "InventoryDispatcher activated for organization {OrgId}",
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
                case InventoryConsumptionDerivedEvent consumptionDerived:
                    await HandleInventoryConsumptionDerivedAsync(consumptionDerived);
                    break;

                case InventoryConsumptionReversalDerivedEvent reversalDerived:
                    await HandleInventoryConsumptionReversalDerivedAsync(reversalDerived);
                    break;

                // Ignore events that are outputs from InventoryGrain
                // (StockConsumedEvent, StockReceivedEvent, etc.)
                // These are published by InventoryGrain for downstream consumers
                default:
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling event {EventType}: {EventId}", item.GetType().Name, item.EventId);
        }
    }

    public Task OnCompletedAsync()
    {
        _logger.LogInformation("Inventory dispatcher stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in inventory dispatcher stream");
        return Task.CompletedTask;
    }

    private async Task HandleInventoryConsumptionDerivedAsync(InventoryConsumptionDerivedEvent evt)
    {
        _logger.LogInformation(
            "Routing inventory consumption to InventoryGrain: Ingredient {IngredientId}, Order {OrderNumber}, Quantity {Quantity}",
            evt.IngredientId,
            evt.OrderNumber,
            evt.Quantity);

        // Route to the appropriate InventoryGrain based on site and ingredient ID
        var ingredientKey = GrainKeys.Inventory(evt.OrganizationId, evt.SiteId, evt.IngredientId);
        var inventoryGrain = GrainFactory.GetGrain<IInventoryGrain>(ingredientKey);

        try
        {
            var result = await inventoryGrain.ConsumeForOrderAsync(
                evt.OrderId,
                evt.Quantity,
                evt.PerformedBy);

            _logger.LogInformation(
                "Consumed {Quantity} of {ProductName} for order {OrderNumber}. " +
                "COGS: {COGS:C}, Remaining: {Remaining}",
                evt.Quantity,
                evt.ProductName,
                evt.OrderNumber,
                result.CostOfGoodsConsumed,
                result.QuantityRemaining);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient"))
        {
            _logger.LogWarning(
                "Insufficient stock for {ProductName} on order {OrderNumber}: {Message}",
                evt.ProductName,
                evt.OrderNumber,
                ex.Message);

            // Publish an alert for stock shortage
            await PublishStockAlertAsync(evt);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist") || ex.Message.Contains("not initialized"))
        {
            // Inventory not set up for this item - skip
            _logger.LogDebug(
                "No inventory tracking for {ProductName} ({IngredientId})",
                evt.ProductName,
                evt.IngredientId);
        }
    }

    private async Task HandleInventoryConsumptionReversalDerivedAsync(InventoryConsumptionReversalDerivedEvent evt)
    {
        _logger.LogInformation(
            "Inventory consumption reversal derived for order {OrderId} - this requires movement lookup",
            evt.OrderId);

        // In a full implementation:
        // 1. Look up the original consumption movements for this order
        //    (would require an order-to-movements index or querying all inventory grains)
        // 2. Call ReverseConsumptionAsync for each movement
        // For now, log and acknowledge

        _logger.LogWarning(
            "Inventory reversal for order {OrderId} not fully implemented - requires movement tracking",
            evt.OrderId);

        await Task.CompletedTask;
    }

    private async Task PublishStockAlertAsync(InventoryConsumptionDerivedEvent evt)
    {
        if (_alertStream != null)
        {
            await _alertStream.OnNextAsync(new AlertTriggeredEvent(
                AlertId: Guid.NewGuid(),
                SiteId: evt.SiteId,
                AlertType: "inventory.stock_shortage",
                Severity: "Warning",
                Title: $"Stock shortage: {evt.ProductName}",
                Message: $"Insufficient stock for {evt.ProductName} (ordered: {evt.Quantity}) on order {evt.OrderNumber}",
                Metadata: new Dictionary<string, string>
                {
                    ["ingredientId"] = evt.IngredientId.ToString(),
                    ["productName"] = evt.ProductName,
                    ["orderId"] = evt.OrderId.ToString(),
                    ["orderNumber"] = evt.OrderNumber,
                    ["quantityOrdered"] = evt.Quantity.ToString()
                })
            {
                OrganizationId = evt.OrganizationId
            });
        }
    }
}
