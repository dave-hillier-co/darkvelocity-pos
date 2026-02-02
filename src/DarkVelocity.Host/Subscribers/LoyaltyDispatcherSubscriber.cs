using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Subscribes to customer-spend-events stream and routes derived spend events to CustomerSpendProjectionGrain.
/// This dispatcher handles the routing complexity of compound grain keys (org:customerId).
///
/// Listens to: customer-spend-events stream (CustomerSpendDerivedEvent, CustomerSpendReversalDerivedEvent)
/// Routes to: CustomerSpendProjectionGrain (keyed by org:customerId)
/// </summary>
[ImplicitStreamSubscription(StreamConstants.CustomerSpendStreamNamespace)]
public class LoyaltyDispatcherSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<LoyaltyDispatcherSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    public LoyaltyDispatcherSubscriberGrain(ILogger<LoyaltyDispatcherSubscriberGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.CustomerSpendStreamNamespace, this.GetPrimaryKeyString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        _subscription = await stream.SubscribeAsync(this);

        _logger.LogInformation(
            "LoyaltyDispatcher activated for organization {OrgId}",
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
                case CustomerSpendDerivedEvent spendDerived:
                    await HandleCustomerSpendDerivedAsync(spendDerived);
                    break;

                case CustomerSpendReversalDerivedEvent reversalDerived:
                    await HandleCustomerSpendReversalDerivedAsync(reversalDerived);
                    break;

                // Ignore events that are outputs from CustomerSpendProjectionGrain
                // (CustomerSpendRecordedEvent, LoyaltyPointsEarnedEvent, CustomerTierChangedEvent)
                // These are handled by CustomerSpendSubscriber for downstream actions
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
        _logger.LogInformation("Loyalty dispatcher stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in loyalty dispatcher stream");
        return Task.CompletedTask;
    }

    private async Task HandleCustomerSpendDerivedAsync(CustomerSpendDerivedEvent evt)
    {
        _logger.LogInformation(
            "Routing customer spend to CustomerSpendProjectionGrain: Customer {CustomerId}, Order {OrderId}",
            evt.CustomerId,
            evt.OrderId);

        // Route to the appropriate CustomerSpendProjectionGrain based on customer ID
        var spendProjectionKey = GrainKeys.CustomerSpendProjection(evt.OrganizationId, evt.CustomerId);
        var spendProjectionGrain = GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(spendProjectionKey);

        try
        {
            // Record the spend - this will calculate and award points based on the loyalty program rules
            var result = await spendProjectionGrain.RecordSpendAsync(new RecordSpendCommand(
                OrderId: evt.OrderId,
                SiteId: evt.SiteId,
                NetSpend: evt.NetSpend,
                GrossSpend: evt.GrossSpend,
                DiscountAmount: evt.DiscountAmount,
                TaxAmount: evt.TaxAmount,
                ItemCount: evt.ItemCount,
                TransactionDate: evt.TransactionDate));

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
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist") || ex.Message.Contains("not initialized"))
        {
            // Customer spend projection not initialized - customer may not be enrolled in loyalty
            _logger.LogDebug(
                "Customer {CustomerId} not enrolled in loyalty program - skipping points for order {OrderNumber}",
                evt.CustomerId,
                evt.OrderNumber);
        }
    }

    private async Task HandleCustomerSpendReversalDerivedAsync(CustomerSpendReversalDerivedEvent evt)
    {
        _logger.LogInformation(
            "Routing customer spend reversal to CustomerSpendProjectionGrain: Customer {CustomerId}, Order {OrderId}",
            evt.CustomerId,
            evt.OrderId);

        // Route to the appropriate CustomerSpendProjectionGrain based on customer ID
        var spendProjectionKey = GrainKeys.CustomerSpendProjection(evt.OrganizationId, evt.CustomerId);
        var spendProjectionGrain = GrainFactory.GetGrain<ICustomerSpendProjectionGrain>(spendProjectionKey);

        try
        {
            await spendProjectionGrain.ReverseSpendAsync(new ReverseSpendCommand(
                OrderId: evt.OrderId,
                Amount: evt.Amount,
                Reason: evt.Reason));

            _logger.LogInformation(
                "Reversed customer spend for customer {CustomerId}, order {OrderId}. Amount: {Amount:C}",
                evt.CustomerId,
                evt.OrderId,
                evt.Amount);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("does not exist") || ex.Message.Contains("not initialized"))
        {
            // Customer spend projection not initialized - nothing to reverse
            _logger.LogDebug(
                "Customer {CustomerId} not enrolled in loyalty program - nothing to reverse for order {OrderId}",
                evt.CustomerId,
                evt.OrderId);
        }
    }
}
