using DarkVelocity.Host.Costing;

namespace DarkVelocity.Host.Grains;

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

[GenerateSerializer]
public record MenuItemAnalysis
{
    [Id(0)] public required Guid ProductId { get; init; }
    [Id(1)] public required string ProductName { get; init; }
    [Id(2)] public required string Category { get; init; }

    // Pricing
    [Id(3)] public required decimal SellingPrice { get; init; }
    [Id(4)] public required decimal TheoreticalCost { get; init; }
    [Id(5)] public required decimal ContributionMargin { get; init; }
    [Id(6)] public required decimal ContributionMarginPercent { get; init; }

    // Sales volume
    [Id(7)] public required int UnitsSold { get; init; }
    [Id(8)] public required decimal TotalRevenue { get; init; }
    [Id(9)] public required decimal TotalContribution { get; init; }

    // Mix analysis
    [Id(10)] public required decimal MenuMix { get; init; } // Units / Category units
    [Id(11)] public required decimal RevenueMix { get; init; } // Revenue / Category revenue
    [Id(12)] public required decimal ContributionMix { get; init; } // Contribution / Category contribution

    // Classification
    [Id(13)] public required MenuClass Classification { get; init; }
    [Id(14)] public required decimal PopularityIndex { get; init; } // vs category average
    [Id(15)] public required decimal ProfitabilityIndex { get; init; } // vs category average

    // Recipe info
    [Id(16)] public Guid? RecipeId { get; init; }
    [Id(17)] public Guid? RecipeVersionId { get; init; }
}

[GenerateSerializer]
public record CategoryAnalysis
{
    [Id(0)] public required string Category { get; init; }
    [Id(1)] public required int ItemCount { get; init; }
    [Id(2)] public required int TotalUnitsSold { get; init; }
    [Id(3)] public required decimal TotalRevenue { get; init; }
    [Id(4)] public required decimal TotalCost { get; init; }
    [Id(5)] public required decimal TotalContribution { get; init; }
    [Id(6)] public required decimal AverageContributionMargin { get; init; }
    [Id(7)] public required decimal AverageContributionMarginPercent { get; init; }
    [Id(8)] public required decimal AverageSellingPrice { get; init; }
    [Id(9)] public required decimal AverageUnitsSold { get; init; }

    // Classification breakdown
    [Id(10)] public required int StarCount { get; init; }
    [Id(11)] public required int PlowhorseCount { get; init; }
    [Id(12)] public required int PuzzleCount { get; init; }
    [Id(13)] public required int DogCount { get; init; }
}

[GenerateSerializer]
public record MenuEngineeringReport
{
    [Id(0)] public required Guid OrgId { get; init; }
    [Id(1)] public required Guid SiteId { get; init; }
    [Id(2)] public required DateTime PeriodStart { get; init; }
    [Id(3)] public required DateTime PeriodEnd { get; init; }

    // Overall metrics
    [Id(4)] public required decimal TotalRevenue { get; init; }
    [Id(5)] public required decimal TotalCost { get; init; }
    [Id(6)] public required decimal TotalContribution { get; init; }
    [Id(7)] public required decimal OverallMarginPercent { get; init; }
    [Id(8)] public required int TotalItemsSold { get; init; }
    [Id(9)] public required int TotalMenuItems { get; init; }

    // Items by category
    [Id(10)] public required IReadOnlyList<MenuItemAnalysis> Items { get; init; }
    [Id(11)] public required IReadOnlyList<CategoryAnalysis> Categories { get; init; }

    // Classification summary
    [Id(12)] public required int StarCount { get; init; }
    [Id(13)] public required int PlowhorseCount { get; init; }
    [Id(14)] public required int PuzzleCount { get; init; }
    [Id(15)] public required int DogCount { get; init; }

    // Top performers
    [Id(16)] public required IReadOnlyList<MenuItemAnalysis> TopStars { get; init; }
    [Id(17)] public required IReadOnlyList<MenuItemAnalysis> TopContributors { get; init; }

    // Items needing attention
    [Id(18)] public required IReadOnlyList<MenuItemAnalysis> LowMarginHighVolume { get; init; }
    [Id(19)] public required IReadOnlyList<MenuItemAnalysis> HighMarginLowVolume { get; init; }
    [Id(20)] public required IReadOnlyList<MenuItemAnalysis> DogsToReview { get; init; }
}

// ============================================================================
// Price Optimization Records
// ============================================================================

[GenerateSerializer]
public record PriceOptimizationSuggestion
{
    [Id(0)] public required Guid ProductId { get; init; }
    [Id(1)] public required string ProductName { get; init; }
    [Id(2)] public required string Category { get; init; }
    [Id(3)] public required decimal CurrentPrice { get; init; }
    [Id(4)] public required decimal SuggestedPrice { get; init; }
    [Id(5)] public required decimal PriceChange { get; init; }
    [Id(6)] public required decimal PriceChangePercent { get; init; }
    [Id(7)] public required decimal CurrentMargin { get; init; }
    [Id(8)] public required decimal ProjectedMargin { get; init; }
    [Id(9)] public required decimal TargetMargin { get; init; }
    [Id(10)] public required string Rationale { get; init; }
    [Id(11)] public required PriceSuggestionType SuggestionType { get; init; }
    [Id(12)] public required decimal ConfidenceScore { get; init; }
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

[GenerateSerializer]
public record InitializeMenuEngineeringCommand(
    [property: Id(0)] Guid OrgId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string SiteName,
    [property: Id(3)] decimal TargetMarginPercent = 70m);

[GenerateSerializer]
public record AnalyzeMenuCommand(
    [property: Id(0)] DateTime PeriodStart,
    [property: Id(1)] DateTime PeriodEnd,
    [property: Id(2)] CostingMethod CostingMethod = CostingMethod.Standard);

[GenerateSerializer]
public record RecordItemSalesCommand(
    [property: Id(0)] Guid ProductId,
    [property: Id(1)] string ProductName,
    [property: Id(2)] string Category,
    [property: Id(3)] decimal SellingPrice,
    [property: Id(4)] decimal TheoreticalCost,
    [property: Id(5)] int UnitsSold,
    [property: Id(6)] decimal TotalRevenue,
    [property: Id(7)] Guid? RecipeId = null,
    [property: Id(8)] Guid? RecipeVersionId = null);

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

// Note: GrainKeys extensions are in the parent namespace's GrainKeys.cs file
