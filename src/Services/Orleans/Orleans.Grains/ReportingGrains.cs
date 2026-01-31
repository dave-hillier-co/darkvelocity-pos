using DarkVelocity.Orleans.Abstractions.Costing;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.Projections;
using DarkVelocity.Orleans.Abstractions.State;
using DarkVelocity.Shared.Contracts.Events;
using Orleans.Runtime;

namespace DarkVelocity.Orleans.Grains;

// ============================================================================
// Daily Inventory Snapshot Grain
// ============================================================================

/// <summary>
/// Grain for daily inventory snapshot at site level.
/// Captures end-of-day stock levels for reporting and valuation.
/// </summary>
public class DailyInventorySnapshotGrain : Grain, IDailyInventorySnapshotGrain
{
    private readonly IPersistentState<DailyInventorySnapshotState> _state;

    public DailyInventorySnapshotGrain(
        [PersistentState("dailyInventorySnapshot", "OrleansStorage")]
        IPersistentState<DailyInventorySnapshotState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(InventorySnapshotCommand command)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new DailyInventorySnapshotState
        {
            OrgId = orgId,
            SiteId = command.SiteId,
            SiteName = command.SiteName,
            BusinessDate = command.BusinessDate,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task RecordIngredientSnapshotAsync(IngredientSnapshot snapshot)
    {
        EnsureInitialized();

        // Update aggregated totals
        _state.State.TotalStockValue += snapshot.TotalValue;
        _state.State.TotalSkuCount++;
        _state.State.ActiveBatchCount += snapshot.ActiveBatchCount;

        if (snapshot.IsLowStock)
            _state.State.LowStockCount++;
        if (snapshot.IsOutOfStock)
            _state.State.OutOfStockCount++;
        if (snapshot.IsExpiringSoon)
        {
            _state.State.ExpiringSoonCount++;
            _state.State.ExpiringSoonValue += snapshot.TotalValue;
        }
        if (snapshot.IsOverPar)
        {
            _state.State.OverParCount++;
            _state.State.OverParValue += snapshot.TotalValue;
        }

        // Store individual snapshot
        _state.State.Ingredients.Add(new IngredientSnapshotState
        {
            IngredientId = snapshot.IngredientId,
            IngredientName = snapshot.IngredientName,
            Sku = snapshot.Sku,
            Category = snapshot.Category,
            OnHandQuantity = snapshot.OnHandQuantity,
            AvailableQuantity = snapshot.AvailableQuantity,
            Unit = snapshot.Unit,
            WeightedAverageCost = snapshot.WeightedAverageCost,
            TotalValue = snapshot.TotalValue,
            EarliestExpiry = snapshot.EarliestExpiry,
            IsLowStock = snapshot.IsLowStock,
            IsOutOfStock = snapshot.IsOutOfStock,
            IsExpiringSoon = snapshot.IsExpiringSoon,
            IsOverPar = snapshot.IsOverPar,
            ActiveBatchCount = snapshot.ActiveBatchCount
        });

        // Create fact record
        var fact = new InventoryFact
        {
            FactId = Guid.NewGuid(),
            Date = _state.State.BusinessDate,
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            SiteName = _state.State.SiteName,
            IngredientId = snapshot.IngredientId,
            IngredientName = snapshot.IngredientName,
            Sku = snapshot.Sku,
            Category = snapshot.Category,
            OnHandQuantity = snapshot.OnHandQuantity,
            ReservedQuantity = snapshot.OnHandQuantity - snapshot.AvailableQuantity,
            AvailableQuantity = snapshot.AvailableQuantity,
            Unit = snapshot.Unit,
            UnitCost = snapshot.WeightedAverageCost,
            TotalValue = snapshot.TotalValue,
            CostingMethod = CostingMethod.WAC,
            ExpiryDate = snapshot.EarliestExpiry,
            FreezeState = FreezeState.Fresh,
            IsLowStock = snapshot.IsLowStock,
            IsOutOfStock = snapshot.IsOutOfStock,
            IsExpiringSoon = snapshot.IsExpiringSoon,
            IsOverPar = snapshot.IsOverPar,
            ReorderPoint = 0,
            ParLevel = 0,
            MaxLevel = 0
        };

        _state.State.Facts.Add(fact);
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<DailyInventorySnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();

        var ingredients = _state.State.Ingredients
            .Select(i => new IngredientSnapshot(
                IngredientId: i.IngredientId,
                IngredientName: i.IngredientName,
                Sku: i.Sku,
                Category: i.Category,
                OnHandQuantity: i.OnHandQuantity,
                AvailableQuantity: i.AvailableQuantity,
                Unit: i.Unit,
                WeightedAverageCost: i.WeightedAverageCost,
                TotalValue: i.TotalValue,
                EarliestExpiry: i.EarliestExpiry,
                IsLowStock: i.IsLowStock,
                IsOutOfStock: i.IsOutOfStock,
                IsExpiringSoon: i.IsExpiringSoon,
                IsOverPar: i.IsOverPar,
                ActiveBatchCount: i.ActiveBatchCount))
            .ToList();

        var snapshot = new DailyInventorySnapshot(
            Date: _state.State.BusinessDate,
            SiteId: _state.State.SiteId,
            SiteName: _state.State.SiteName,
            TotalStockValue: _state.State.TotalStockValue,
            TotalSkuCount: _state.State.TotalSkuCount,
            LowStockCount: _state.State.LowStockCount,
            OutOfStockCount: _state.State.OutOfStockCount,
            ExpiringSoonCount: _state.State.ExpiringSoonCount,
            ExpiringSoonValue: _state.State.ExpiringSoonValue,
            Ingredients: ingredients);

        return Task.FromResult(snapshot);
    }

    public Task<StockHealthMetrics> GetHealthMetricsAsync()
    {
        EnsureInitialized();

        var metrics = new StockHealthMetrics
        {
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            AsOfDate = _state.State.BusinessDate,
            TotalStockValue = _state.State.TotalStockValue,
            TotalSkuCount = _state.State.TotalSkuCount,
            ActiveBatchCount = _state.State.ActiveBatchCount,
            StockTurn = 0, // Calculated at period level
            AverageDaysOnHand = 0, // Calculated at period level
            LowStockCount = _state.State.LowStockCount,
            OutOfStockCount = _state.State.OutOfStockCount,
            ExpiringSoonCount = _state.State.ExpiringSoonCount,
            ExpiringSoonValue = _state.State.ExpiringSoonValue,
            OverParCount = _state.State.OverParCount,
            OverParValue = _state.State.OverParValue,
            ItemsAtPar = _state.State.TotalSkuCount - _state.State.LowStockCount - _state.State.OutOfStockCount - _state.State.OverParCount,
            TotalTrackedItems = _state.State.TotalSkuCount,
            AgedStockValue = 0 // Calculated separately
        };

        return Task.FromResult(metrics);
    }

    public Task<IReadOnlyList<InventoryFact>> GetFactsAsync()
    {
        EnsureInitialized();
        return Task.FromResult<IReadOnlyList<InventoryFact>>(_state.State.Facts);
    }

    public async Task FinalizeAsync()
    {
        EnsureInitialized();
        _state.State.IsFinalized = true;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private void EnsureInitialized()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Daily inventory snapshot grain not initialized");
    }
}

// ============================================================================
// Daily Consumption Grain
// ============================================================================

/// <summary>
/// Grain for daily consumption tracking at site level.
/// Tracks theoretical vs actual consumption for variance analysis.
/// </summary>
public class DailyConsumptionGrain : Grain, IDailyConsumptionGrain
{
    private readonly IPersistentState<DailyConsumptionState> _state;

    public DailyConsumptionGrain(
        [PersistentState("dailyConsumption", "OrleansStorage")]
        IPersistentState<DailyConsumptionState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(DateTime businessDate, Guid siteId)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new DailyConsumptionState
        {
            OrgId = orgId,
            SiteId = siteId,
            BusinessDate = businessDate,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task RecordConsumptionAsync(RecordConsumptionCommand command)
    {
        EnsureInitialized();

        // Update aggregated totals
        _state.State.TotalTheoreticalCost += command.TheoreticalCost;
        _state.State.TotalActualCost += command.ActualCost;

        // Update by-ingredient breakdown
        if (!_state.State.ByIngredient.TryGetValue(command.IngredientId, out var agg))
        {
            agg = new ConsumptionAggregation
            {
                IngredientId = command.IngredientId,
                IngredientName = command.IngredientName,
                Category = command.Category
            };
            _state.State.ByIngredient[command.IngredientId] = agg;
        }

        agg.TheoreticalQuantity += command.TheoreticalQuantity;
        agg.TheoreticalCost += command.TheoreticalCost;
        agg.ActualQuantity += command.ActualQuantity;
        agg.ActualCost += command.ActualCost;

        // Create fact record
        var fact = new ConsumptionFact
        {
            FactId = Guid.NewGuid(),
            Date = _state.State.BusinessDate,
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            IngredientId = command.IngredientId,
            IngredientName = command.IngredientName,
            Category = command.Category,
            Unit = command.Unit,
            TheoreticalQuantity = command.TheoreticalQuantity,
            TheoreticalCost = command.TheoreticalCost,
            ActualQuantity = command.ActualQuantity,
            ActualCost = command.ActualCost,
            CostingMethod = command.CostingMethod,
            OrderId = command.OrderId,
            MenuItemId = command.MenuItemId,
            RecipeVersionId = command.RecipeVersionId
        };

        _state.State.Facts.Add(fact);
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<DailyConsumptionSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();

        var totalVariance = _state.State.TotalActualCost - _state.State.TotalTheoreticalCost;
        var variancePercent = _state.State.TotalTheoreticalCost > 0
            ? totalVariance / _state.State.TotalTheoreticalCost * 100
            : 0;

        var topVariances = _state.State.ByIngredient.Values
            .Select(a => new VarianceBreakdown
            {
                IngredientId = a.IngredientId,
                IngredientName = a.IngredientName,
                Category = a.Category,
                TheoreticalUsage = a.TheoreticalQuantity,
                ActualUsage = a.ActualQuantity,
                TheoreticalCost = a.TheoreticalCost,
                ActualCost = a.ActualCost
            })
            .OrderByDescending(v => Math.Abs(v.CostVariance))
            .Take(10)
            .ToList();

        var snapshot = new DailyConsumptionSnapshot(
            Date: _state.State.BusinessDate,
            SiteId: _state.State.SiteId,
            TotalTheoreticalCost: _state.State.TotalTheoreticalCost,
            TotalActualCost: _state.State.TotalActualCost,
            TotalVariance: totalVariance,
            VariancePercent: variancePercent,
            TopVariances: topVariances);

        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<ConsumptionFact>> GetFactsAsync()
    {
        EnsureInitialized();
        return Task.FromResult<IReadOnlyList<ConsumptionFact>>(_state.State.Facts);
    }

    public Task<IReadOnlyList<VarianceBreakdown>> GetVarianceBreakdownAsync()
    {
        EnsureInitialized();

        var variances = _state.State.ByIngredient.Values
            .Select(a => new VarianceBreakdown
            {
                IngredientId = a.IngredientId,
                IngredientName = a.IngredientName,
                Category = a.Category,
                TheoreticalUsage = a.TheoreticalQuantity,
                ActualUsage = a.ActualQuantity,
                TheoreticalCost = a.TheoreticalCost,
                ActualCost = a.ActualCost
            })
            .OrderByDescending(v => Math.Abs(v.CostVariance))
            .ToList();

        return Task.FromResult<IReadOnlyList<VarianceBreakdown>>(variances);
    }

    private void EnsureInitialized()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Daily consumption grain not initialized");
    }
}

// ============================================================================
// Daily Waste Grain
// ============================================================================

/// <summary>
/// Grain for daily waste tracking at site level.
/// Tracks waste by reason and category for loss prevention.
/// </summary>
public class DailyWasteGrain : Grain, IDailyWasteGrain
{
    private readonly IPersistentState<DailyWasteState> _state;

    public DailyWasteGrain(
        [PersistentState("dailyWaste", "OrleansStorage")]
        IPersistentState<DailyWasteState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(DateTime businessDate, Guid siteId)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new DailyWasteState
        {
            OrgId = orgId,
            SiteId = siteId,
            BusinessDate = businessDate,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task RecordWasteAsync(RecordWasteFactCommand command)
    {
        EnsureInitialized();

        // Update aggregated totals
        _state.State.TotalWasteValue += command.CostBasis;
        _state.State.TotalWasteCount++;

        // Update by-reason breakdown
        if (!_state.State.ByReason.TryGetValue(command.Reason, out var reasonTotal))
            reasonTotal = 0;
        _state.State.ByReason[command.Reason] = reasonTotal + command.CostBasis;

        // Update by-category breakdown
        if (!_state.State.ByCategory.TryGetValue(command.Category, out var categoryTotal))
            categoryTotal = 0;
        _state.State.ByCategory[command.Category] = categoryTotal + command.CostBasis;

        // Create fact record
        var fact = new WasteFact
        {
            FactId = command.WasteId,
            Date = _state.State.BusinessDate,
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            SiteName = string.Empty, // Will be populated if needed
            IngredientId = command.IngredientId,
            IngredientName = command.IngredientName,
            Sku = command.Sku,
            Category = command.Category,
            BatchId = command.BatchId,
            Quantity = command.Quantity,
            Unit = command.Unit,
            Reason = command.Reason,
            ReasonDetails = command.ReasonDetails,
            CostBasis = command.CostBasis,
            PhotoUrl = command.PhotoUrl,
            RecordedBy = command.RecordedBy,
            ApprovedBy = command.ApprovedBy
        };

        _state.State.Facts.Add(fact);
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<DailyWasteSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();

        var snapshot = new DailyWasteSnapshot(
            Date: _state.State.BusinessDate,
            SiteId: _state.State.SiteId,
            TotalWasteValue: _state.State.TotalWasteValue,
            TotalWasteCount: _state.State.TotalWasteCount,
            WasteByReason: _state.State.ByReason,
            WasteByCategory: _state.State.ByCategory);

        return Task.FromResult(snapshot);
    }

    public Task<IReadOnlyList<WasteFact>> GetFactsAsync()
    {
        EnsureInitialized();
        return Task.FromResult<IReadOnlyList<WasteFact>>(_state.State.Facts);
    }

    private void EnsureInitialized()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Daily waste grain not initialized");
    }
}

// ============================================================================
// Period Aggregation Grain
// ============================================================================

/// <summary>
/// Grain for period aggregation (weekly, monthly).
/// Aggregates daily snapshots into higher-level summaries.
/// </summary>
public class PeriodAggregationGrain : Grain, IPeriodAggregationGrain
{
    private readonly IPersistentState<PeriodAggregationState> _state;

    public PeriodAggregationGrain(
        [PersistentState("periodAggregation", "OrleansStorage")]
        IPersistentState<PeriodAggregationState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(PeriodAggregationCommand command)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var siteId = Guid.Parse(parts[1]);

        _state.State = new PeriodAggregationState
        {
            OrgId = orgId,
            SiteId = siteId,
            PeriodType = command.PeriodType,
            PeriodStart = command.PeriodStart,
            PeriodEnd = command.PeriodEnd,
            PeriodNumber = command.PeriodNumber,
            FiscalYear = command.FiscalYear,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task AggregateFromDailyAsync(
        DateTime date,
        DailySalesSnapshot sales,
        DailyInventorySnapshot inventory,
        DailyConsumptionSnapshot consumption,
        DailyWasteSnapshot waste)
    {
        EnsureInitialized();

        // Aggregate sales
        _state.State.GrossSales += sales.GrossSales;
        _state.State.NetSales += sales.NetSales;
        _state.State.TransactionCount += sales.TransactionCount;
        _state.State.CoversServed += sales.GuestCount;

        // Aggregate COGS (using ActualCOGS for both FIFO and WAC for simplicity)
        _state.State.FifoActualCOGS += sales.ActualCOGS;
        _state.State.FifoTheoreticalCOGS += sales.TheoreticalCOGS;
        _state.State.WacActualCOGS += sales.ActualCOGS;
        _state.State.WacTheoreticalCOGS += sales.TheoreticalCOGS;

        // Use the latest inventory snapshot for closing values
        _state.State.ClosingStockValue = inventory.TotalStockValue;
        _state.State.LowStockCount = inventory.LowStockCount;
        _state.State.OutOfStockCount = inventory.OutOfStockCount;

        // Aggregate waste
        _state.State.TotalWasteValue += waste.TotalWasteValue;

        // Track included dates
        if (!_state.State.IncludedDates.Contains(date))
            _state.State.IncludedDates.Add(date);

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<PeriodSummary> GetSummaryAsync()
    {
        EnsureInitialized();

        var salesMetrics = new SalesMetrics
        {
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            PeriodStart = _state.State.PeriodStart,
            PeriodEnd = _state.State.PeriodEnd,
            PeriodType = _state.State.PeriodType.ToString(),
            GrossSales = _state.State.GrossSales,
            Discounts = _state.State.Discounts,
            Voids = _state.State.Voids,
            Comps = _state.State.Comps,
            Tax = _state.State.Tax,
            NetSales = _state.State.NetSales,
            TransactionCount = _state.State.TransactionCount,
            CoversServed = _state.State.CoversServed
        };

        var fifoGrossProfit = new GrossProfitMetrics
        {
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            PeriodStart = _state.State.PeriodStart,
            PeriodEnd = _state.State.PeriodEnd,
            PeriodType = _state.State.PeriodType.ToString(),
            NetSales = _state.State.NetSales,
            ActualCOGS = _state.State.FifoActualCOGS,
            TheoreticalCOGS = _state.State.FifoTheoreticalCOGS,
            CostingMethod = CostingMethod.FIFO
        };

        var wacGrossProfit = new GrossProfitMetrics
        {
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            PeriodStart = _state.State.PeriodStart,
            PeriodEnd = _state.State.PeriodEnd,
            PeriodType = _state.State.PeriodType.ToString(),
            NetSales = _state.State.NetSales,
            ActualCOGS = _state.State.WacActualCOGS,
            TheoreticalCOGS = _state.State.WacTheoreticalCOGS,
            CostingMethod = CostingMethod.WAC
        };

        var stockHealth = new StockHealthMetrics
        {
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            AsOfDate = _state.State.PeriodEnd,
            TotalStockValue = _state.State.ClosingStockValue,
            TotalSkuCount = 0,
            ActiveBatchCount = 0,
            StockTurn = _state.State.ClosingStockValue > 0
                ? _state.State.FifoActualCOGS / _state.State.ClosingStockValue
                : 0,
            AverageDaysOnHand = 0,
            LowStockCount = _state.State.LowStockCount,
            OutOfStockCount = _state.State.OutOfStockCount,
            ExpiringSoonCount = 0,
            ExpiringSoonValue = 0,
            OverParCount = 0,
            OverParValue = 0,
            ItemsAtPar = 0,
            TotalTrackedItems = 0,
            AgedStockValue = 0
        };

        var summary = new PeriodSummary(
            PeriodType: _state.State.PeriodType,
            PeriodStart: _state.State.PeriodStart,
            PeriodEnd: _state.State.PeriodEnd,
            PeriodNumber: _state.State.PeriodNumber,
            SalesMetrics: salesMetrics,
            FifoGrossProfit: fifoGrossProfit,
            WacGrossProfit: wacGrossProfit,
            StockHealth: stockHealth,
            TotalWasteValue: _state.State.TotalWasteValue);

        return Task.FromResult(summary);
    }

    public Task<SalesMetrics> GetSalesMetricsAsync()
    {
        EnsureInitialized();

        var metrics = new SalesMetrics
        {
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            PeriodStart = _state.State.PeriodStart,
            PeriodEnd = _state.State.PeriodEnd,
            PeriodType = _state.State.PeriodType.ToString(),
            GrossSales = _state.State.GrossSales,
            Discounts = _state.State.Discounts,
            Voids = _state.State.Voids,
            Comps = _state.State.Comps,
            Tax = _state.State.Tax,
            NetSales = _state.State.NetSales,
            TransactionCount = _state.State.TransactionCount,
            CoversServed = _state.State.CoversServed
        };

        return Task.FromResult(metrics);
    }

    public Task<GrossProfitMetrics> GetGrossProfitMetricsAsync(CostingMethod method)
    {
        EnsureInitialized();

        var metrics = new GrossProfitMetrics
        {
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            PeriodStart = _state.State.PeriodStart,
            PeriodEnd = _state.State.PeriodEnd,
            PeriodType = _state.State.PeriodType.ToString(),
            NetSales = _state.State.NetSales,
            ActualCOGS = method == CostingMethod.FIFO ? _state.State.FifoActualCOGS : _state.State.WacActualCOGS,
            TheoreticalCOGS = method == CostingMethod.FIFO ? _state.State.FifoTheoreticalCOGS : _state.State.WacTheoreticalCOGS,
            CostingMethod = method
        };

        return Task.FromResult(metrics);
    }

    public async Task FinalizeAsync()
    {
        EnsureInitialized();
        _state.State.IsFinalized = true;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private void EnsureInitialized()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Period aggregation grain not initialized");
    }
}

// ============================================================================
// Site Dashboard Grain
// ============================================================================

/// <summary>
/// Grain for site dashboard (GM view).
/// Provides real-time metrics and alerts for site managers.
/// </summary>
public class SiteDashboardGrain : Grain, ISiteDashboardGrain
{
    private readonly IPersistentState<SiteDashboardState> _state;
    private readonly IGrainFactory _grainFactory;

    public SiteDashboardGrain(
        [PersistentState("siteDashboard", "OrleansStorage")]
        IPersistentState<SiteDashboardState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public async Task InitializeAsync(Guid orgId, Guid siteId, string siteName)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        _state.State = new SiteDashboardState
        {
            OrgId = orgId,
            SiteId = siteId,
            SiteName = siteName,
            CurrentBusinessDate = DateTime.UtcNow.Date,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task RefreshAsync()
    {
        EnsureInitialized();

        var today = DateTime.UtcNow.Date;
        _state.State.CurrentBusinessDate = today;
        _state.State.LastRefreshed = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<DashboardMetrics> GetMetricsAsync()
    {
        EnsureInitialized();

        var topVariances = _state.State.TopVariances
            .Select(v => new VarianceBreakdown
            {
                IngredientId = v.IngredientId,
                IngredientName = v.IngredientName,
                Category = v.Category,
                TheoreticalUsage = v.TheoreticalUsage,
                ActualUsage = v.ActualUsage,
                TheoreticalCost = v.TheoreticalCost,
                ActualCost = v.ActualCost,
                LikelyReason = v.LikelyReason
            })
            .ToList();

        var lowStockItems = _state.State.LowStockItems
            .Select(i => new IngredientSnapshot(
                IngredientId: i.IngredientId,
                IngredientName: i.IngredientName,
                Sku: i.Sku,
                Category: i.Category,
                OnHandQuantity: i.OnHandQuantity,
                AvailableQuantity: i.AvailableQuantity,
                Unit: i.Unit,
                WeightedAverageCost: i.WeightedAverageCost,
                TotalValue: i.TotalValue,
                EarliestExpiry: i.EarliestExpiry,
                IsLowStock: i.IsLowStock,
                IsOutOfStock: i.IsOutOfStock,
                IsExpiringSoon: i.IsExpiringSoon,
                IsOverPar: i.IsOverPar,
                ActiveBatchCount: i.ActiveBatchCount))
            .ToList();

        var metrics = new DashboardMetrics(
            TodayNetSales: 0, // Populated from daily sales grain
            TodayNetSalesVsLastWeek: 0,
            TodayNetSalesVsLastYear: 0,
            TodayGrossProfitPercent: 0,
            TodayGrossProfitVsBudget: _state.State.BudgetGrossProfitPercent,
            WtdNetSales: 0,
            WtdGrossProfitPercent: 0,
            PtdNetSales: 0,
            PtdGrossProfitPercent: 0,
            LowStockAlertCount: _state.State.LowStockAlertCount,
            OutOfStockAlertCount: _state.State.OutOfStockAlertCount,
            ExpiryRiskCount: _state.State.ExpiryRiskCount,
            HighVarianceCount: _state.State.HighVarianceCount,
            OutstandingPOValue: _state.State.OutstandingPOValue,
            TopVariances: topVariances,
            LowStockItems: lowStockItems);

        return Task.FromResult(metrics);
    }

    public async Task<DailySalesSnapshot> GetTodaySalesAsync()
    {
        EnsureInitialized();

        var today = DateTime.UtcNow.Date;
        var key = $"{_state.State.OrgId}:{_state.State.SiteId}:sales:{today:yyyy-MM-dd}";
        var salesGrain = _grainFactory.GetGrain<IDailySalesGrain>(key);

        try
        {
            return await salesGrain.GetSnapshotAsync();
        }
        catch
        {
            // Return empty snapshot if not initialized
            return new DailySalesSnapshot(
                Date: today,
                SiteId: _state.State.SiteId,
                SiteName: _state.State.SiteName,
                GrossSales: 0,
                NetSales: 0,
                TheoreticalCOGS: 0,
                ActualCOGS: 0,
                GrossProfit: 0,
                GrossProfitPercent: 0,
                TransactionCount: 0,
                GuestCount: 0,
                AverageTicket: 0,
                SalesByChannel: new Dictionary<SaleChannel, decimal>(),
                SalesByCategory: new Dictionary<string, decimal>());
        }
    }

    public async Task<DailyInventorySnapshot> GetCurrentInventoryAsync()
    {
        EnsureInitialized();

        var today = DateTime.UtcNow.Date;
        var key = $"{_state.State.OrgId}:{_state.State.SiteId}:inventory-snapshot:{today:yyyy-MM-dd}";
        var inventoryGrain = _grainFactory.GetGrain<IDailyInventorySnapshotGrain>(key);

        try
        {
            return await inventoryGrain.GetSnapshotAsync();
        }
        catch
        {
            // Return empty snapshot if not initialized
            return new DailyInventorySnapshot(
                Date: today,
                SiteId: _state.State.SiteId,
                SiteName: _state.State.SiteName,
                TotalStockValue: 0,
                TotalSkuCount: 0,
                LowStockCount: 0,
                OutOfStockCount: 0,
                ExpiringSoonCount: 0,
                ExpiringSoonValue: 0,
                Ingredients: []);
        }
    }

    public async Task<IReadOnlyList<VarianceBreakdown>> GetTopVariancesAsync(int count)
    {
        EnsureInitialized();

        var today = DateTime.UtcNow.Date;
        var key = $"{_state.State.OrgId}:{_state.State.SiteId}:consumption:{today:yyyy-MM-dd}";
        var consumptionGrain = _grainFactory.GetGrain<IDailyConsumptionGrain>(key);

        try
        {
            var variances = await consumptionGrain.GetVarianceBreakdownAsync();
            return variances.Take(count).ToList();
        }
        catch
        {
            return [];
        }
    }

    private void EnsureInitialized()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Site dashboard grain not initialized");
    }
}
