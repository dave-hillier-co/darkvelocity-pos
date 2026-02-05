using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.Domains.System;

/// <summary>
/// State for the notification grain.
/// </summary>
[GenerateSerializer]
public sealed class NotificationState
{
    [Id(0)] public Guid OrgId { get; set; }

    /// <summary>
    /// Recent notifications (kept for querying, older ones aged out).
    /// </summary>
    [Id(1)] public List<NotificationRecord> Notifications { get; set; } = [];

    /// <summary>
    /// Configured notification channels.
    /// </summary>
    [Id(2)] public List<NotificationChannelRecord> Channels { get; set; } = [];

    /// <summary>
    /// Maximum notifications to keep in memory.
    /// </summary>
    [Id(3)] public int MaxNotifications { get; set; } = 1000;

    [Id(4)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class NotificationRecord
{
    [Id(0)] public Guid NotificationId { get; set; }
    [Id(1)] public NotificationType Type { get; set; }
    [Id(2)] public string Recipient { get; set; } = string.Empty;
    [Id(3)] public string Subject { get; set; } = string.Empty;
    [Id(4)] public string Body { get; set; } = string.Empty;
    [Id(5)] public NotificationStatus Status { get; set; }
    [Id(6)] public DateTime CreatedAt { get; set; }
    [Id(7)] public DateTime? SentAt { get; set; }
    [Id(8)] public int RetryCount { get; set; }
    [Id(9)] public string? ErrorMessage { get; set; }
    [Id(10)] public string? ExternalMessageId { get; set; }
    [Id(11)] public Dictionary<string, string>? Metadata { get; set; }
    [Id(12)] public Guid? TriggeredByAlertId { get; set; }
}

[GenerateSerializer]
public sealed class NotificationChannelRecord
{
    [Id(0)] public Guid ChannelId { get; set; }
    [Id(1)] public NotificationType Type { get; set; }
    [Id(2)] public string Target { get; set; } = string.Empty;
    [Id(3)] public bool IsEnabled { get; set; }
    [Id(4)] public List<AlertType>? AlertTypes { get; set; }
    [Id(5)] public AlertSeverity? MinimumSeverity { get; set; }
    [Id(6)] public Dictionary<string, string>? Configuration { get; set; }
}
