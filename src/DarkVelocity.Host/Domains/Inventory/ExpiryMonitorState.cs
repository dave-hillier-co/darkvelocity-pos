using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

[GenerateSerializer]
public sealed class ExpiryMonitorState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }

    /// <summary>
    /// Registered ingredients to monitor.
    /// </summary>
    [Id(2)] public Dictionary<Guid, MonitoredIngredient> MonitoredIngredients { get; set; } = new();

    /// <summary>
    /// Monitoring settings.
    /// </summary>
    [Id(3)] public ExpiryMonitorSettings Settings { get; set; } = new();

    /// <summary>
    /// Last scan timestamp.
    /// </summary>
    [Id(4)] public DateTime? LastScanAt { get; set; }

    /// <summary>
    /// Cached report from last scan.
    /// </summary>
    [Id(5)] public ExpiryReportCache? CachedReport { get; set; }
}

[GenerateSerializer]
public sealed class MonitoredIngredient
{
    [Id(0)] public Guid IngredientId { get; set; }
    [Id(1)] public string IngredientName { get; set; } = string.Empty;
    [Id(2)] public string Sku { get; set; } = string.Empty;
    [Id(3)] public string Category { get; set; } = string.Empty;
    [Id(4)] public DateTime RegisteredAt { get; set; }
}

[GenerateSerializer]
public sealed class ExpiryReportCache
{
    [Id(0)] public DateTime GeneratedAt { get; set; }
    [Id(1)] public int TotalItemsMonitored { get; set; }
    [Id(2)] public int ExpiredCount { get; set; }
    [Id(3)] public int CriticalCount { get; set; }
    [Id(4)] public int UrgentCount { get; set; }
    [Id(5)] public int WarningCount { get; set; }
    [Id(6)] public decimal TotalExpiredValue { get; set; }
    [Id(7)] public decimal TotalAtRiskValue { get; set; }
}
