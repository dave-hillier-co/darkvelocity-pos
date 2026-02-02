using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Subscribes to sales-events stream and routes events to DailySalesGrain.
/// This dispatcher handles the routing complexity of compound grain keys (org:site:date).
///
/// Listens to: sales-events stream (SaleRecordedEvent, VoidRecordedEvent)
/// Routes to: DailySalesGrain (keyed by org:site:date)
/// </summary>
[ImplicitStreamSubscription(StreamConstants.SalesStreamNamespace)]
public class DailySalesDispatcherSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<DailySalesDispatcherSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    public DailySalesDispatcherSubscriberGrain(ILogger<DailySalesDispatcherSubscriberGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.SalesStreamNamespace, this.GetPrimaryKeyString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        _subscription = await stream.SubscribeAsync(this);

        _logger.LogInformation(
            "DailySalesDispatcher activated for organization {OrgId}",
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
                case SaleRecordedEvent saleEvent:
                    await HandleSaleRecordedAsync(saleEvent);
                    break;

                case VoidRecordedEvent voidEvent:
                    await HandleVoidRecordedAsync(voidEvent);
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
        _logger.LogInformation("Daily sales dispatcher stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in daily sales dispatcher stream");
        return Task.CompletedTask;
    }

    private async Task HandleSaleRecordedAsync(SaleRecordedEvent evt)
    {
        _logger.LogInformation(
            "Routing sale to DailySalesGrain: Order {OrderId}, Site {SiteId}, Date {BusinessDate}",
            evt.OrderId,
            evt.SiteId,
            evt.BusinessDate);

        // Route to the appropriate DailySalesGrain based on site and business date
        var dailySalesKey = GrainKeys.DailySales(evt.OrganizationId, evt.SiteId, evt.BusinessDate);
        var dailySalesGrain = GrainFactory.GetGrain<IDailySalesGrain>(dailySalesKey);

        await dailySalesGrain.RecordSaleAsync(new RecordSaleFromStreamCommand(
            OrderId: evt.OrderId,
            GrossSales: evt.GrossSales,
            Discounts: evt.DiscountAmount,
            Tax: evt.Tax,
            GuestCount: evt.GuestCount,
            ItemCount: evt.ItemCount,
            Channel: evt.Channel,
            TheoreticalCOGS: evt.TheoreticalCOGS));

        _logger.LogDebug(
            "Routed sale to daily sales grain for site {SiteId} on {BusinessDate}",
            evt.SiteId,
            evt.BusinessDate);
    }

    private async Task HandleVoidRecordedAsync(VoidRecordedEvent evt)
    {
        _logger.LogInformation(
            "Routing void to DailySalesGrain: Order {OrderId}, Site {SiteId}, Date {BusinessDate}",
            evt.OrderId,
            evt.SiteId,
            evt.BusinessDate);

        // Route to the appropriate DailySalesGrain based on site and business date
        var dailySalesKey = GrainKeys.DailySales(evt.OrganizationId, evt.SiteId, evt.BusinessDate);
        var dailySalesGrain = GrainFactory.GetGrain<IDailySalesGrain>(dailySalesKey);

        await dailySalesGrain.RecordVoidAsync(evt.OrderId, evt.VoidAmount, evt.Reason);

        _logger.LogDebug(
            "Routed void to daily sales grain for site {SiteId} on {BusinessDate}",
            evt.SiteId,
            evt.BusinessDate);
    }
}
