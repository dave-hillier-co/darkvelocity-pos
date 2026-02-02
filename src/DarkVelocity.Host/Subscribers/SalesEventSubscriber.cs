using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Subscribes to order completion events and derives sales events for aggregation.
/// This decouples the Order domain from the Sales/Reporting domain via pub/sub.
///
/// Listens to: order-events stream (OrderCompletedEvent, OrderVoidedEvent)
/// Publishes to: sales-events stream (SaleRecordedEvent, VoidRecordedEvent)
/// </summary>
[ImplicitStreamSubscription(StreamConstants.OrderStreamNamespace)]
public class SalesEventSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<SalesEventSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;
    private IAsyncStream<IStreamEvent>? _salesStream;

    public SalesEventSubscriberGrain(ILogger<SalesEventSubscriberGrain> logger)
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

        // Get sales stream for publishing derived events
        var salesStreamId = StreamId.Create(StreamConstants.SalesStreamNamespace, this.GetPrimaryKeyString());
        _salesStream = streamProvider.GetStream<IStreamEvent>(salesStreamId);

        _logger.LogInformation(
            "SalesEventSubscriber activated for organization {OrgId}",
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
                    // Ignore other order events (OrderCreatedEvent, OrderLineAddedEvent, etc.)
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
        _logger.LogInformation("Sales event stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in sales event stream");
        return Task.CompletedTask;
    }

    private async Task HandleOrderCompletedAsync(OrderCompletedEvent evt)
    {
        var businessDate = evt.BusinessDate ?? DateOnly.FromDateTime(evt.OccurredAt);
        var netSales = evt.Subtotal - evt.DiscountAmount;
        var itemCount = evt.Lines.Sum(l => l.Quantity);

        _logger.LogInformation(
            "Deriving sale from order {OrderNumber}: Net Sales {NetSales:C}",
            evt.OrderNumber,
            netSales);

        // Derive and publish SaleRecordedEvent to sales stream
        // This allows DailySalesGrain to subscribe independently
        if (_salesStream != null)
        {
            await _salesStream.OnNextAsync(new SaleRecordedEvent(
                OrderId: evt.OrderId,
                SiteId: evt.SiteId,
                BusinessDate: businessDate,
                GrossSales: evt.Subtotal,
                DiscountAmount: evt.DiscountAmount,
                NetSales: netSales,
                Tax: evt.Tax,
                TheoreticalCOGS: 0m, // Would be calculated from recipes if available
                ItemCount: itemCount,
                GuestCount: evt.GuestCount,
                Channel: evt.Channel)
            {
                OrganizationId = evt.OrganizationId
            });
        }

        _logger.LogInformation(
            "Published SaleRecordedEvent for order {OrderNumber} to sales stream",
            evt.OrderNumber);
    }

    private async Task HandleOrderVoidedAsync(OrderVoidedEvent evt)
    {
        var businessDate = evt.BusinessDate ?? DateOnly.FromDateTime(evt.OccurredAt);

        _logger.LogInformation(
            "Deriving void from order {OrderNumber}: Amount {VoidAmount:C}",
            evt.OrderNumber,
            evt.VoidedAmount);

        // Derive and publish VoidRecordedEvent to sales stream
        if (_salesStream != null)
        {
            await _salesStream.OnNextAsync(new VoidRecordedEvent(
                OrderId: evt.OrderId,
                SiteId: evt.SiteId,
                BusinessDate: businessDate,
                VoidAmount: evt.VoidedAmount,
                Reason: evt.Reason)
            {
                OrganizationId = evt.OrganizationId
            });
        }

        _logger.LogInformation(
            "Published VoidRecordedEvent for order {OrderNumber} to sales stream",
            evt.OrderNumber);
    }
}
