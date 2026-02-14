namespace DarkVelocity.Host.State;

public enum StockLevel
{
    OutOfStock,
    Low,
    Normal,
    AbovePar
}

public enum BatchStatus
{
    Active,
    Exhausted,
    Expired,
    WrittenOff
}

[GenerateSerializer]
public record StockBatch
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public string BatchNumber { get; init; } = string.Empty;
    [Id(2)] public DateTime ReceivedDate { get; init; }
    [Id(3)] public DateTime? ExpiryDate { get; init; }
    [Id(4)] public decimal Quantity { get; init; }
    [Id(5)] public decimal OriginalQuantity { get; init; }
    [Id(6)] public decimal UnitCost { get; init; }
    [Id(7)] public decimal TotalCost { get; init; }
    [Id(8)] public Guid? SupplierId { get; init; }
    [Id(9)] public Guid? DeliveryId { get; init; }
    [Id(10)] public Guid? PurchaseOrderLineId { get; init; }
    [Id(11)] public BatchStatus Status { get; init; }
    [Id(12)] public string? Location { get; init; }
    [Id(13)] public string? Notes { get; init; }
    [Id(14)] public Guid? SkuId { get; init; }
}

public enum MovementType
{
    Receipt,
    Consumption,
    Waste,
    Transfer,
    Adjustment,
    Sample
}

[GenerateSerializer]
public record StockMovement
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public DateTime Timestamp { get; init; }
    [Id(2)] public MovementType Type { get; init; }
    [Id(3)] public decimal Quantity { get; init; }
    [Id(4)] public Guid? BatchId { get; init; }
    [Id(5)] public decimal UnitCost { get; init; }
    [Id(6)] public decimal TotalCost { get; init; }
    [Id(7)] public string Reason { get; init; } = string.Empty;
    [Id(8)] public string? ReferenceType { get; init; }
    [Id(9)] public Guid? ReferenceId { get; init; }
    [Id(10)] public Guid PerformedBy { get; init; }
    [Id(11)] public string? Notes { get; init; }
}

[GenerateSerializer]
public sealed class InventoryState
{
    [Id(0)] public Guid IngredientId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public string IngredientName { get; set; } = string.Empty;
    [Id(4)] public string Sku { get; set; } = string.Empty;
    [Id(5)] public string Unit { get; set; } = string.Empty;
    [Id(6)] public string Category { get; set; } = string.Empty;

    [Id(7)] public List<StockBatch> Batches { get; set; } = [];
    [Id(8)] public decimal QuantityOnHand { get; set; }
    [Id(9)] public decimal QuantityReserved { get; set; }
    [Id(10)] public decimal QuantityAvailable { get; set; }
    /// <summary>
    /// Tracks consumption beyond available batches (negative stock).
    /// Per design: "Negative stock is the default - service doesn't stop for inventory discrepancies"
    /// </summary>
    [Id(21)] public decimal UnbatchedDeficit { get; set; }

    [Id(11)] public decimal ReorderPoint { get; set; }
    [Id(12)] public decimal ReorderQuantity { get; set; }
    [Id(13)] public decimal ParLevel { get; set; }
    [Id(14)] public decimal MaxLevel { get; set; }

    [Id(15)] public decimal WeightedAverageCost { get; set; }
    [Id(16)] public StockLevel StockLevel { get; set; }

    [Id(17)] public DateTime? LastReceivedAt { get; set; }
    [Id(18)] public DateTime? LastConsumedAt { get; set; }
    [Id(19)] public DateTime? LastCountedAt { get; set; }

    [Id(20)] public List<StockMovement> RecentMovements { get; set; } = [];
}
