using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Subscribes to order completion events and publishes inventory consumption derived events.
/// This decouples the Order domain from the Inventory domain via pub/sub.
///
/// Listens to: order-events stream (OrderCompletedEvent, OrderVoidedEvent)
/// Publishes to: inventory-events stream (InventoryConsumptionDerivedEvent, InventoryConsumptionReversalDerivedEvent)
/// </summary>
[ImplicitStreamSubscription(StreamConstants.OrderStreamNamespace)]
public class InventoryConsumptionSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<InventoryConsumptionSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;
    private IAsyncStream<IStreamEvent>? _inventoryStream;

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

        // Get inventory stream for publishing consumption events
        var inventoryStreamId = StreamId.Create(StreamConstants.InventoryStreamNamespace, this.GetPrimaryKeyString());
        _inventoryStream = streamProvider.GetStream<IStreamEvent>(inventoryStreamId);

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
            "Publishing inventory consumption requests for order {OrderNumber} with {LineCount} line items",
            evt.OrderNumber,
            evt.Lines.Count);

        var publishedCount = 0;

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

            // Publish inventory consumption derived event
            // The InventoryDispatcher will route this to the appropriate InventoryGrain
            if (_inventoryStream != null)
            {
                // For now, use ProductId as ingredient ID (simplified)
                // In a full implementation, we would look up the recipe and publish
                // consumption events for each ingredient
                await _inventoryStream.OnNextAsync(new InventoryConsumptionDerivedEvent(
                    IngredientId: line.ProductId,
                    SiteId: evt.SiteId,
                    OrderId: evt.OrderId,
                    OrderNumber: evt.OrderNumber,
                    LineId: line.LineId,
                    ProductName: line.ProductName,
                    Quantity: line.Quantity,
                    RecipeId: line.RecipeId,
                    PerformedBy: evt.ServerId)
                {
                    OrganizationId = evt.OrganizationId
                });

                publishedCount++;
            }
        }

        _logger.LogInformation(
            "Published {PublishedCount} inventory consumption requests for order {OrderNumber}",
            publishedCount,
            evt.OrderNumber);
    }

    private async Task HandleOrderVoidedAsync(OrderVoidedEvent evt)
    {
        _logger.LogInformation(
            "Deriving inventory consumption reversal for order {OrderNumber}",
            evt.OrderNumber);

        // Publish inventory consumption reversal derived event
        // The InventoryDispatcher will handle looking up original consumptions
        if (_inventoryStream != null)
        {
            await _inventoryStream.OnNextAsync(new InventoryConsumptionReversalDerivedEvent(
                OrderId: evt.OrderId,
                SiteId: evt.SiteId,
                Reason: evt.Reason)
            {
                OrganizationId = evt.OrganizationId
            });
        }

        _logger.LogInformation(
            "Published InventoryConsumptionReversalDerivedEvent for order {OrderNumber}",
            evt.OrderNumber);
    }
}
