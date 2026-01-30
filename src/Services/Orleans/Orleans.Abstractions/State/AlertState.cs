using DarkVelocity.Orleans.Abstractions.Grains;

namespace DarkVelocity.Orleans.Abstractions.State;

/// <summary>
/// State for the alert grain.
/// </summary>
[GenerateSerializer]
public sealed class AlertState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }

    /// <summary>
    /// All alerts (active and historical).
    /// </summary>
    [Id(2)] public List<AlertRecord> Alerts { get; set; } = [];

    /// <summary>
    /// Customized alert rules.
    /// </summary>
    [Id(3)] public List<AlertRuleRecord> Rules { get; set; } = [];

    /// <summary>
    /// Last time each rule was triggered (for cooldown).
    /// </summary>
    [Id(4)] public Dictionary<Guid, DateTime> RuleLastTriggered { get; set; } = [];

    [Id(5)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class AlertRecord
{
    [Id(0)] public Guid AlertId { get; set; }
    [Id(1)] public AlertType Type { get; set; }
    [Id(2)] public AlertSeverity Severity { get; set; }
    [Id(3)] public string Title { get; set; } = string.Empty;
    [Id(4)] public string Message { get; set; } = string.Empty;
    [Id(5)] public Guid? EntityId { get; set; }
    [Id(6)] public string? EntityType { get; set; }
    [Id(7)] public DateTime TriggeredAt { get; set; }
    [Id(8)] public AlertStatus Status { get; set; }
    [Id(9)] public DateTime? AcknowledgedAt { get; set; }
    [Id(10)] public Guid? AcknowledgedBy { get; set; }
    [Id(11)] public DateTime? ResolvedAt { get; set; }
    [Id(12)] public Guid? ResolvedBy { get; set; }
    [Id(13)] public string? ResolutionNotes { get; set; }
    [Id(14)] public DateTime? SnoozedUntil { get; set; }
    [Id(15)] public Dictionary<string, string>? Metadata { get; set; }
}

[GenerateSerializer]
public sealed class AlertRuleRecord
{
    [Id(0)] public Guid RuleId { get; set; }
    [Id(1)] public AlertType Type { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string Description { get; set; } = string.Empty;
    [Id(4)] public bool IsEnabled { get; set; }
    [Id(5)] public AlertSeverity DefaultSeverity { get; set; }
    [Id(6)] public string Metric { get; set; } = string.Empty;
    [Id(7)] public ComparisonOperator Operator { get; set; }
    [Id(8)] public decimal Threshold { get; set; }
    [Id(9)] public string? SecondaryMetric { get; set; }
    [Id(10)] public decimal? SecondaryThreshold { get; set; }
    [Id(11)] public TimeSpan? CooldownPeriod { get; set; }
}

/// <summary>
/// State for the notification grain.
/// </summary>
[GenerateSerializer]
public sealed class NotificationState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public List<NotificationChannelRecord> Channels { get; set; } = [];
    [Id(2)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class NotificationChannelRecord
{
    [Id(0)] public string Type { get; set; } = string.Empty;
    [Id(1)] public string Target { get; set; } = string.Empty;
    [Id(2)] public bool IsEnabled { get; set; }
    [Id(3)] public List<AlertType>? AlertTypes { get; set; }
    [Id(4)] public AlertSeverity? MinimumSeverity { get; set; }
}
