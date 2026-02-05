using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.Domains.System;

// ============================================================================
// Notification Types
// ============================================================================

public enum NotificationType
{
    Email,
    Sms,
    Push,
    Slack
}

public enum NotificationStatus
{
    Queued,
    Sending,
    Sent,
    Failed,
    Retrying
}

// ============================================================================
// Notification Records
// ============================================================================

[GenerateSerializer]
public record Notification
{
    [Id(0)] public required Guid NotificationId { get; init; }
    [Id(1)] public required NotificationType Type { get; init; }
    [Id(2)] public required string Recipient { get; init; }
    [Id(3)] public required string Subject { get; init; }
    [Id(4)] public required string Body { get; init; }
    [Id(5)] public required NotificationStatus Status { get; init; }
    [Id(6)] public required DateTime CreatedAt { get; init; }
    [Id(7)] public DateTime? SentAt { get; init; }
    [Id(8)] public int RetryCount { get; init; }
    [Id(9)] public string? ErrorMessage { get; init; }
    [Id(10)] public string? ExternalMessageId { get; init; }
    [Id(11)] public Dictionary<string, string>? Metadata { get; init; }
    [Id(12)] public Guid? TriggeredByAlertId { get; init; }
}

// ============================================================================
// Notification Commands
// ============================================================================

[GenerateSerializer]
public record SendEmailCommand(
    [property: Id(0)] string To,
    [property: Id(1)] string Subject,
    [property: Id(2)] string Body,
    [property: Id(3)] bool IsHtml = true,
    [property: Id(4)] Guid? TriggeredByAlertId = null,
    [property: Id(5)] Dictionary<string, string>? Metadata = null);

[GenerateSerializer]
public record SendSmsCommand(
    [property: Id(0)] string To,
    [property: Id(1)] string Message,
    [property: Id(2)] Guid? TriggeredByAlertId = null,
    [property: Id(3)] Dictionary<string, string>? Metadata = null);

[GenerateSerializer]
public record SendPushCommand(
    [property: Id(0)] string DeviceToken,
    [property: Id(1)] string Title,
    [property: Id(2)] string Body,
    [property: Id(3)] Dictionary<string, string>? Data = null,
    [property: Id(4)] Guid? TriggeredByAlertId = null,
    [property: Id(5)] Dictionary<string, string>? Metadata = null);

[GenerateSerializer]
public record SendSlackCommand(
    [property: Id(0)] string WebhookUrl,
    [property: Id(1)] string Message,
    [property: Id(2)] string? Channel = null,
    [property: Id(3)] string? Username = null,
    [property: Id(4)] string? IconEmoji = null,
    [property: Id(5)] Guid? TriggeredByAlertId = null,
    [property: Id(6)] Dictionary<string, string>? Metadata = null);

[GenerateSerializer]
public record SendNotificationForAlertCommand(
    [property: Id(0)] Alert Alert,
    [property: Id(1)] IReadOnlyList<NotificationChannel> Channels);

// ============================================================================
// Notification Channel Configuration (moved from IAlertGrain.cs)
// ============================================================================

[GenerateSerializer]
public record NotificationChannelConfig
{
    [Id(0)] public required Guid ChannelId { get; init; }
    [Id(1)] public required NotificationType Type { get; init; }
    [Id(2)] public required string Target { get; init; }
    [Id(3)] public required bool IsEnabled { get; init; }
    [Id(4)] public IReadOnlyList<AlertType>? AlertTypes { get; init; }
    [Id(5)] public AlertSeverity? MinimumSeverity { get; init; }
    [Id(6)] public Dictionary<string, string>? Configuration { get; init; }
}

// ============================================================================
// Notification Grain Interface
// ============================================================================

/// <summary>
/// Grain for sending and tracking notifications.
/// Key: "{orgId}:notifications"
/// </summary>
public interface INotificationGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initializes the notification grain for an organization.
    /// </summary>
    Task InitializeAsync(Guid orgId);

    // ============================================================================
    // Send Operations
    // ============================================================================

    /// <summary>
    /// Sends an email notification.
    /// </summary>
    Task<Notification> SendEmailAsync(SendEmailCommand command);

    /// <summary>
    /// Sends an SMS notification.
    /// </summary>
    Task<Notification> SendSmsAsync(SendSmsCommand command);

    /// <summary>
    /// Sends a push notification.
    /// </summary>
    Task<Notification> SendPushAsync(SendPushCommand command);

    /// <summary>
    /// Sends a Slack notification.
    /// </summary>
    Task<Notification> SendSlackAsync(SendSlackCommand command);

    /// <summary>
    /// Sends notifications for an alert to configured channels.
    /// </summary>
    Task SendForAlertAsync(SendNotificationForAlertCommand command);

    // ============================================================================
    // Channel Management
    // ============================================================================

    /// <summary>
    /// Gets all configured notification channels.
    /// </summary>
    Task<IReadOnlyList<NotificationChannelConfig>> GetChannelsAsync();

    /// <summary>
    /// Adds a new notification channel.
    /// </summary>
    Task<NotificationChannelConfig> AddChannelAsync(NotificationChannelConfig channel);

    /// <summary>
    /// Updates an existing notification channel.
    /// </summary>
    Task UpdateChannelAsync(NotificationChannelConfig channel);

    /// <summary>
    /// Removes a notification channel.
    /// </summary>
    Task RemoveChannelAsync(Guid channelId);

    /// <summary>
    /// Enables or disables a channel.
    /// </summary>
    Task SetChannelEnabledAsync(Guid channelId, bool enabled);

    // ============================================================================
    // Query Operations
    // ============================================================================

    /// <summary>
    /// Gets a specific notification by ID.
    /// </summary>
    Task<Notification?> GetNotificationAsync(Guid notificationId);

    /// <summary>
    /// Gets recent notifications with optional filtering.
    /// </summary>
    Task<IReadOnlyList<Notification>> GetNotificationsAsync(
        NotificationType? type = null,
        NotificationStatus? status = null,
        int limit = 100);

    /// <summary>
    /// Gets notifications for a specific alert.
    /// </summary>
    Task<IReadOnlyList<Notification>> GetNotificationsForAlertAsync(Guid alertId);

    /// <summary>
    /// Retries a failed notification.
    /// </summary>
    Task<Notification> RetryAsync(Guid notificationId);

    /// <summary>
    /// Checks if the grain has been initialized.
    /// </summary>
    Task<bool> ExistsAsync();
}
