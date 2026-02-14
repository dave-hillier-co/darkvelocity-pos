using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class InventoryGrain : JournaledGrain<InventoryState, IInventoryEvent>, IInventoryGrain
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<InventoryGrain> _logger;
    private Lazy<IAsyncStream<IStreamEvent>>? _inventoryStream;
    private Lazy<IAsyncStream<IStreamEvent>>? _alertStream;
    private Lazy<ILedgerGrain>? _ledger;

    public InventoryGrain(
        IGrainFactory grainFactory,
        ILogger<InventoryGrain> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (State.OrganizationId != Guid.Empty)
        {
            InitializeLazyFields();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    protected override void TransitionState(InventoryState state, IInventoryEvent @event)
    {
        switch (@event)
        {
            case InventoryInitialized e:
                state.IngredientId = e.IngredientId;
                state.OrganizationId = e.OrganizationId;
                state.SiteId = e.SiteId;
                state.IngredientName = e.IngredientName;
                state.Unit = e.Unit;
                state.ReorderPoint = e.ReorderPoint;
                state.ParLevel = e.ParLevel;
                break;

            case StockBatchReceived e:
                // First reduce unbatched deficit (negative stock) if any
                var receivedQty = e.Quantity;
                if (state.UnbatchedDeficit > 0)
                {
                    var deficitReduction = Math.Min(state.UnbatchedDeficit, receivedQty);
                    state.UnbatchedDeficit -= deficitReduction;
                    receivedQty -= deficitReduction;
                }
                // Add remaining quantity as a batch
                if (receivedQty > 0)
                {
                    var batch = new StockBatch
                    {
                        Id = e.BatchId,
                        BatchNumber = e.BatchNumber,
                        ReceivedDate = e.OccurredAt,
                        ExpiryDate = e.ExpiryDate.HasValue ? e.ExpiryDate.Value.ToDateTime(TimeOnly.MinValue) : null,
                        Quantity = receivedQty,
                        OriginalQuantity = e.Quantity,
                        UnitCost = e.UnitCost,
                        TotalCost = e.Quantity * e.UnitCost,
                        SupplierId = e.SupplierId,
                        DeliveryId = e.DeliveryId,
                        SkuId = e.SkuId,
                        Status = BatchStatus.Active
                    };
                    state.Batches.Add(batch);
                }
                state.LastReceivedAt = e.OccurredAt;
                RecalculateQuantitiesAndCost(state);
                break;

            case StockTransferredIn e:
                // First reduce unbatched deficit (negative stock) if any
                var transferQty = e.Quantity;
                if (state.UnbatchedDeficit > 0)
                {
                    var deficitReduction = Math.Min(state.UnbatchedDeficit, transferQty);
                    state.UnbatchedDeficit -= deficitReduction;
                    transferQty -= deficitReduction;
                }
                // Add remaining quantity as a batch
                if (transferQty > 0)
                {
                    var transferBatch = new StockBatch
                    {
                        Id = Guid.NewGuid(),
                        BatchNumber = $"XFER-{e.TransferId.ToString()[..8]}",
                        ReceivedDate = e.OccurredAt,
                        Quantity = transferQty,
                        OriginalQuantity = e.Quantity,
                        UnitCost = e.UnitCost,
                        TotalCost = e.Quantity * e.UnitCost,
                        Status = BatchStatus.Active
                    };
                    state.Batches.Add(transferBatch);
                }
                state.LastReceivedAt = e.OccurredAt;
                RecalculateQuantitiesAndCost(state);
                break;

            case StockConsumed e:
                if (e.BatchBreakdown.Count > 0)
                    ApplyBatchBreakdown(state, e.BatchBreakdown, e.Quantity);
                else
                    ConsumeFifoForState(state, e.Quantity);
                state.LastConsumedAt = e.OccurredAt;
                RecalculateQuantitiesAndCost(state);
                break;

            case StockWrittenOff e:
                if (e.BatchBreakdown.Count > 0)
                    ApplyBatchBreakdown(state, e.BatchBreakdown, e.Quantity);
                else
                    ConsumeFifoForState(state, e.Quantity);
                RecalculateQuantitiesAndCost(state);
                break;

            case StockTransferredOut e:
                if (e.BatchBreakdown.Count > 0)
                    ApplyBatchBreakdown(state, e.BatchBreakdown, e.Quantity);
                else
                    ConsumeFifoForState(state, e.Quantity);
                RecalculateQuantitiesAndCost(state);
                break;

            case StockAdjusted e:
                var variance = e.NewQuantity - e.PreviousQuantity;
                if (variance > 0)
                {
                    // First reduce unbatched deficit (negative stock) if any
                    var adjQty = variance;
                    if (state.UnbatchedDeficit > 0)
                    {
                        var deficitReduction = Math.Min(state.UnbatchedDeficit, adjQty);
                        state.UnbatchedDeficit -= deficitReduction;
                        adjQty -= deficitReduction;
                    }
                    // Add remaining quantity as a batch
                    if (adjQty > 0)
                    {
                        var adjBatch = new StockBatch
                        {
                            Id = Guid.NewGuid(),
                            BatchNumber = $"ADJ-{e.OccurredAt:yyyyMMdd}",
                            ReceivedDate = e.OccurredAt,
                            Quantity = adjQty,
                            OriginalQuantity = variance,
                            UnitCost = state.WeightedAverageCost,
                            TotalCost = variance * state.WeightedAverageCost,
                            Status = BatchStatus.Active,
                            Notes = e.Reason
                        };
                        state.Batches.Add(adjBatch);
                    }
                }
                else if (variance < 0)
                {
                    ConsumeFifoForState(state, Math.Abs(variance));
                }
                state.LastCountedAt = e.OccurredAt;
                RecalculateQuantitiesAndCost(state);
                break;

            case InventorySettingsUpdated e:
                if (e.ReorderPoint.HasValue)
                    state.ReorderPoint = e.ReorderPoint.Value;
                if (e.ParLevel.HasValue)
                    state.ParLevel = e.ParLevel.Value;
                UpdateStockLevel(state);
                break;

            case LowStockAlertTriggered:
            case StockDepleted:
                // These are marker events, no state change needed
                break;
        }
    }

    private static void ConsumeFifoForState(InventoryState state, decimal quantity)
    {
        var remaining = quantity;
        var activeBatches = state.Batches
            .Where(b => b.Status == BatchStatus.Active && b.Quantity > 0)
            .OrderBy(b => b.ReceivedDate)
            .ToList();

        foreach (var batch in activeBatches)
        {
            if (remaining <= 0) break;

            var consumeQty = Math.Min(remaining, batch.Quantity);
            var index = state.Batches.FindIndex(b => b.Id == batch.Id);

            var newQty = batch.Quantity - consumeQty;
            var newStatus = newQty <= 0 ? BatchStatus.Exhausted : batch.Status;

            state.Batches[index] = batch with { Quantity = newQty, Status = newStatus };
            remaining -= consumeQty;
        }

        // Track unbatched consumption for negative stock support
        // Per design: "Negative stock is the default - service doesn't stop for inventory discrepancies"
        if (remaining > 0)
        {
            state.UnbatchedDeficit += remaining;
        }
    }

    private static void ApplyBatchBreakdown(InventoryState state, IReadOnlyList<EventBatchConsumptionDetail> breakdown, decimal totalQuantity)
    {
        var consumedFromBatches = 0m;
        foreach (var detail in breakdown)
        {
            var index = state.Batches.FindIndex(b => b.Id == detail.BatchId);
            if (index >= 0)
            {
                var batch = state.Batches[index];
                var newQty = batch.Quantity - detail.Quantity;
                var newStatus = newQty <= 0 ? BatchStatus.Exhausted : batch.Status;
                state.Batches[index] = batch with { Quantity = newQty, Status = newStatus };
                consumedFromBatches += detail.Quantity;
            }
        }

        // Track unbatched consumption for negative stock support
        var unbatched = totalQuantity - consumedFromBatches;
        if (unbatched > 0)
        {
            state.UnbatchedDeficit += unbatched;
        }
    }

    private static void RecalculateQuantitiesAndCost(InventoryState state)
    {
        var activeBatches = state.Batches.Where(b => b.Status == BatchStatus.Active);

        // Include unbatched deficit for negative stock support
        // Per design: "Negative stock is the default - service doesn't stop for inventory discrepancies"
        state.QuantityOnHand = activeBatches.Sum(b => b.Quantity) - state.UnbatchedDeficit;
        state.QuantityAvailable = state.QuantityOnHand - state.QuantityReserved;

        var totalValue = activeBatches.Sum(b => b.Quantity * b.UnitCost);
        state.WeightedAverageCost = state.QuantityOnHand > 0
            ? totalValue / state.QuantityOnHand
            : 0;

        UpdateStockLevel(state);
    }

    private static void UpdateStockLevel(InventoryState state)
    {
        if (state.QuantityAvailable <= 0)
            state.StockLevel = StockLevel.OutOfStock;
        else if (state.QuantityAvailable <= state.ReorderPoint)
            state.StockLevel = StockLevel.Low;
        else if (state.QuantityAvailable > state.ParLevel && state.ParLevel > 0)
            state.StockLevel = StockLevel.AbovePar;
        else
            state.StockLevel = StockLevel.Normal;
    }

    private void InitializeLazyFields()
    {
        var orgId = State.OrganizationId;
        var siteId = State.SiteId;
        var ingredientId = State.IngredientId;

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
        if (State.IngredientId != Guid.Empty)
            throw new InvalidOperationException("Inventory already initialized");

        RaiseEvent(new InventoryInitialized
        {
            IngredientId = command.IngredientId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            IngredientName = command.IngredientName,
            Unit = command.Unit,
            ReorderPoint = command.ReorderPoint,
            ParLevel = command.ParLevel,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Store additional fields not in the event
        State.Sku = command.Sku;
        State.Category = command.Category;

        InitializeLazyFields();

        // Initialize the ledger for quantity tracking
        await Ledger.InitializeAsync(command.OrganizationId);
    }

    public Task<InventoryState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public async Task<BatchReceivedResult> ReceiveBatchAsync(ReceiveBatchCommand command)
    {
        EnsureExists();

        var batchId = Guid.NewGuid();

        RaiseEvent(new StockBatchReceived
        {
            IngredientId = State.IngredientId,
            BatchId = batchId,
            BatchNumber = command.BatchNumber ?? "",
            Quantity = command.Quantity,
            UnitCost = command.UnitCost,
            ExpiryDate = command.ExpiryDate.HasValue ? DateOnly.FromDateTime(command.ExpiryDate.Value) : null,
            SupplierId = command.SupplierId,
            DeliveryId = command.DeliveryId,
            OccurredAt = DateTime.UtcNow,
            SkuId = command.SkuId
        });
        await ConfirmEvents();

        // Update batch with location and notes (not stored in event)
        var batchIndex = State.Batches.FindIndex(b => b.Id == batchId);
        if (batchIndex >= 0)
        {
            State.Batches[batchIndex] = State.Batches[batchIndex] with
            {
                Location = command.Location,
                Notes = command.Notes
            };
        }

        // Credit ledger with received quantity
        await Ledger.CreditAsync(
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

        // Publish stream event for integration
        await InventoryStream.OnNextAsync(new StockReceivedEvent(
            State.IngredientId,
            State.SiteId,
            State.IngredientName,
            command.Quantity,
            State.Unit,
            command.UnitCost,
            State.QuantityOnHand,
            command.BatchNumber,
            command.ExpiryDate.HasValue ? DateOnly.FromDateTime(command.ExpiryDate.Value) : null,
            command.SupplierId,
            command.DeliveryId)
        {
            OrganizationId = State.OrganizationId
        });

        _logger.LogInformation(
            "Stock received for {IngredientName}: {Quantity} {Unit} at {UnitCost:C}",
            State.IngredientName,
            command.Quantity,
            State.Unit,
            command.UnitCost);

        return new BatchReceivedResult(batchId, State.QuantityOnHand, State.WeightedAverageCost);
    }

    public async Task<BatchReceivedResult> ReceiveTransferAsync(ReceiveTransferCommand command)
    {
        EnsureExists();

        RaiseEvent(new StockTransferredIn
        {
            IngredientId = State.IngredientId,
            TransferId = command.TransferId,
            SourceSiteId = command.SourceSiteId,
            Quantity = command.Quantity,
            UnitCost = command.UnitCost,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Get the batch ID from the newly added batch
        var batchId = State.Batches.Last().Id;

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

        return new BatchReceivedResult(batchId, State.QuantityOnHand, State.WeightedAverageCost);
    }

    public async Task<ConsumptionResult> ConsumeAsync(ConsumeStockCommand command)
    {
        EnsureExists();

        var previousLevel = State.StockLevel;

        // Calculate FIFO breakdown for available batches
        var breakdown = CalculateFifoBreakdown(command.Quantity);
        var totalCost = breakdown.Sum(b => b.TotalCost);

        // If consuming more than available (negative stock), estimate cost from WAC
        var consumedFromBatches = breakdown.Sum(b => b.Quantity);
        if (consumedFromBatches < command.Quantity)
        {
            var unbatchedQuantity = command.Quantity - consumedFromBatches;
            var estimatedCost = unbatchedQuantity * State.WeightedAverageCost;
            totalCost += estimatedCost;
        }

        RaiseEvent(new StockConsumed
        {
            IngredientId = State.IngredientId,
            Quantity = command.Quantity,
            CostOfGoodsConsumed = totalCost,
            OrderId = command.OrderId,
            Reason = command.Reason,
            OccurredAt = DateTime.UtcNow,
            BatchBreakdown = breakdown.Select(b => new EventBatchConsumptionDetail
            {
                BatchId = b.BatchId,
                Quantity = b.Quantity,
                UnitCost = b.UnitCost
            }).ToList()
        });
        await ConfirmEvents();

        // Debit ledger - always allow negative (service doesn't stop for inventory discrepancies)
        await Ledger.DebitAsync(
            command.Quantity,
            "consumption",
            command.Reason,
            new Dictionary<string, string>
            {
                ["orderId"] = command.OrderId?.ToString() ?? "",
                ["totalCost"] = totalCost.ToString(),
                ["performedBy"] = (command.PerformedBy ?? Guid.Empty).ToString(),
                ["allowNegative"] = "true"
            });

        RecordMovement(MovementType.Consumption, -command.Quantity, State.WeightedAverageCost, command.Reason, null, command.PerformedBy ?? Guid.Empty, command.OrderId);

        // Publish stream event for integration
        await InventoryStream.OnNextAsync(new StockConsumedEvent(
            State.IngredientId,
            State.SiteId,
            State.IngredientName,
            command.Quantity,
            State.Unit,
            totalCost,
            State.QuantityAvailable,
            command.OrderId,
            command.Reason)
        {
            OrganizationId = State.OrganizationId
        });

        // Check for stock level events
        await CheckAndPublishStockAlertsAsync(previousLevel, command.OrderId);

        _logger.LogInformation(
            "Stock consumed for {IngredientName}: {Quantity} {Unit}. Remaining: {Remaining}",
            State.IngredientName,
            command.Quantity,
            State.Unit,
            State.QuantityAvailable);

        return new ConsumptionResult(command.Quantity, totalCost, breakdown, totalCost, State.QuantityAvailable);
    }

    private List<BatchConsumptionDetail> CalculateFifoBreakdown(decimal quantity)
    {
        var remaining = quantity;
        var breakdown = new List<BatchConsumptionDetail>();

        var activeBatches = State.Batches
            .Where(b => b.Status == BatchStatus.Active && b.Quantity > 0)
            .OrderBy(b => b.ReceivedDate)
            .ToList();

        foreach (var batch in activeBatches)
        {
            if (remaining <= 0) break;

            var consumeQty = Math.Min(remaining, batch.Quantity);
            breakdown.Add(new BatchConsumptionDetail(batch.Id, batch.BatchNumber, consumeQty, batch.UnitCost, consumeQty * batch.UnitCost));
            remaining -= consumeQty;
        }

        return breakdown;
    }

    private async Task CheckAndPublishStockAlertsAsync(StockLevel previousLevel, Guid? lastOrderId = null)
    {
        var currentLevel = State.StockLevel;

        // Publish reorder point breached event when crossing threshold
        if (currentLevel == StockLevel.Low && previousLevel != StockLevel.Low)
        {
            RaiseEvent(new LowStockAlertTriggered
            {
                IngredientId = State.IngredientId,
                QuantityOnHand = State.QuantityOnHand,
                ReorderPoint = State.ReorderPoint,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();

            var quantityToOrder = State.ParLevel - State.QuantityAvailable;
            await AlertStream.OnNextAsync(new ReorderPointBreachedEvent(
                State.IngredientId,
                State.SiteId,
                State.IngredientName,
                State.QuantityAvailable,
                State.ReorderPoint,
                State.ParLevel,
                quantityToOrder > 0 ? quantityToOrder : 0)
            {
                OrganizationId = State.OrganizationId
            });

            _logger.LogWarning(
                "Reorder point breached for {IngredientName}: {Quantity} {Unit} (Reorder point: {ReorderPoint})",
                State.IngredientName,
                State.QuantityAvailable,
                State.Unit,
                State.ReorderPoint);
        }

        // Publish stock depleted event
        if (currentLevel == StockLevel.OutOfStock && previousLevel != StockLevel.OutOfStock)
        {
            RaiseEvent(new StockDepleted
            {
                IngredientId = State.IngredientId,
                LastConsumingOrderId = lastOrderId,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();

            await AlertStream.OnNextAsync(new Streams.StockDepletedEvent(
                State.IngredientId,
                State.SiteId,
                State.IngredientName,
                DateTime.UtcNow,
                lastOrderId)
            {
                OrganizationId = State.OrganizationId
            });

            _logger.LogError(
                "Stock depleted: {IngredientName} at site {SiteId}",
                State.IngredientName,
                State.SiteId);
        }
    }

    public Task<ConsumptionResult> ConsumeForOrderAsync(Guid orderId, decimal quantity, Guid? performedBy)
    {
        return ConsumeAsync(new ConsumeStockCommand(quantity, $"Order {orderId}", orderId, performedBy));
    }

    public async Task ReverseConsumptionAsync(Guid movementId, string reason, Guid reversedBy)
    {
        EnsureExists();

        var movement = State.RecentMovements.FirstOrDefault(m => m.Id == movementId)
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

        // Use stock adjustment event for reversal
        RaiseEvent(new StockAdjusted
        {
            IngredientId = State.IngredientId,
            PreviousQuantity = State.QuantityOnHand,
            NewQuantity = State.QuantityOnHand + reversedQuantity,
            Variance = reversedQuantity,
            Reason = $"Reversal: {reason}",
            AdjustedBy = reversedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        var batchId = State.Batches.Last().Id;
        RecordMovement(MovementType.Adjustment, reversedQuantity, movement.UnitCost, $"Reversal: {reason}", batchId, reversedBy);
    }

    public async Task<int> ReverseOrderConsumptionAsync(Guid orderId, string reason, Guid reversedBy)
    {
        EnsureExists();

        // Find all consumption movements for this order
        var orderMovements = State.RecentMovements
            .Where(m => m.ReferenceId == orderId && m.Type == MovementType.Consumption)
            .ToList();

        if (orderMovements.Count == 0)
        {
            return 0;
        }

        var totalReversedQuantity = 0m;
        var totalCost = 0m;

        foreach (var movement in orderMovements)
        {
            var reversedQuantity = Math.Abs(movement.Quantity);
            totalReversedQuantity += reversedQuantity;
            totalCost += movement.TotalCost;

            // Credit ledger for reversed quantity
            await Ledger.CreditAsync(
                reversedQuantity,
                "order_void_reversal",
                $"Order void reversal: {reason}",
                new Dictionary<string, string>
                {
                    ["originalMovementId"] = movement.Id.ToString(),
                    ["orderId"] = orderId.ToString(),
                    ["reversedBy"] = reversedBy.ToString()
                });
        }

        // Raise a single adjustment event for the total reversal
        RaiseEvent(new StockAdjusted
        {
            IngredientId = State.IngredientId,
            PreviousQuantity = State.QuantityOnHand,
            NewQuantity = State.QuantityOnHand + totalReversedQuantity,
            Variance = totalReversedQuantity,
            Reason = $"Order void reversal: {reason}",
            AdjustedBy = reversedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Record a single movement for the reversal
        var batchId = State.Batches.LastOrDefault()?.Id;
        var avgUnitCost = totalReversedQuantity > 0 ? totalCost / totalReversedQuantity : State.WeightedAverageCost;
        RecordMovement(MovementType.Adjustment, totalReversedQuantity, avgUnitCost, $"Order void reversal: {reason}", batchId, reversedBy, orderId);

        return orderMovements.Count;
    }

    public async Task RecordWasteAsync(RecordWasteCommand command)
    {
        EnsureExists();

        // Calculate the breakdown before raising event
        var breakdown = CalculateFifoBreakdown(command.Quantity);
        var totalCost = breakdown.Sum(b => b.TotalCost);

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

        RaiseEvent(new StockWrittenOff
        {
            IngredientId = State.IngredientId,
            Quantity = command.Quantity,
            CostWrittenOff = totalCost,
            Category = command.WasteCategory.ToString(),
            Reason = command.Reason,
            RecordedBy = command.RecordedBy,
            OccurredAt = DateTime.UtcNow,
            BatchBreakdown = breakdown.Select(b => new EventBatchConsumptionDetail
            {
                BatchId = b.BatchId,
                Quantity = b.Quantity,
                UnitCost = b.UnitCost
            }).ToList()
        });
        await ConfirmEvents();

        RecordMovement(MovementType.Waste, -command.Quantity, State.WeightedAverageCost, $"{command.WasteCategory}: {command.Reason}", null, command.RecordedBy);
    }

    public async Task AdjustQuantityAsync(AdjustQuantityCommand command)
    {
        EnsureExists();

        var previousQuantity = State.QuantityOnHand;
        var variance = command.NewQuantity - previousQuantity;

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

        RaiseEvent(new StockAdjusted
        {
            IngredientId = State.IngredientId,
            PreviousQuantity = previousQuantity,
            NewQuantity = command.NewQuantity,
            Variance = variance,
            Reason = command.Reason,
            AdjustedBy = command.AdjustedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        RecordMovement(MovementType.Adjustment, variance, State.WeightedAverageCost, command.Reason, null, command.AdjustedBy);
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

        var breakdown = CalculateFifoBreakdown(command.Quantity);

        RaiseEvent(new StockTransferredOut
        {
            IngredientId = State.IngredientId,
            TransferId = command.TransferId,
            DestinationSiteId = command.DestinationSiteId,
            Quantity = command.Quantity,
            UnitCost = State.WeightedAverageCost,
            TransferredBy = command.TransferredBy,
            OccurredAt = DateTime.UtcNow,
            BatchBreakdown = breakdown.Select(b => new EventBatchConsumptionDetail
            {
                BatchId = b.BatchId,
                Quantity = b.Quantity,
                UnitCost = b.UnitCost
            }).ToList()
        });
        await ConfirmEvents();

        RecordMovement(MovementType.Transfer, -command.Quantity, State.WeightedAverageCost, $"Transfer to site {command.DestinationSiteId}", null, command.TransferredBy, command.TransferId);
    }

    public async Task WriteOffExpiredBatchesAsync(Guid performedBy)
    {
        EnsureExists();

        var now = DateTime.UtcNow;
        var expiredBatches = State.Batches
            .Where(b => b.Status == BatchStatus.Active && b.ExpiryDate.HasValue && b.ExpiryDate.Value < now)
            .ToList();

        var totalExpiredQuantity = expiredBatches.Sum(b => b.Quantity);
        var totalExpiredCost = expiredBatches.Sum(b => b.Quantity * b.UnitCost);

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

            RaiseEvent(new StockWrittenOff
            {
                IngredientId = State.IngredientId,
                Quantity = totalExpiredQuantity,
                CostWrittenOff = totalExpiredCost,
                Category = "expiry",
                Reason = "Expired batch write-off",
                RecordedBy = performedBy,
                OccurredAt = now
            });
            await ConfirmEvents();

            foreach (var batch in expiredBatches)
            {
                var index = State.Batches.FindIndex(b => b.Id == batch.Id);
                State.Batches[index] = batch with { Status = BatchStatus.WrittenOff, Quantity = 0 };
                RecordMovement(MovementType.Waste, -batch.Quantity, batch.UnitCost, "Expired batch write-off", batch.Id, performedBy);
            }

            RecalculateQuantitiesAndCost(State);
        }
    }

    public async Task SetReorderPointAsync(decimal reorderPoint)
    {
        EnsureExists();

        RaiseEvent(new InventorySettingsUpdated
        {
            IngredientId = State.IngredientId,
            ReorderPoint = reorderPoint,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task SetParLevelAsync(decimal parLevel)
    {
        EnsureExists();

        RaiseEvent(new InventorySettingsUpdated
        {
            IngredientId = State.IngredientId,
            ParLevel = parLevel,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task UpdateSettingsAsync(UpdateInventorySettingsCommand command)
    {
        EnsureExists();

        RaiseEvent(new InventorySettingsUpdated
        {
            IngredientId = State.IngredientId,
            ReorderPoint = command.ReorderPoint,
            ParLevel = command.ParLevel,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<InventoryLevelInfo> GetLevelInfoAsync()
    {
        DateTime? earliestExpiry = State.Batches
            .Where(b => b.Status == BatchStatus.Active && b.ExpiryDate.HasValue)
            .Select(b => b.ExpiryDate)
            .Min();

        return Task.FromResult(new InventoryLevelInfo(
            State.QuantityOnHand,
            State.QuantityAvailable,
            State.WeightedAverageCost,
            State.StockLevel,
            earliestExpiry));
    }

    public async Task<bool> HasSufficientStockAsync(decimal quantity)
    {
        if (State.OrganizationId == Guid.Empty)
            return false;
        return await Ledger.HasSufficientBalanceAsync(quantity);
    }

    public Task<StockLevel> GetStockLevelAsync()
    {
        return Task.FromResult(State.StockLevel);
    }

    public Task<IReadOnlyList<StockBatch>> GetActiveBatchesAsync()
    {
        var active = State.Batches.Where(b => b.Status == BatchStatus.Active).ToList();
        return Task.FromResult<IReadOnlyList<StockBatch>>(active);
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.IngredientId != Guid.Empty);

    private void EnsureExists()
    {
        if (State.IngredientId == Guid.Empty)
            throw new InvalidOperationException("Inventory not initialized");
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

        State.RecentMovements.Add(movement);

        // Keep only last 100 movements
        if (State.RecentMovements.Count > 100)
            State.RecentMovements.RemoveAt(0);
    }
}
