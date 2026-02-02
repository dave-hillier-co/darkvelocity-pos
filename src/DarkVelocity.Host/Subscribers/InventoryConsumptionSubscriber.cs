using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Subscribes to order completion events and routes inventory consumption to InventoryGrain.
/// This decouples the Order domain from the Inventory domain via pub/sub.
///
/// Listens to: order-events stream (OrderCompletedEvent, OrderVoidedEvent)
/// Routes to: InventoryGrain (keyed by org:site:ingredientId)
/// </summary>
[ImplicitStreamSubscription(StreamConstants.OrderStreamNamespace)]
public class InventoryConsumptionSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<InventoryConsumptionSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;
    private IAsyncStream<IStreamEvent>? _alertStream;

    public InventoryConsumptionSubscriberGrain(ILogger<InventoryConsumptionSubscriberGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);

        // Subscribe to order-events stream
        var orderStreamId = StreamId.Create(StreamConstants.OrderStreamNamespace, this.GetPrimaryKeyString());
        var orderStream = streamProvider.GetStream<IStreamEvent>(orderStreamId);
        _subscription = await orderStream.SubscribeAsync(this);

        // Get alert stream for publishing stock shortage alerts
        var alertStreamId = StreamId.Create(StreamConstants.AlertStreamNamespace, this.GetPrimaryKeyString());
        _alertStream = streamProvider.GetStream<IStreamEvent>(alertStreamId);

        _logger.LogInformation(
            "InventoryConsumptionSubscriber activated for organization {OrgId}",
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
                case OrderCompletedEvent completedEvent:
                    await HandleOrderCompletedAsync(completedEvent);
                    break;

                case OrderVoidedEvent voidedEvent:
                    await HandleOrderVoidedAsync(voidedEvent);
                    break;

                default:
                    // Ignore other order events
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
        _logger.LogInformation("Inventory consumption event stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in inventory consumption event stream");
        return Task.CompletedTask;
    }

    private async Task HandleOrderCompletedAsync(OrderCompletedEvent evt)
    {
        _logger.LogInformation(
            "Processing inventory consumption for order {OrderNumber} with {LineCount} line items",
            evt.OrderNumber,
            evt.Lines.Count);

        var processedCount = 0;

        foreach (var line in evt.Lines)
        {
            // Skip lines without a recipe - they don't consume inventory
            if (line.RecipeId == null)
            {
                _logger.LogDebug(
                    "Skipping line {ProductName} - no recipe assigned",
                    line.ProductName);
                continue;
            }

            // Route directly to the appropriate InventoryGrain based on site and ingredient ID
            // For now, use ProductId as ingredient ID (simplified)
            // In a full implementation, we would look up the recipe and route
            // consumption to each ingredient
            var ingredientKey = GrainKeys.Inventory(evt.OrganizationId, evt.SiteId, line.ProductId);
            var inventoryGrain = GrainFactory.GetGrain<IInventoryGrain>(ingredientKey);

            try
            {
                var result = await inventoryGrain.ConsumeForOrderAsync(
                    evt.OrderId,
                    line.Quantity,
                    evt.ServerId);

                _logger.LogInformation(
                    "Consumed {Quantity} of {ProductName} for order {OrderNumber}. " +
                    "COGS: {COGS:C}, Remaining: {Remaining}",
                    line.Quantity,
                    line.ProductName,
                    evt.OrderNumber,
                    result.CostOfGoodsConsumed,
                    result.QuantityRemaining);

                processedCount++;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient"))
            {
                _logger.LogWarning(
                    "Insufficient stock for {ProductName} on order {OrderNumber}: {Message}",
                    line.ProductName,
                    evt.OrderNumber,
                    ex.Message);

                // Publish an alert for stock shortage
                await PublishStockAlertAsync(evt, line);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist") || ex.Message.Contains("not initialized"))
            {
                // Inventory not set up for this item - skip
                _logger.LogDebug(
                    "No inventory tracking for {ProductName} ({ProductId})",
                    line.ProductName,
                    line.ProductId);
            }
        }

        _logger.LogInformation(
            "Processed {ProcessedCount} inventory consumptions for order {OrderNumber}",
            processedCount,
            evt.OrderNumber);
    }

    private async Task HandleOrderVoidedAsync(OrderVoidedEvent evt)
    {
        _logger.LogInformation(
            "Inventory consumption reversal for order {OrderId} - this requires movement lookup",
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

    private async Task PublishStockAlertAsync(OrderCompletedEvent evt, OrderLineSnapshot line)
    {
        if (_alertStream != null)
        {
            await _alertStream.OnNextAsync(new AlertTriggeredEvent(
                AlertId: Guid.NewGuid(),
                SiteId: evt.SiteId,
                AlertType: "inventory.stock_shortage",
                Severity: "Warning",
                Title: $"Stock shortage: {line.ProductName}",
                Message: $"Insufficient stock for {line.ProductName} (ordered: {line.Quantity}) on order {evt.OrderNumber}",
                Metadata: new Dictionary<string, string>
                {
                    ["ingredientId"] = line.ProductId.ToString(),
                    ["productName"] = line.ProductName,
                    ["orderId"] = evt.OrderId.ToString(),
                    ["orderNumber"] = evt.OrderNumber,
                    ["quantityOrdered"] = line.Quantity.ToString()
                })
            {
                OrganizationId = evt.OrganizationId
            });
        }
    }
}
