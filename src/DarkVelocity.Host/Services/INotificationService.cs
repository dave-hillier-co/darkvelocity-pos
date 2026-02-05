namespace DarkVelocity.Host.Services;

/// <summary>
/// Service for sending email notifications.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email to the specified recipient.
    /// </summary>
    /// <param name="to">The recipient email address.</param>
    /// <param name="subject">The email subject.</param>
    /// <param name="body">The email body (HTML or plain text).</param>
    /// <param name="isHtml">Whether the body is HTML (default: true).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<NotificationResult> SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for sending SMS notifications.
/// </summary>
public interface ISmsService
{
    /// <summary>
    /// Sends an SMS to the specified phone number.
    /// </summary>
    /// <param name="to">The recipient phone number (E.164 format).</param>
    /// <param name="message">The message text (max 160 chars for single SMS).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<NotificationResult> SendAsync(
        string to,
        string message,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for sending push notifications to mobile devices.
/// </summary>
public interface IPushService
{
    /// <summary>
    /// Sends a push notification to the specified device.
    /// </summary>
    /// <param name="deviceToken">The device push token (APNs or FCM).</param>
    /// <param name="title">The notification title.</param>
    /// <param name="body">The notification body.</param>
    /// <param name="data">Optional custom data payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<NotificationResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for sending Slack notifications.
/// </summary>
public interface ISlackService
{
    /// <summary>
    /// Sends a message to a Slack channel via webhook.
    /// </summary>
    /// <param name="webhookUrl">The Slack webhook URL.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="channel">Optional channel override.</param>
    /// <param name="username">Optional username override.</param>
    /// <param name="iconEmoji">Optional icon emoji.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<NotificationResult> SendAsync(
        string webhookUrl,
        string message,
        string? channel = null,
        string? username = null,
        string? iconEmoji = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a rich message with blocks to a Slack channel.
    /// </summary>
    Task<NotificationResult> SendBlocksAsync(
        string webhookUrl,
        object blocks,
        string? text = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a notification send attempt.
/// </summary>
[GenerateSerializer]
public record NotificationResult
{
    /// <summary>
    /// Whether the notification was sent successfully.
    /// </summary>
    [Id(0)] public required bool Success { get; init; }

    /// <summary>
    /// Error message if the notification failed.
    /// </summary>
    [Id(1)] public string? ErrorMessage { get; init; }

    /// <summary>
    /// Error code if available from the provider.
    /// </summary>
    [Id(2)] public string? ErrorCode { get; init; }

    /// <summary>
    /// Provider-specific message ID for tracking.
    /// </summary>
    [Id(3)] public string? MessageId { get; init; }

    /// <summary>
    /// Additional metadata from the provider.
    /// </summary>
    [Id(4)] public Dictionary<string, string>? Metadata { get; init; }

    public static NotificationResult Succeeded(string? messageId = null, Dictionary<string, string>? metadata = null)
        => new() { Success = true, MessageId = messageId, Metadata = metadata };

    public static NotificationResult Failed(string errorMessage, string? errorCode = null)
        => new() { Success = false, ErrorMessage = errorMessage, ErrorCode = errorCode };
}
