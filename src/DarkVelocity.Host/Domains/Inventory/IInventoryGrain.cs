using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record InitializeInventoryCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid IngredientId,
    [property: Id(3)] string IngredientName,
    [property: Id(4)] string Sku,
    [property: Id(5)] string Unit,
    [property: Id(6)] string Category,
    [property: Id(7)] decimal ReorderPoint = 0,
    [property: Id(8)] decimal ParLevel = 0);

[GenerateSerializer]
public record ReceiveBatchCommand(
    [property: Id(0)] string BatchNumber,
    [property: Id(1)] decimal Quantity,
    [property: Id(2)] decimal UnitCost,
    [property: Id(3)] DateTime? ExpiryDate = null,
    [property: Id(4)] Guid? SupplierId = null,
    [property: Id(5)] Guid? DeliveryId = null,
    [property: Id(6)] string? Location = null,
    [property: Id(7)] string? Notes = null,
    [property: Id(8)] Guid? ReceivedBy = null);

[GenerateSerializer]
public record ConsumeStockCommand(
    [property: Id(0)] decimal Quantity,
    [property: Id(1)] string Reason,
    [property: Id(2)] Guid? OrderId = null,
    [property: Id(3)] Guid? PerformedBy = null);

[GenerateSerializer]
public record RecordWasteCommand(
    [property: Id(0)] decimal Quantity,
    [property: Id(1)] string Reason,
    [property: Id(2)] string WasteCategory,
    [property: Id(3)] Guid RecordedBy);

[GenerateSerializer]
public record AdjustQuantityCommand(
    [property: Id(0)] decimal NewQuantity,
    [property: Id(1)] string Reason,
    [property: Id(2)] Guid AdjustedBy,
    [property: Id(3)] Guid? ApprovedBy = null);

[GenerateSerializer]
public record TransferOutCommand(
    [property: Id(0)] decimal Quantity,
    [property: Id(1)] Guid DestinationSiteId,
    [property: Id(2)] Guid TransferId,
    [property: Id(3)] Guid TransferredBy);

[GenerateSerializer]
public record ReceiveTransferCommand(
    [property: Id(0)] decimal Quantity,
    [property: Id(1)] decimal UnitCost,
    [property: Id(2)] Guid SourceSiteId,
    [property: Id(3)] Guid TransferId,
    [property: Id(4)] string? BatchNumber = null);

[GenerateSerializer]
public record BatchReceivedResult([property: Id(0)] Guid BatchId, [property: Id(1)] decimal NewQuantityOnHand, [property: Id(2)] decimal NewWeightedAverageCost);

[GenerateSerializer]
public record ConsumptionResult(
    [property: Id(0)] decimal QuantityConsumed,
    [property: Id(1)] decimal TotalCost,
    [property: Id(2)] IReadOnlyList<BatchConsumptionDetail> BatchBreakdown,
    [property: Id(3)] decimal CostOfGoodsConsumed,
    [property: Id(4)] decimal QuantityRemaining);

[GenerateSerializer]
public record BatchConsumptionDetail(
    [property: Id(0)] Guid BatchId,
    [property: Id(1)] string BatchNumber,
    [property: Id(2)] decimal Quantity,
    [property: Id(3)] decimal UnitCost,
    [property: Id(4)] decimal TotalCost);

[GenerateSerializer]
public record InventoryLevelInfo(
    [property: Id(0)] decimal QuantityOnHand,
    [property: Id(1)] decimal QuantityAvailable,
    [property: Id(2)] decimal WeightedAverageCost,
    [property: Id(3)] StockLevel Level,
    [property: Id(4)] DateTime? EarliestExpiry);

public interface IInventoryGrain : IGrainWithStringKey
{
    Task InitializeAsync(InitializeInventoryCommand command);
    Task<InventoryState> GetStateAsync();

    // Receiving
    Task<BatchReceivedResult> ReceiveBatchAsync(ReceiveBatchCommand command);
    Task<BatchReceivedResult> ReceiveTransferAsync(ReceiveTransferCommand command);

    // Consumption (FIFO)
    Task<ConsumptionResult> ConsumeAsync(ConsumeStockCommand command);
    Task<ConsumptionResult> ConsumeForOrderAsync(Guid orderId, decimal quantity, Guid? performedBy);
    Task ReverseConsumptionAsync(Guid movementId, string reason, Guid reversedBy);

    // Waste & Adjustments
    Task RecordWasteAsync(RecordWasteCommand command);
    Task AdjustQuantityAsync(AdjustQuantityCommand command);
    Task RecordPhysicalCountAsync(decimal countedQuantity, Guid countedBy, Guid? approvedBy = null);

    // Transfers
    Task TransferOutAsync(TransferOutCommand command);

    // Batch management
    Task WriteOffExpiredBatchesAsync(Guid performedBy);

    // Configuration
    Task SetReorderPointAsync(decimal reorderPoint);
    Task SetParLevelAsync(decimal parLevel);

    // Queries
    Task<InventoryLevelInfo> GetLevelInfoAsync();
    Task<bool> HasSufficientStockAsync(decimal quantity);
    Task<StockLevel> GetStockLevelAsync();
    Task<IReadOnlyList<StockBatch>> GetActiveBatchesAsync();
    Task<bool> ExistsAsync();
}
