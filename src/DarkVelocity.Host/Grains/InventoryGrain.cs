using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Context for creating stock movement transactions.
/// </summary>
public record StockMovementContext(
    MovementType Type,
    decimal UnitCost,
    string Reason,
    Guid? BatchId = null,
    Guid? ReferenceId = null,
    Guid PerformedBy = default);

public class InventoryGrain : LedgerGrain<InventoryState, StockMovement>, IInventoryGrain
{
    private readonly ILogger<InventoryGrain> _logger;
    private IAsyncStream<IStreamEvent>? _inventoryStream;
    private IAsyncStream<IStreamEvent>? _alertStream;

    public InventoryGrain(
        [PersistentState("inventory", "OrleansStorage")]
        IPersistentState<InventoryState> state,
        ILogger<InventoryGrain> logger) : base(state)
    {
        _logger = logger;
    }

    protected override bool IsInitialized => State.State.IngredientId != Guid.Empty;

    protected override StockMovement CreateTransaction(
        decimal amount,
        decimal balanceAfter,
        string? notes,
        object? context)
    {
        var ctx = context as StockMovementContext
            ?? new StockMovementContext(MovementType.Adjustment, 0, notes ?? "Unknown");

        return new StockMovement
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Type = ctx.Type,
            Quantity = amount,
            BatchId = ctx.BatchId,
            UnitCost = ctx.UnitCost,
            TotalCost = Math.Abs(amount) * ctx.UnitCost,
            Reason = ctx.Reason,
            ReferenceId = ctx.ReferenceId,
            PerformedBy = ctx.PerformedBy,
            Notes = notes
        };
    }

    protected override void OnBalanceChanged(decimal previousBalance, decimal newBalance)
    {
        UpdateStockLevel();
    }

    private IAsyncStream<IStreamEvent> GetInventoryStream()
    {
        if (_inventoryStream == null && State.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.InventoryStreamNamespace, State.State.OrganizationId.ToString());
            _inventoryStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _inventoryStream!;
    }

    private IAsyncStream<IStreamEvent> GetAlertStream()
    {
        if (_alertStream == null && State.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.AlertStreamNamespace, State.State.OrganizationId.ToString());
            _alertStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _alertStream!;
    }

    public async Task InitializeAsync(InitializeInventoryCommand command)
    {
        if (State.State.IngredientId != Guid.Empty)
            throw new InvalidOperationException("Inventory already initialized");

        State.State = new InventoryState
        {
            IngredientId = command.IngredientId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            IngredientName = command.IngredientName,
            Sku = command.Sku,
            Unit = command.Unit,
            Category = command.Category,
            ReorderPoint = command.ReorderPoint,
            ParLevel = command.ParLevel,
            Version = 1
        };

        await State.WriteStateAsync();
    }

    public Task<InventoryState> GetStateAsync()
    {
        return Task.FromResult(State.State);
    }

    public async Task<BatchReceivedResult> ReceiveBatchAsync(ReceiveBatchCommand command)
    {
        EnsureExists();

        var batchId = Guid.NewGuid();
        var batch = new StockBatch
        {
            Id = batchId,
            BatchNumber = command.BatchNumber,
            ReceivedDate = DateTime.UtcNow,
            ExpiryDate = command.ExpiryDate,
            Quantity = command.Quantity,
            OriginalQuantity = command.Quantity,
            UnitCost = command.UnitCost,
            TotalCost = command.Quantity * command.UnitCost,
            SupplierId = command.SupplierId,
            DeliveryId = command.DeliveryId,
            Status = BatchStatus.Active,
            Location = command.Location,
            Notes = command.Notes
        };

        State.State.Batches.Add(batch);
        RecalculateQuantitiesAndCost();
        State.State.LastReceivedAt = DateTime.UtcNow;

        RecordMovement(MovementType.Receipt, command.Quantity, command.UnitCost, "Batch received", batchId, command.ReceivedBy ?? Guid.Empty);

        State.State.Version++;
        await State.WriteStateAsync();

        // Publish stock received event
        await GetInventoryStream().OnNextAsync(new StockReceivedEvent(
            State.State.IngredientId,
            State.State.SiteId,
            State.State.IngredientName,
            command.Quantity,
            State.State.Unit,
            command.UnitCost,
            State.State.QuantityOnHand,
            command.BatchNumber,
            command.ExpiryDate.HasValue ? DateOnly.FromDateTime(command.ExpiryDate.Value) : null,
            command.SupplierId,
            command.DeliveryId)
        {
            OrganizationId = State.State.OrganizationId
        });

        _logger.LogInformation(
            "Stock received for {IngredientName}: {Quantity} {Unit} at {UnitCost:C}",
            State.State.IngredientName,
            command.Quantity,
            State.State.Unit,
            command.UnitCost);

        return new BatchReceivedResult(batchId, State.State.QuantityOnHand, State.State.WeightedAverageCost);
    }

    public async Task<BatchReceivedResult> ReceiveTransferAsync(ReceiveTransferCommand command)
    {
        EnsureExists();

        var batchId = Guid.NewGuid();
        var batch = new StockBatch
        {
            Id = batchId,
            BatchNumber = command.BatchNumber ?? $"XFER-{command.TransferId.ToString()[..8]}",
            ReceivedDate = DateTime.UtcNow,
            Quantity = command.Quantity,
            OriginalQuantity = command.Quantity,
            UnitCost = command.UnitCost,
            TotalCost = command.Quantity * command.UnitCost,
            Status = BatchStatus.Active
        };

        State.State.Batches.Add(batch);
        RecalculateQuantitiesAndCost();
        State.State.LastReceivedAt = DateTime.UtcNow;

        RecordMovement(MovementType.Transfer, command.Quantity, command.UnitCost, $"Transfer from site {command.SourceSiteId}", batchId, Guid.Empty, command.TransferId);

        State.State.Version++;
        await State.WriteStateAsync();

        return new BatchReceivedResult(batchId, State.State.QuantityOnHand, State.State.WeightedAverageCost);
    }

    public async Task<ConsumptionResult> ConsumeAsync(ConsumeStockCommand command)
    {
        EnsureExists();

        if (command.Quantity > State.State.QuantityAvailable)
            throw new InvalidOperationException("Insufficient stock");

        var previousLevel = State.State.StockLevel;
        var breakdown = ConsumeFifo(command.Quantity);

        State.State.LastConsumedAt = DateTime.UtcNow;
        RecordMovement(MovementType.Consumption, -command.Quantity, State.State.WeightedAverageCost, command.Reason, null, command.PerformedBy ?? Guid.Empty, command.OrderId);

        State.State.Version++;
        await State.WriteStateAsync();

        var totalCost = breakdown.Sum(b => b.TotalCost);

        // Publish stock consumed event
        await GetInventoryStream().OnNextAsync(new StockConsumedEvent(
            State.State.IngredientId,
            State.State.SiteId,
            State.State.IngredientName,
            command.Quantity,
            State.State.Unit,
            totalCost,
            State.State.QuantityAvailable,
            command.OrderId,
            command.Reason)
        {
            OrganizationId = State.State.OrganizationId
        });

        // Check for stock level events
        await CheckAndPublishStockAlertsAsync(previousLevel, command.OrderId);

        _logger.LogInformation(
            "Stock consumed for {IngredientName}: {Quantity} {Unit}. Remaining: {Remaining}",
            State.State.IngredientName,
            command.Quantity,
            State.State.Unit,
            State.State.QuantityAvailable);

        return new ConsumptionResult(command.Quantity, totalCost, breakdown);
    }

    private async Task CheckAndPublishStockAlertsAsync(StockLevel previousLevel, Guid? lastOrderId = null)
    {
        var currentLevel = State.State.StockLevel;

        // Publish reorder point breached event when crossing threshold
        if (currentLevel == StockLevel.Low && previousLevel != StockLevel.Low)
        {
            var quantityToOrder = State.State.ParLevel - State.State.QuantityAvailable;
            await GetAlertStream().OnNextAsync(new ReorderPointBreachedEvent(
                State.State.IngredientId,
                State.State.SiteId,
                State.State.IngredientName,
                State.State.QuantityAvailable,
                State.State.ReorderPoint,
                State.State.ParLevel,
                quantityToOrder > 0 ? quantityToOrder : 0)
            {
                OrganizationId = State.State.OrganizationId
            });

            _logger.LogWarning(
                "Reorder point breached for {IngredientName}: {Quantity} {Unit} (Reorder point: {ReorderPoint})",
                State.State.IngredientName,
                State.State.QuantityAvailable,
                State.State.Unit,
                State.State.ReorderPoint);
        }

        // Publish stock depleted event
        if (currentLevel == StockLevel.OutOfStock && previousLevel != StockLevel.OutOfStock)
        {
            await GetAlertStream().OnNextAsync(new StockDepletedEvent(
                State.State.IngredientId,
                State.State.SiteId,
                State.State.IngredientName,
                DateTime.UtcNow,
                lastOrderId)
            {
                OrganizationId = State.State.OrganizationId
            });

            _logger.LogError(
                "Stock depleted: {IngredientName} at site {SiteId}",
                State.State.IngredientName,
                State.State.SiteId);
        }
    }

    public Task<ConsumptionResult> ConsumeForOrderAsync(Guid orderId, decimal quantity, Guid? performedBy)
    {
        return ConsumeAsync(new ConsumeStockCommand(quantity, $"Order {orderId}", orderId, performedBy));
    }

    public async Task ReverseConsumptionAsync(Guid movementId, string reason, Guid reversedBy)
    {
        EnsureExists();

        var movement = State.State.RecentMovements.FirstOrDefault(m => m.Id == movementId)
            ?? throw new InvalidOperationException("Movement not found");

        // Create a new batch for the reversed quantity
        var batchId = Guid.NewGuid();
        var batch = new StockBatch
        {
            Id = batchId,
            BatchNumber = $"REV-{movementId.ToString()[..8]}",
            ReceivedDate = DateTime.UtcNow,
            Quantity = Math.Abs(movement.Quantity),
            OriginalQuantity = Math.Abs(movement.Quantity),
            UnitCost = movement.UnitCost,
            TotalCost = Math.Abs(movement.Quantity) * movement.UnitCost,
            Status = BatchStatus.Active,
            Notes = $"Reversed: {reason}"
        };

        State.State.Batches.Add(batch);
        RecalculateQuantitiesAndCost();

        RecordMovement(MovementType.Adjustment, Math.Abs(movement.Quantity), movement.UnitCost, $"Reversal: {reason}", batchId, reversedBy);

        State.State.Version++;
        await State.WriteStateAsync();
    }

    public async Task RecordWasteAsync(RecordWasteCommand command)
    {
        EnsureExists();

        if (command.Quantity > State.State.QuantityAvailable)
            throw new InvalidOperationException("Insufficient stock");

        var breakdown = ConsumeFifo(command.Quantity);
        var totalCost = breakdown.Sum(b => b.TotalCost);

        RecordMovement(MovementType.Waste, -command.Quantity, State.State.WeightedAverageCost, $"{command.WasteCategory}: {command.Reason}", null, command.RecordedBy);

        State.State.Version++;
        await State.WriteStateAsync();
    }

    public async Task AdjustQuantityAsync(AdjustQuantityCommand command)
    {
        EnsureExists();

        var variance = command.NewQuantity - State.State.QuantityOnHand;

        if (variance > 0)
        {
            // Adding stock - create adjustment batch
            var batchId = Guid.NewGuid();
            var batch = new StockBatch
            {
                Id = batchId,
                BatchNumber = $"ADJ-{DateTime.UtcNow:yyyyMMdd}",
                ReceivedDate = DateTime.UtcNow,
                Quantity = variance,
                OriginalQuantity = variance,
                UnitCost = State.State.WeightedAverageCost,
                TotalCost = variance * State.State.WeightedAverageCost,
                Status = BatchStatus.Active,
                Notes = command.Reason
            };
            State.State.Batches.Add(batch);
        }
        else if (variance < 0)
        {
            // Removing stock - consume FIFO
            ConsumeFifo(Math.Abs(variance));
        }

        RecalculateQuantitiesAndCost();
        RecordMovement(MovementType.Adjustment, variance, State.State.WeightedAverageCost, command.Reason, null, command.AdjustedBy);

        State.State.LastCountedAt = DateTime.UtcNow;
        State.State.Version++;
        await State.WriteStateAsync();
    }

    public async Task RecordPhysicalCountAsync(decimal countedQuantity, Guid countedBy, Guid? approvedBy = null)
    {
        await AdjustQuantityAsync(new AdjustQuantityCommand(countedQuantity, "Physical count", countedBy, approvedBy));
    }

    public async Task TransferOutAsync(TransferOutCommand command)
    {
        EnsureExists();

        if (command.Quantity > State.State.QuantityAvailable)
            throw new InvalidOperationException("Insufficient stock for transfer");

        var breakdown = ConsumeFifo(command.Quantity);
        RecordMovement(MovementType.Transfer, -command.Quantity, State.State.WeightedAverageCost, $"Transfer to site {command.DestinationSiteId}", null, command.TransferredBy, command.TransferId);

        State.State.Version++;
        await State.WriteStateAsync();
    }

    public async Task WriteOffExpiredBatchesAsync(Guid performedBy)
    {
        EnsureExists();

        var now = DateTime.UtcNow;
        var expiredBatches = State.State.Batches
            .Where(b => b.Status == BatchStatus.Active && b.ExpiryDate.HasValue && b.ExpiryDate.Value < now)
            .ToList();

        foreach (var batch in expiredBatches)
        {
            var index = State.State.Batches.FindIndex(b => b.Id == batch.Id);
            State.State.Batches[index] = batch with { Status = BatchStatus.WrittenOff, Quantity = 0 };

            RecordMovement(MovementType.Waste, -batch.Quantity, batch.UnitCost, "Expired batch write-off", batch.Id, performedBy);
        }

        RecalculateQuantitiesAndCost();
        State.State.Version++;
        await State.WriteStateAsync();
    }

    public async Task SetReorderPointAsync(decimal reorderPoint)
    {
        EnsureExists();
        State.State.ReorderPoint = reorderPoint;
        UpdateStockLevel();
        State.State.Version++;
        await State.WriteStateAsync();
    }

    public async Task SetParLevelAsync(decimal parLevel)
    {
        EnsureExists();
        State.State.ParLevel = parLevel;
        UpdateStockLevel();
        State.State.Version++;
        await State.WriteStateAsync();
    }

    public Task<InventoryLevelInfo> GetLevelInfoAsync()
    {
        DateTime? earliestExpiry = State.State.Batches
            .Where(b => b.Status == BatchStatus.Active && b.ExpiryDate.HasValue)
            .Select(b => b.ExpiryDate)
            .Min();

        return Task.FromResult(new InventoryLevelInfo(
            State.State.QuantityOnHand,
            State.State.QuantityAvailable,
            State.State.WeightedAverageCost,
            State.State.StockLevel,
            earliestExpiry));
    }

    public Task<bool> HasSufficientStockAsync(decimal quantity)
    {
        return Task.FromResult(State.State.QuantityAvailable >= quantity);
    }

    public Task<StockLevel> GetStockLevelAsync()
    {
        return Task.FromResult(State.State.StockLevel);
    }

    public Task<IReadOnlyList<StockBatch>> GetActiveBatchesAsync()
    {
        var active = State.State.Batches.Where(b => b.Status == BatchStatus.Active).ToList();
        return Task.FromResult<IReadOnlyList<StockBatch>>(active);
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.State.IngredientId != Guid.Empty);

    private void EnsureExists() => EnsureInitialized();

    private List<BatchConsumptionDetail> ConsumeFifo(decimal quantity)
    {
        var remaining = quantity;
        var breakdown = new List<BatchConsumptionDetail>();

        // Order by received date (oldest first) for FIFO
        var activeBatches = State.State.Batches
            .Where(b => b.Status == BatchStatus.Active && b.Quantity > 0)
            .OrderBy(b => b.ReceivedDate)
            .ToList();

        foreach (var batch in activeBatches)
        {
            if (remaining <= 0) break;

            var consumeQty = Math.Min(remaining, batch.Quantity);
            var index = State.State.Batches.FindIndex(b => b.Id == batch.Id);

            var newQty = batch.Quantity - consumeQty;
            var newStatus = newQty <= 0 ? BatchStatus.Exhausted : batch.Status;

            State.State.Batches[index] = batch with { Quantity = newQty, Status = newStatus };

            breakdown.Add(new BatchConsumptionDetail(batch.Id, batch.BatchNumber, consumeQty, batch.UnitCost, consumeQty * batch.UnitCost));
            remaining -= consumeQty;
        }

        RecalculateQuantitiesAndCost();
        return breakdown;
    }

    private void RecalculateQuantitiesAndCost()
    {
        var activeBatches = State.State.Batches.Where(b => b.Status == BatchStatus.Active);

        State.State.QuantityOnHand = activeBatches.Sum(b => b.Quantity);
        State.State.QuantityAvailable = State.State.QuantityOnHand - State.State.QuantityReserved;

        var totalValue = activeBatches.Sum(b => b.Quantity * b.UnitCost);
        State.State.WeightedAverageCost = State.State.QuantityOnHand > 0
            ? totalValue / State.State.QuantityOnHand
            : 0;

        UpdateStockLevel();
    }

    private void UpdateStockLevel()
    {
        if (State.State.QuantityAvailable <= 0)
            State.State.StockLevel = StockLevel.OutOfStock;
        else if (State.State.QuantityAvailable <= State.State.ReorderPoint)
            State.State.StockLevel = StockLevel.Low;
        else if (State.State.QuantityAvailable > State.State.ParLevel && State.State.ParLevel > 0)
            State.State.StockLevel = StockLevel.AbovePar;
        else
            State.State.StockLevel = StockLevel.Normal;
    }

    private void RecordMovement(MovementType type, decimal quantity, decimal unitCost, string reason, Guid? batchId, Guid performedBy, Guid? referenceId = null)
    {
        var movement = new StockMovement
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Type = type,
            Quantity = quantity,
            BatchId = batchId,
            UnitCost = unitCost,
            TotalCost = Math.Abs(quantity) * unitCost,
            Reason = reason,
            ReferenceId = referenceId,
            PerformedBy = performedBy
        };

        State.State.RecentMovements.Add(movement);

        // Keep only last 100 movements
        if (State.State.RecentMovements.Count > 100)
            State.State.RecentMovements.RemoveAt(0);
    }
}
