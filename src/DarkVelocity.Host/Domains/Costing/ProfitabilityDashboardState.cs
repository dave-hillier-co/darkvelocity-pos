namespace DarkVelocity.Host.State;

/// <summary>
/// Persistent state for the profitability dashboard grain.
/// </summary>
[GenerateSerializer]
public sealed class ProfitabilityDashboardState
{
    [Id(0)]
    public Guid OrgId { get; set; }

    [Id(1)]
    public Guid SiteId { get; set; }

    [Id(2)]
    public string SiteName { get; set; } = string.Empty;

    [Id(3)]
    public List<ItemCostDataState> ItemCostData { get; set; } = [];

    [Id(4)]
    public List<CostTrendState> CostTrends { get; set; } = [];

    [Id(5)]
    public int Version { get; set; }

    [Id(6)]
    public DateTime CreatedAt { get; set; }

    [Id(7)]
    public DateTime ModifiedAt { get; set; }
}

/// <summary>
/// Item cost data stored in the profitability dashboard.
/// </summary>
[GenerateSerializer]
public sealed class ItemCostDataState
{
    [Id(0)]
    public Guid ItemId { get; set; }

    [Id(1)]
    public string ItemName { get; set; } = string.Empty;

    [Id(2)]
    public string Category { get; set; } = string.Empty;

    [Id(3)]
    public decimal SellingPrice { get; set; }

    [Id(4)]
    public decimal TheoreticalCost { get; set; }

    [Id(5)]
    public decimal ActualCost { get; set; }

    [Id(6)]
    public int UnitsSold { get; set; }

    [Id(7)]
    public decimal TotalRevenue { get; set; }

    [Id(8)]
    public DateTime RecordedDate { get; set; }

    [Id(9)]
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Cost trend data point stored in the profitability dashboard.
/// </summary>
[GenerateSerializer]
public sealed class CostTrendState
{
    [Id(0)]
    public DateTime Date { get; set; }

    [Id(1)]
    public decimal FoodCostPercent { get; set; }

    [Id(2)]
    public decimal BeverageCostPercent { get; set; }

    [Id(3)]
    public decimal TotalCost { get; set; }

    [Id(4)]
    public decimal TotalRevenue { get; set; }
}
