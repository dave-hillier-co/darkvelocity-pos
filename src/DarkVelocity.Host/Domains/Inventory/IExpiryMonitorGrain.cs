namespace DarkVelocity.Host.Grains;

/// <summary>
/// Urgency level for expiring items.
/// </summary>
public enum ExpiryUrgency
{
    /// <summary>Not expiring soon (more than configured days).</summary>
    Normal,
    /// <summary>Expiring within 14-30 days (configurable).</summary>
    Warning,
    /// <summary>Expiring within 7-14 days (configurable).</summary>
    Urgent,
    /// <summary>Expiring within 7 days (configurable).</summary>
    Critical,
    /// <summary>Already expired.</summary>
    Expired
}

[GenerateSerializer]
public record ExpiryMonitorSettings(
    [property: Id(0)] int WarningDays = 30,
    [property: Id(1)] int UrgentDays = 14,
    [property: Id(2)] int CriticalDays = 7,
    [property: Id(3)] bool AutoWriteOffExpired = false,
    [property: Id(4)] bool SendAlerts = true,
    [property: Id(5)] List<string>? AlertRecipients = null);

[GenerateSerializer]
public record ExpiringItem
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public string IngredientName { get; init; } = string.Empty;
    [Id(2)] public string Sku { get; init; } = string.Empty;
    [Id(3)] public string Category { get; init; } = string.Empty;
    [Id(4)] public Guid BatchId { get; init; }
    [Id(5)] public string BatchNumber { get; init; } = string.Empty;
    [Id(6)] public DateTime ExpiryDate { get; init; }
    [Id(7)] public int DaysUntilExpiry { get; init; }
    [Id(8)] public decimal Quantity { get; init; }
    [Id(9)] public string Unit { get; init; } = string.Empty;
    [Id(10)] public decimal UnitCost { get; init; }
    [Id(11)] public decimal ValueAtRisk { get; init; }
    [Id(12)] public ExpiryUrgency Urgency { get; init; }
    [Id(13)] public string? Location { get; init; }
}

[GenerateSerializer]
public record ExpiryReport
{
    [Id(0)] public DateTime GeneratedAt { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public int TotalItemsMonitored { get; init; }
    [Id(3)] public int ExpiredCount { get; init; }
    [Id(4)] public int CriticalCount { get; init; }
    [Id(5)] public int UrgentCount { get; init; }
    [Id(6)] public int WarningCount { get; init; }
    [Id(7)] public decimal TotalExpiredValue { get; init; }
    [Id(8)] public decimal TotalAtRiskValue { get; init; }
    [Id(9)] public List<ExpiringItem> ExpiredItems { get; init; } = [];
    [Id(10)] public List<ExpiringItem> CriticalItems { get; init; } = [];
    [Id(11)] public List<ExpiringItem> UrgentItems { get; init; } = [];
    [Id(12)] public List<ExpiringItem> WarningItems { get; init; } = [];
    [Id(13)] public Dictionary<string, decimal> ValueAtRiskByCategory { get; init; } = new();
}

[GenerateSerializer]
public record ExpiredBatchWriteOff
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public string IngredientName { get; init; } = string.Empty;
    [Id(2)] public Guid BatchId { get; init; }
    [Id(3)] public string BatchNumber { get; init; } = string.Empty;
    [Id(4)] public decimal Quantity { get; init; }
    [Id(5)] public decimal UnitCost { get; init; }
    [Id(6)] public decimal TotalCost { get; init; }
    [Id(7)] public DateTime ExpiryDate { get; init; }
    [Id(8)] public DateTime WrittenOffAt { get; init; }
}

/// <summary>
/// Grain for monitoring inventory expiry dates.
/// Provides alerts and reports for items approaching expiry.
/// One per site.
/// </summary>
public interface IExpiryMonitorGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initializes the expiry monitor for a site.
    /// </summary>
    Task InitializeAsync(Guid organizationId, Guid siteId);

    /// <summary>
    /// Configures expiry monitoring settings.
    /// </summary>
    Task ConfigureAsync(ExpiryMonitorSettings settings);

    /// <summary>
    /// Gets current settings.
    /// </summary>
    Task<ExpiryMonitorSettings> GetSettingsAsync();

    /// <summary>
    /// Registers an ingredient for expiry monitoring.
    /// Called when inventory is initialized or updated.
    /// </summary>
    Task RegisterIngredientAsync(Guid ingredientId, string ingredientName, string sku, string category);

    /// <summary>
    /// Removes an ingredient from monitoring.
    /// </summary>
    Task UnregisterIngredientAsync(Guid ingredientId);

    /// <summary>
    /// Scans all registered inventory for expiring items.
    /// This is the main monitoring method, can be called by a timer or explicitly.
    /// </summary>
    Task<ExpiryReport> ScanForExpiringItemsAsync();

    /// <summary>
    /// Gets items expiring within the specified number of days.
    /// </summary>
    Task<IReadOnlyList<ExpiringItem>> GetExpiringItemsAsync(int daysAhead = 30);

    /// <summary>
    /// Gets already expired items that haven't been written off.
    /// </summary>
    Task<IReadOnlyList<ExpiringItem>> GetExpiredItemsAsync();

    /// <summary>
    /// Gets items at critical urgency level.
    /// </summary>
    Task<IReadOnlyList<ExpiringItem>> GetCriticalItemsAsync();

    /// <summary>
    /// Writes off all expired batches across registered inventory.
    /// </summary>
    Task<IReadOnlyList<ExpiredBatchWriteOff>> WriteOffExpiredBatchesAsync(Guid performedBy);

    /// <summary>
    /// Gets the full expiry report.
    /// </summary>
    Task<ExpiryReport> GetReportAsync();

    /// <summary>
    /// Gets value at risk by urgency level.
    /// </summary>
    Task<Dictionary<ExpiryUrgency, decimal>> GetValueAtRiskByUrgencyAsync();

    /// <summary>
    /// Gets value at risk by category.
    /// </summary>
    Task<Dictionary<string, decimal>> GetValueAtRiskByCategoryAsync();

    /// <summary>
    /// Checks if the monitor is initialized.
    /// </summary>
    Task<bool> ExistsAsync();
}
