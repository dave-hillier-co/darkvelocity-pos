using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

public class InventoryGrain : Grain, IInventoryGrain
{
    private readonly IPersistentState<InventoryState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<InventoryGrain> _logger;
    private Lazy<IAsyncStream<IStreamEvent>>? _inventoryStream;
    private Lazy<IAsyncStream<IStreamEvent>>? _alertStream;
    private Lazy<ILedgerGrain>? _ledger;

    public InventoryGrain(
        [PersistentState("inventory", "OrleansStorage")]
        IPersistentState<InventoryState> state,
        IGrainFactory grainFactory,
        ILogger<InventoryGrain> logger)
    {
        _state = state;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.OrganizationId != Guid.Empty)
        {
            InitializeLazyFields();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    private void InitializeLazyFields()
    {
        var orgId = _state.State.OrganizationId;
        var siteId = _state.State.SiteId;
        var ingredientId = _state.State.IngredientId;

        _ledger = new Lazy<ILedgerGrain>(() =>
        {
            var ledgerKey = GrainKeys.InventoryLedger(orgId, siteId, ingredientId);
            return _grainFactory.GetGrain<ILedgerGrain>(ledgerKey);
        });

        _inventoryStream = new Lazy<IAsyncStream<IStreamEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.InventoryStreamNamespace, orgId.ToString());
            return streamProvider.GetStream<IStreamEvent>(streamId);
        });

        _alertStream = new Lazy<IAsyncStream<IStreamEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.AlertStreamNamespace, orgId.ToString());
            return streamProvider.GetStream<IStreamEvent>(streamId);
        });
    }

    private ILedgerGrain Ledger => _ledger!.Value;
    private IAsyncStream<IStreamEvent> InventoryStream => _inventoryStream!.Value;
    private IAsyncStream<IStreamEvent> AlertStream => _alertStream!.Value;

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
        InitializeLazyFields();

        // Initialize the ledger for quantity tracking
        await Ledger.InitializeAsync(command.OrganizationId);
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

        // Credit ledger with received quantity
        var ledgerResult = await Ledger.CreditAsync(
            command.Quantity,
            "receipt",
            "Batch received",
            new Dictionary<string, string>
            {
                ["batchId"] = batchId.ToString(),
                ["batchNumber"] = command.BatchNumber ?? "",
                ["unitCost"] = command.UnitCost.ToString(),
                ["supplierId"] = command.SupplierId?.ToString() ?? ""
            });

        RecordMovement(MovementType.Receipt, command.Quantity, command.UnitCost, "Batch received", batchId, command.ReceivedBy ?? Guid.Empty);

        _state.State.Version++;
        await _state.WriteStateAsync();

        // Publish stock received event
        await InventoryStream.OnNextAsync(new StockReceivedEvent(
            _state.State.IngredientId,
            _state.State.SiteId,
            _state.State.IngredientName,
            command.Quantity,
            _state.State.Unit,
            command.UnitCost,
            _state.State.QuantityOnHand,
            command.BatchNumber,
            command.ExpiryDate.HasValue ? DateOnly.FromDateTime(command.ExpiryDate.Value) : null,
            command.SupplierId,
            command.DeliveryId)
        {
            OrganizationId = _state.State.OrganizationId
        });

        _logger.LogInformation(
            "Stock received for {IngredientName}: {Quantity} {Unit} at {UnitCost:C}",
            _state.State.IngredientName,
            command.Quantity,
            _state.State.Unit,
            command.UnitCost);

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

        // Credit ledger with transferred quantity
        await Ledger.CreditAsync(
            command.Quantity,
            "transfer_in",
            $"Transfer from site {command.SourceSiteId}",
            new Dictionary<string, string>
            {
                ["batchId"] = batchId.ToString(),
                ["transferId"] = command.TransferId.ToString(),
                ["sourceSiteId"] = command.SourceSiteId.ToString()
            });

        RecordMovement(MovementType.Transfer, command.Quantity, command.UnitCost, $"Transfer from site {command.SourceSiteId}", batchId, Guid.Empty, command.TransferId);

        _state.State.Version++;
        await _state.WriteStateAsync();

        return new BatchReceivedResult(batchId, _state.State.QuantityOnHand, _state.State.WeightedAverageCost);
    }

    public async Task<ConsumptionResult> ConsumeAsync(ConsumeStockCommand command)
    {
        EnsureExists();

        // Check ledger balance for sufficient stock
        var hasSufficient = await Ledger.HasSufficientBalanceAsync(command.Quantity);
        if (!hasSufficient)
            throw new InvalidOperationException("Insufficient stock");

        var previousLevel = _state.State.StockLevel;
        var breakdown = ConsumeFifo(command.Quantity);
        var totalCost = breakdown.Sum(b => b.TotalCost);

        // Debit ledger for consumed quantity
        var ledgerResult = await Ledger.DebitAsync(
            command.Quantity,
            "consumption",
            command.Reason,
            new Dictionary<string, string>
            {
                ["orderId"] = command.OrderId?.ToString() ?? "",
                ["totalCost"] = totalCost.ToString(),
                ["performedBy"] = (command.PerformedBy ?? Guid.Empty).ToString()
            });

        if (!ledgerResult.Success)
            throw new InvalidOperationException(ledgerResult.Error ?? "Insufficient stock");

        _state.State.LastConsumedAt = DateTime.UtcNow;
        RecordMovement(MovementType.Consumption, -command.Quantity, _state.State.WeightedAverageCost, command.Reason, null, command.PerformedBy ?? Guid.Empty, command.OrderId);

        _state.State.Version++;
        await _state.WriteStateAsync();

        // Publish stock consumed event
        await InventoryStream.OnNextAsync(new StockConsumedEvent(
            _state.State.IngredientId,
            _state.State.SiteId,
            _state.State.IngredientName,
            command.Quantity,
            _state.State.Unit,
            totalCost,
            _state.State.QuantityAvailable,
            command.OrderId,
            command.Reason)
        {
            OrganizationId = _state.State.OrganizationId
        });

        // Check for stock level events
        await CheckAndPublishStockAlertsAsync(previousLevel, command.OrderId);

        _logger.LogInformation(
            "Stock consumed for {IngredientName}: {Quantity} {Unit}. Remaining: {Remaining}",
            _state.State.IngredientName,
            command.Quantity,
            _state.State.Unit,
            _state.State.QuantityAvailable);

        return new ConsumptionResult(command.Quantity, totalCost, breakdown);
    }

    private async Task CheckAndPublishStockAlertsAsync(StockLevel previousLevel, Guid? lastOrderId = null)
    {
        var currentLevel = _state.State.StockLevel;

        // Publish reorder point breached event when crossing threshold
        if (currentLevel == StockLevel.Low && previousLevel != StockLevel.Low)
        {
            var quantityToOrder = _state.State.ParLevel - _state.State.QuantityAvailable;
            await AlertStream.OnNextAsync(new ReorderPointBreachedEvent(
                _state.State.IngredientId,
                _state.State.SiteId,
                _state.State.IngredientName,
                _state.State.QuantityAvailable,
                _state.State.ReorderPoint,
                _state.State.ParLevel,
                quantityToOrder > 0 ? quantityToOrder : 0)
            {
                OrganizationId = _state.State.OrganizationId
            });

            _logger.LogWarning(
                "Reorder point breached for {IngredientName}: {Quantity} {Unit} (Reorder point: {ReorderPoint})",
                _state.State.IngredientName,
                _state.State.QuantityAvailable,
                _state.State.Unit,
                _state.State.ReorderPoint);
        }

        // Publish stock depleted event
        if (currentLevel == StockLevel.OutOfStock && previousLevel != StockLevel.OutOfStock)
        {
            await AlertStream.OnNextAsync(new StockDepletedEvent(
                _state.State.IngredientId,
                _state.State.SiteId,
                _state.State.IngredientName,
                DateTime.UtcNow,
                lastOrderId)
            {
                OrganizationId = _state.State.OrganizationId
            });

            _logger.LogError(
                "Stock depleted: {IngredientName} at site {SiteId}",
                _state.State.IngredientName,
                _state.State.SiteId);
        }
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

        var reversedQuantity = Math.Abs(movement.Quantity);

        // Credit ledger for reversed quantity
        await Ledger.CreditAsync(
            reversedQuantity,
            "reversal",
            $"Reversal: {reason}",
            new Dictionary<string, string>
            {
                ["originalMovementId"] = movementId.ToString(),
                ["reversedBy"] = reversedBy.ToString()
            });

        // Create a new batch for the reversed quantity
        var batchId = Guid.NewGuid();
        var batch = new StockBatch
        {
            Id = batchId,
            BatchNumber = $"REV-{movementId.ToString()[..8]}",
            ReceivedDate = DateTime.UtcNow,
            Quantity = reversedQuantity,
            OriginalQuantity = reversedQuantity,
            UnitCost = movement.UnitCost,
            TotalCost = reversedQuantity * movement.UnitCost,
            Status = BatchStatus.Active,
            Notes = $"Reversed: {reason}"
        };

        _state.State.Batches.Add(batch);
        RecalculateQuantitiesAndCost();

        RecordMovement(MovementType.Adjustment, reversedQuantity, movement.UnitCost, $"Reversal: {reason}", batchId, reversedBy);

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RecordWasteAsync(RecordWasteCommand command)
    {
        EnsureExists();

        // Debit ledger for wasted quantity
        var ledgerResult = await Ledger.DebitAsync(
            command.Quantity,
            "waste",
            $"{command.WasteCategory}: {command.Reason}",
            new Dictionary<string, string>
            {
                ["wasteCategory"] = command.WasteCategory.ToString(),
                ["recordedBy"] = command.RecordedBy.ToString()
            });

        if (!ledgerResult.Success)
            throw new InvalidOperationException(ledgerResult.Error ?? "Insufficient stock");

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

        // Adjust ledger to new quantity
        var ledgerResult = await Ledger.AdjustToAsync(
            command.NewQuantity,
            command.Reason,
            new Dictionary<string, string>
            {
                ["adjustedBy"] = command.AdjustedBy.ToString(),
                ["variance"] = variance.ToString(),
                ["approvedBy"] = command.ApprovedBy?.ToString() ?? ""
            });

        if (!ledgerResult.Success)
            throw new InvalidOperationException(ledgerResult.Error ?? "Failed to adjust quantity");

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

        // Debit ledger for transferred quantity
        var ledgerResult = await Ledger.DebitAsync(
            command.Quantity,
            "transfer_out",
            $"Transfer to site {command.DestinationSiteId}",
            new Dictionary<string, string>
            {
                ["transferId"] = command.TransferId.ToString(),
                ["destinationSiteId"] = command.DestinationSiteId.ToString(),
                ["transferredBy"] = command.TransferredBy.ToString()
            });

        if (!ledgerResult.Success)
            throw new InvalidOperationException(ledgerResult.Error ?? "Insufficient stock for transfer");

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

        var totalExpiredQuantity = expiredBatches.Sum(b => b.Quantity);
        if (totalExpiredQuantity > 0)
        {
            // Debit ledger for total expired quantity
            await Ledger.DebitAsync(
                totalExpiredQuantity,
                "expiry_writeoff",
                "Expired batch write-off",
                new Dictionary<string, string>
                {
                    ["performedBy"] = performedBy.ToString(),
                    ["batchCount"] = expiredBatches.Count.ToString()
                });
        }

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

    public async Task<bool> HasSufficientStockAsync(decimal quantity)
    {
        if (_state.State.OrganizationId == Guid.Empty)
            return false;
        return await Ledger.HasSufficientBalanceAsync(quantity);
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
