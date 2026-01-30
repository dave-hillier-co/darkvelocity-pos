namespace DarkVelocity.Shared.Contracts.Events;

// ============================================================================
// Receipt Events
// ============================================================================

/// <summary>
/// Stock batch received from supplier delivery.
/// </summary>
public sealed record StockBatchReceived : DomainEvent
{
    public override string EventType => "inventory.batch.received";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid BatchId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required string BatchNumber { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required decimal UnitCost { get; init; }
    public required decimal TotalCost { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public Guid? SupplierId { get; init; }
    public string? SupplierName { get; init; }
    public Guid? DeliveryId { get; init; }
    public Guid? PurchaseOrderId { get; init; }
    public Guid? PurchaseOrderLineId { get; init; }
    public string? Location { get; init; }
    public string? Notes { get; init; }
    public Guid ReceivedBy { get; init; }
}

/// <summary>
/// Full delivery received from supplier.
/// </summary>
public sealed record DeliveryReceived : DomainEvent
{
    public override string EventType => "inventory.delivery.received";
    public override string AggregateType => "Delivery";
    public override Guid AggregateId => DeliveryId;

    public required Guid DeliveryId { get; init; }
    public required Guid SupplierId { get; init; }
    public required string SupplierName { get; init; }
    public Guid? PurchaseOrderId { get; init; }
    public required string DeliveryReference { get; init; }
    public required DateTime DeliveryDate { get; init; }
    public required IReadOnlyList<DeliveryLineItem> Lines { get; init; }
    public required decimal TotalValue { get; init; }
    public required Guid ReceivedBy { get; init; }
    public string? Notes { get; init; }
}

public sealed record DeliveryLineItem
{
    public required Guid LineId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal OrderedQuantity { get; init; }
    public required decimal ReceivedQuantity { get; init; }
    public required string Unit { get; init; }
    public required decimal UnitCost { get; init; }
    public Guid? BatchId { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public string? BatchNumber { get; init; }
}

/// <summary>
/// Stock received via inter-site transfer.
/// </summary>
public sealed record TransferReceived : DomainEvent
{
    public override string EventType => "inventory.transfer.received";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid TransferId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required Guid SourceSiteId { get; init; }
    public required string SourceSiteName { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required decimal UnitCost { get; init; }
    public required decimal TotalCost { get; init; }
    public Guid? BatchId { get; init; }
    public string? BatchNumber { get; init; }
    public DateTime? ExpiryDate { get; init; }
    public required Guid ReceivedBy { get; init; }
}

/// <summary>
/// Receipt quantity adjusted after initial recording.
/// </summary>
public sealed record ReceiptAdjusted : DomainEvent
{
    public override string EventType => "inventory.receipt.adjusted";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid BatchId { get; init; }
    public required Guid IngredientId { get; init; }
    public required decimal OriginalQuantity { get; init; }
    public required decimal AdjustedQuantity { get; init; }
    public required decimal Variance { get; init; }
    public required string Reason { get; init; }
    public required Guid AdjustedBy { get; init; }
    public Guid? ApprovedBy { get; init; }
}

/// <summary>
/// Batch rejected on quality grounds.
/// </summary>
public sealed record BatchRejected : DomainEvent
{
    public override string EventType => "inventory.batch.rejected";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid BatchId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required decimal UnitCost { get; init; }
    public required RejectionReason Reason { get; init; }
    public required string ReasonDetails { get; init; }
    public Guid? SupplierId { get; init; }
    public Guid? DeliveryId { get; init; }
    public required Guid RejectedBy { get; init; }
    public string? PhotoUrl { get; init; }
}

public enum RejectionReason
{
    QualityIssue,
    DamagedPackaging,
    IncorrectProduct,
    TemperatureAbuse,
    ShortExpiry,
    Contamination,
    Other
}

// ============================================================================
// Consumption Events
// ============================================================================

/// <summary>
/// Stock consumed from inventory (generic consumption).
/// </summary>
public sealed record StockConsumed : DomainEvent
{
    public override string EventType => "inventory.stock.consumed";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required IReadOnlyList<BatchConsumptionBreakdown> BatchBreakdown { get; init; }
    public required decimal TotalCost { get; init; }
    public required string CostingMethod { get; init; } // FIFO, WAC, Standard
    public required ConsumptionReason Reason { get; init; }
    public string? ReasonDetails { get; init; }
    public Guid? ReferenceId { get; init; }
    public string? ReferenceType { get; init; }
    public required Guid ConsumedBy { get; init; }
}

public sealed record BatchConsumptionBreakdown
{
    public required Guid BatchId { get; init; }
    public required string BatchNumber { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitCost { get; init; }
    public required decimal TotalCost { get; init; }
    public required decimal RemainingQuantity { get; init; }
    public DateTime? ExpiryDate { get; init; }
}

public enum ConsumptionReason
{
    SaleOrder,
    Waste,
    Sample,
    Transfer,
    Adjustment,
    Production,
    LineCleaning,
    Training,
    Other
}

/// <summary>
/// Stock consumed for a specific order with theoretical comparison.
/// </summary>
public sealed record StockConsumedForOrder : DomainEvent
{
    public override string EventType => "inventory.stock.consumed_for_order";
    public override string AggregateType => "Order";
    public override Guid AggregateId => OrderId;

    public required Guid OrderId { get; init; }
    public required string OrderNumber { get; init; }
    public required Guid MenuItemId { get; init; }
    public required string MenuItemName { get; init; }
    public required int Quantity { get; init; }
    public Guid? RecipeVersionId { get; init; }
    public required IReadOnlyList<IngredientConsumptionDetail> Ingredients { get; init; }
    public required decimal TheoreticalCOGS { get; init; }
    public required decimal ActualCOGS { get; init; }
    public required decimal Variance { get; init; }
    public required decimal VariancePercent { get; init; }
}

public sealed record IngredientConsumptionDetail
{
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal TheoreticalQuantity { get; init; }
    public required decimal ActualQuantity { get; init; }
    public required decimal TheoreticalCost { get; init; }
    public required decimal ActualCost { get; init; }
    public required IReadOnlyList<BatchConsumptionBreakdown> BatchBreakdown { get; init; }
}

/// <summary>
/// Stock wasted (spoilage, spillage, etc.).
/// </summary>
public sealed record StockWasted : DomainEvent
{
    public override string EventType => "inventory.stock.wasted";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid WasteId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public Guid? BatchId { get; init; }
    public string? BatchNumber { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required WasteReason Reason { get; init; }
    public required string ReasonDetails { get; init; }
    public required decimal CostBasis { get; init; }
    public required Guid RecordedBy { get; init; }
    public Guid? ApprovedBy { get; init; }
    public string? PhotoUrl { get; init; }
}

public enum WasteReason
{
    Spoilage,
    Expired,
    LineCleaning,
    Breakage,
    OverProduction,
    CustomerReturn,
    QualityRejection,
    SpillageAccident,
    Theft,
    PrepWaste,
    Other
}

/// <summary>
/// Stock sampled for quality testing or tasting.
/// </summary>
public sealed record StockSampled : DomainEvent
{
    public override string EventType => "inventory.stock.sampled";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public Guid? BatchId { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required string Purpose { get; init; }
    public required decimal CostBasis { get; init; }
    public required Guid SampledBy { get; init; }
}

/// <summary>
/// Stock transferred out to another site.
/// </summary>
public sealed record StockTransferredOut : DomainEvent
{
    public override string EventType => "inventory.stock.transferred_out";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid TransferId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required Guid DestinationSiteId { get; init; }
    public required string DestinationSiteName { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required decimal UnitCost { get; init; }
    public required decimal TotalCost { get; init; }
    public required IReadOnlyList<BatchConsumptionBreakdown> BatchBreakdown { get; init; }
    public required Guid TransferredBy { get; init; }
}

/// <summary>
/// Consumption reversed (e.g., voided order).
/// </summary>
public sealed record ConsumptionReversed : DomainEvent
{
    public override string EventType => "inventory.consumption.reversed";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid OriginalMovementId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal CostBasis { get; init; }
    public required string Reason { get; init; }
    public Guid? OrderId { get; init; }
    public required Guid ReversedBy { get; init; }
}

// ============================================================================
// Batch Lifecycle Events
// ============================================================================

/// <summary>
/// Batch fully consumed.
/// </summary>
public sealed record BatchExhausted : DomainEvent
{
    public override string EventType => "inventory.batch.exhausted";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid BatchId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string BatchNumber { get; init; }
    public required decimal OriginalQuantity { get; init; }
    public required decimal TotalConsumed { get; init; }
    public required decimal TotalWasted { get; init; }
    public required DateTime ReceivedDate { get; init; }
    public required DateTime ExhaustedDate { get; init; }
    public required int DaysInInventory { get; init; }
}

/// <summary>
/// Batch expired.
/// </summary>
public sealed record BatchExpired : DomainEvent
{
    public override string EventType => "inventory.batch.expired";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid BatchId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required string BatchNumber { get; init; }
    public required decimal RemainingQuantity { get; init; }
    public required decimal ValueAtRisk { get; init; }
    public required DateTime ExpiryDate { get; init; }
}

/// <summary>
/// Expired batch written off.
/// </summary>
public sealed record BatchWrittenOff : DomainEvent
{
    public override string EventType => "inventory.batch.written_off";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid BatchId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required string BatchNumber { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitCost { get; init; }
    public required decimal TotalCost { get; init; }
    public required string Reason { get; init; }
    public required Guid PerformedBy { get; init; }
    public Guid? ApprovedBy { get; init; }
}

/// <summary>
/// Multiple batches merged into one.
/// </summary>
public sealed record BatchesMerged : DomainEvent
{
    public override string EventType => "inventory.batches.merged";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid NewBatchId { get; init; }
    public required Guid IngredientId { get; init; }
    public required IReadOnlyList<Guid> SourceBatchIds { get; init; }
    public required decimal TotalQuantity { get; init; }
    public required decimal WeightedAverageCost { get; init; }
    public required DateTime? EarliestExpiry { get; init; }
    public required string NewBatchNumber { get; init; }
    public required Guid MergedBy { get; init; }
}

/// <summary>
/// Stock frozen for preservation.
/// </summary>
public sealed record StockFrozen : DomainEvent
{
    public override string EventType => "inventory.stock.frozen";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid BatchId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal Quantity { get; init; }
    public required DateTime FrozenAt { get; init; }
    public required DateTime OriginalExpiryDate { get; init; }
    public DateTime? ExtendedExpiryDate { get; init; }
    public required Guid FrozenBy { get; init; }
}

/// <summary>
/// Stock defrosted for use.
/// </summary>
public sealed record StockDefrosted : DomainEvent
{
    public override string EventType => "inventory.stock.defrosted";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid BatchId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal Quantity { get; init; }
    public required DateTime DefrostedAt { get; init; }
    public required DateTime UseByDate { get; init; }
    public required Guid DefrostedBy { get; init; }
}

/// <summary>
/// Container unpacked into batches.
/// </summary>
public sealed record ContainerUnpacked : DomainEvent
{
    public override string EventType => "inventory.container.unpacked";
    public override string AggregateType => "Container";
    public override Guid AggregateId => ContainerId;

    public required Guid ContainerId { get; init; }
    public required string ContainerCode { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required IReadOnlyList<UnpackedBatch> Batches { get; init; }
    public required Guid UnpackedBy { get; init; }
}

public sealed record UnpackedBatch
{
    public required Guid BatchId { get; init; }
    public required string BatchNumber { get; init; }
    public required decimal Quantity { get; init; }
    public required decimal UnitCost { get; init; }
    public DateTime? ExpiryDate { get; init; }
}

// ============================================================================
// Level Monitoring Events
// ============================================================================

/// <summary>
/// Low stock alert triggered.
/// </summary>
public sealed record LowStockAlertTriggered : DomainEvent
{
    public override string EventType => "inventory.alert.low_stock";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid AlertId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required string Sku { get; init; }
    public required decimal CurrentQuantity { get; init; }
    public required decimal ReorderPoint { get; init; }
    public required decimal ParLevel { get; init; }
    public required string Unit { get; init; }
    public required int DaysOfSupply { get; init; }
}

/// <summary>
/// Out of stock alert triggered.
/// </summary>
public sealed record OutOfStockAlertTriggered : DomainEvent
{
    public override string EventType => "inventory.alert.out_of_stock";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid AlertId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required string Sku { get; init; }
    public required DateTime LastConsumedAt { get; init; }
    public required int AffectedMenuItems { get; init; }
}

/// <summary>
/// Stock level returned to normal.
/// </summary>
public sealed record StockLevelNormalized : DomainEvent
{
    public override string EventType => "inventory.alert.normalized";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid AlertId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal PreviousQuantity { get; init; }
    public required decimal CurrentQuantity { get; init; }
    public required decimal ParLevel { get; init; }
}

/// <summary>
/// Par level exceeded (overstocking).
/// </summary>
public sealed record ParLevelExceeded : DomainEvent
{
    public override string EventType => "inventory.alert.par_exceeded";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid AlertId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal CurrentQuantity { get; init; }
    public required decimal ParLevel { get; init; }
    public required decimal MaxLevel { get; init; }
    public required decimal ExcessQuantity { get; init; }
    public required decimal ExcessValue { get; init; }
}

// ============================================================================
// Adjustment Events
// ============================================================================

/// <summary>
/// Quantity adjusted (count correction).
/// </summary>
public sealed record QuantityAdjusted : DomainEvent
{
    public override string EventType => "inventory.quantity.adjusted";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid AdjustmentId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal PreviousQuantity { get; init; }
    public required decimal NewQuantity { get; init; }
    public required decimal Variance { get; init; }
    public required decimal VarianceValue { get; init; }
    public required string Reason { get; init; }
    public required Guid AdjustedBy { get; init; }
    public Guid? ApprovedBy { get; init; }
}

/// <summary>
/// Physical count recorded (stock take).
/// </summary>
public sealed record PhysicalCountRecorded : DomainEvent
{
    public override string EventType => "inventory.count.recorded";
    public override string AggregateType => "StockTake";
    public override Guid AggregateId => StockTakeId;

    public required Guid StockTakeId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal SystemQuantity { get; init; }
    public required decimal CountedQuantity { get; init; }
    public required decimal Variance { get; init; }
    public required decimal VariancePercent { get; init; }
    public required decimal VarianceValue { get; init; }
    public required Guid CountedBy { get; init; }
    public Guid? VerifiedBy { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Cost adjusted for a batch.
/// </summary>
public sealed record CostAdjusted : DomainEvent
{
    public override string EventType => "inventory.cost.adjusted";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid BatchId { get; init; }
    public required Guid IngredientId { get; init; }
    public required decimal PreviousUnitCost { get; init; }
    public required decimal NewUnitCost { get; init; }
    public required decimal Variance { get; init; }
    public required string Reason { get; init; }
    public Guid? InvoiceId { get; init; }
    public required Guid AdjustedBy { get; init; }
}

/// <summary>
/// Weighted average cost recalculated.
/// </summary>
public sealed record WeightedAverageCostRecalculated : DomainEvent
{
    public override string EventType => "inventory.wac.recalculated";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => IngredientId;

    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal PreviousWAC { get; init; }
    public required decimal NewWAC { get; init; }
    public required decimal TriggeringBatchQty { get; init; }
    public required decimal TriggeringBatchCost { get; init; }
    public required decimal TotalQuantity { get; init; }
    public required decimal TotalValue { get; init; }
    public Guid? TriggeringBatchId { get; init; }
}

// ============================================================================
// Transfer Events
// ============================================================================

/// <summary>
/// Inter-site transfer initiated.
/// </summary>
public sealed record TransferInitiated : DomainEvent
{
    public override string EventType => "inventory.transfer.initiated";
    public override string AggregateType => "Transfer";
    public override Guid AggregateId => TransferId;

    public required Guid TransferId { get; init; }
    public required string TransferNumber { get; init; }
    public required Guid SourceSiteId { get; init; }
    public required string SourceSiteName { get; init; }
    public required Guid DestinationSiteId { get; init; }
    public required string DestinationSiteName { get; init; }
    public required IReadOnlyList<TransferLineItem> Lines { get; init; }
    public required decimal TotalValue { get; init; }
    public required Guid InitiatedBy { get; init; }
    public DateTime? ExpectedArrival { get; init; }
    public string? Notes { get; init; }
}

public sealed record TransferLineItem
{
    public required Guid LineId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal Quantity { get; init; }
    public required string Unit { get; init; }
    public required decimal UnitCost { get; init; }
    public Guid? BatchId { get; init; }
}

/// <summary>
/// Transfer shipped/in transit.
/// </summary>
public sealed record TransferSent : DomainEvent
{
    public override string EventType => "inventory.transfer.sent";
    public override string AggregateType => "Transfer";
    public override Guid AggregateId => TransferId;

    public required Guid TransferId { get; init; }
    public required Guid SourceSiteId { get; init; }
    public required Guid DestinationSiteId { get; init; }
    public required DateTime SentAt { get; init; }
    public required Guid SentBy { get; init; }
    public string? TrackingNumber { get; init; }
    public string? Carrier { get; init; }
}

/// <summary>
/// Transfer completed (received at destination).
/// </summary>
public sealed record TransferCompleted : DomainEvent
{
    public override string EventType => "inventory.transfer.completed";
    public override string AggregateType => "Transfer";
    public override Guid AggregateId => TransferId;

    public required Guid TransferId { get; init; }
    public required Guid SourceSiteId { get; init; }
    public required Guid DestinationSiteId { get; init; }
    public required IReadOnlyList<TransferReceivedLine> Lines { get; init; }
    public required decimal TotalReceived { get; init; }
    public required decimal Variance { get; init; }
    public required Guid ReceivedBy { get; init; }
    public required DateTime ReceivedAt { get; init; }
}

public sealed record TransferReceivedLine
{
    public required Guid LineId { get; init; }
    public required Guid IngredientId { get; init; }
    public required decimal ExpectedQuantity { get; init; }
    public required decimal ReceivedQuantity { get; init; }
    public required decimal Variance { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// Transfer cancelled.
/// </summary>
public sealed record TransferCancelled : DomainEvent
{
    public override string EventType => "inventory.transfer.cancelled";
    public override string AggregateType => "Transfer";
    public override Guid AggregateId => TransferId;

    public required Guid TransferId { get; init; }
    public required string Reason { get; init; }
    public required Guid CancelledBy { get; init; }
    public required bool StockReturned { get; init; }
}

// ============================================================================
// Yield Tracking Events
// ============================================================================

/// <summary>
/// Yield recorded for batch (kegs, roasts, etc.).
/// </summary>
public sealed record YieldRecorded : DomainEvent
{
    public override string EventType => "inventory.yield.recorded";
    public override string AggregateType => "Inventory";
    public override Guid AggregateId => BatchId;

    public required Guid BatchId { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required decimal ExpectedYield { get; init; }
    public required decimal ActualYield { get; init; }
    public required decimal YieldPercentage { get; init; }
    public required decimal Variance { get; init; }
    public required string Unit { get; init; }
    public required Guid RecordedBy { get; init; }
    public string? Notes { get; init; }
}
