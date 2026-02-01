using DarkVelocity.Host.Streams;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Search;

/// <summary>
/// Grain interface for search projection.
/// One grain per organization subscribes to all relevant event streams
/// and projects them into the search index.
/// </summary>
public interface ISearchProjectionGrain : IGrainWithStringKey
{
    /// <summary>
    /// Activates the projection and begins subscribing to event streams.
    /// Called automatically on grain activation, but can be called explicitly
    /// to ensure the projection is running.
    /// </summary>
    Task ActivateProjectionAsync();

    /// <summary>
    /// Gets the projection status for monitoring.
    /// </summary>
    Task<SearchProjectionStatus> GetStatusAsync();
}

[GenerateSerializer]
public sealed record SearchProjectionStatus
{
    [Id(0)] public required Guid OrganizationId { get; init; }
    [Id(1)] public required DateTime ActivatedAt { get; init; }
    [Id(2)] public required long OrderEventsProcessed { get; init; }
    [Id(3)] public required long PaymentEventsProcessed { get; init; }
    [Id(4)] public required long CustomerEventsProcessed { get; init; }
    [Id(5)] public required DateTime? LastEventAt { get; init; }
}

/// <summary>
/// Orleans grain that projects events from order, payment, and customer streams
/// into the search index. One grain per organization.
///
/// Key format: "org:{orgId}"
/// </summary>
public class SearchProjectionGrain : Grain, ISearchProjectionGrain
{
    private readonly ISearchIndexer _indexer;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<SearchProjectionGrain> _logger;

    private Guid _organizationId;
    private DateTime _activatedAt;
    private long _orderEventsProcessed;
    private long _paymentEventsProcessed;
    private long _customerEventsProcessed;
    private DateTime? _lastEventAt;

    private StreamSubscriptionHandle<IStreamEvent>? _orderSubscription;
    private StreamSubscriptionHandle<IStreamEvent>? _paymentSubscription;
    private StreamSubscriptionHandle<IStreamEvent>? _customerSubscription;

    public SearchProjectionGrain(
        ISearchIndexer indexer,
        IGrainFactory grainFactory,
        ILogger<SearchProjectionGrain> logger)
    {
        _indexer = indexer;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Parse org ID from grain key: "org:{guid}"
        var key = this.GetPrimaryKeyString();
        if (!key.StartsWith("org:") || !Guid.TryParse(key[4..], out _organizationId))
        {
            _logger.LogError("Invalid search projection grain key: {Key}", key);
            throw new ArgumentException($"Invalid grain key format: {key}");
        }

        _activatedAt = DateTime.UtcNow;
        await SubscribeToStreamsAsync();

        _logger.LogInformation(
            "Search projection activated for organization {OrgId}",
            _organizationId);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        // Unsubscribe from streams
        if (_orderSubscription != null)
            await _orderSubscription.UnsubscribeAsync();
        if (_paymentSubscription != null)
            await _paymentSubscription.UnsubscribeAsync();
        if (_customerSubscription != null)
            await _customerSubscription.UnsubscribeAsync();

        _logger.LogInformation(
            "Search projection deactivated for organization {OrgId}. Processed: Orders={Orders}, Payments={Payments}, Customers={Customers}",
            _organizationId, _orderEventsProcessed, _paymentEventsProcessed, _customerEventsProcessed);
    }

    public Task ActivateProjectionAsync()
    {
        // Grain is already activated when this is called
        return Task.CompletedTask;
    }

    public Task<SearchProjectionStatus> GetStatusAsync()
    {
        return Task.FromResult(new SearchProjectionStatus
        {
            OrganizationId = _organizationId,
            ActivatedAt = _activatedAt,
            OrderEventsProcessed = _orderEventsProcessed,
            PaymentEventsProcessed = _paymentEventsProcessed,
            CustomerEventsProcessed = _customerEventsProcessed,
            LastEventAt = _lastEventAt
        });
    }

    private async Task SubscribeToStreamsAsync()
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var orgIdStr = _organizationId.ToString();

        // Subscribe to order events
        var orderStream = streamProvider.GetStream<IStreamEvent>(
            StreamId.Create(StreamConstants.OrderStreamNamespace, orgIdStr));
        _orderSubscription = await orderStream.SubscribeAsync(OnOrderEventAsync);

        // Subscribe to payment events
        var paymentStream = streamProvider.GetStream<IStreamEvent>(
            StreamId.Create(StreamConstants.PaymentStreamNamespace, orgIdStr));
        _paymentSubscription = await paymentStream.SubscribeAsync(OnPaymentEventAsync);

        // Subscribe to customer events
        var customerStream = streamProvider.GetStream<IStreamEvent>(
            StreamId.Create(StreamConstants.CustomerStreamNamespace, orgIdStr));
        _customerSubscription = await customerStream.SubscribeAsync(OnCustomerEventAsync);
    }

    private async Task OnOrderEventAsync(IStreamEvent @event, StreamSequenceToken? token)
    {
        try
        {
            switch (@event)
            {
                case OrderCreatedEvent created:
                    await HandleOrderCreatedAsync(created);
                    break;

                case OrderCompletedEvent completed:
                    await _indexer.UpdateOrderStatusAsync(
                        completed.OrderId,
                        "Completed",
                        DateTime.UtcNow);
                    break;

                case OrderVoidedEvent voided:
                    await _indexer.UpdateOrderStatusAsync(
                        voided.OrderId,
                        "Voided",
                        null);
                    break;
            }

            _orderEventsProcessed++;
            _lastEventAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing order event {EventType} for org {OrgId}",
                @event.GetType().Name, _organizationId);
            throw;
        }
    }

    private async Task OnPaymentEventAsync(IStreamEvent @event, StreamSequenceToken? token)
    {
        try
        {
            switch (@event)
            {
                case PaymentInitiatedEvent initiated:
                    await HandlePaymentInitiatedAsync(initiated);
                    break;

                case PaymentCompletedEvent completed:
                    await _indexer.UpdatePaymentStatusAsync(
                        completed.PaymentId,
                        "Completed",
                        DateTime.UtcNow);
                    break;

                case PaymentVoidedEvent voided:
                    await _indexer.UpdatePaymentStatusAsync(
                        voided.PaymentId,
                        "Voided",
                        null);
                    break;

                case PaymentRefundedEvent refunded:
                    await _indexer.UpdatePaymentStatusAsync(
                        refunded.PaymentId,
                        "Refunded",
                        null);
                    break;
            }

            _paymentEventsProcessed++;
            _lastEventAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing payment event {EventType} for org {OrgId}",
                @event.GetType().Name, _organizationId);
            throw;
        }
    }

    private async Task OnCustomerEventAsync(IStreamEvent @event, StreamSequenceToken? token)
    {
        try
        {
            switch (@event)
            {
                case CustomerCreatedEvent created:
                    await HandleCustomerCreatedAsync(created);
                    break;

                case CustomerProfileUpdatedEvent updated:
                    // Fetch full state and re-index
                    await ReindexCustomerAsync(updated.CustomerId);
                    break;
            }

            _customerEventsProcessed++;
            _lastEventAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing customer event {EventType} for org {OrgId}",
                @event.GetType().Name, _organizationId);
            throw;
        }
    }

    private async Task HandleOrderCreatedAsync(OrderCreatedEvent @event)
    {
        // For new orders, we need to fetch additional details from the grain
        // In a production system, you might include more data in the event
        var document = new OrderSearchDocument
        {
            Id = @event.OrderId,
            OrgId = @event.OrganizationId,
            SiteId = @event.SiteId,
            OrderNumber = @event.OrderNumber,
            CustomerName = null, // Will be updated when order is assigned to customer
            ServerName = null, // Could fetch from employee grain if needed
            TableNumber = null,
            Notes = null,
            Status = "Open",
            OrderType = "DineIn", // Default, could be in event
            GrandTotal = 0,
            CreatedAt = @event.OccurredAt,
            ClosedAt = null,
            ItemCount = 0,
            GuestCount = 1
        };

        await _indexer.IndexOrderAsync(document);
    }

    private async Task HandlePaymentInitiatedAsync(PaymentInitiatedEvent @event)
    {
        // Fetch order number for denormalization
        string orderNumber = ""; // Would need to fetch from order grain or include in event

        var document = new PaymentSearchDocument
        {
            Id = @event.PaymentId,
            OrgId = @event.OrganizationId,
            SiteId = @event.SiteId,
            OrderId = @event.OrderId,
            OrderNumber = orderNumber,
            CustomerName = null,
            CardLastFour = null,
            GatewayReference = null,
            Method = @event.Method,
            Status = "Initiated",
            Amount = @event.Amount,
            TipAmount = 0,
            CreatedAt = @event.OccurredAt,
            CompletedAt = null
        };

        await _indexer.IndexPaymentAsync(document);
    }

    private async Task HandleCustomerCreatedAsync(CustomerCreatedEvent @event)
    {
        var document = new CustomerSearchDocument
        {
            Id = @event.CustomerId,
            OrgId = @event.OrganizationId,
            DisplayName = @event.DisplayName,
            FirstName = null,
            LastName = null,
            Email = @event.Email,
            Phone = @event.Phone,
            Status = "Active",
            LoyaltyTier = null,
            LifetimeSpend = 0,
            VisitCount = 0,
            LastVisitAt = null,
            CreatedAt = @event.OccurredAt,
            Segment = "New",
            Tags = []
        };

        await _indexer.IndexCustomerAsync(document);
    }

    private async Task ReindexCustomerAsync(Guid customerId)
    {
        // Fetch current customer state from grain and re-index
        // This ensures search index stays in sync with grain state
        var customerGrain = _grainFactory.GetGrain<Grains.ICustomerGrain>(
            $"{_organizationId}:customer:{customerId}");

        var state = await customerGrain.GetStateAsync();
        if (state == null) return;

        var document = new CustomerSearchDocument
        {
            Id = state.Id,
            OrgId = state.OrganizationId,
            DisplayName = state.DisplayName,
            FirstName = state.FirstName,
            LastName = state.LastName,
            Email = state.Contact.Email,
            Phone = state.Contact.Phone,
            Status = state.Status.ToString(),
            LoyaltyTier = state.Loyalty?.TierName,
            LifetimeSpend = state.Stats.TotalSpend,
            VisitCount = state.Stats.TotalVisits,
            LastVisitAt = state.LastVisitAt,
            CreatedAt = state.CreatedAt,
            Segment = state.Stats.Segment.ToString(),
            Tags = state.Tags
        };

        await _indexer.IndexCustomerAsync(document);
    }
}
