using DarkVelocity.Orleans.Abstractions.Grains;

namespace DarkVelocity.Orleans.Abstractions.State;

/// <summary>
/// State for the menu engineering grain.
/// </summary>
[GenerateSerializer]
public sealed class MenuEngineeringState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public string SiteName { get; set; } = string.Empty;
    [Id(3)] public decimal DefaultTargetMarginPercent { get; set; } = 70m;
    [Id(4)] public Dictionary<string, decimal> CategoryTargetMargins { get; set; } = [];

    // Period tracking
    [Id(5)] public DateTime? CurrentPeriodStart { get; set; }
    [Id(6)] public DateTime? CurrentPeriodEnd { get; set; }

    // Item data
    [Id(7)] public List<MenuItemRecord> Items { get; set; } = [];

    // Cached analysis
    [Id(8)] public DateTime? LastAnalyzedAt { get; set; }
    [Id(9)] public List<MenuItemAnalysisRecord> CachedAnalysis { get; set; } = [];
    [Id(10)] public List<CategoryAnalysisRecord> CachedCategoryAnalysis { get; set; } = [];

    [Id(11)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class MenuItemRecord
{
    [Id(0)] public Guid ProductId { get; set; }
    [Id(1)] public string ProductName { get; set; } = string.Empty;
    [Id(2)] public string Category { get; set; } = string.Empty;
    [Id(3)] public decimal SellingPrice { get; set; }
    [Id(4)] public decimal TheoreticalCost { get; set; }
    [Id(5)] public int UnitsSold { get; set; }
    [Id(6)] public decimal TotalRevenue { get; set; }
    [Id(7)] public Guid? RecipeId { get; set; }
    [Id(8)] public Guid? RecipeVersionId { get; set; }
    [Id(9)] public DateTime LastUpdated { get; set; }
}

[GenerateSerializer]
public sealed class MenuItemAnalysisRecord
{
    [Id(0)] public Guid ProductId { get; set; }
    [Id(1)] public string ProductName { get; set; } = string.Empty;
    [Id(2)] public string Category { get; set; } = string.Empty;
    [Id(3)] public decimal SellingPrice { get; set; }
    [Id(4)] public decimal TheoreticalCost { get; set; }
    [Id(5)] public decimal ContributionMargin { get; set; }
    [Id(6)] public decimal ContributionMarginPercent { get; set; }
    [Id(7)] public int UnitsSold { get; set; }
    [Id(8)] public decimal TotalRevenue { get; set; }
    [Id(9)] public decimal TotalContribution { get; set; }
    [Id(10)] public decimal MenuMix { get; set; }
    [Id(11)] public decimal RevenueMix { get; set; }
    [Id(12)] public decimal ContributionMix { get; set; }
    [Id(13)] public MenuClass Classification { get; set; }
    [Id(14)] public decimal PopularityIndex { get; set; }
    [Id(15)] public decimal ProfitabilityIndex { get; set; }
    [Id(16)] public Guid? RecipeId { get; set; }
    [Id(17)] public Guid? RecipeVersionId { get; set; }
}

[GenerateSerializer]
public sealed class CategoryAnalysisRecord
{
    [Id(0)] public string Category { get; set; } = string.Empty;
    [Id(1)] public int ItemCount { get; set; }
    [Id(2)] public int TotalUnitsSold { get; set; }
    [Id(3)] public decimal TotalRevenue { get; set; }
    [Id(4)] public decimal TotalCost { get; set; }
    [Id(5)] public decimal TotalContribution { get; set; }
    [Id(6)] public decimal AverageContributionMargin { get; set; }
    [Id(7)] public decimal AverageContributionMarginPercent { get; set; }
    [Id(8)] public decimal AverageSellingPrice { get; set; }
    [Id(9)] public decimal AverageUnitsSold { get; set; }
    [Id(10)] public int StarCount { get; set; }
    [Id(11)] public int PlowhorseCount { get; set; }
    [Id(12)] public int PuzzleCount { get; set; }
    [Id(13)] public int DogCount { get; set; }
}
