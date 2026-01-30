using DarkVelocity.Orleans.Abstractions.Costing;

namespace DarkVelocity.Orleans.Abstractions.Grains;

// ============================================================================
// Menu Engineering Classifications
// ============================================================================

/// <summary>
/// Menu engineering classification based on popularity and profitability.
/// </summary>
public enum MenuClass
{
    /// <summary>
    /// High margin, high popularity - promote these items.
    /// </summary>
    Star,

    /// <summary>
    /// Low margin, high popularity - consider price increase or cost reduction.
    /// </summary>
    Plowhorse,

    /// <summary>
    /// High margin, low popularity - needs marketing/repositioning.
    /// </summary>
    Puzzle,

    /// <summary>
    /// Low margin, low popularity - consider removing from menu.
    /// </summary>
    Dog
}

// ============================================================================
// Menu Item Analysis Records
// ============================================================================

public record MenuItemAnalysis
{
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required string Category { get; init; }

    // Pricing
    public required decimal SellingPrice { get; init; }
    public required decimal TheoreticalCost { get; init; }
    public required decimal ContributionMargin { get; init; }
    public required decimal ContributionMarginPercent { get; init; }

    // Sales volume
    public required int UnitsSold { get; init; }
    public required decimal TotalRevenue { get; init; }
    public required decimal TotalContribution { get; init; }

    // Mix analysis
    public required decimal MenuMix { get; init; } // Units / Category units
    public required decimal RevenueMix { get; init; } // Revenue / Category revenue
    public required decimal ContributionMix { get; init; } // Contribution / Category contribution

    // Classification
    public required MenuClass Classification { get; init; }
    public required decimal PopularityIndex { get; init; } // vs category average
    public required decimal ProfitabilityIndex { get; init; } // vs category average

    // Recipe info
    public Guid? RecipeId { get; init; }
    public Guid? RecipeVersionId { get; init; }
}

public record CategoryAnalysis
{
    public required string Category { get; init; }
    public required int ItemCount { get; init; }
    public required int TotalUnitsSold { get; init; }
    public required decimal TotalRevenue { get; init; }
    public required decimal TotalCost { get; init; }
    public required decimal TotalContribution { get; init; }
    public required decimal AverageContributionMargin { get; init; }
    public required decimal AverageContributionMarginPercent { get; init; }
    public required decimal AverageSellingPrice { get; init; }
    public required decimal AverageUnitsSold { get; init; }

    // Classification breakdown
    public required int StarCount { get; init; }
    public required int PlowhorseCount { get; init; }
    public required int PuzzleCount { get; init; }
    public required int DogCount { get; init; }
}

public record MenuEngineeringReport
{
    public required Guid OrgId { get; init; }
    public required Guid SiteId { get; init; }
    public required DateTime PeriodStart { get; init; }
    public required DateTime PeriodEnd { get; init; }

    // Overall metrics
    public required decimal TotalRevenue { get; init; }
    public required decimal TotalCost { get; init; }
    public required decimal TotalContribution { get; init; }
    public required decimal OverallMarginPercent { get; init; }
    public required int TotalItemsSold { get; init; }
    public required int TotalMenuItems { get; init; }

    // Items by category
    public required IReadOnlyList<MenuItemAnalysis> Items { get; init; }
    public required IReadOnlyList<CategoryAnalysis> Categories { get; init; }

    // Classification summary
    public required int StarCount { get; init; }
    public required int PlowhorseCount { get; init; }
    public required int PuzzleCount { get; init; }
    public required int DogCount { get; init; }

    // Top performers
    public required IReadOnlyList<MenuItemAnalysis> TopStars { get; init; }
    public required IReadOnlyList<MenuItemAnalysis> TopContributors { get; init; }

    // Items needing attention
    public required IReadOnlyList<MenuItemAnalysis> LowMarginHighVolume { get; init; }
    public required IReadOnlyList<MenuItemAnalysis> HighMarginLowVolume { get; init; }
    public required IReadOnlyList<MenuItemAnalysis> DogsToReview { get; init; }
}

// ============================================================================
// Price Optimization Records
// ============================================================================

public record PriceOptimizationSuggestion
{
    public required Guid ProductId { get; init; }
    public required string ProductName { get; init; }
    public required string Category { get; init; }
    public required decimal CurrentPrice { get; init; }
    public required decimal SuggestedPrice { get; init; }
    public required decimal PriceChange { get; init; }
    public required decimal PriceChangePercent { get; init; }
    public required decimal CurrentMargin { get; init; }
    public required decimal ProjectedMargin { get; init; }
    public required decimal TargetMargin { get; init; }
    public required string Rationale { get; init; }
    public required PriceSuggestionType SuggestionType { get; init; }
    public required decimal ConfidenceScore { get; init; }
}

public enum PriceSuggestionType
{
    IncreaseToTargetMargin,
    DecreaseToCompetitive,
    IncreaseHighDemand,
    RoundToPsychological,
    CostRecovery,
    Promotional
}

// ============================================================================
// Commands
// ============================================================================

public record InitializeMenuEngineeringCommand(
    Guid OrgId,
    Guid SiteId,
    string SiteName,
    decimal TargetMarginPercent = 70m);

public record AnalyzeMenuCommand(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    CostingMethod CostingMethod = CostingMethod.Standard);

public record RecordItemSalesCommand(
    Guid ProductId,
    string ProductName,
    string Category,
    decimal SellingPrice,
    decimal TheoreticalCost,
    int UnitsSold,
    decimal TotalRevenue,
    Guid? RecipeId = null,
    Guid? RecipeVersionId = null);

// ============================================================================
// Menu Engineering Grain Interface
// ============================================================================

/// <summary>
/// Grain for menu engineering analysis at site level.
/// Key: "{orgId}:{siteId}:menu-engineering"
/// </summary>
public interface IMenuEngineeringGrain : IGrainWithStringKey
{
    Task InitializeAsync(InitializeMenuEngineeringCommand command);

    // Data input
    Task RecordItemSalesAsync(RecordItemSalesCommand command);
    Task BulkRecordSalesAsync(IReadOnlyList<RecordItemSalesCommand> commands);

    // Analysis
    Task<MenuEngineeringReport> AnalyzeAsync(AnalyzeMenuCommand command);
    Task<IReadOnlyList<MenuItemAnalysis>> GetItemAnalysisAsync(string? category = null);
    Task<IReadOnlyList<CategoryAnalysis>> GetCategoryAnalysisAsync();
    Task<MenuItemAnalysis?> GetItemAsync(Guid productId);

    // Classification helpers
    Task<IReadOnlyList<MenuItemAnalysis>> GetItemsByClassAsync(MenuClass menuClass);
    Task<IReadOnlyDictionary<MenuClass, int>> GetClassificationCountsAsync();

    // Price optimization
    Task<IReadOnlyList<PriceOptimizationSuggestion>> GetPriceSuggestionsAsync(
        decimal targetMarginPercent,
        decimal maxPriceChangePercent = 15m);

    // Configuration
    Task SetTargetMarginAsync(decimal targetMarginPercent);
    Task SetCategoryTargetMarginAsync(string category, decimal targetMarginPercent);
}

// ============================================================================
// Grain Keys
// ============================================================================

public static partial class GrainKeys
{
    /// <summary>
    /// Creates a key for a daily sales grain.
    /// </summary>
    public static string DailySales(Guid orgId, Guid siteId, DateOnly date)
        => $"{orgId}:{siteId}:sales:{date:yyyy-MM-dd}";

    /// <summary>
    /// Creates a key for a daily inventory snapshot grain.
    /// </summary>
    public static string DailyInventorySnapshot(Guid orgId, Guid siteId, DateOnly date)
        => $"{orgId}:{siteId}:inventory-snapshot:{date:yyyy-MM-dd}";

    /// <summary>
    /// Creates a key for a daily consumption grain.
    /// </summary>
    public static string DailyConsumption(Guid orgId, Guid siteId, DateOnly date)
        => $"{orgId}:{siteId}:consumption:{date:yyyy-MM-dd}";

    /// <summary>
    /// Creates a key for a daily waste grain.
    /// </summary>
    public static string DailyWaste(Guid orgId, Guid siteId, DateOnly date)
        => $"{orgId}:{siteId}:waste:{date:yyyy-MM-dd}";

    /// <summary>
    /// Creates a key for a period aggregation grain.
    /// </summary>
    public static string PeriodAggregation(Guid orgId, Guid siteId, PeriodType periodType, int year, int periodNumber)
        => $"{orgId}:{siteId}:period:{periodType}:{year}:{periodNumber}";

    /// <summary>
    /// Creates a key for a site dashboard grain.
    /// </summary>
    public static string SiteDashboard(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:dashboard";

    /// <summary>
    /// Creates a key for an alert grain.
    /// </summary>
    public static string Alerts(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:alerts";

    /// <summary>
    /// Creates a key for a notification grain.
    /// </summary>
    public static string Notifications(Guid orgId)
        => $"{orgId}:notifications";

    /// <summary>
    /// Creates a key for a menu engineering grain.
    /// </summary>
    public static string MenuEngineering(Guid orgId, Guid siteId)
        => $"{orgId}:{siteId}:menu-engineering";
}
