using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains.Subscribers;

/// <summary>
/// Subscribes to order events and creates kitchen tickets.
/// This decouples the Sales domain from the Kitchen domain via pub/sub.
///
/// Reacts to:
/// - OrderSentToKitchenEvent: Creates a new kitchen ticket with all sent items
/// </summary>
[ImplicitStreamSubscription(StreamConstants.OrderStreamNamespace)]
public class KitchenEventSubscriberGrain : Grain, IGrainWithStringKey, IAsyncObserver<IStreamEvent>
{
    private readonly ILogger<KitchenEventSubscriberGrain> _logger;
    private StreamSubscriptionHandle<IStreamEvent>? _subscription;

    public KitchenEventSubscriberGrain(ILogger<KitchenEventSubscriberGrain> logger)
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
            "KitchenEventSubscriber activated for organization {OrgId}",
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
                case OrderSentToKitchenEvent sentEvent:
                    await HandleOrderSentToKitchenAsync(sentEvent);
                    break;

                case OrderVoidedEvent voidedEvent:
                    await HandleOrderVoidedAsync(voidedEvent);
                    break;

                default:
                    // Ignore other order events - we only care about kitchen-relevant events
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
        _logger.LogInformation("Kitchen event stream completed");
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in kitchen event stream");
        return Task.CompletedTask;
    }

    private async Task HandleOrderSentToKitchenAsync(OrderSentToKitchenEvent evt)
    {
        if (evt.Lines.Count == 0)
        {
            _logger.LogDebug("No items to send to kitchen for order {OrderNumber}", evt.OrderNumber);
            return;
        }

        _logger.LogInformation(
            "Creating kitchen ticket for order {OrderNumber} with {ItemCount} items",
            evt.OrderNumber,
            evt.Lines.Count);

        // Create a new kitchen ticket
        var ticketId = Guid.NewGuid();
        var ticketKey = GrainKeys.KitchenOrder(evt.OrganizationId, evt.SiteId, ticketId);
        var ticketGrain = GrainFactory.GetGrain<IKitchenTicketGrain>(ticketKey);

        // Parse order type from string
        if (!Enum.TryParse<OrderType>(evt.OrderType, true, out var orderType))
        {
            orderType = OrderType.DineIn;
        }

        // Create the ticket
        var result = await ticketGrain.CreateAsync(new CreateKitchenTicketCommand(
            OrganizationId: evt.OrganizationId,
            SiteId: evt.SiteId,
            OrderId: evt.OrderId,
            OrderNumber: evt.OrderNumber,
            OrderType: orderType,
            TableNumber: evt.TableNumber,
            GuestCount: evt.GuestCount,
            ServerName: evt.ServerName,
            Notes: evt.Notes));

        // Add each line item to the ticket
        foreach (var line in evt.Lines)
        {
            await ticketGrain.AddItemAsync(new AddTicketItemCommand(
                OrderLineId: line.LineId,
                MenuItemId: line.MenuItemId,
                Name: line.Name,
                Quantity: line.Quantity,
                Modifiers: line.Modifiers,
                SpecialInstructions: line.SpecialInstructions,
                StationId: line.StationId));
        }

        _logger.LogInformation(
            "Kitchen ticket {TicketNumber} created for order {OrderNumber}",
            result.TicketNumber,
            evt.OrderNumber);
    }

    private Task HandleOrderVoidedAsync(OrderVoidedEvent evt)
    {
        _logger.LogInformation(
            "Order {OrderNumber} voided - kitchen ticket should be voided if exists",
            evt.OrderNumber);

        // In a full implementation, we would:
        // 1. Look up any active kitchen tickets for this order
        // 2. Void them with the same reason
        // This would require an index grain or query mechanism

        return Task.CompletedTask;
    }
}
