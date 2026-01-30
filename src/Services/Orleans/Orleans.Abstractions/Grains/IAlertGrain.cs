namespace DarkVelocity.Orleans.Abstractions.Grains;

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

public record Alert
{
    public required Guid AlertId { get; init; }
    public required AlertType Type { get; init; }
    public required AlertSeverity Severity { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required Guid OrgId { get; init; }
    public required Guid SiteId { get; init; }
    public Guid? EntityId { get; init; }
    public string? EntityType { get; init; }
    public required DateTime TriggeredAt { get; init; }
    public required AlertStatus Status { get; init; }
    public DateTime? AcknowledgedAt { get; init; }
    public Guid? AcknowledgedBy { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public Guid? ResolvedBy { get; init; }
    public string? ResolutionNotes { get; init; }
    public DateTime? SnoozedUntil { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
}

public record CreateAlertCommand(
    AlertType Type,
    AlertSeverity Severity,
    string Title,
    string Message,
    Guid? EntityId = null,
    string? EntityType = null,
    Dictionary<string, object>? Metadata = null);

public record AcknowledgeAlertCommand(
    Guid AlertId,
    Guid AcknowledgedBy);

public record ResolveAlertCommand(
    Guid AlertId,
    Guid ResolvedBy,
    string? ResolutionNotes = null);

public record SnoozeAlertCommand(
    Guid AlertId,
    TimeSpan Duration,
    Guid SnoozedBy);

public record DismissAlertCommand(
    Guid AlertId,
    Guid DismissedBy,
    string? Reason = null);

// ============================================================================
// Alert Rules
// ============================================================================

public record AlertRule
{
    public required Guid RuleId { get; init; }
    public required AlertType Type { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required bool IsEnabled { get; init; }
    public required AlertSeverity DefaultSeverity { get; init; }
    public required AlertRuleCondition Condition { get; init; }
    public required IReadOnlyList<AlertAction> Actions { get; init; }
    public TimeSpan? CooldownPeriod { get; init; }
}

public record AlertRuleCondition
{
    public required string Metric { get; init; }
    public required ComparisonOperator Operator { get; init; }
    public required decimal Threshold { get; init; }
    public string? SecondaryMetric { get; init; }
    public decimal? SecondaryThreshold { get; init; }
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

public record AlertAction
{
    public required AlertActionType ActionType { get; init; }
    public IReadOnlyDictionary<string, string>? Configuration { get; init; }
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
        Actions = [new AlertAction { ActionType = AlertActionType.CreateAlert }],
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
        Actions = [new AlertAction { ActionType = AlertActionType.CreateAlert }],
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
        Actions = [new AlertAction { ActionType = AlertActionType.CreateAlert }],
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
        Actions = [new AlertAction { ActionType = AlertActionType.CreateAlert }],
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
        Actions = [new AlertAction { ActionType = AlertActionType.CreateAlert }],
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
        Actions = [new AlertAction { ActionType = AlertActionType.CreateAlert }],
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
        Actions = [new AlertAction { ActionType = AlertActionType.CreateAlert }],
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

public record NotificationChannel
{
    public required string Type { get; init; } // slack, email, push, webhook
    public required string Target { get; init; } // channel name, email, device token, url
    public required bool IsEnabled { get; init; }
    public IReadOnlyList<AlertType>? AlertTypes { get; init; } // null = all types
    public AlertSeverity? MinimumSeverity { get; init; }
}

public record SendNotificationCommand(
    Alert Alert,
    IReadOnlyList<NotificationChannel> Channels);

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
