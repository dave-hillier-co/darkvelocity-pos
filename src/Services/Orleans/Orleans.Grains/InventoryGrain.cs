using DarkVelocity.Orleans.Abstractions;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using Orleans.Runtime;

namespace DarkVelocity.Orleans.Grains;

public class InventoryGrain : Grain, IInventoryGrain
{
    private readonly IPersistentState<InventoryState> _state;

    public InventoryGrain(
        [PersistentState("inventory", "OrleansStorage")]
        IPersistentState<InventoryState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(InitializeInventoryCommand command)
    {
        if (_state.State.IngredientId != Guid.Empty)
            throw new InvalidOperationException("Inventory already initialized");

        _state.State = new InventoryState
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

        await _state.WriteStateAsync();
    }

    public Task<InventoryState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
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

        _state.State.Batches.Add(batch);
        RecalculateQuantitiesAndCost();
        _state.State.LastReceivedAt = DateTime.UtcNow;

        RecordMovement(MovementType.Receipt, command.Quantity, command.UnitCost, "Batch received", batchId, command.ReceivedBy ?? Guid.Empty);

        _state.State.Version++;
        await _state.WriteStateAsync();

        return new BatchReceivedResult(batchId, _state.State.QuantityOnHand, _state.State.WeightedAverageCost);
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

        _state.State.Batches.Add(batch);
        RecalculateQuantitiesAndCost();
        _state.State.LastReceivedAt = DateTime.UtcNow;

        RecordMovement(MovementType.Transfer, command.Quantity, command.UnitCost, $"Transfer from site {command.SourceSiteId}", batchId, Guid.Empty, command.TransferId);

        _state.State.Version++;
        await _state.WriteStateAsync();

        return new BatchReceivedResult(batchId, _state.State.QuantityOnHand, _state.State.WeightedAverageCost);
    }

    public async Task<ConsumptionResult> ConsumeAsync(ConsumeStockCommand command)
    {
        EnsureExists();

        if (command.Quantity > _state.State.QuantityAvailable)
            throw new InvalidOperationException("Insufficient stock");

        var breakdown = ConsumeFifo(command.Quantity);

        _state.State.LastConsumedAt = DateTime.UtcNow;
        RecordMovement(MovementType.Consumption, -command.Quantity, _state.State.WeightedAverageCost, command.Reason, null, command.PerformedBy ?? Guid.Empty, command.OrderId);

        _state.State.Version++;
        await _state.WriteStateAsync();

        var totalCost = breakdown.Sum(b => b.TotalCost);
        return new ConsumptionResult(command.Quantity, totalCost, breakdown);
    }

    public Task<ConsumptionResult> ConsumeForOrderAsync(Guid orderId, decimal quantity, Guid? performedBy)
    {
        return ConsumeAsync(new ConsumeStockCommand(quantity, $"Order {orderId}", orderId, performedBy));
    }

    public async Task ReverseConsumptionAsync(Guid movementId, string reason, Guid reversedBy)
    {
        EnsureExists();

        var movement = _state.State.RecentMovements.FirstOrDefault(m => m.Id == movementId)
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

        _state.State.Batches.Add(batch);
        RecalculateQuantitiesAndCost();

        RecordMovement(MovementType.Adjustment, Math.Abs(movement.Quantity), movement.UnitCost, $"Reversal: {reason}", batchId, reversedBy);

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RecordWasteAsync(RecordWasteCommand command)
    {
        EnsureExists();

        if (command.Quantity > _state.State.QuantityAvailable)
            throw new InvalidOperationException("Insufficient stock");

        var breakdown = ConsumeFifo(command.Quantity);
        var totalCost = breakdown.Sum(b => b.TotalCost);

        RecordMovement(MovementType.Waste, -command.Quantity, _state.State.WeightedAverageCost, $"{command.WasteCategory}: {command.Reason}", null, command.RecordedBy);

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task AdjustQuantityAsync(AdjustQuantityCommand command)
    {
        EnsureExists();

        var variance = command.NewQuantity - _state.State.QuantityOnHand;

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
                UnitCost = _state.State.WeightedAverageCost,
                TotalCost = variance * _state.State.WeightedAverageCost,
                Status = BatchStatus.Active,
                Notes = command.Reason
            };
            _state.State.Batches.Add(batch);
        }
        else if (variance < 0)
        {
            // Removing stock - consume FIFO
            ConsumeFifo(Math.Abs(variance));
        }

        RecalculateQuantitiesAndCost();
        RecordMovement(MovementType.Adjustment, variance, _state.State.WeightedAverageCost, command.Reason, null, command.AdjustedBy);

        _state.State.LastCountedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RecordPhysicalCountAsync(decimal countedQuantity, Guid countedBy, Guid? approvedBy = null)
    {
        await AdjustQuantityAsync(new AdjustQuantityCommand(countedQuantity, "Physical count", countedBy, approvedBy));
    }

    public async Task TransferOutAsync(TransferOutCommand command)
    {
        EnsureExists();

        if (command.Quantity > _state.State.QuantityAvailable)
            throw new InvalidOperationException("Insufficient stock for transfer");

        var breakdown = ConsumeFifo(command.Quantity);
        RecordMovement(MovementType.Transfer, -command.Quantity, _state.State.WeightedAverageCost, $"Transfer to site {command.DestinationSiteId}", null, command.TransferredBy, command.TransferId);

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task WriteOffExpiredBatchesAsync(Guid performedBy)
    {
        EnsureExists();

        var now = DateTime.UtcNow;
        var expiredBatches = _state.State.Batches
            .Where(b => b.Status == BatchStatus.Active && b.ExpiryDate.HasValue && b.ExpiryDate.Value < now)
            .ToList();

        foreach (var batch in expiredBatches)
        {
            var index = _state.State.Batches.FindIndex(b => b.Id == batch.Id);
            _state.State.Batches[index] = batch with { Status = BatchStatus.WrittenOff, Quantity = 0 };

            RecordMovement(MovementType.Waste, -batch.Quantity, batch.UnitCost, "Expired batch write-off", batch.Id, performedBy);
        }

        RecalculateQuantitiesAndCost();
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetReorderPointAsync(decimal reorderPoint)
    {
        EnsureExists();
        _state.State.ReorderPoint = reorderPoint;
        UpdateStockLevel();
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetParLevelAsync(decimal parLevel)
    {
        EnsureExists();
        _state.State.ParLevel = parLevel;
        UpdateStockLevel();
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<InventoryLevelInfo> GetLevelInfoAsync()
    {
        DateTime? earliestExpiry = _state.State.Batches
            .Where(b => b.Status == BatchStatus.Active && b.ExpiryDate.HasValue)
            .Select(b => b.ExpiryDate)
            .Min();

        return Task.FromResult(new InventoryLevelInfo(
            _state.State.QuantityOnHand,
            _state.State.QuantityAvailable,
            _state.State.WeightedAverageCost,
            _state.State.StockLevel,
            earliestExpiry));
    }

    public Task<bool> HasSufficientStockAsync(decimal quantity)
    {
        return Task.FromResult(_state.State.QuantityAvailable >= quantity);
    }

    public Task<StockLevel> GetStockLevelAsync()
    {
        return Task.FromResult(_state.State.StockLevel);
    }

    public Task<IReadOnlyList<StockBatch>> GetActiveBatchesAsync()
    {
        var active = _state.State.Batches.Where(b => b.Status == BatchStatus.Active).ToList();
        return Task.FromResult<IReadOnlyList<StockBatch>>(active);
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.IngredientId != Guid.Empty);

    private void EnsureExists()
    {
        if (_state.State.IngredientId == Guid.Empty)
            throw new InvalidOperationException("Inventory not initialized");
    }

    private List<BatchConsumptionDetail> ConsumeFifo(decimal quantity)
    {
        var remaining = quantity;
        var breakdown = new List<BatchConsumptionDetail>();

        // Order by received date (oldest first) for FIFO
        var activeBatches = _state.State.Batches
            .Where(b => b.Status == BatchStatus.Active && b.Quantity > 0)
            .OrderBy(b => b.ReceivedDate)
            .ToList();

        foreach (var batch in activeBatches)
        {
            if (remaining <= 0) break;

            var consumeQty = Math.Min(remaining, batch.Quantity);
            var index = _state.State.Batches.FindIndex(b => b.Id == batch.Id);

            var newQty = batch.Quantity - consumeQty;
            var newStatus = newQty <= 0 ? BatchStatus.Exhausted : batch.Status;

            _state.State.Batches[index] = batch with { Quantity = newQty, Status = newStatus };

            breakdown.Add(new BatchConsumptionDetail(batch.Id, batch.BatchNumber, consumeQty, batch.UnitCost, consumeQty * batch.UnitCost));
            remaining -= consumeQty;
        }

        RecalculateQuantitiesAndCost();
        return breakdown;
    }

    private void RecalculateQuantitiesAndCost()
    {
        var activeBatches = _state.State.Batches.Where(b => b.Status == BatchStatus.Active);

        _state.State.QuantityOnHand = activeBatches.Sum(b => b.Quantity);
        _state.State.QuantityAvailable = _state.State.QuantityOnHand - _state.State.QuantityReserved;

        var totalValue = activeBatches.Sum(b => b.Quantity * b.UnitCost);
        _state.State.WeightedAverageCost = _state.State.QuantityOnHand > 0
            ? totalValue / _state.State.QuantityOnHand
            : 0;

        UpdateStockLevel();
    }

    private void UpdateStockLevel()
    {
        if (_state.State.QuantityAvailable <= 0)
            _state.State.StockLevel = StockLevel.OutOfStock;
        else if (_state.State.QuantityAvailable <= _state.State.ReorderPoint)
            _state.State.StockLevel = StockLevel.Low;
        else if (_state.State.QuantityAvailable > _state.State.ParLevel && _state.State.ParLevel > 0)
            _state.State.StockLevel = StockLevel.AbovePar;
        else
            _state.State.StockLevel = StockLevel.Normal;
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

        _state.State.RecentMovements.Add(movement);

        // Keep only last 100 movements
        if (_state.State.RecentMovements.Count > 100)
            _state.State.RecentMovements.RemoveAt(0);
    }
}
