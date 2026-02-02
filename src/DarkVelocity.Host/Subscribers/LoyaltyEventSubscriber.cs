using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Subscribes to order completion events and awards loyalty points.
/// This decouples the Sales domain from the Loyalty domain via pub/sub.
///
/// Reacts to:
/// - OrderCompletedEvent: Awards points to customer based on spend
/// - OrderVoidedEvent: Reverses points if customer was assigned
/// </summary>
[ImplicitStreamSubscription(StreamConstants.OrderStreamNamespace)]
public class LoyaltyEventSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<LoyaltyEventSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    public LoyaltyEventSubscriberGrain(ILogger<LoyaltyEventSubscriberGrain> logger)
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
            "Awarding loyalty points for order {OrderNumber} to customer {CustomerId}. Spend: {Total:C}",
            evt.OrderNumber,
            evt.CustomerId,
            evt.Total);

        // Get the customer spend projection grain to record the spend and calculate points
        var spendProjectionKey = GrainKeys.CustomerSpendProjection(evt.OrganizationId, evt.CustomerId.Value);
        var spendProjectionGrain = GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(spendProjectionKey);

        try
        {
            // Record the spend - this will calculate and award points based on the loyalty program rules
            var result = await spendProjectionGrain.RecordSpendAsync(new RecordSpendCommand(
                OrderId: evt.OrderId,
                SiteId: evt.SiteId,
                NetSpend: evt.Total - evt.Tax, // Net spend before tax
                GrossSpend: evt.Subtotal,
                DiscountAmount: evt.DiscountAmount,
                TaxAmount: evt.Tax,
                ItemCount: evt.Lines.Sum(l => l.Quantity),
                TransactionDate: DateOnly.FromDateTime(evt.OccurredAt)));

            _logger.LogInformation(
                "Customer {CustomerId} earned {PointsEarned} points for order {OrderNumber}. " +
                "Total points: {TotalPoints}, Tier: {Tier}, TierChanged: {TierChanged}",
                evt.CustomerId,
                result.PointsEarned,
                evt.OrderNumber,
                result.TotalPoints,
                result.CurrentTier,
                result.TierChanged);

            if (result.TierChanged)
            {
                _logger.LogInformation(
                    "Customer {CustomerId} tier changed to {NewTier}!",
                    evt.CustomerId,
                    result.NewTier);
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist"))
        {
            // Customer spend projection not initialized - customer may not be enrolled in loyalty
            _logger.LogDebug(
                "Customer {CustomerId} not enrolled in loyalty program - skipping points",
                evt.CustomerId);
        }
    }

    private async Task HandleOrderVoidedAsync(OrderVoidedEvent evt)
    {
        // We would need to track which customer was on the voided order
        // For now, log that we received the event
        _logger.LogInformation(
            "Order {OrderNumber} voided for {VoidedAmount:C}. Loyalty points reversal would require customer lookup.",
            evt.OrderNumber,
            evt.VoidedAmount);

        // In a full implementation, you would:
        // 1. Look up the original order to get the customer ID
        // 2. Call ReverseSpendAsync on the CustomerSpendProjectionGrain
        // This would require storing customer ID in the voided event or maintaining an order index

        await Task.CompletedTask;
    }
}
