using DarkVelocity.Host;
using DarkVelocity.Host.Costing;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Subscribes to inventory-events stream and routes events to reporting grains.
/// This dispatcher handles the routing complexity of compound grain keys (org:site:date).
///
/// Listens to: inventory-events stream (StockConsumedEvent, StockWrittenOffEvent)
/// Routes to: DailyConsumptionGrain, DailyWasteGrain (keyed by org:site:date)
/// </summary>
[ImplicitStreamSubscription(StreamConstants.InventoryStreamNamespace)]
public class InventoryReportingDispatcherSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<InventoryReportingDispatcherSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    public InventoryReportingDispatcherSubscriberGrain(ILogger<InventoryReportingDispatcherSubscriberGrain> logger)
    {
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var streamId = StreamId.Create(StreamConstants.InventoryStreamNamespace, this.GetPrimaryKeyString());
        var stream = streamProvider.GetStream<IStreamEvent>(streamId);

        _subscription = await stream.SubscribeAsync(this);

        _logger.LogInformation(
            "InventoryReportingDispatcher activated for organization {OrgId}",
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
                case StockConsumedEvent consumedEvent:
                    await HandleStockConsumedAsync(consumedEvent);
                    break;

                case StockWrittenOffEvent writtenOffEvent:
                    await HandleStockWrittenOffAsync(writtenOffEvent);
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
        _logger.LogInformation("Inventory reporting dispatcher stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in inventory reporting dispatcher stream");
        return Task.CompletedTask;
    }

    private async Task HandleStockConsumedAsync(StockConsumedEvent evt)
    {
        var businessDate = DateOnly.FromDateTime(evt.OccurredAt);

        _logger.LogInformation(
            "Routing consumption to DailyConsumptionGrain: Ingredient {IngredientId}, Site {SiteId}, Date {BusinessDate}",
            evt.IngredientId,
            evt.SiteId,
            businessDate);

        // Route to the appropriate DailyConsumptionGrain based on site and business date
        var consumptionKey = GrainKeys.DailyConsumption(evt.OrganizationId, evt.SiteId, businessDate);
        var consumptionGrain = GrainFactory.GetGrain<IDailyConsumptionGrain>(consumptionKey);

        // Record the consumption - actual values from the inventory event
        // Theoretical values would require recipe lookup; for now we track actual only
        await consumptionGrain.RecordConsumptionAsync(new RecordConsumptionCommand(
            IngredientId: evt.IngredientId,
            IngredientName: evt.IngredientName,
            Category: string.Empty, // Category not available in stream event
            Unit: evt.Unit,
            TheoreticalQuantity: evt.QuantityConsumed, // Without recipe context, use actual as theoretical
            TheoreticalCost: evt.CostOfGoodsConsumed,
            ActualQuantity: evt.QuantityConsumed,
            ActualCost: evt.CostOfGoodsConsumed,
            CostingMethod: CostingMethod.FIFO, // Default to FIFO; site settings could override
            OrderId: evt.OrderId,
            MenuItemId: null, // Would require order lookup
            RecipeVersionId: null)); // Would require recipe lookup

        _logger.LogDebug(
            "Routed consumption to daily consumption grain for site {SiteId} on {BusinessDate}",
            evt.SiteId,
            businessDate);
    }

    private async Task HandleStockWrittenOffAsync(StockWrittenOffEvent evt)
    {
        var businessDate = DateOnly.FromDateTime(evt.OccurredAt);

        _logger.LogInformation(
            "Routing waste to DailyWasteGrain: Ingredient {IngredientId}, Site {SiteId}, Date {BusinessDate}",
            evt.IngredientId,
            evt.SiteId,
            businessDate);

        // Route to the appropriate DailyWasteGrain based on site and business date
        var wasteKey = GrainKeys.DailyWaste(evt.OrganizationId, evt.SiteId, businessDate);
        var wasteGrain = GrainFactory.GetGrain<IDailyWasteGrain>(wasteKey);

        // Map the write-off category string to WasteReason enum
        var wasteReason = ParseWasteReason(evt.WriteOffCategory);

        await wasteGrain.RecordWasteAsync(new RecordWasteFactCommand(
            WasteId: Guid.NewGuid(),
            IngredientId: evt.IngredientId,
            IngredientName: evt.IngredientName,
            Sku: string.Empty, // SKU not available in stream event
            Category: string.Empty, // Category not available in stream event
            BatchId: null, // Batch ID not available in stream event
            Quantity: evt.QuantityWrittenOff,
            Unit: string.Empty, // Unit not available in write-off event
            Reason: wasteReason,
            ReasonDetails: evt.Reason,
            CostBasis: evt.CostWrittenOff,
            RecordedBy: evt.RecordedBy,
            ApprovedBy: null,
            PhotoUrl: null));

        _logger.LogDebug(
            "Routed waste to daily waste grain for site {SiteId} on {BusinessDate}",
            evt.SiteId,
            businessDate);
    }

    private static WasteReason ParseWasteReason(string category)
    {
        return category.ToLowerInvariant() switch
        {
            "spoilage" => WasteReason.Spoilage,
            "expired" or "expiry" => WasteReason.Expired,
            "line_cleaning" or "linecleaning" => WasteReason.LineCleaning,
            "breakage" or "broken" => WasteReason.Breakage,
            "overproduction" or "over_production" => WasteReason.OverProduction,
            "customer_return" or "customerreturn" or "return" => WasteReason.CustomerReturn,
            "quality_rejection" or "qualityrejection" or "quality" => WasteReason.QualityRejection,
            "spillage_accident" or "spillageaccident" or "spillage" => WasteReason.SpillageAccident,
            "theft" => WasteReason.Theft,
            "prep_waste" or "prepwaste" or "prep" => WasteReason.PrepWaste,
            _ => WasteReason.Other
        };
    }
}
