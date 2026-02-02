using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Subscribes to order completion events and triggers inventory consumption.
/// This decouples the Sales domain from the Inventory domain via pub/sub.
///
/// Reacts to:
/// - OrderCompletedEvent: Consumes stock for each line item based on recipes
/// - OrderVoidedEvent: Reverses inventory consumption if applicable
/// </summary>
[ImplicitStreamSubscription(StreamConstants.OrderStreamNamespace)]
public class InventoryConsumptionSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<InventoryConsumptionSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    public InventoryConsumptionSubscriberGrain(ILogger<InventoryConsumptionSubscriberGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.OrderStreamNamespace, this.GetPrimaryKeyString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        _subscription = await stream.SubscribeAsync(this);

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

        var consumedCount = 0;

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

            // In a full implementation, we would:
            // 1. Look up the recipe to get ingredient breakdown
            // 2. For each ingredient, consume the appropriate quantity
            // For now, we'll consume directly using the product ID as ingredient ID
            // (this is a simplification for demonstration)

            var ingredientKey = GrainKeys.Inventory(evt.OrganizationId, evt.SiteId, line.ProductId);
            var inventoryGrain = GrainFactory.GetGrain<IInventoryGrain>(ingredientKey);

            try
            {
                var result = await inventoryGrain.ConsumeForOrderAsync(
                    evt.OrderId,
                    line.Quantity,
                    evt.ServerId);

                consumedCount++;

                _logger.LogDebug(
                    "Consumed {Quantity} of {ProductName} for order {OrderNumber}. " +
                    "COGS: {COGS:C}, Remaining: {Remaining}",
                    line.Quantity,
                    line.ProductName,
                    evt.OrderNumber,
                    result.CostOfGoodsConsumed,
                    result.QuantityRemaining);
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
            catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist"))
            {
                // Inventory not set up for this item - skip
                _logger.LogDebug(
                    "No inventory tracking for {ProductName} ({ProductId})",
                    line.ProductName,
                    line.ProductId);
            }
        }

        _logger.LogInformation(
            "Completed inventory consumption for order {OrderNumber}. Consumed {ConsumedCount}/{TotalCount} items",
            evt.OrderNumber,
            consumedCount,
            evt.Lines.Count);
    }

    private async Task HandleOrderVoidedAsync(OrderVoidedEvent evt)
    {
        _logger.LogInformation(
            "Order {OrderNumber} voided - inventory consumption reversal would be handled here",
            evt.OrderNumber);

        // In a full implementation:
        // 1. Look up the original consumption movements for this order
        // 2. Call ReverseConsumptionAsync for each movement
        // This would require storing movement IDs or maintaining an order-to-movements index

        await Task.CompletedTask;
    }

    private async Task PublishStockAlertAsync(OrderCompletedEvent orderEvent, OrderLineSnapshot line)
    {
        var alertStreamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var alertStreamId = StreamId.Create(StreamConstants.AlertStreamNamespace, orderEvent.OrganizationId.ToString());
        var alertStream = alertStreamProvider.GetStream<IStreamEvent>(alertStreamId);

        await alertStream.OnNextAsync(new AlertTriggeredEvent(
            AlertId: Guid.NewGuid(),
            SiteId: orderEvent.SiteId,
            AlertType: "inventory.stock_shortage",
            Severity: "Warning",
            Title: $"Stock shortage: {line.ProductName}",
            Message: $"Insufficient stock for {line.ProductName} (ordered: {line.Quantity}) on order {orderEvent.OrderNumber}",
            Metadata: new Dictionary<string, string>
            {
                ["productId"] = line.ProductId.ToString(),
                ["productName"] = line.ProductName,
                ["orderId"] = orderEvent.OrderId.ToString(),
                ["orderNumber"] = orderEvent.OrderNumber,
                ["quantityOrdered"] = line.Quantity.ToString()
            })
        {
            OrganizationId = orderEvent.OrganizationId
        });
    }
}
