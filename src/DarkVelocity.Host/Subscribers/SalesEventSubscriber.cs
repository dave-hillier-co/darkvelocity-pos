using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Implicit stream subscriber that aggregates sales events into daily sales grains.
/// </summary>
[ImplicitStreamSubscription(StreamConstants.SalesStreamNamespace)]
public class SalesEventSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<SalesEventSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    public SalesEventSubscriberGrain(ILogger<SalesEventSubscriberGrain> logger)
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
        _logger.LogInformation("Sales event stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in sales event stream");
        return Task.CompletedTask;
    }

    private async Task HandleSaleRecordedAsync(SaleRecordedEvent evt)
    {
        _logger.LogInformation(
            "Sale recorded: Order {OrderId}, Net Sales {NetSales:C}",
            evt.OrderId,
            evt.NetSales);

        // Get the daily sales grain for this site and date
        var dailySalesKey = GrainKeys.DailySales(evt.OrganizationId, evt.SiteId, evt.BusinessDate);
        var dailySalesGrain = GrainFactory.GetGrain<IDailySalesGrain>(dailySalesKey);

        // Record the sale in the daily aggregation
        await dailySalesGrain.RecordSaleAsync(new RecordSaleFromStreamCommand(
            OrderId: evt.OrderId,
            GrossSales: evt.GrossSales,
            Discounts: evt.DiscountAmount,
            Tax: evt.Tax,
            GuestCount: evt.GuestCount,
            ItemCount: evt.ItemCount,
            Channel: evt.Channel,
            TheoreticalCOGS: evt.TheoreticalCOGS));

        _logger.LogInformation(
            "Aggregated sale to daily sales for site {SiteId} on {BusinessDate}",
            evt.SiteId,
            evt.BusinessDate);
    }

    private async Task HandleVoidRecordedAsync(VoidRecordedEvent evt)
    {
        _logger.LogInformation(
            "Void recorded: Order {OrderId}, Amount {VoidAmount:C}, Reason: {Reason}",
            evt.OrderId,
            evt.VoidAmount,
            evt.Reason);

        // Get the daily sales grain for this site and date
        var dailySalesKey = GrainKeys.DailySales(evt.OrganizationId, evt.SiteId, evt.BusinessDate);
        var dailySalesGrain = GrainFactory.GetGrain<IDailySalesGrain>(dailySalesKey);

        // Record the void in the daily aggregation
        await dailySalesGrain.RecordVoidAsync(evt.OrderId, evt.VoidAmount, evt.Reason);

        _logger.LogInformation(
            "Aggregated void to daily sales for site {SiteId} on {BusinessDate}",
            evt.SiteId,
            evt.BusinessDate);
    }
}
