namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Inventory events used in event sourcing.
/// </summary>
public interface IInventoryEvent
{
    Guid IngredientId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record InventoryInitialized : IInventoryEvent
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SiteId { get; init; }
    [Id(3)] public string IngredientName { get; init; } = "";
    [Id(4)] public string Unit { get; init; } = "";
    [Id(5)] public decimal ReorderPoint { get; init; }
    [Id(6)] public decimal ParLevel { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record StockBatchReceived : IInventoryEvent
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public Guid BatchId { get; init; }
    [Id(2)] public string BatchNumber { get; init; } = "";
    [Id(3)] public decimal Quantity { get; init; }
    [Id(4)] public decimal UnitCost { get; init; }
    [Id(5)] public DateOnly? ExpiryDate { get; init; }
    [Id(6)] public Guid? SupplierId { get; init; }
    [Id(7)] public Guid? DeliveryId { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
    [Id(9)] public Guid? SkuId { get; init; }
}

[GenerateSerializer]
public sealed record EventBatchConsumptionDetail
{
    [Id(0)] public Guid BatchId { get; init; }
    [Id(1)] public decimal Quantity { get; init; }
    [Id(2)] public decimal UnitCost { get; init; }
}

[GenerateSerializer]
public sealed record StockConsumed : IInventoryEvent
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public decimal Quantity { get; init; }
    [Id(2)] public decimal CostOfGoodsConsumed { get; init; }
    [Id(3)] public Guid? OrderId { get; init; }
    [Id(4)] public string Reason { get; init; } = "";
    [Id(5)] public DateTime OccurredAt { get; init; }
    [Id(6)] public IReadOnlyList<EventBatchConsumptionDetail> BatchBreakdown { get; init; } = [];
}

[GenerateSerializer]
public sealed record StockAdjusted : IInventoryEvent
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public decimal PreviousQuantity { get; init; }
    [Id(2)] public decimal NewQuantity { get; init; }
    [Id(3)] public decimal Variance { get; init; }
    [Id(4)] public string Reason { get; init; } = "";
    [Id(5)] public Guid AdjustedBy { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record StockWrittenOff : IInventoryEvent
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public decimal Quantity { get; init; }
    [Id(2)] public decimal CostWrittenOff { get; init; }
    [Id(3)] public string Category { get; init; } = ""; // waste, spoilage, theft
    [Id(4)] public string Reason { get; init; } = "";
    [Id(5)] public Guid RecordedBy { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
    [Id(7)] public IReadOnlyList<EventBatchConsumptionDetail> BatchBreakdown { get; init; } = [];
}

[GenerateSerializer]
public sealed record StockTransferredOut : IInventoryEvent
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public Guid TransferId { get; init; }
    [Id(2)] public Guid DestinationSiteId { get; init; }
    [Id(3)] public decimal Quantity { get; init; }
    [Id(4)] public decimal UnitCost { get; init; }
    [Id(5)] public Guid TransferredBy { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
    [Id(7)] public IReadOnlyList<EventBatchConsumptionDetail> BatchBreakdown { get; init; } = [];
}

[GenerateSerializer]
public sealed record StockTransferredIn : IInventoryEvent
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public Guid TransferId { get; init; }
    [Id(2)] public Guid SourceSiteId { get; init; }
    [Id(3)] public decimal Quantity { get; init; }
    [Id(4)] public decimal UnitCost { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record InventorySettingsUpdated : IInventoryEvent
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public decimal? ReorderPoint { get; init; }
    [Id(2)] public decimal? ParLevel { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record LowStockAlertTriggered : IInventoryEvent
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public decimal QuantityOnHand { get; init; }
    [Id(2)] public decimal ReorderPoint { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record StockDepleted : IInventoryEvent
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public Guid? LastConsumingOrderId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}
