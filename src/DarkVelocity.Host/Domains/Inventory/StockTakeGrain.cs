using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class StockTakeGrain : JournaledGrain<StockTakeState, IStockTakeEvent>, IStockTakeGrain
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<StockTakeGrain> _logger;
    private Lazy<IAsyncStream<IStreamEvent>>? _inventoryStream;

    public StockTakeGrain(
        IGrainFactory grainFactory,
        ILogger<StockTakeGrain> logger)
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

    protected override void TransitionState(StockTakeState state, IStockTakeEvent @event)
    {
        switch (@event)
        {
            case StockTakeStarted e:
                state.StockTakeId = e.StockTakeId;
                state.OrganizationId = e.OrganizationId;
                state.SiteId = e.SiteId;
                state.Name = e.Name;
                state.BlindCount = e.BlindCount;
                state.Category = e.Category;
                state.IngredientIds = e.IngredientIds;
                state.StartedBy = e.StartedBy;
                state.StartedAt = e.OccurredAt;
                state.Notes = e.Notes;
                state.Status = StockTakeStatus.InProgress;
                break;

            case StockTakeLineItemAdded e:
                state.LineItems.Add(new StockTakeLineItemState
                {
                    IngredientId = e.IngredientId,
                    IngredientName = e.IngredientName,
                    Sku = e.Sku,
                    Unit = e.Unit,
                    Category = e.Category,
                    TheoreticalQuantity = e.TheoreticalQuantity,
                    UnitCost = e.UnitCost
                });
                break;

            case CountRecorded e:
                var lineItem = state.LineItems.FirstOrDefault(li => li.IngredientId == e.IngredientId);
                if (lineItem != null)
                {
                    lineItem.CountedQuantity = e.CountedQuantity;
                    lineItem.Variance = e.Variance;
                    lineItem.VariancePercentage = e.VariancePercentage;
                    lineItem.VarianceValue = e.VarianceValue;
                    lineItem.Severity = e.Severity;
                    lineItem.CountedBy = e.CountedBy;
                    lineItem.CountedAt = e.OccurredAt;
                    lineItem.BatchNumber = e.BatchNumber;
                    lineItem.Location = e.Location;
                    lineItem.Notes = e.Notes;
                }
                RecalculateVarianceTotals(state);
                break;

            case StockTakeSubmittedForApproval e:
                state.Status = StockTakeStatus.PendingApproval;
                state.SubmittedBy = e.SubmittedBy;
                state.SubmittedAt = e.OccurredAt;
                break;

            case StockTakeFinalized e:
                state.Status = StockTakeStatus.Finalized;
                state.FinalizedBy = e.FinalizedBy;
                state.FinalizedAt = e.OccurredAt;
                state.AdjustmentsApplied = e.AdjustmentsApplied;
                state.ApprovalNotes = e.ApprovalNotes;
                state.TotalVarianceValue = e.TotalVarianceValue;
                state.TotalPositiveVariance = e.TotalPositiveVariance;
                state.TotalNegativeVariance = e.TotalNegativeVariance;
                break;

            case StockTakeCancelled e:
                state.Status = StockTakeStatus.Cancelled;
                state.CancelledBy = e.CancelledBy;
                state.CancelledAt = e.OccurredAt;
                state.CancellationReason = e.Reason;
                break;

            case VarianceCalculated:
                // This is an informational event, state already updated in CountRecorded
                break;
        }
    }

    private static void RecalculateVarianceTotals(StockTakeState state)
    {
        var countedItems = state.LineItems.Where(li => li.CountedQuantity.HasValue).ToList();

        state.TotalVarianceValue = countedItems.Sum(li => li.VarianceValue);
        state.TotalPositiveVariance = countedItems.Where(li => li.Variance > 0).Sum(li => li.VarianceValue);
        state.TotalNegativeVariance = countedItems.Where(li => li.Variance < 0).Sum(li => Math.Abs(li.VarianceValue));
        state.ItemsWithVariance = countedItems.Count(li => li.Variance != 0);
    }

    private void InitializeLazyFields()
    {
        var orgId = State.OrganizationId;

        _inventoryStream = new Lazy<IAsyncStream<IStreamEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.InventoryStreamNamespace, orgId.ToString());
            return streamProvider.GetStream<IStreamEvent>(streamId);
        });
    }

    private IAsyncStream<IStreamEvent> InventoryStream => _inventoryStream!.Value;

    public async Task StartAsync(StartStockTakeCommand command)
    {
        if (State.StockTakeId != Guid.Empty)
            throw new InvalidOperationException("Stock take already started");

        var stockTakeId = Guid.Parse(this.GetPrimaryKeyString().Split(':').Last());

        RaiseEvent(new StockTakeStarted
        {
            StockTakeId = stockTakeId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            Name = command.Name,
            BlindCount = command.BlindCount,
            Category = command.Category,
            IngredientIds = command.IngredientIds ?? [],
            StartedBy = command.StartedBy,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        InitializeLazyFields();

        // Freeze theoretical inventory values
        await LoadInventoryItemsAsync(command);

        _logger.LogInformation(
            "Stock take '{Name}' started for site {SiteId} by {StartedBy}. Blind count: {BlindCount}",
            command.Name,
            command.SiteId,
            command.StartedBy,
            command.BlindCount);
    }

    private async Task LoadInventoryItemsAsync(StartStockTakeCommand command)
    {
        // Get inventory items from the site
        // For now, we'll use the ingredient IDs provided or load all from a registry
        var ingredientIds = command.IngredientIds ?? [];

        // If specific ingredient IDs provided, use those
        // Otherwise, this would typically query an inventory index/registry
        foreach (var ingredientId in ingredientIds)
        {
            var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(
                GrainKeys.Inventory(command.OrganizationId, command.SiteId, ingredientId));

            if (await inventoryGrain.ExistsAsync())
            {
                var inventoryState = await inventoryGrain.GetStateAsync();

                // Apply category filter if specified
                if (!string.IsNullOrEmpty(command.Category) &&
                    !inventoryState.Category.Equals(command.Category, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                RaiseEvent(new StockTakeLineItemAdded
                {
                    StockTakeId = State.StockTakeId,
                    IngredientId = ingredientId,
                    IngredientName = inventoryState.IngredientName,
                    Sku = inventoryState.Sku,
                    Unit = inventoryState.Unit,
                    Category = inventoryState.Category,
                    TheoreticalQuantity = inventoryState.QuantityOnHand,
                    UnitCost = inventoryState.WeightedAverageCost,
                    OccurredAt = DateTime.UtcNow
                });
            }
        }

        await ConfirmEvents();
    }

    public Task<StockTakeState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public Task<StockTakeSummary> GetSummaryAsync()
    {
        EnsureExists();

        return Task.FromResult(new StockTakeSummary
        {
            StockTakeId = State.StockTakeId,
            Name = State.Name,
            Status = State.Status,
            StartedAt = State.StartedAt,
            FinalizedAt = State.FinalizedAt,
            TotalItems = State.LineItems.Count,
            ItemsCounted = State.LineItems.Count(li => li.CountedQuantity.HasValue),
            TotalVarianceValue = State.TotalVarianceValue,
            BlindCount = State.BlindCount
        });
    }

    public async Task RecordCountAsync(RecordCountCommand command)
    {
        EnsureExists();
        EnsureInProgress();

        var lineItem = State.LineItems.FirstOrDefault(li => li.IngredientId == command.IngredientId)
            ?? throw new InvalidOperationException($"Ingredient {command.IngredientId} not found in stock take");

        var variance = command.CountedQuantity - lineItem.TheoreticalQuantity;
        var variancePercentage = lineItem.TheoreticalQuantity != 0
            ? (variance / lineItem.TheoreticalQuantity) * 100
            : (command.CountedQuantity != 0 ? 100 : 0);
        var varianceValue = variance * lineItem.UnitCost;
        var severity = CalculateVarianceSeverity(Math.Abs(variancePercentage));

        RaiseEvent(new CountRecorded
        {
            StockTakeId = State.StockTakeId,
            IngredientId = command.IngredientId,
            CountedQuantity = command.CountedQuantity,
            TheoreticalQuantity = lineItem.TheoreticalQuantity,
            Variance = variance,
            VariancePercentage = variancePercentage,
            VarianceValue = varianceValue,
            UnitCost = lineItem.UnitCost,
            Severity = severity,
            CountedBy = command.CountedBy,
            BatchNumber = command.BatchNumber,
            Location = command.Location,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });

        // Also raise a variance calculated event for tracking
        RaiseEvent(new VarianceCalculated
        {
            StockTakeId = State.StockTakeId,
            IngredientId = command.IngredientId,
            TheoreticalQuantity = lineItem.TheoreticalQuantity,
            CountedQuantity = command.CountedQuantity,
            Variance = variance,
            VariancePercentage = variancePercentage,
            VarianceValue = varianceValue,
            Severity = severity,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();

        _logger.LogInformation(
            "Count recorded for {IngredientName}: counted={Counted}, theoretical={Theoretical}, variance={Variance} ({Severity})",
            lineItem.IngredientName,
            command.CountedQuantity,
            lineItem.TheoreticalQuantity,
            variance,
            severity);
    }

    public async Task RecordCountsAsync(IEnumerable<RecordCountCommand> commands)
    {
        foreach (var command in commands)
        {
            await RecordCountAsync(command);
        }
    }

    public Task<IReadOnlyList<StockTakeLineItem>> GetLineItemsAsync(bool includeTheoretical = true)
    {
        EnsureExists();

        var items = State.LineItems.Select(li => new StockTakeLineItem
        {
            IngredientId = li.IngredientId,
            IngredientName = li.IngredientName,
            Sku = li.Sku,
            Unit = li.Unit,
            Category = li.Category,
            // Hide theoretical in blind count mode until finalized
            TheoreticalQuantity = (State.BlindCount && State.Status != StockTakeStatus.Finalized && !includeTheoretical)
                ? 0 : li.TheoreticalQuantity,
            CountedQuantity = li.CountedQuantity,
            Variance = li.Variance,
            VariancePercentage = li.VariancePercentage,
            VarianceValue = li.VarianceValue,
            UnitCost = li.UnitCost,
            CountedBy = li.CountedBy,
            CountedAt = li.CountedAt,
            Severity = li.Severity,
            Location = li.Location,
            Notes = li.Notes
        }).ToList();

        return Task.FromResult<IReadOnlyList<StockTakeLineItem>>(items);
    }

    public Task<IReadOnlyList<StockTakeLineItem>> GetPendingItemsAsync()
    {
        EnsureExists();

        var pendingItems = State.LineItems
            .Where(li => !li.CountedQuantity.HasValue)
            .Select(li => new StockTakeLineItem
            {
                IngredientId = li.IngredientId,
                IngredientName = li.IngredientName,
                Sku = li.Sku,
                Unit = li.Unit,
                Category = li.Category,
                TheoreticalQuantity = State.BlindCount ? 0 : li.TheoreticalQuantity,
                UnitCost = li.UnitCost,
                Location = li.Location
            }).ToList();

        return Task.FromResult<IReadOnlyList<StockTakeLineItem>>(pendingItems);
    }

    public async Task SubmitForApprovalAsync(Guid submittedBy)
    {
        EnsureExists();
        EnsureInProgress();

        var totalItems = State.LineItems.Count;
        var itemsCounted = State.LineItems.Count(li => li.CountedQuantity.HasValue);

        if (itemsCounted == 0)
            throw new InvalidOperationException("Cannot submit stock take with no counts recorded");

        RaiseEvent(new StockTakeSubmittedForApproval
        {
            StockTakeId = State.StockTakeId,
            SubmittedBy = submittedBy,
            TotalItems = totalItems,
            ItemsCounted = itemsCounted,
            TotalVarianceValue = State.TotalVarianceValue,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogInformation(
            "Stock take '{Name}' submitted for approval. {ItemsCounted}/{TotalItems} items counted, variance: {Variance:C}",
            State.Name,
            itemsCounted,
            totalItems,
            State.TotalVarianceValue);
    }

    public async Task FinalizeAsync(FinalizeStockTakeCommand command)
    {
        EnsureExists();

        if (State.Status != StockTakeStatus.PendingApproval && State.Status != StockTakeStatus.InProgress)
            throw new InvalidOperationException($"Cannot finalize stock take in status {State.Status}");

        var itemsAdjusted = 0;

        if (command.ApplyAdjustments)
        {
            // Apply inventory adjustments for counted items
            foreach (var lineItem in State.LineItems.Where(li => li.CountedQuantity.HasValue))
            {
                if (lineItem.Variance != 0)
                {
                    var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(
                        GrainKeys.Inventory(State.OrganizationId, State.SiteId, lineItem.IngredientId));

                    await inventoryGrain.RecordPhysicalCountAsync(
                        lineItem.CountedQuantity!.Value,
                        command.ApprovedBy,
                        command.ApprovedBy);

                    itemsAdjusted++;
                }
            }
        }

        RaiseEvent(new StockTakeFinalized
        {
            StockTakeId = State.StockTakeId,
            FinalizedBy = command.ApprovedBy,
            AdjustmentsApplied = command.ApplyAdjustments,
            TotalVarianceValue = State.TotalVarianceValue,
            TotalPositiveVariance = State.TotalPositiveVariance,
            TotalNegativeVariance = State.TotalNegativeVariance,
            ItemsAdjusted = itemsAdjusted,
            ApprovalNotes = command.ApprovalNotes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Publish stream event
        await InventoryStream.OnNextAsync(new StockTakeFinalizedEvent(
            State.StockTakeId,
            State.SiteId,
            State.Name,
            State.TotalVarianceValue,
            itemsAdjusted,
            command.ApplyAdjustments)
        {
            OrganizationId = State.OrganizationId
        });

        _logger.LogInformation(
            "Stock take '{Name}' finalized by {ApprovedBy}. Adjustments applied: {Applied}, Items adjusted: {Count}, Total variance: {Variance:C}",
            State.Name,
            command.ApprovedBy,
            command.ApplyAdjustments,
            itemsAdjusted,
            State.TotalVarianceValue);
    }

    public async Task CancelAsync(Guid cancelledBy, string reason)
    {
        EnsureExists();

        if (State.Status == StockTakeStatus.Finalized)
            throw new InvalidOperationException("Cannot cancel a finalized stock take");

        if (State.Status == StockTakeStatus.Cancelled)
            throw new InvalidOperationException("Stock take already cancelled");

        RaiseEvent(new StockTakeCancelled
        {
            StockTakeId = State.StockTakeId,
            CancelledBy = cancelledBy,
            Reason = reason,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogInformation(
            "Stock take '{Name}' cancelled by {CancelledBy}. Reason: {Reason}",
            State.Name,
            cancelledBy,
            reason);
    }

    public Task<StockTakeVarianceReport> GetVarianceReportAsync()
    {
        EnsureExists();

        var countedItems = State.LineItems.Where(li => li.CountedQuantity.HasValue).ToList();
        var itemsWithVariance = countedItems.Where(li => li.Variance != 0).ToList();

        var varianceByCategory = countedItems
            .GroupBy(li => li.Category)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(li => li.VarianceValue));

        var highVarianceItems = countedItems
            .Where(li => li.Severity >= VarianceSeverity.High)
            .OrderByDescending(li => Math.Abs(li.VarianceValue))
            .Select(li => new StockTakeLineItem
            {
                IngredientId = li.IngredientId,
                IngredientName = li.IngredientName,
                Sku = li.Sku,
                Unit = li.Unit,
                Category = li.Category,
                TheoreticalQuantity = li.TheoreticalQuantity,
                CountedQuantity = li.CountedQuantity,
                Variance = li.Variance,
                VariancePercentage = li.VariancePercentage,
                VarianceValue = li.VarianceValue,
                UnitCost = li.UnitCost,
                CountedBy = li.CountedBy,
                CountedAt = li.CountedAt,
                Severity = li.Severity
            })
            .ToList();

        var accuracyPercentage = countedItems.Count > 0
            ? ((countedItems.Count - itemsWithVariance.Count) / (decimal)countedItems.Count) * 100
            : 100;

        return Task.FromResult(new StockTakeVarianceReport
        {
            StockTakeId = State.StockTakeId,
            TotalItems = State.LineItems.Count,
            ItemsCounted = countedItems.Count,
            ItemsWithVariance = itemsWithVariance.Count,
            TotalVarianceValue = State.TotalVarianceValue,
            TotalPositiveVariance = State.TotalPositiveVariance,
            TotalNegativeVariance = State.TotalNegativeVariance,
            CriticalVarianceCount = countedItems.Count(li => li.Severity == VarianceSeverity.Critical),
            HighVarianceCount = countedItems.Count(li => li.Severity == VarianceSeverity.High),
            MediumVarianceCount = countedItems.Count(li => li.Severity == VarianceSeverity.Medium),
            LowVarianceCount = countedItems.Count(li => li.Severity == VarianceSeverity.Low),
            VarianceByCategory = varianceByCategory,
            HighVarianceItems = highVarianceItems,
            AccuracyPercentage = accuracyPercentage
        });
    }

    public Task<IReadOnlyList<StockTakeLineItem>> GetHighVarianceItemsAsync(decimal thresholdPercentage = 5)
    {
        EnsureExists();

        var highVarianceItems = State.LineItems
            .Where(li => li.CountedQuantity.HasValue && Math.Abs(li.VariancePercentage) >= thresholdPercentage)
            .OrderByDescending(li => Math.Abs(li.VarianceValue))
            .Select(li => new StockTakeLineItem
            {
                IngredientId = li.IngredientId,
                IngredientName = li.IngredientName,
                Sku = li.Sku,
                Unit = li.Unit,
                Category = li.Category,
                TheoreticalQuantity = li.TheoreticalQuantity,
                CountedQuantity = li.CountedQuantity,
                Variance = li.Variance,
                VariancePercentage = li.VariancePercentage,
                VarianceValue = li.VarianceValue,
                UnitCost = li.UnitCost,
                CountedBy = li.CountedBy,
                CountedAt = li.CountedAt,
                Severity = li.Severity
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<StockTakeLineItem>>(highVarianceItems);
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.StockTakeId != Guid.Empty);

    private void EnsureExists()
    {
        if (State.StockTakeId == Guid.Empty)
            throw new InvalidOperationException("Stock take not started");
    }

    private void EnsureInProgress()
    {
        if (State.Status != StockTakeStatus.InProgress)
            throw new InvalidOperationException($"Stock take is not in progress (current status: {State.Status})");
    }

    private static VarianceSeverity CalculateVarianceSeverity(decimal absVariancePercentage)
    {
        return absVariancePercentage switch
        {
            0 => VarianceSeverity.None,
            < 2 => VarianceSeverity.Low,
            < 5 => VarianceSeverity.Medium,
            < 10 => VarianceSeverity.High,
            _ => VarianceSeverity.Critical
        };
    }
}
