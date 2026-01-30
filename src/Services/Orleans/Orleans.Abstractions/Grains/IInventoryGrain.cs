using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Grains;

public record InitializeInventoryCommand(
    Guid OrganizationId,
    Guid SiteId,
    Guid IngredientId,
    string IngredientName,
    string Sku,
    string Unit,
    string Category,
    decimal ReorderPoint = 0,
    decimal ParLevel = 0);

public record ReceiveBatchCommand(
    string BatchNumber,
    decimal Quantity,
    decimal UnitCost,
    DateTime? ExpiryDate = null,
    Guid? SupplierId = null,
    Guid? DeliveryId = null,
    string? Location = null,
    string? Notes = null,
    Guid? ReceivedBy = null);

public record ConsumeStockCommand(
    decimal Quantity,
    string Reason,
    Guid? OrderId = null,
    Guid? PerformedBy = null);

public record RecordWasteCommand(
    decimal Quantity,
    string Reason,
    string WasteCategory,
    Guid RecordedBy);

public record AdjustQuantityCommand(
    decimal NewQuantity,
    string Reason,
    Guid AdjustedBy,
    Guid? ApprovedBy = null);

public record TransferOutCommand(
    decimal Quantity,
    Guid DestinationSiteId,
    Guid TransferId,
    Guid TransferredBy);

public record ReceiveTransferCommand(
    decimal Quantity,
    decimal UnitCost,
    Guid SourceSiteId,
    Guid TransferId,
    string? BatchNumber = null);

public record BatchReceivedResult(Guid BatchId, decimal NewQuantityOnHand, decimal NewWeightedAverageCost);

public record ConsumptionResult(
    decimal QuantityConsumed,
    decimal TotalCost,
    IReadOnlyList<BatchConsumptionDetail> BatchBreakdown);

public record BatchConsumptionDetail(
    Guid BatchId,
    string BatchNumber,
    decimal Quantity,
    decimal UnitCost,
    decimal TotalCost);

public record InventoryLevelInfo(
    decimal QuantityOnHand,
    decimal QuantityAvailable,
    decimal WeightedAverageCost,
    StockLevel Level,
    DateTime? EarliestExpiry);

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
