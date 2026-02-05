using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Services;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Domains.System;

/// <summary>
/// Grain for sending and tracking notifications.
/// </summary>
public class NotificationGrain : Grain, INotificationGrain
{
    private readonly IPersistentState<NotificationState> _state;
    private readonly IEmailService _emailService;
    private readonly ISmsService _smsService;
    private readonly IPushService _pushService;
    private readonly ISlackService _slackService;
    private readonly ILogger<NotificationGrain> _logger;
    private IAsyncStream<IStreamEvent>? _notificationStream;

    private const int MaxRetries = 3;

    public NotificationGrain(
        [PersistentState("notifications", "OrleansStorage")]
        IPersistentState<NotificationState> state,
        IEmailService emailService,
        ISmsService smsService,
        IPushService pushService,
        ISlackService slackService,
        ILogger<NotificationGrain> logger)
    {
        _state = state;
        _emailService = emailService;
        _smsService = smsService;
        _pushService = pushService;
        _slackService = slackService;
        _logger = logger;
    }

    private IAsyncStream<IStreamEvent> GetNotificationStream()
    {
        if (_notificationStream == null && _state.State.OrgId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.NotificationStreamNamespace, _state.State.OrgId.ToString());
            _notificationStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _notificationStream!;
    }

    public async Task InitializeAsync(Guid orgId)
    {
        if (_state.State.OrgId != Guid.Empty)
            return; // Already initialized

        _state.State = new NotificationState
        {
            OrgId = orgId,
            Version = 1
        };

        await _state.WriteStateAsync();
    }

    public async Task<Notification> SendEmailAsync(SendEmailCommand command)
    {
        EnsureInitialized();

        var notification = await CreateNotificationRecord(
            NotificationType.Email,
            command.To,
            command.Subject,
            command.Body,
            command.TriggeredByAlertId,
            command.Metadata);

        // Publish queued event
        await PublishNotificationQueuedEvent(notification);

        // Attempt to send
        var result = await _emailService.SendAsync(
            command.To,
            command.Subject,
            command.Body,
            command.IsHtml);

        await UpdateNotificationWithResult(notification.NotificationId, result);

        return await GetNotificationByIdAsync(notification.NotificationId);
    }

    public async Task<Notification> SendSmsAsync(SendSmsCommand command)
    {
        EnsureInitialized();

        var notification = await CreateNotificationRecord(
            NotificationType.Sms,
            command.To,
            "SMS", // SMS has no subject
            command.Message,
            command.TriggeredByAlertId,
            command.Metadata);

        // Publish queued event
        await PublishNotificationQueuedEvent(notification);

        // Attempt to send
        var result = await _smsService.SendAsync(command.To, command.Message);

        await UpdateNotificationWithResult(notification.NotificationId, result);

        return await GetNotificationByIdAsync(notification.NotificationId);
    }

    public async Task<Notification> SendPushAsync(SendPushCommand command)
    {
        EnsureInitialized();

        var notification = await CreateNotificationRecord(
            NotificationType.Push,
            command.DeviceToken,
            command.Title,
            command.Body,
            command.TriggeredByAlertId,
            command.Metadata);

        // Publish queued event
        await PublishNotificationQueuedEvent(notification);

        // Attempt to send
        var result = await _pushService.SendAsync(
            command.DeviceToken,
            command.Title,
            command.Body,
            command.Data);

        await UpdateNotificationWithResult(notification.NotificationId, result);

        return await GetNotificationByIdAsync(notification.NotificationId);
    }

    public async Task<Notification> SendSlackAsync(SendSlackCommand command)
    {
        EnsureInitialized();

        var notification = await CreateNotificationRecord(
            NotificationType.Slack,
            command.WebhookUrl,
            command.Channel ?? "default",
            command.Message,
            command.TriggeredByAlertId,
            command.Metadata);

        // Publish queued event
        await PublishNotificationQueuedEvent(notification);

        // Attempt to send
        var result = await _slackService.SendAsync(
            command.WebhookUrl,
            command.Message,
            command.Channel,
            command.Username,
            command.IconEmoji);

        await UpdateNotificationWithResult(notification.NotificationId, result);

        return await GetNotificationByIdAsync(notification.NotificationId);
    }

    public async Task SendForAlertAsync(SendNotificationForAlertCommand command)
    {
        EnsureInitialized();

        var alert = command.Alert;
        var alertSeverity = Enum.Parse<AlertSeverity>(alert.Severity.ToString());

        foreach (var channel in command.Channels)
        {
            // Check if channel is enabled
            if (!channel.IsEnabled)
                continue;

            // Check severity filter
            if (channel.MinimumSeverity.HasValue && alertSeverity < channel.MinimumSeverity.Value)
                continue;

            // Check alert type filter
            if (channel.AlertTypes != null && channel.AlertTypes.Count > 0)
            {
                if (!channel.AlertTypes.Contains(alert.Type))
                    continue;
            }

            try
            {
                switch (channel.Type.ToLowerInvariant())
                {
                    case "email":
                        await SendEmailAsync(new SendEmailCommand(
                            To: channel.Target,
                            Subject: $"[{alert.Severity}] {alert.Title}",
                            Body: FormatAlertEmailBody(alert),
                            TriggeredByAlertId: alert.AlertId));
                        break;

                    case "sms":
                        await SendSmsAsync(new SendSmsCommand(
                            To: channel.Target,
                            Message: $"[{alert.Severity}] {alert.Title}: {alert.Message}",
                            TriggeredByAlertId: alert.AlertId));
                        break;

                    case "push":
                        await SendPushAsync(new SendPushCommand(
                            DeviceToken: channel.Target,
                            Title: $"[{alert.Severity}] {alert.Title}",
                            Body: alert.Message,
                            TriggeredByAlertId: alert.AlertId));
                        break;

                    case "slack":
                    case "webhook":
                        await SendSlackAsync(new SendSlackCommand(
                            WebhookUrl: channel.Target,
                            Message: FormatAlertSlackMessage(alert),
                            TriggeredByAlertId: alert.AlertId));
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send notification to channel {ChannelType}:{Target} for alert {AlertId}",
                    channel.Type, channel.Target, alert.AlertId);
            }
        }
    }

    public Task<IReadOnlyList<NotificationChannelConfig>> GetChannelsAsync()
    {
        EnsureInitialized();

        var channels = _state.State.Channels.Select(c => new NotificationChannelConfig
        {
            ChannelId = c.ChannelId,
            Type = c.Type,
            Target = c.Target,
            IsEnabled = c.IsEnabled,
            AlertTypes = c.AlertTypes,
            MinimumSeverity = c.MinimumSeverity,
            Configuration = c.Configuration
        }).ToList();

        return Task.FromResult<IReadOnlyList<NotificationChannelConfig>>(channels);
    }

    public async Task<NotificationChannelConfig> AddChannelAsync(NotificationChannelConfig channel)
    {
        EnsureInitialized();

        var channelId = channel.ChannelId == Guid.Empty ? Guid.NewGuid() : channel.ChannelId;

        var record = new NotificationChannelRecord
        {
            ChannelId = channelId,
            Type = channel.Type,
            Target = channel.Target,
            IsEnabled = channel.IsEnabled,
            AlertTypes = channel.AlertTypes?.ToList(),
            MinimumSeverity = channel.MinimumSeverity,
            Configuration = channel.Configuration
        };

        _state.State.Channels.Add(record);
        _state.State.Version++;

        await _state.WriteStateAsync();

        return channel with { ChannelId = channelId };
    }

    public async Task UpdateChannelAsync(NotificationChannelConfig channel)
    {
        EnsureInitialized();

        var existing = _state.State.Channels.FirstOrDefault(c => c.ChannelId == channel.ChannelId)
            ?? throw new InvalidOperationException($"Channel not found: {channel.ChannelId}");

        existing.Type = channel.Type;
        existing.Target = channel.Target;
        existing.IsEnabled = channel.IsEnabled;
        existing.AlertTypes = channel.AlertTypes?.ToList();
        existing.MinimumSeverity = channel.MinimumSeverity;
        existing.Configuration = channel.Configuration;

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveChannelAsync(Guid channelId)
    {
        EnsureInitialized();

        var channel = _state.State.Channels.FirstOrDefault(c => c.ChannelId == channelId);
        if (channel != null)
        {
            _state.State.Channels.Remove(channel);
            _state.State.Version++;
            await _state.WriteStateAsync();
        }
    }

    public async Task SetChannelEnabledAsync(Guid channelId, bool enabled)
    {
        EnsureInitialized();

        var channel = _state.State.Channels.FirstOrDefault(c => c.ChannelId == channelId)
            ?? throw new InvalidOperationException($"Channel not found: {channelId}");

        channel.IsEnabled = enabled;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<Notification?> GetNotificationAsync(Guid notificationId)
    {
        EnsureInitialized();

        var record = _state.State.Notifications.FirstOrDefault(n => n.NotificationId == notificationId);
        return Task.FromResult(record != null ? ToNotification(record) : null);
    }

    public Task<IReadOnlyList<Notification>> GetNotificationsAsync(
        NotificationType? type = null,
        NotificationStatus? status = null,
        int limit = 100)
    {
        EnsureInitialized();

        var query = _state.State.Notifications.AsEnumerable();

        if (type.HasValue)
            query = query.Where(n => n.Type == type.Value);

        if (status.HasValue)
            query = query.Where(n => n.Status == status.Value);

        var notifications = query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .Select(ToNotification)
            .ToList();

        return Task.FromResult<IReadOnlyList<Notification>>(notifications);
    }

    public Task<IReadOnlyList<Notification>> GetNotificationsForAlertAsync(Guid alertId)
    {
        EnsureInitialized();

        var notifications = _state.State.Notifications
            .Where(n => n.TriggeredByAlertId == alertId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(ToNotification)
            .ToList();

        return Task.FromResult<IReadOnlyList<Notification>>(notifications);
    }

    public async Task<Notification> RetryAsync(Guid notificationId)
    {
        EnsureInitialized();

        var record = _state.State.Notifications.FirstOrDefault(n => n.NotificationId == notificationId)
            ?? throw new InvalidOperationException($"Notification not found: {notificationId}");

        if (record.Status != NotificationStatus.Failed)
            throw new InvalidOperationException($"Can only retry failed notifications. Current status: {record.Status}");

        if (record.RetryCount >= MaxRetries)
            throw new InvalidOperationException($"Maximum retries ({MaxRetries}) exceeded");

        record.Status = NotificationStatus.Retrying;
        record.RetryCount++;
        await _state.WriteStateAsync();

        // Publish retry event
        await GetNotificationStream().OnNextAsync(new NotificationRetriedEvent(
            record.NotificationId,
            record.Type.ToString(),
            record.Recipient,
            record.RetryCount)
        {
            OrganizationId = _state.State.OrgId
        });

        // Attempt to send again based on type
        NotificationResult result;
        switch (record.Type)
        {
            case NotificationType.Email:
                result = await _emailService.SendAsync(record.Recipient, record.Subject, record.Body);
                break;
            case NotificationType.Sms:
                result = await _smsService.SendAsync(record.Recipient, record.Body);
                break;
            case NotificationType.Push:
                result = await _pushService.SendAsync(record.Recipient, record.Subject, record.Body);
                break;
            case NotificationType.Slack:
                result = await _slackService.SendAsync(record.Recipient, record.Body);
                break;
            default:
                throw new InvalidOperationException($"Unknown notification type: {record.Type}");
        }

        await UpdateNotificationWithResult(notificationId, result);

        return await GetNotificationByIdAsync(notificationId);
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.OrgId != Guid.Empty);

    // ============================================================================
    // Private Helper Methods
    // ============================================================================

    private void EnsureInitialized()
    {
        if (_state.State.OrgId == Guid.Empty)
            throw new InvalidOperationException("Notification grain not initialized");
    }

    private async Task<NotificationRecord> CreateNotificationRecord(
        NotificationType type,
        string recipient,
        string subject,
        string body,
        Guid? triggeredByAlertId,
        Dictionary<string, string>? metadata)
    {
        var record = new NotificationRecord
        {
            NotificationId = Guid.NewGuid(),
            Type = type,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = NotificationStatus.Queued,
            CreatedAt = DateTime.UtcNow,
            TriggeredByAlertId = triggeredByAlertId,
            Metadata = metadata
        };

        _state.State.Notifications.Insert(0, record);

        // Trim old notifications if needed
        while (_state.State.Notifications.Count > _state.State.MaxNotifications)
        {
            _state.State.Notifications.RemoveAt(_state.State.Notifications.Count - 1);
        }

        _state.State.Version++;
        await _state.WriteStateAsync();

        return record;
    }

    private async Task UpdateNotificationWithResult(Guid notificationId, NotificationResult result)
    {
        var record = _state.State.Notifications.First(n => n.NotificationId == notificationId);

        if (result.Success)
        {
            record.Status = NotificationStatus.Sent;
            record.SentAt = DateTime.UtcNow;
            record.ExternalMessageId = result.MessageId;

            _logger.LogInformation(
                "Notification {NotificationId} sent successfully. MessageId: {MessageId}",
                notificationId, result.MessageId);

            // Publish sent event
            await GetNotificationStream().OnNextAsync(new NotificationSentEvent(
                record.NotificationId,
                record.Type.ToString(),
                record.Recipient,
                record.ExternalMessageId)
            {
                OrganizationId = _state.State.OrgId
            });
        }
        else
        {
            record.Status = NotificationStatus.Failed;
            record.ErrorMessage = result.ErrorMessage;

            _logger.LogWarning(
                "Notification {NotificationId} failed. Error: {Error}",
                notificationId, result.ErrorMessage);

            // Publish failed event
            await GetNotificationStream().OnNextAsync(new NotificationFailedEvent(
                record.NotificationId,
                record.Type.ToString(),
                record.Recipient,
                result.ErrorMessage ?? "Unknown error",
                result.ErrorCode)
            {
                OrganizationId = _state.State.OrgId
            });
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private async Task PublishNotificationQueuedEvent(NotificationRecord record)
    {
        await GetNotificationStream().OnNextAsync(new NotificationQueuedEvent(
            record.NotificationId,
            record.Type.ToString(),
            record.Recipient,
            record.Subject,
            record.TriggeredByAlertId)
        {
            OrganizationId = _state.State.OrgId
        });
    }

    private async Task<Notification> GetNotificationByIdAsync(Guid notificationId)
    {
        var record = _state.State.Notifications.First(n => n.NotificationId == notificationId);
        return ToNotification(record);
    }

    private static Notification ToNotification(NotificationRecord record) => new()
    {
        NotificationId = record.NotificationId,
        Type = record.Type,
        Recipient = record.Recipient,
        Subject = record.Subject,
        Body = record.Body,
        Status = record.Status,
        CreatedAt = record.CreatedAt,
        SentAt = record.SentAt,
        RetryCount = record.RetryCount,
        ErrorMessage = record.ErrorMessage,
        ExternalMessageId = record.ExternalMessageId,
        Metadata = record.Metadata,
        TriggeredByAlertId = record.TriggeredByAlertId
    };

    private static string FormatAlertEmailBody(Alert alert)
    {
        return $"""
            <html>
            <body>
            <h2>{alert.Title}</h2>
            <p><strong>Severity:</strong> {alert.Severity}</p>
            <p><strong>Message:</strong> {alert.Message}</p>
            <p><strong>Site:</strong> {alert.SiteId}</p>
            <p><strong>Triggered At:</strong> {alert.TriggeredAt:yyyy-MM-dd HH:mm:ss} UTC</p>
            {(alert.Metadata != null && alert.Metadata.Count > 0
                ? $"<p><strong>Details:</strong><br/>{string.Join("<br/>", alert.Metadata.Select(kv => $"{kv.Key}: {kv.Value}"))}</p>"
                : "")}
            </body>
            </html>
            """;
    }

    private static string FormatAlertSlackMessage(Alert alert)
    {
        var emoji = alert.Severity.ToString().ToUpperInvariant() switch
        {
            "CRITICAL" => ":rotating_light:",
            "HIGH" => ":warning:",
            "MEDIUM" => ":large_yellow_circle:",
            _ => ":information_source:"
        };

        return $"{emoji} *[{alert.Severity}] {alert.Title}*\n{alert.Message}";
    }
}
