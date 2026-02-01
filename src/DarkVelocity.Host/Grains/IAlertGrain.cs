namespace DarkVelocity.Host.Grains;

// ============================================================================
// Alert Types and Severity
// ============================================================================

public enum AlertType
{
    // Inventory alerts
    LowStock,
    OutOfStock,
    ExpiryRisk,
    NegativeStock,
    ParExceeded,
    AgedStock,

    // Cost/GP alerts
    GPDropped,
    HighVariance,
    CostSpike,
    NegativeMargin,

    // Supplier alerts
    SupplierPriceSpike,
    DeliveryLate,
    InvoiceDiscrepancy,

    // Operational alerts
    HighWaste,
    HighVoidRate,
    HighCompRate,

    // System alerts
    SyncError,
    DataAnomaly
}

public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum AlertStatus
{
    Active,
    Acknowledged,
    Resolved,
    Snoozed,
    Dismissed
}

// ============================================================================
// Alert Records
// ============================================================================

[GenerateSerializer]
public record Alert
{
    [Id(0)] public required Guid AlertId { get; init; }
    [Id(1)] public required AlertType Type { get; init; }
    [Id(2)] public required AlertSeverity Severity { get; init; }
    [Id(3)] public required string Title { get; init; }
    [Id(4)] public required string Message { get; init; }
    [Id(5)] public required Guid OrgId { get; init; }
    [Id(6)] public required Guid SiteId { get; init; }
    [Id(7)] public Guid? EntityId { get; init; }
    [Id(8)] public string? EntityType { get; init; }
    [Id(9)] public required DateTime TriggeredAt { get; init; }
    [Id(10)] public required AlertStatus Status { get; init; }
    [Id(11)] public DateTime? AcknowledgedAt { get; init; }
    [Id(12)] public Guid? AcknowledgedBy { get; init; }
    [Id(13)] public DateTime? ResolvedAt { get; init; }
    [Id(14)] public Guid? ResolvedBy { get; init; }
    [Id(15)] public string? ResolutionNotes { get; init; }
    [Id(16)] public DateTime? SnoozedUntil { get; init; }
    [Id(17)] public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

[GenerateSerializer]
public record CreateAlertCommand(
    [property: Id(0)] AlertType Type,
    [property: Id(1)] AlertSeverity Severity,
    [property: Id(2)] string Title,
    [property: Id(3)] string Message,
    [property: Id(4)] Guid? EntityId = null,
    [property: Id(5)] string? EntityType = null,
    [property: Id(6)] Dictionary<string, string>? Metadata = null);

[GenerateSerializer]
public record AcknowledgeAlertCommand(
    [property: Id(0)] Guid AlertId,
    [property: Id(1)] Guid AcknowledgedBy);

[GenerateSerializer]
public record ResolveAlertCommand(
    [property: Id(0)] Guid AlertId,
    [property: Id(1)] Guid ResolvedBy,
    [property: Id(2)] string? ResolutionNotes = null);

[GenerateSerializer]
public record SnoozeAlertCommand(
    [property: Id(0)] Guid AlertId,
    [property: Id(1)] TimeSpan Duration,
    [property: Id(2)] Guid SnoozedBy);

[GenerateSerializer]
public record DismissAlertCommand(
    [property: Id(0)] Guid AlertId,
    [property: Id(1)] Guid DismissedBy,
    [property: Id(2)] string? Reason = null);

// ============================================================================
// Alert Rules
// ============================================================================

[GenerateSerializer]
public record AlertRule
{
    [Id(0)] public required Guid RuleId { get; init; }
    [Id(1)] public required AlertType Type { get; init; }
    [Id(2)] public required string Name { get; init; }
    [Id(3)] public required string Description { get; init; }
    [Id(4)] public required bool IsEnabled { get; init; }
    [Id(5)] public required AlertSeverity DefaultSeverity { get; init; }
    [Id(6)] public required AlertRuleCondition Condition { get; init; }
    [Id(7)] public required IReadOnlyList<AlertAction> Actions { get; init; }
    [Id(8)] public TimeSpan? CooldownPeriod { get; init; }
}

[GenerateSerializer]
public record AlertRuleCondition
{
    [Id(0)] public required string Metric { get; init; }
    [Id(1)] public required ComparisonOperator Operator { get; init; }
    [Id(2)] public required decimal Threshold { get; init; }
    [Id(3)] public string? SecondaryMetric { get; init; }
    [Id(4)] public decimal? SecondaryThreshold { get; init; }
}

public enum ComparisonOperator
{
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Equal,
    NotEqual,
    ChangedBy
}

[GenerateSerializer]
public record AlertAction
{
    [Id(0)] public required AlertActionType ActionType { get; init; }
    [Id(1)] public IReadOnlyDictionary<string, string>? Configuration { get; init; }
}

public enum AlertActionType
{
    CreateAlert,
    SendSlack,
    SendEmail,
    SendPush,
    CreateTask,
    Webhook
}

// ============================================================================
// Alert Configuration (predefined rules)
// ============================================================================

public static class AlertRules
{
    public static readonly AlertRule GPDropped = new()
    {
        RuleId = Guid.Parse("00000001-0000-0000-0000-000000000001"),
        Type = AlertType.GPDropped,
        Name = "GP% Drop",
        Description = "Gross profit percentage dropped more than 3 points vs last week",
        IsEnabled = true,
        DefaultSeverity = AlertSeverity.High,
        Condition = new AlertRuleCondition
        {
            Metric = "GrossProfitPercent",
            Operator = ComparisonOperator.ChangedBy,
            Threshold = -3.0m,
            SecondaryMetric = "GrossProfitPercentLastWeek"
        },
        Actions = new List<AlertAction> { new AlertAction { ActionType = AlertActionType.CreateAlert } },
        CooldownPeriod = TimeSpan.FromHours(24)
    };

    public static readonly AlertRule HighVariance = new()
    {
        RuleId = Guid.Parse("00000001-0000-0000-0000-000000000002"),
        Type = AlertType.HighVariance,
        Name = "High Cost Variance",
        Description = "Actual vs theoretical cost variance exceeds 15%",
        IsEnabled = true,
        DefaultSeverity = AlertSeverity.Medium,
        Condition = new AlertRuleCondition
        {
            Metric = "COGSVariancePercent",
            Operator = ComparisonOperator.GreaterThan,
            Threshold = 15.0m
        },
        Actions = new List<AlertAction> { new AlertAction { ActionType = AlertActionType.CreateAlert } },
        CooldownPeriod = TimeSpan.FromHours(24)
    };

    public static readonly AlertRule LowStock = new()
    {
        RuleId = Guid.Parse("00000001-0000-0000-0000-000000000003"),
        Type = AlertType.LowStock,
        Name = "Low Stock",
        Description = "Stock level below reorder point",
        IsEnabled = true,
        DefaultSeverity = AlertSeverity.Medium,
        Condition = new AlertRuleCondition
        {
            Metric = "QuantityAvailable",
            Operator = ComparisonOperator.LessThan,
            Threshold = 0, // Dynamic - uses ReorderPoint
            SecondaryMetric = "ReorderPoint"
        },
        Actions = new List<AlertAction> { new AlertAction { ActionType = AlertActionType.CreateAlert } },
        CooldownPeriod = TimeSpan.FromHours(4)
    };

    public static readonly AlertRule OutOfStock = new()
    {
        RuleId = Guid.Parse("00000001-0000-0000-0000-000000000004"),
        Type = AlertType.OutOfStock,
        Name = "Out of Stock",
        Description = "Ingredient is out of stock",
        IsEnabled = true,
        DefaultSeverity = AlertSeverity.High,
        Condition = new AlertRuleCondition
        {
            Metric = "QuantityAvailable",
            Operator = ComparisonOperator.LessThanOrEqual,
            Threshold = 0
        },
        Actions = new List<AlertAction> { new AlertAction { ActionType = AlertActionType.CreateAlert } },
        CooldownPeriod = TimeSpan.FromHours(1)
    };

    public static readonly AlertRule ExpiryRisk = new()
    {
        RuleId = Guid.Parse("00000001-0000-0000-0000-000000000005"),
        Type = AlertType.ExpiryRisk,
        Name = "Expiry Risk",
        Description = "Stock expiring within 3 days",
        IsEnabled = true,
        DefaultSeverity = AlertSeverity.High,
        Condition = new AlertRuleCondition
        {
            Metric = "DaysUntilExpiry",
            Operator = ComparisonOperator.LessThanOrEqual,
            Threshold = 3
        },
        Actions = new List<AlertAction> { new AlertAction { ActionType = AlertActionType.CreateAlert } },
        CooldownPeriod = TimeSpan.FromHours(24)
    };

    public static readonly AlertRule PriceSpike = new()
    {
        RuleId = Guid.Parse("00000001-0000-0000-0000-000000000006"),
        Type = AlertType.SupplierPriceSpike,
        Name = "Supplier Price Spike",
        Description = "Supplier price increased more than 10% vs last invoice",
        IsEnabled = true,
        DefaultSeverity = AlertSeverity.Medium,
        Condition = new AlertRuleCondition
        {
            Metric = "PriceChangePercent",
            Operator = ComparisonOperator.GreaterThan,
            Threshold = 10.0m
        },
        Actions = new List<AlertAction> { new AlertAction { ActionType = AlertActionType.CreateAlert } },
        CooldownPeriod = TimeSpan.FromDays(7)
    };

    public static readonly AlertRule NegativeStock = new()
    {
        RuleId = Guid.Parse("00000001-0000-0000-0000-000000000007"),
        Type = AlertType.NegativeStock,
        Name = "Negative Stock",
        Description = "Stock quantity is negative (data issue)",
        IsEnabled = true,
        DefaultSeverity = AlertSeverity.Critical,
        Condition = new AlertRuleCondition
        {
            Metric = "QuantityOnHand",
            Operator = ComparisonOperator.LessThan,
            Threshold = 0
        },
        Actions = new List<AlertAction> { new AlertAction { ActionType = AlertActionType.CreateAlert } },
        CooldownPeriod = TimeSpan.FromMinutes(30)
    };

    public static IReadOnlyList<AlertRule> All =>
    [
        GPDropped,
        HighVariance,
        LowStock,
        OutOfStock,
        ExpiryRisk,
        PriceSpike,
        NegativeStock
    ];
}

// ============================================================================
// Alert Grain Interface
// ============================================================================

/// <summary>
/// Grain for managing alerts at site level.
/// Key: "{orgId}:{siteId}:alerts"
/// </summary>
public interface IAlertGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid orgId, Guid siteId);

    // Alert management
    Task<Alert> CreateAlertAsync(CreateAlertCommand command);
    Task AcknowledgeAsync(AcknowledgeAlertCommand command);
    Task ResolveAsync(ResolveAlertCommand command);
    Task SnoozeAsync(SnoozeAlertCommand command);
    Task DismissAsync(DismissAlertCommand command);

    // Queries
    Task<IReadOnlyList<Alert>> GetActiveAlertsAsync();
    Task<IReadOnlyList<Alert>> GetAlertsAsync(AlertStatus? status = null, AlertType? type = null, int? limit = null);
    Task<Alert?> GetAlertAsync(Guid alertId);
    Task<int> GetActiveAlertCountAsync();
    Task<IReadOnlyDictionary<AlertType, int>> GetAlertCountsByTypeAsync();

    // Rule evaluation
    Task EvaluateRulesAsync();
    Task<IReadOnlyList<AlertRule>> GetRulesAsync();
    Task UpdateRuleAsync(AlertRule rule);
}

// ============================================================================
// Notification Grain Interface
// ============================================================================

[GenerateSerializer]
public record NotificationChannel
{
    [Id(0)] public required string Type { get; init; } // slack, email, push, webhook
    [Id(1)] public required string Target { get; init; } // channel name, email, device token, url
    [Id(2)] public required bool IsEnabled { get; init; }
    [Id(3)] public IReadOnlyList<AlertType>? AlertTypes { get; init; } // null = all types
    [Id(4)] public AlertSeverity? MinimumSeverity { get; init; }
}

[GenerateSerializer]
public record SendNotificationCommand(
    [property: Id(0)] Alert Alert,
    [property: Id(1)] IReadOnlyList<NotificationChannel> Channels);

/// <summary>
/// Grain for sending notifications.
/// Key: "{orgId}:notifications"
/// </summary>
public interface INotificationGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid orgId);
    Task SendAsync(SendNotificationCommand command);
    Task<IReadOnlyList<NotificationChannel>> GetChannelsAsync();
    Task AddChannelAsync(NotificationChannel channel);
    Task RemoveChannelAsync(string channelType, string target);
    Task UpdateChannelAsync(NotificationChannel channel);
}
