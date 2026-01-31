using DarkVelocity.Orleans.Abstractions;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Orleans.Grains.Subscribers;

/// <summary>
/// Implicit stream subscriber that handles order events.
/// Syncs order completions to inventory consumption.
/// </summary>
[ImplicitStreamSubscription(StreamConstants.OrderStreamNamespace)]
public class OrderEventSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<OrderEventSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    public OrderEventSubscriberGrain(ILogger<OrderEventSubscriberGrain> logger)
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
            "OrderEventSubscriber activated for organization {OrgId}",
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

                case OrderLineAddedEvent lineAddedEvent:
                    await HandleOrderLineAddedAsync(lineAddedEvent);
                    break;

                default:
                    _logger.LogDebug("Ignoring unhandled event type: {EventType}", item.GetType().Name);
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
        _logger.LogInformation("Order event stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in order event stream");
        return Task.CompletedTask;
    }

    private async Task HandleOrderCompletedAsync(OrderCompletedEvent evt)
    {
        _logger.LogInformation(
            "Order {OrderNumber} completed. Processing inventory consumption for {LineCount} items",
            evt.OrderNumber,
            evt.Lines.Count);

        // In a real implementation, you would:
        // 1. Look up recipe for each line item
        // 2. Calculate ingredient consumption
        // 3. Call InventoryGrain.ConsumeAsync for each ingredient

        // For now, we'll publish to an inventory stream that the InventoryGrain can subscribe to
        var inventoryStreamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var inventoryStreamId = StreamId.Create(StreamConstants.InventoryStreamNamespace, evt.OrganizationId.ToString());
        var inventoryStream = inventoryStreamProvider.GetStream<IStreamEvent>(inventoryStreamId);

        foreach (var line in evt.Lines)
        {
            // Publish stock consumed event for each line
            // In production, this would include actual ingredient breakdown from recipes
            await inventoryStream.OnNextAsync(new StockConsumedEvent(
                line.ProductId, // This would be ingredient ID in production
                evt.SiteId,
                line.ProductName,
                line.Quantity,
                "unit",
                line.LineTotal * 0.3m, // Estimated COGS at 30%
                evt.OrderId,
                "Order completion")
            {
                OrganizationId = evt.OrganizationId
            });
        }

        _logger.LogInformation(
            "Published inventory consumption events for order {OrderNumber}",
            evt.OrderNumber);
    }

    private Task HandleOrderVoidedAsync(OrderVoidedEvent evt)
    {
        _logger.LogInformation(
            "Order {OrderNumber} voided for {VoidedAmount:C}. Reason: {Reason}",
            evt.OrderNumber,
            evt.VoidedAmount,
            evt.Reason);

        // In a real implementation, you might:
        // 1. Reverse inventory consumption
        // 2. Update kitchen display
        // 3. Trigger alerts for manager review

        return Task.CompletedTask;
    }

    private Task HandleOrderLineAddedAsync(OrderLineAddedEvent evt)
    {
        _logger.LogDebug(
            "Line added to order {OrderId}: {ProductName} x{Quantity}",
            evt.OrderId,
            evt.ProductName,
            evt.Quantity);

        // In a real implementation, you might:
        // 1. Send to kitchen display system
        // 2. Update real-time sales dashboard

        return Task.CompletedTask;
    }
}
