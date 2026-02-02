using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Legacy order event subscriber - now primarily for logging and monitoring.
///
/// Domain-specific event handling has been moved to dedicated subscribers:
/// - KitchenEventSubscriberGrain: Creates kitchen tickets from OrderSentToKitchenEvent
/// - LoyaltyEventSubscriberGrain: Awards loyalty points from OrderCompletedEvent
/// - InventoryConsumptionSubscriberGrain: Consumes inventory from OrderCompletedEvent
/// - SalesEventSubscriberGrain: Aggregates sales data from SaleRecordedEvent
///
/// This follows the pub/sub pattern where a single domain event triggers
/// multiple independent subscribers, each handling their own bounded context.
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

    public Task OnNextAsync(IStreamEvent item, StreamSequenceToken? token = null)
    {
        // Log all order events for monitoring/debugging purposes
        // Actual domain handling is done by dedicated subscribers
        switch (item)
        {
            case OrderCreatedEvent evt:
                _logger.LogInformation(
                    "Order {OrderNumber} created at site {SiteId}",
                    evt.OrderNumber,
                    evt.SiteId);
                break;

            case OrderLineAddedEvent evt:
                _logger.LogDebug(
                    "Line added to order {OrderId}: {ProductName} x{Quantity}",
                    evt.OrderId,
                    evt.ProductName,
                    evt.Quantity);
                break;

            case OrderSentToKitchenEvent evt:
                _logger.LogInformation(
                    "Order {OrderNumber} sent to kitchen with {LineCount} items",
                    evt.OrderNumber,
                    evt.Lines.Count);
                break;

            case OrderCompletedEvent evt:
                _logger.LogInformation(
                    "Order {OrderNumber} completed. Total: {Total:C}, Lines: {LineCount}, Customer: {CustomerId}",
                    evt.OrderNumber,
                    evt.Total,
                    evt.Lines.Count,
                    evt.CustomerId?.ToString() ?? "none");
                break;

            case OrderVoidedEvent evt:
                _logger.LogWarning(
                    "Order {OrderNumber} voided for {VoidedAmount:C}. Reason: {Reason}",
                    evt.OrderNumber,
                    evt.VoidedAmount,
                    evt.Reason);
                break;

            default:
                _logger.LogDebug("Order event received: {EventType}", item.GetType().Name);
                break;
        }

        return Task.CompletedTask;
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
}
