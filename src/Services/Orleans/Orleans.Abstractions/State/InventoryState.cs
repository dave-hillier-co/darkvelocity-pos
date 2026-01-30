namespace DarkVelocity.Orleans.Abstractions.State;

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

public record StockBatch
{
    public Guid Id { get; init; }
    public string BatchNumber { get; init; } = string.Empty;
    public DateTime ReceivedDate { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public decimal Quantity { get; init; }
    public decimal OriginalQuantity { get; init; }
    public decimal UnitCost { get; init; }
    public decimal TotalCost { get; init; }
    public Guid? SupplierId { get; init; }
    public Guid? DeliveryId { get; init; }
    public Guid? PurchaseOrderLineId { get; init; }
    public BatchStatus Status { get; init; }
    public string? Location { get; init; }
    public string? Notes { get; init; }
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

public record StockMovement
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public MovementType Type { get; init; }
    public decimal Quantity { get; init; }
    public Guid? BatchId { get; init; }
    public decimal UnitCost { get; init; }
    public decimal TotalCost { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? ReferenceType { get; init; }
    public Guid? ReferenceId { get; init; }
    public Guid PerformedBy { get; init; }
    public string? Notes { get; init; }
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

    [Id(21)] public int Version { get; set; }
}
