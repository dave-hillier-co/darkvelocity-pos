using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Subscribes to order completion events and publishes customer spend derived events.
/// This decouples the Order domain from the Loyalty domain via pub/sub.
///
/// Listens to: order-events stream (OrderCompletedEvent, OrderVoidedEvent)
/// Publishes to: customer-spend-events stream (CustomerSpendDerivedEvent, CustomerSpendReversalDerivedEvent)
/// </summary>
[ImplicitStreamSubscription(StreamConstants.OrderStreamNamespace)]
public class LoyaltyEventSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<LoyaltyEventSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;
    private IAsyncStream<IStreamEvent>? _customerSpendStream;

    public LoyaltyEventSubscriberGrain(ILogger<LoyaltyEventSubscriberGrain> logger)
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

        // Get customer-spend stream for publishing loyalty events
        var spendStreamId = StreamId.Create(StreamConstants.CustomerSpendStreamNamespace, this.GetPrimaryKeyString());
        _customerSpendStream = streamProvider.GetStream<IStreamEvent>(spendStreamId);

        _logger.LogInformation(
            "LoyaltyEventSubscriber activated for organization {OrgId}",
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
                    // Ignore other order events - we only care about loyalty-relevant events
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
        _logger.LogInformation("Loyalty event stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in loyalty event stream");
        return Task.CompletedTask;
    }

    private async Task HandleOrderCompletedAsync(OrderCompletedEvent evt)
    {
        // Only process if there's a customer attached to the order
        if (evt.CustomerId == null)
        {
            _logger.LogDebug(
                "Order {OrderNumber} completed without customer - no loyalty points to award",
                evt.OrderNumber);
            return;
        }

        _logger.LogInformation(
            "Deriving customer spend for order {OrderNumber}, customer {CustomerId}. Spend: {Total:C}",
            evt.OrderNumber,
            evt.CustomerId,
            evt.Total);

        var transactionDate = evt.BusinessDate ?? DateOnly.FromDateTime(evt.OccurredAt);

        // Publish customer spend derived event to customer-spend stream
        // The LoyaltyDispatcher will route this to the appropriate CustomerSpendProjectionGrain
        if (_customerSpendStream != null)
        {
            await _customerSpendStream.OnNextAsync(new CustomerSpendDerivedEvent(
                CustomerId: evt.CustomerId.Value,
                OrderId: evt.OrderId,
                SiteId: evt.SiteId,
                NetSpend: evt.Total - evt.Tax,
                GrossSpend: evt.Subtotal,
                DiscountAmount: evt.DiscountAmount,
                TaxAmount: evt.Tax,
                ItemCount: evt.Lines.Sum(l => l.Quantity),
                TransactionDate: transactionDate,
                OrderNumber: evt.OrderNumber)
            {
                OrganizationId = evt.OrganizationId
            });
        }

        _logger.LogInformation(
            "Published CustomerSpendDerivedEvent for order {OrderNumber} to customer-spend stream",
            evt.OrderNumber);
    }

    private async Task HandleOrderVoidedAsync(OrderVoidedEvent evt)
    {
        // Only process if there was a customer on the voided order
        if (evt.CustomerId == null)
        {
            _logger.LogDebug(
                "Order {OrderNumber} voided without customer - no loyalty reversal needed",
                evt.OrderNumber);
            return;
        }

        _logger.LogInformation(
            "Deriving customer spend reversal for order {OrderNumber}, customer {CustomerId}",
            evt.OrderNumber,
            evt.CustomerId);

        // Publish customer spend reversal derived event to customer-spend stream
        if (_customerSpendStream != null)
        {
            await _customerSpendStream.OnNextAsync(new CustomerSpendReversalDerivedEvent(
                CustomerId: evt.CustomerId.Value,
                OrderId: evt.OrderId,
                Amount: evt.VoidedAmount,
                Reason: evt.Reason)
            {
                OrganizationId = evt.OrganizationId
            });
        }

        _logger.LogInformation(
            "Published CustomerSpendReversalDerivedEvent for order {OrderNumber} to customer-spend stream",
            evt.OrderNumber);
    }
}
