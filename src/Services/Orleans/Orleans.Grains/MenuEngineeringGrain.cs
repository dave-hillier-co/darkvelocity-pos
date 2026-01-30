using DarkVelocity.Orleans.Abstractions.Costing;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using Orleans.Runtime;

namespace DarkVelocity.Orleans.Grains;

/// <summary>
/// Grain for menu engineering analysis at site level.
/// Classifies menu items as Stars, Plowhorses, Puzzles, or Dogs.
/// </summary>
public class MenuEngineeringGrain : Grain, IMenuEngineeringGrain
{
    private readonly IPersistentState<MenuEngineeringState> _state;

    public MenuEngineeringGrain(
        [PersistentState("menuEngineering", "OrleansStorage")]
        IPersistentState<MenuEngineeringState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(InitializeMenuEngineeringCommand command)
    {
        if (_state.State.SiteId != Guid.Empty)
            return; // Already initialized

        _state.State = new MenuEngineeringState
        {
            OrgId = command.OrgId,
            SiteId = command.SiteId,
            SiteName = command.SiteName,
            DefaultTargetMarginPercent = command.TargetMarginPercent,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task RecordItemSalesAsync(RecordItemSalesCommand command)
    {
        EnsureInitialized();

        var existing = _state.State.Items.FirstOrDefault(i => i.ProductId == command.ProductId);
        if (existing != null)
        {
            existing.SellingPrice = command.SellingPrice;
            existing.TheoreticalCost = command.TheoreticalCost;
            existing.UnitsSold += command.UnitsSold;
            existing.TotalRevenue += command.TotalRevenue;
            existing.LastUpdated = DateTime.UtcNow;
        }
        else
        {
            _state.State.Items.Add(new MenuItemRecord
            {
                ProductId = command.ProductId,
                ProductName = command.ProductName,
                Category = command.Category,
                SellingPrice = command.SellingPrice,
                TheoreticalCost = command.TheoreticalCost,
                UnitsSold = command.UnitsSold,
                TotalRevenue = command.TotalRevenue,
                RecipeId = command.RecipeId,
                RecipeVersionId = command.RecipeVersionId,
                LastUpdated = DateTime.UtcNow
            });
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task BulkRecordSalesAsync(IReadOnlyList<RecordItemSalesCommand> commands)
    {
        foreach (var command in commands)
        {
            await RecordItemSalesAsync(command);
        }
    }

    public async Task<MenuEngineeringReport> AnalyzeAsync(AnalyzeMenuCommand command)
    {
        EnsureInitialized();

        _state.State.CurrentPeriodStart = command.PeriodStart;
        _state.State.CurrentPeriodEnd = command.PeriodEnd;

        // Calculate category-level metrics first
        var categoryMetrics = CalculateCategoryMetrics();

        // Analyze each item
        var analyses = new List<MenuItemAnalysisRecord>();
        foreach (var item in _state.State.Items)
        {
            var analysis = AnalyzeItem(item, categoryMetrics);
            analyses.Add(analysis);
        }

        // Cache the analysis
        _state.State.CachedAnalysis = analyses;
        _state.State.CachedCategoryAnalysis = categoryMetrics.Values.ToList();
        _state.State.LastAnalyzedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Build the report
        return BuildReport(analyses, categoryMetrics);
    }

    public Task<IReadOnlyList<MenuItemAnalysis>> GetItemAnalysisAsync(string? category = null)
    {
        EnsureInitialized();

        var analyses = _state.State.CachedAnalysis
            .Where(a => category == null || a.Category == category)
            .Select(ToMenuItemAnalysis)
            .ToList();

        return Task.FromResult<IReadOnlyList<MenuItemAnalysis>>(analyses);
    }

    public Task<IReadOnlyList<CategoryAnalysis>> GetCategoryAnalysisAsync()
    {
        EnsureInitialized();

        var analyses = _state.State.CachedCategoryAnalysis
            .Select(ToCategoryAnalysis)
            .ToList();

        return Task.FromResult<IReadOnlyList<CategoryAnalysis>>(analyses);
    }

    public Task<MenuItemAnalysis?> GetItemAsync(Guid productId)
    {
        EnsureInitialized();

        var analysis = _state.State.CachedAnalysis.FirstOrDefault(a => a.ProductId == productId);
        return Task.FromResult(analysis != null ? ToMenuItemAnalysis(analysis) : null);
    }

    public Task<IReadOnlyList<MenuItemAnalysis>> GetItemsByClassAsync(MenuClass menuClass)
    {
        EnsureInitialized();

        var analyses = _state.State.CachedAnalysis
            .Where(a => a.Classification == menuClass)
            .Select(ToMenuItemAnalysis)
            .ToList();

        return Task.FromResult<IReadOnlyList<MenuItemAnalysis>>(analyses);
    }

    public Task<IReadOnlyDictionary<MenuClass, int>> GetClassificationCountsAsync()
    {
        EnsureInitialized();

        var counts = _state.State.CachedAnalysis
            .GroupBy(a => a.Classification)
            .ToDictionary(g => g.Key, g => g.Count());

        // Ensure all classes are represented
        foreach (var menuClass in Enum.GetValues<MenuClass>())
        {
            if (!counts.ContainsKey(menuClass))
                counts[menuClass] = 0;
        }

        return Task.FromResult<IReadOnlyDictionary<MenuClass, int>>(counts);
    }

    public Task<IReadOnlyList<PriceOptimizationSuggestion>> GetPriceSuggestionsAsync(
        decimal targetMarginPercent,
        decimal maxPriceChangePercent = 15m)
    {
        EnsureInitialized();

        var suggestions = new List<PriceOptimizationSuggestion>();

        foreach (var item in _state.State.CachedAnalysis)
        {
            var currentMargin = item.ContributionMarginPercent;

            if (currentMargin < targetMarginPercent - 5)
            {
                // Below target margin - suggest price increase
                var targetPrice = item.TheoreticalCost / (1 - targetMarginPercent / 100);
                var priceChange = targetPrice - item.SellingPrice;
                var priceChangePercent = item.SellingPrice > 0 ? priceChange / item.SellingPrice * 100 : 0;

                if (priceChangePercent <= maxPriceChangePercent && priceChange > 0)
                {
                    suggestions.Add(new PriceOptimizationSuggestion
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Category = item.Category,
                        CurrentPrice = item.SellingPrice,
                        SuggestedPrice = Math.Round(targetPrice, 2),
                        PriceChange = Math.Round(priceChange, 2),
                        PriceChangePercent = Math.Round(priceChangePercent, 1),
                        CurrentMargin = Math.Round(currentMargin, 1),
                        ProjectedMargin = Math.Round(targetMarginPercent, 1),
                        TargetMargin = targetMarginPercent,
                        Rationale = $"Current margin {currentMargin:F1}% is below target of {targetMarginPercent:F1}%",
                        SuggestionType = PriceSuggestionType.IncreaseToTargetMargin,
                        ConfidenceScore = 0.8m
                    });
                }
            }
        }

        return Task.FromResult<IReadOnlyList<PriceOptimizationSuggestion>>(
            suggestions.OrderByDescending(s => Math.Abs(s.TargetMargin - s.CurrentMargin)).ToList());
    }

    public async Task SetTargetMarginAsync(decimal targetMarginPercent)
    {
        EnsureInitialized();
        _state.State.DefaultTargetMarginPercent = targetMarginPercent;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task SetCategoryTargetMarginAsync(string category, decimal targetMarginPercent)
    {
        EnsureInitialized();
        _state.State.CategoryTargetMargins[category] = targetMarginPercent;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private void EnsureInitialized()
    {
        if (_state.State.SiteId == Guid.Empty)
            throw new InvalidOperationException("Menu engineering grain not initialized");
    }

    private Dictionary<string, CategoryAnalysisRecord> CalculateCategoryMetrics()
    {
        return _state.State.Items
            .GroupBy(i => i.Category)
            .ToDictionary(g => g.Key, g =>
            {
                var items = g.ToList();
                var totalUnits = items.Sum(i => i.UnitsSold);
                var totalRevenue = items.Sum(i => i.TotalRevenue);
                var totalCost = items.Sum(i => i.TheoreticalCost * i.UnitsSold);
                var totalContribution = totalRevenue - totalCost;

                return new CategoryAnalysisRecord
                {
                    Category = g.Key,
                    ItemCount = items.Count,
                    TotalUnitsSold = totalUnits,
                    TotalRevenue = totalRevenue,
                    TotalCost = totalCost,
                    TotalContribution = totalContribution,
                    AverageContributionMargin = items.Count > 0 ? totalContribution / items.Count : 0,
                    AverageContributionMarginPercent = totalRevenue > 0 ? totalContribution / totalRevenue * 100 : 0,
                    AverageSellingPrice = items.Count > 0 ? items.Average(i => i.SellingPrice) : 0,
                    AverageUnitsSold = items.Count > 0 ? (decimal)totalUnits / items.Count : 0
                };
            });
    }

    private MenuItemAnalysisRecord AnalyzeItem(MenuItemRecord item, Dictionary<string, CategoryAnalysisRecord> categoryMetrics)
    {
        var contributionMargin = item.SellingPrice - item.TheoreticalCost;
        var contributionMarginPercent = item.SellingPrice > 0 ? contributionMargin / item.SellingPrice * 100 : 0;
        var totalContribution = contributionMargin * item.UnitsSold;

        var category = categoryMetrics.GetValueOrDefault(item.Category);
        var menuMix = category?.TotalUnitsSold > 0 ? (decimal)item.UnitsSold / category.TotalUnitsSold * 100 : 0;
        var revenueMix = category?.TotalRevenue > 0 ? item.TotalRevenue / category.TotalRevenue * 100 : 0;
        var contributionMix = category?.TotalContribution > 0 ? totalContribution / category.TotalContribution * 100 : 0;

        // Calculate indexes (vs category average)
        var popularityIndex = category?.AverageUnitsSold > 0 ? item.UnitsSold / category.AverageUnitsSold : 1;
        var profitabilityIndex = category?.AverageContributionMargin > 0 ? contributionMargin / category.AverageContributionMargin : 1;

        // Classify based on popularity and profitability
        var isHighPopularity = popularityIndex >= 1.0m;
        var isHighProfitability = profitabilityIndex >= 1.0m;

        var classification = (isHighPopularity, isHighProfitability) switch
        {
            (true, true) => MenuClass.Star,
            (true, false) => MenuClass.Plowhorse,
            (false, true) => MenuClass.Puzzle,
            (false, false) => MenuClass.Dog
        };

        return new MenuItemAnalysisRecord
        {
            ProductId = item.ProductId,
            ProductName = item.ProductName,
            Category = item.Category,
            SellingPrice = item.SellingPrice,
            TheoreticalCost = item.TheoreticalCost,
            ContributionMargin = contributionMargin,
            ContributionMarginPercent = contributionMarginPercent,
            UnitsSold = item.UnitsSold,
            TotalRevenue = item.TotalRevenue,
            TotalContribution = totalContribution,
            MenuMix = menuMix,
            RevenueMix = revenueMix,
            ContributionMix = contributionMix,
            Classification = classification,
            PopularityIndex = popularityIndex,
            ProfitabilityIndex = profitabilityIndex,
            RecipeId = item.RecipeId,
            RecipeVersionId = item.RecipeVersionId
        };
    }

    private MenuEngineeringReport BuildReport(
        List<MenuItemAnalysisRecord> analyses,
        Dictionary<string, CategoryAnalysisRecord> categoryMetrics)
    {
        var itemAnalyses = analyses.Select(ToMenuItemAnalysis).ToList();
        var categoryAnalyses = categoryMetrics.Values.Select(ToCategoryAnalysis).ToList();

        // Update classification counts in category analysis
        foreach (var cat in categoryMetrics)
        {
            var catItems = analyses.Where(a => a.Category == cat.Key).ToList();
            cat.Value.StarCount = catItems.Count(a => a.Classification == MenuClass.Star);
            cat.Value.PlowhorseCount = catItems.Count(a => a.Classification == MenuClass.Plowhorse);
            cat.Value.PuzzleCount = catItems.Count(a => a.Classification == MenuClass.Puzzle);
            cat.Value.DogCount = catItems.Count(a => a.Classification == MenuClass.Dog);
        }

        var totalRevenue = analyses.Sum(a => a.TotalRevenue);
        var totalCost = analyses.Sum(a => a.TheoreticalCost * a.UnitsSold);
        var totalContribution = totalRevenue - totalCost;

        return new MenuEngineeringReport
        {
            OrgId = _state.State.OrgId,
            SiteId = _state.State.SiteId,
            PeriodStart = _state.State.CurrentPeriodStart ?? DateTime.Today.AddMonths(-1),
            PeriodEnd = _state.State.CurrentPeriodEnd ?? DateTime.Today,
            TotalRevenue = totalRevenue,
            TotalCost = totalCost,
            TotalContribution = totalContribution,
            OverallMarginPercent = totalRevenue > 0 ? totalContribution / totalRevenue * 100 : 0,
            TotalItemsSold = analyses.Sum(a => a.UnitsSold),
            TotalMenuItems = analyses.Count,
            Items = itemAnalyses,
            Categories = categoryAnalyses,
            StarCount = analyses.Count(a => a.Classification == MenuClass.Star),
            PlowhorseCount = analyses.Count(a => a.Classification == MenuClass.Plowhorse),
            PuzzleCount = analyses.Count(a => a.Classification == MenuClass.Puzzle),
            DogCount = analyses.Count(a => a.Classification == MenuClass.Dog),
            TopStars = itemAnalyses
                .Where(i => i.Classification == MenuClass.Star)
                .OrderByDescending(i => i.TotalContribution)
                .Take(10)
                .ToList(),
            TopContributors = itemAnalyses
                .OrderByDescending(i => i.TotalContribution)
                .Take(10)
                .ToList(),
            LowMarginHighVolume = itemAnalyses
                .Where(i => i.Classification == MenuClass.Plowhorse)
                .OrderBy(i => i.ContributionMarginPercent)
                .Take(10)
                .ToList(),
            HighMarginLowVolume = itemAnalyses
                .Where(i => i.Classification == MenuClass.Puzzle)
                .OrderByDescending(i => i.ContributionMarginPercent)
                .Take(10)
                .ToList(),
            DogsToReview = itemAnalyses
                .Where(i => i.Classification == MenuClass.Dog)
                .OrderBy(i => i.TotalContribution)
                .Take(10)
                .ToList()
        };
    }

    private static MenuItemAnalysis ToMenuItemAnalysis(MenuItemAnalysisRecord record)
    {
        return new MenuItemAnalysis
        {
            ProductId = record.ProductId,
            ProductName = record.ProductName,
            Category = record.Category,
            SellingPrice = record.SellingPrice,
            TheoreticalCost = record.TheoreticalCost,
            ContributionMargin = record.ContributionMargin,
            ContributionMarginPercent = record.ContributionMarginPercent,
            UnitsSold = record.UnitsSold,
            TotalRevenue = record.TotalRevenue,
            TotalContribution = record.TotalContribution,
            MenuMix = record.MenuMix,
            RevenueMix = record.RevenueMix,
            ContributionMix = record.ContributionMix,
            Classification = record.Classification,
            PopularityIndex = record.PopularityIndex,
            ProfitabilityIndex = record.ProfitabilityIndex,
            RecipeId = record.RecipeId,
            RecipeVersionId = record.RecipeVersionId
        };
    }

    private static CategoryAnalysis ToCategoryAnalysis(CategoryAnalysisRecord record)
    {
        return new CategoryAnalysis
        {
            Category = record.Category,
            ItemCount = record.ItemCount,
            TotalUnitsSold = record.TotalUnitsSold,
            TotalRevenue = record.TotalRevenue,
            TotalCost = record.TotalCost,
            TotalContribution = record.TotalContribution,
            AverageContributionMargin = record.AverageContributionMargin,
            AverageContributionMarginPercent = record.AverageContributionMarginPercent,
            AverageSellingPrice = record.AverageSellingPrice,
            AverageUnitsSold = record.AverageUnitsSold,
            StarCount = record.StarCount,
            PlowhorseCount = record.PlowhorseCount,
            PuzzleCount = record.PuzzleCount,
            DogCount = record.DogCount
        };
    }
}
