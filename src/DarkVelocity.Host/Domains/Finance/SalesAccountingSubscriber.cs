using DarkVelocity.Host.Events;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Stream subscriber that posts sales transactions to the general ledger.
/// Listens to order completion events and creates journal entries for revenue and cash/receivables.
/// </summary>
[ImplicitStreamSubscription(StreamConstants.OrderStreamNamespace)]
public class SalesAccountingSubscriber : Grain, IAsyncObserver<DomainEvent>
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<SalesAccountingSubscriber> _logger;
    private StreamSubscriptionHandle<DomainEvent>? _subscription;

    public SalesAccountingSubscriber(
        IGrainFactory grainFactory,
        ILogger<SalesAccountingSubscriber> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var stream = streamProvider.GetStream<DomainEvent>(
            StreamConstants.OrderStreamNamespace,
            this.GetPrimaryKeyString());

        _subscription = await stream.SubscribeAsync(this);

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

    public async Task OnNextAsync(DomainEvent evt, StreamSequenceToken? token = null)
    {
        try
        {
            switch (evt)
            {
                case OrderCompletedDomainEvent orderCompleted:
                    await PostSalesJournalEntryAsync(orderCompleted);
                    break;

                case OrderVoidedDomainEvent orderVoided:
                    await PostVoidJournalEntryAsync(orderVoided);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing {EventType} for accounting",
                evt.EventType);
        }
    }

    public Task OnCompletedAsync()
    {
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in sales accounting stream subscription");
        return Task.CompletedTask;
    }

    private async Task PostSalesJournalEntryAsync(OrderCompletedDomainEvent evt)
    {
        var orgId = evt.OrgId;
        var journalEntryId = Guid.NewGuid();

        // Create journal entry for the sale
        // Debit: Cash/Bank/Receivables (Asset) - increases
        // Credit: Sales Revenue (Revenue) - increases

        var lines = new List<JournalEntryLineCommand>();

        // Debit to Cash/Card based on payment method
        var cashAccountNumber = evt.PaymentMethod switch
        {
            "Cash" => "1110", // Cash on Hand
            "Card" => "1120", // Cash in Bank (card settlements)
            _ => "1200"       // Accounts Receivable
        };

        lines.Add(new JournalEntryLineCommand(
            cashAccountNumber,
            evt.TotalAmount,
            0,
            $"Payment received for order {evt.OrderNumber}"));

        // Credit to appropriate revenue account
        // In a real implementation, you'd break this down by item category
        lines.Add(new JournalEntryLineCommand(
            "4100", // Food Sales (simplified - would be itemized in real impl)
            0,
            evt.SubTotal,
            $"Food sales - Order {evt.OrderNumber}"));

        // If there's tax, credit the tax payable account
        if (evt.TaxAmount > 0)
        {
            lines.Add(new JournalEntryLineCommand(
                "2300", // Sales Tax Payable
                0,
                evt.TaxAmount,
                $"Sales tax - Order {evt.OrderNumber}"));
        }

        // If there's a discount, debit the sales discount account
        if (evt.DiscountAmount > 0)
        {
            lines.Add(new JournalEntryLineCommand(
                "4900", // Sales Discounts
                evt.DiscountAmount,
                0,
                $"Discount - Order {evt.OrderNumber}"));
        }

        // Create the journal entry
        var journalGrain = _grainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        await journalGrain.CreateAsync(new CreateJournalEntryCommand(
            orgId,
            journalEntryId,
            DateOnly.FromDateTime(evt.OccurredAt),
            lines,
            Guid.Empty, // System-generated
            $"Sales - Order {evt.OrderNumber}",
            ReferenceNumber: evt.OrderNumber,
            ReferenceType: "Order",
            ReferenceId: evt.OrderId,
            AutoPost: true));

        _logger.LogInformation(
            "Posted sales journal entry {JournalEntryId} for order {OrderNumber}, amount {Amount:C}",
            journalEntryId,
            evt.OrderNumber,
            evt.TotalAmount);
    }

    private async Task PostVoidJournalEntryAsync(OrderVoidedDomainEvent evt)
    {
        // Create reversing entry for voided order
        // In a real implementation, you'd find the original entry and reverse it

        _logger.LogInformation(
            "Order {OrderId} voided - reversing journal entry needed",
            evt.OrderId);

        // The actual reversal would look up the original journal entry
        // and call ReverseAsync on it
    }
}

/// <summary>
/// Stream subscriber that posts COGS (Cost of Goods Sold) from inventory consumption.
/// Listens to inventory consumption events and creates journal entries.
/// </summary>
[ImplicitStreamSubscription(StreamConstants.InventoryStreamNamespace)]
public class CogsAccountingSubscriber : Grain, IAsyncObserver<DomainEvent>
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<CogsAccountingSubscriber> _logger;
    private StreamSubscriptionHandle<DomainEvent>? _subscription;

    public CogsAccountingSubscriber(
        IGrainFactory grainFactory,
        ILogger<CogsAccountingSubscriber> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
        var stream = streamProvider.GetStream<DomainEvent>(
            StreamConstants.InventoryStreamNamespace,
            this.GetPrimaryKeyString());

        _subscription = await stream.SubscribeAsync(this);

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

    public async Task OnNextAsync(DomainEvent evt, StreamSequenceToken? token = null)
    {
        try
        {
            switch (evt)
            {
                case InventoryConsumedDomainEvent consumed:
                    await PostCogsJournalEntryAsync(consumed);
                    break;

                case InventoryReceivedDomainEvent received:
                    await PostInventoryReceiptJournalEntryAsync(received);
                    break;

                case InventoryAdjustedDomainEvent adjusted:
                    await PostInventoryAdjustmentJournalEntryAsync(adjusted);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error processing {EventType} for COGS accounting",
                evt.EventType);
        }
    }

    public Task OnCompletedAsync()
    {
        return Task.CompletedTask;
    }

    public Task OnErrorAsync(Exception ex)
    {
        _logger.LogError(ex, "Error in COGS accounting stream subscription");
        return Task.CompletedTask;
    }

    private async Task PostCogsJournalEntryAsync(InventoryConsumedDomainEvent evt)
    {
        var orgId = evt.OrgId;
        var journalEntryId = Guid.NewGuid();

        // COGS journal entry:
        // Debit: Cost of Goods Sold (Expense) - increases expense
        // Credit: Inventory (Asset) - decreases asset

        var cogsAccount = GetCogsAccountForCategory(evt.Category);
        var inventoryAccount = GetInventoryAccountForCategory(evt.Category);

        var lines = new List<JournalEntryLineCommand>
        {
            new(cogsAccount,
                evt.CostAmount,
                0,
                $"COGS - {evt.IngredientName}"),

            new(inventoryAccount,
                0,
                evt.CostAmount,
                $"Inventory consumed - {evt.IngredientName}")
        };

        var journalGrain = _grainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        await journalGrain.CreateAsync(new CreateJournalEntryCommand(
            orgId,
            journalEntryId,
            DateOnly.FromDateTime(evt.OccurredAt),
            lines,
            Guid.Empty, // System-generated
            $"COGS - {evt.IngredientName} ({evt.Quantity} {evt.Unit})",
            ReferenceNumber: evt.OrderId?.ToString(),
            ReferenceType: "InventoryConsumption",
            ReferenceId: evt.IngredientId,
            AutoPost: true));

        _logger.LogInformation(
            "Posted COGS journal entry for {Ingredient}, amount {Amount:C}",
            evt.IngredientName,
            evt.CostAmount);
    }

    private async Task PostInventoryReceiptJournalEntryAsync(InventoryReceivedDomainEvent evt)
    {
        var orgId = evt.OrgId;
        var journalEntryId = Guid.NewGuid();

        // Inventory receipt journal entry:
        // Debit: Inventory (Asset) - increases
        // Credit: Accounts Payable or Cash (Liability/Asset) - depends on payment

        var inventoryAccount = GetInventoryAccountForCategory(evt.Category);

        var lines = new List<JournalEntryLineCommand>
        {
            new(inventoryAccount,
                evt.TotalCost,
                0,
                $"Inventory received - {evt.IngredientName}"),

            new("2100", // Accounts Payable
                0,
                evt.TotalCost,
                $"Payable to {evt.SupplierName}")
        };

        var journalGrain = _grainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        await journalGrain.CreateAsync(new CreateJournalEntryCommand(
            orgId,
            journalEntryId,
            DateOnly.FromDateTime(evt.OccurredAt),
            lines,
            Guid.Empty, // System-generated
            $"Inventory received from {evt.SupplierName}",
            ReferenceNumber: evt.PurchaseOrderNumber,
            ReferenceType: "InventoryReceipt",
            ReferenceId: evt.DeliveryId,
            AutoPost: true));

        _logger.LogInformation(
            "Posted inventory receipt journal entry for {Ingredient}, amount {Amount:C}",
            evt.IngredientName,
            evt.TotalCost);
    }

    private async Task PostInventoryAdjustmentJournalEntryAsync(InventoryAdjustedDomainEvent evt)
    {
        var orgId = evt.OrgId;
        var journalEntryId = Guid.NewGuid();

        // Inventory adjustment (e.g., waste, theft, spoilage):
        // If quantity decreased:
        //   Debit: Expense account (waste, shrinkage)
        //   Credit: Inventory
        // If quantity increased (e.g., found stock):
        //   Debit: Inventory
        //   Credit: Adjustment account

        var inventoryAccount = GetInventoryAccountForCategory(evt.Category);
        var adjustmentCost = Math.Abs(evt.QuantityChange) * evt.UnitCost;

        var lines = new List<JournalEntryLineCommand>();

        if (evt.QuantityChange < 0)
        {
            // Loss/waste
            lines.Add(new JournalEntryLineCommand(
                "6990", // Miscellaneous Expense (could be more specific)
                adjustmentCost,
                0,
                $"Inventory adjustment - {evt.Reason}"));

            lines.Add(new JournalEntryLineCommand(
                inventoryAccount,
                0,
                adjustmentCost,
                $"Inventory reduced - {evt.IngredientName}"));
        }
        else
        {
            // Found/added stock
            lines.Add(new JournalEntryLineCommand(
                inventoryAccount,
                adjustmentCost,
                0,
                $"Inventory increased - {evt.IngredientName}"));

            lines.Add(new JournalEntryLineCommand(
                "4500", // Other Revenue
                0,
                adjustmentCost,
                $"Inventory adjustment gain - {evt.Reason}"));
        }

        var journalGrain = _grainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        await journalGrain.CreateAsync(new CreateJournalEntryCommand(
            orgId,
            journalEntryId,
            DateOnly.FromDateTime(evt.OccurredAt),
            lines,
            Guid.Empty, // System-generated
            $"Inventory adjustment - {evt.Reason}",
            ReferenceType: "InventoryAdjustment",
            ReferenceId: evt.IngredientId,
            AutoPost: true));

        _logger.LogInformation(
            "Posted inventory adjustment journal entry for {Ingredient}, amount {Amount:C}",
            evt.IngredientName,
            adjustmentCost);
    }

    private static string GetCogsAccountForCategory(string? category)
    {
        return category?.ToLowerInvariant() switch
        {
            "food" => "5100",     // Food Cost
            "beverage" => "5200", // Beverage Cost
            "packaging" => "5300", // Packaging Cost
            _ => "5000"           // General COGS
        };
    }

    private static string GetInventoryAccountForCategory(string? category)
    {
        return category?.ToLowerInvariant() switch
        {
            "food" => "1310",     // Food Inventory
            "beverage" => "1320", // Beverage Inventory
            "supplies" => "1330", // Supplies Inventory
            _ => "1300"           // General Inventory
        };
    }
}

// ============================================================================
// Domain Events for Integration
// ============================================================================

/// <summary>
/// Domain event for order completion (consumed by SalesAccountingSubscriber).
/// </summary>
[GenerateSerializer]
public sealed record OrderCompletedDomainEvent : DomainEvent
{
    public override string EventType => "order.completed";
    public override string AggregateType => "Order";
    public override Guid AggregateId => OrderId;

    [Id(100)] public required Guid OrderId { get; init; }
    [Id(101)] public required string OrderNumber { get; init; }
    [Id(102)] public required decimal SubTotal { get; init; }
    [Id(103)] public required decimal TaxAmount { get; init; }
    [Id(104)] public required decimal DiscountAmount { get; init; }
    [Id(105)] public required decimal TotalAmount { get; init; }
    [Id(106)] public required string PaymentMethod { get; init; }
}

/// <summary>
/// Domain event for order void (consumed by SalesAccountingSubscriber).
/// </summary>
[GenerateSerializer]
public sealed record OrderVoidedDomainEvent : DomainEvent
{
    public override string EventType => "order.voided";
    public override string AggregateType => "Order";
    public override Guid AggregateId => OrderId;

    [Id(100)] public required Guid OrderId { get; init; }
    [Id(101)] public required string OrderNumber { get; init; }
    [Id(102)] public required decimal TotalAmount { get; init; }
    [Id(103)] public required string VoidReason { get; init; }
}

/// <summary>
/// Domain event for inventory consumption.
/// </summary>
[GenerateSerializer]
public sealed record InventoryConsumedDomainEvent : DomainEvent
{
    public override string EventType => "inventory.consumed";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    [Id(100)] public required Guid IngredientId { get; init; }
    [Id(101)] public required string IngredientName { get; init; }
    [Id(102)] public required decimal Quantity { get; init; }
    [Id(103)] public required string Unit { get; init; }
    [Id(104)] public required decimal CostAmount { get; init; }
    [Id(105)] public required string? Category { get; init; }
    [Id(106)] public Guid? OrderId { get; init; }
}

/// <summary>
/// Domain event for inventory receipt.
/// </summary>
[GenerateSerializer]
public sealed record InventoryReceivedDomainEvent : DomainEvent
{
    public override string EventType => "inventory.received";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    [Id(100)] public required Guid IngredientId { get; init; }
    [Id(101)] public required string IngredientName { get; init; }
    [Id(102)] public required decimal Quantity { get; init; }
    [Id(103)] public required decimal TotalCost { get; init; }
    [Id(104)] public required string? Category { get; init; }
    [Id(105)] public Guid? SupplierId { get; init; }
    [Id(106)] public string? SupplierName { get; init; }
    [Id(107)] public string? PurchaseOrderNumber { get; init; }
    [Id(108)] public Guid? DeliveryId { get; init; }
}

/// <summary>
/// Domain event for inventory adjustment.
/// </summary>
[GenerateSerializer]
public sealed record InventoryAdjustedDomainEvent : DomainEvent
{
    public override string EventType => "inventory.adjusted";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    [Id(100)] public required Guid IngredientId { get; init; }
    [Id(101)] public required string IngredientName { get; init; }
    [Id(102)] public required decimal QuantityChange { get; init; }
    [Id(103)] public required decimal UnitCost { get; init; }
    [Id(104)] public required string? Category { get; init; }
    [Id(105)] public required string Reason { get; init; }
}
