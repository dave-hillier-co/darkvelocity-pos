using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain that aggregates profitability data for a site.
/// </summary>
public class ProfitabilityDashboardGrain : Grain, IProfitabilityDashboardGrain
{
    private readonly IPersistentState<ProfitabilityDashboardState> _state;
    private const int MaxTrendPoints = 365; // One year of daily data
    private const int MaxItemRecords = 5000;

    public ProfitabilityDashboardGrain(
        [PersistentState("profitability", "OrleansStorage")]
        IPersistentState<ProfitabilityDashboardState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(InitializeProfitabilityDashboardCommand command)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        _state.State = new ProfitabilityDashboardState
        {
            OrgId = command.OrgId,
            SiteId = command.SiteId,
            SiteName = command.SiteName,
            CreatedAt = DateTime.UtcNow
        };

        await _state.WriteStateAsync();
    }

    public async Task RecordItemCostDataAsync(RecordItemCostDataCommand command)
    {
        EnsureInitialized();

        var itemData = _state.State.ItemCostData.FirstOrDefault(i => i.ItemId == command.ItemId);
        if (itemData != null)
        {
            // Update existing
            itemData.ItemName = command.ItemName;
            itemData.Category = command.Category;
            itemData.SellingPrice = command.SellingPrice;
            itemData.TheoreticalCost = command.TheoreticalCost;
            itemData.ActualCost = command.ActualCost;
            itemData.UnitsSold += command.UnitsSold;
            itemData.TotalRevenue += command.TotalRevenue;
            itemData.LastUpdated = DateTime.UtcNow;
        }
        else
        {
            // Add new
            _state.State.ItemCostData.Add(new ItemCostDataState
            {
                ItemId = command.ItemId,
                ItemName = command.ItemName,
                Category = command.Category,
                SellingPrice = command.SellingPrice,
                TheoreticalCost = command.TheoreticalCost,
                ActualCost = command.ActualCost,
                UnitsSold = command.UnitsSold,
                TotalRevenue = command.TotalRevenue,
                RecordedDate = command.RecordedDate,
                LastUpdated = DateTime.UtcNow
            });

            // Trim if needed
            if (_state.State.ItemCostData.Count > MaxItemRecords)
            {
                _state.State.ItemCostData = _state.State.ItemCostData
                    .OrderByDescending(i => i.LastUpdated)
                    .Take(MaxItemRecords)
                    .ToList();
            }
        }

        _state.State.ModifiedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RecordDailyCostSummaryAsync(RecordDailyCostSummaryCommand command)
    {
        EnsureInitialized();

        var existingIndex = _state.State.CostTrends.FindIndex(t => t.Date.Date == command.Date.Date);
        if (existingIndex >= 0)
        {
            _state.State.CostTrends[existingIndex] = new CostTrendState
            {
                Date = command.Date,
                FoodCostPercent = command.FoodCostPercent,
                BeverageCostPercent = command.BeverageCostPercent,
                TotalCost = command.TotalCost,
                TotalRevenue = command.TotalRevenue
            };
        }
        else
        {
            _state.State.CostTrends.Add(new CostTrendState
            {
                Date = command.Date,
                FoodCostPercent = command.FoodCostPercent,
                BeverageCostPercent = command.BeverageCostPercent,
                TotalCost = command.TotalCost,
                TotalRevenue = command.TotalRevenue
            });

            // Trim if needed
            if (_state.State.CostTrends.Count > MaxTrendPoints)
            {
                _state.State.CostTrends = _state.State.CostTrends
                    .OrderByDescending(t => t.Date)
                    .Take(MaxTrendPoints)
                    .ToList();
            }
        }

        _state.State.ModifiedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<ProfitabilityDashboard> GetDashboardAsync(DateRange range)
    {
        EnsureInitialized();

        var filteredItems = _state.State.ItemCostData
            .Where(i => i.RecordedDate >= range.StartDate && i.RecordedDate <= range.EndDate)
            .ToList();

        var totalRevenue = filteredItems.Sum(i => i.TotalRevenue);
        var totalCost = filteredItems.Sum(i => i.ActualCost * i.UnitsSold);
        var grossProfit = totalRevenue - totalCost;
        var grossProfitMargin = totalRevenue > 0 ? grossProfit / totalRevenue * 100 : 0;
        var overallCostPercent = totalRevenue > 0 ? totalCost / totalRevenue * 100 : 0;

        // Calculate food and beverage cost percentages
        var foodItems = filteredItems.Where(i => IsFoodCategory(i.Category)).ToList();
        var beverageItems = filteredItems.Where(i => IsBeverageCategory(i.Category)).ToList();

        var foodRevenue = foodItems.Sum(i => i.TotalRevenue);
        var foodCost = foodItems.Sum(i => i.ActualCost * i.UnitsSold);
        var foodCostPercent = foodRevenue > 0 ? foodCost / foodRevenue * 100 : 0;

        var beverageRevenue = beverageItems.Sum(i => i.TotalRevenue);
        var beverageCost = beverageItems.Sum(i => i.ActualCost * i.UnitsSold);
        var beverageCostPercent = beverageRevenue > 0 ? beverageCost / beverageRevenue * 100 : 0;

        // Theoretical vs actual variance
        var totalTheoreticalCost = filteredItems.Sum(i => i.TheoreticalCost * i.UnitsSold);
        var totalVariance = totalCost - totalTheoreticalCost;
        var variancePercent = totalTheoreticalCost > 0 ? totalVariance / totalTheoreticalCost * 100 : 0;

        var dashboard = new ProfitabilityDashboard(
            _state.State.OrgId,
            _state.State.SiteId,
            range.StartDate,
            range.EndDate,
            totalRevenue,
            totalCost,
            grossProfit,
            grossProfitMargin,
            foodCostPercent,
            beverageCostPercent,
            overallCostPercent,
            GetCategoryBreakdownInternal(filteredItems),
            GetTopMarginItemsInternal(filteredItems, 10),
            GetBottomMarginItemsInternal(filteredItems, 10),
            GetCostTrendsInternal(range),
            totalTheoreticalCost,
            totalCost,
            totalVariance,
            variancePercent,
            GetTopVarianceItemsInternal(filteredItems, 10));

        return Task.FromResult(dashboard);
    }

    public Task<IReadOnlyList<CategoryCostBreakdown>> GetCategoryBreakdownAsync()
    {
        EnsureInitialized();
        return Task.FromResult(GetCategoryBreakdownInternal(_state.State.ItemCostData));
    }

    public Task<ItemProfitability?> GetItemProfitabilityAsync(Guid itemId)
    {
        EnsureInitialized();

        var item = _state.State.ItemCostData.FirstOrDefault(i => i.ItemId == itemId);
        if (item == null)
            return Task.FromResult<ItemProfitability?>(null);

        return Task.FromResult<ItemProfitability?>(ToItemProfitability(item));
    }

    public Task<IReadOnlyList<CostTrendPoint>> GetCostTrendsAsync(DateRange range)
    {
        EnsureInitialized();
        return Task.FromResult(GetCostTrendsInternal(range));
    }

    public Task<IReadOnlyList<CostVariance>> GetTopVarianceItemsAsync(int count = 10)
    {
        EnsureInitialized();
        return Task.FromResult(GetTopVarianceItemsInternal(_state.State.ItemCostData, count));
    }

    public Task<IReadOnlyList<ItemProfitability>> GetTopMarginItemsAsync(int count = 10)
    {
        EnsureInitialized();
        return Task.FromResult(GetTopMarginItemsInternal(_state.State.ItemCostData, count));
    }

    public Task<IReadOnlyList<ItemProfitability>> GetBottomMarginItemsAsync(int count = 10)
    {
        EnsureInitialized();
        return Task.FromResult(GetBottomMarginItemsInternal(_state.State.ItemCostData, count));
    }

    public async Task ClearAsync()
    {
        _state.State.ItemCostData.Clear();
        _state.State.CostTrends.Clear();
        _state.State.ModifiedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    // ============================================================================
    // Private Helpers
    // ============================================================================

    private void EnsureInitialized()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Profitability dashboard not initialized");
    }

    private static bool IsFoodCategory(string category)
    {
        var lowerCategory = category.ToLowerInvariant();
        return lowerCategory.Contains("food") ||
               lowerCategory.Contains("appetizer") ||
               lowerCategory.Contains("entree") ||
               lowerCategory.Contains("main") ||
               lowerCategory.Contains("side") ||
               lowerCategory.Contains("dessert") ||
               lowerCategory.Contains("salad") ||
               lowerCategory.Contains("soup");
    }

    private static bool IsBeverageCategory(string category)
    {
        var lowerCategory = category.ToLowerInvariant();
        return lowerCategory.Contains("beverage") ||
               lowerCategory.Contains("drink") ||
               lowerCategory.Contains("beer") ||
               lowerCategory.Contains("wine") ||
               lowerCategory.Contains("cocktail") ||
               lowerCategory.Contains("spirit") ||
               lowerCategory.Contains("coffee") ||
               lowerCategory.Contains("tea") ||
               lowerCategory.Contains("soda") ||
               lowerCategory.Contains("juice");
    }

    private static IReadOnlyList<CategoryCostBreakdown> GetCategoryBreakdownInternal(List<ItemCostDataState> items)
    {
        return items
            .GroupBy(i => i.Category)
            .Select(g =>
            {
                var categoryItems = g.ToList();
                var totalCost = categoryItems.Sum(i => i.ActualCost * i.UnitsSold);
                var totalRevenue = categoryItems.Sum(i => i.TotalRevenue);
                var contribution = totalRevenue - totalCost;
                var costPercent = totalRevenue > 0 ? totalCost / totalRevenue * 100 : 0;
                var marginPercent = totalRevenue > 0 ? contribution / totalRevenue * 100 : 0;

                return new CategoryCostBreakdown(
                    g.Key,
                    totalCost,
                    totalRevenue,
                    contribution,
                    costPercent,
                    marginPercent,
                    categoryItems.Count,
                    categoryItems.Sum(i => i.UnitsSold));
            })
            .OrderByDescending(c => c.TotalRevenue)
            .ToList();
    }

    private static IReadOnlyList<ItemProfitability> GetTopMarginItemsInternal(List<ItemCostDataState> items, int count)
    {
        return items
            .Where(i => i.UnitsSold > 0)
            .Select(ToItemProfitability)
            .OrderByDescending(i => i.ContributionMarginPercent)
            .Take(count)
            .ToList();
    }

    private static IReadOnlyList<ItemProfitability> GetBottomMarginItemsInternal(List<ItemCostDataState> items, int count)
    {
        return items
            .Where(i => i.UnitsSold > 0)
            .Select(ToItemProfitability)
            .OrderBy(i => i.ContributionMarginPercent)
            .Take(count)
            .ToList();
    }

    private IReadOnlyList<CostTrendPoint> GetCostTrendsInternal(DateRange range)
    {
        return _state.State.CostTrends
            .Where(t => t.Date >= range.StartDate && t.Date <= range.EndDate)
            .OrderBy(t => t.Date)
            .Select(t =>
            {
                var overallCostPercent = t.TotalRevenue > 0 ? t.TotalCost / t.TotalRevenue * 100 : 0;
                return new CostTrendPoint(
                    t.Date,
                    t.FoodCostPercent,
                    t.BeverageCostPercent,
                    overallCostPercent,
                    t.TotalCost,
                    t.TotalRevenue);
            })
            .ToList();
    }

    private static IReadOnlyList<CostVariance> GetTopVarianceItemsInternal(List<ItemCostDataState> items, int count)
    {
        return items
            .Where(i => i.UnitsSold > 0)
            .Select(i =>
            {
                var varianceAmount = i.ActualCost - i.TheoreticalCost;
                var variancePercent = i.TheoreticalCost > 0 ? varianceAmount / i.TheoreticalCost * 100 : 0;
                var totalVariance = varianceAmount * i.UnitsSold;

                return new CostVariance(
                    i.ItemId,
                    i.ItemName,
                    i.Category,
                    i.TheoreticalCost,
                    i.ActualCost,
                    varianceAmount,
                    variancePercent,
                    i.UnitsSold,
                    totalVariance);
            })
            .OrderByDescending(v => Math.Abs(v.TotalVariance))
            .Take(count)
            .ToList();
    }

    private static ItemProfitability ToItemProfitability(ItemCostDataState item)
    {
        var contributionMargin = item.SellingPrice - item.ActualCost;
        var contributionMarginPercent = item.SellingPrice > 0 ? contributionMargin / item.SellingPrice * 100 : 0;
        var variance = item.ActualCost - item.TheoreticalCost;
        var variancePercent = item.TheoreticalCost > 0 ? variance / item.TheoreticalCost * 100 : 0;

        return new ItemProfitability(
            item.ItemId,
            item.ItemName,
            item.Category,
            item.SellingPrice,
            item.TheoreticalCost,
            item.ActualCost,
            contributionMargin,
            contributionMarginPercent,
            variance,
            variancePercent,
            item.UnitsSold,
            item.TotalRevenue,
            contributionMargin * item.UnitsSold);
    }
}
