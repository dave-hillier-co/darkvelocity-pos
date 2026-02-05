using Microsoft.Extensions.Logging;

namespace DarkVelocity.Host.Services;

/// <summary>
/// Stub email service for development/testing.
/// Logs email sends instead of actually sending.
/// </summary>
public sealed class StubEmailService : IEmailService
{
    private readonly ILogger<StubEmailService> _logger;

    public StubEmailService(ILogger<StubEmailService> logger)
    {
        _logger = logger;
    }

    public Task<NotificationResult> SendAsync(
        string to,
        string subject,
        string body,
        bool isHtml = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB EMAIL] To: {To}, Subject: {Subject}, Body length: {BodyLength}, IsHtml: {IsHtml}",
            to, subject, body.Length, isHtml);

        return Task.FromResult(NotificationResult.Succeeded(
            messageId: $"stub-email-{Guid.NewGuid():N}",
            metadata: new Dictionary<string, string>
            {
                ["provider"] = "stub",
                ["sentAt"] = DateTime.UtcNow.ToString("O")
            }));
    }
}

/// <summary>
/// Stub SMS service for development/testing.
/// Logs SMS sends instead of actually sending.
/// </summary>
public sealed class StubSmsService : ISmsService
{
    private readonly ILogger<StubSmsService> _logger;

    public StubSmsService(ILogger<StubSmsService> logger)
    {
        _logger = logger;
    }

    public Task<NotificationResult> SendAsync(
        string to,
        string message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB SMS] To: {To}, Message: {Message}",
            to, message);

        return Task.FromResult(NotificationResult.Succeeded(
            messageId: $"stub-sms-{Guid.NewGuid():N}",
            metadata: new Dictionary<string, string>
            {
                ["provider"] = "stub",
                ["sentAt"] = DateTime.UtcNow.ToString("O")
            }));
    }
}

/// <summary>
/// Stub push notification service for development/testing.
/// Logs push notifications instead of actually sending.
/// </summary>
public sealed class StubPushService : IPushService
{
    private readonly ILogger<StubPushService> _logger;

    public StubPushService(ILogger<StubPushService> logger)
    {
        _logger = logger;
    }

    public Task<NotificationResult> SendAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB PUSH] Device: {DeviceToken}, Title: {Title}, Body: {Body}, Data: {Data}",
            deviceToken[..Math.Min(20, deviceToken.Length)] + "...",
            title,
            body,
            data != null ? string.Join(", ", data.Select(kv => $"{kv.Key}={kv.Value}")) : "none");

        return Task.FromResult(NotificationResult.Succeeded(
            messageId: $"stub-push-{Guid.NewGuid():N}",
            metadata: new Dictionary<string, string>
            {
                ["provider"] = "stub",
                ["sentAt"] = DateTime.UtcNow.ToString("O")
            }));
    }
}

/// <summary>
/// Stub Slack service for development/testing.
/// Logs Slack messages instead of actually sending.
/// </summary>
public sealed class StubSlackService : ISlackService
{
    private readonly ILogger<StubSlackService> _logger;

    public StubSlackService(ILogger<StubSlackService> logger)
    {
        _logger = logger;
    }

    public Task<NotificationResult> SendAsync(
        string webhookUrl,
        string message,
        string? channel = null,
        string? username = null,
        string? iconEmoji = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB SLACK] WebhookUrl: {WebhookUrl}, Message: {Message}, Channel: {Channel}, Username: {Username}",
            webhookUrl[..Math.Min(50, webhookUrl.Length)] + "...",
            message,
            channel ?? "default",
            username ?? "default");

        return Task.FromResult(NotificationResult.Succeeded(
            messageId: $"stub-slack-{Guid.NewGuid():N}",
            metadata: new Dictionary<string, string>
            {
                ["provider"] = "stub",
                ["sentAt"] = DateTime.UtcNow.ToString("O")
            }));
    }

    public Task<NotificationResult> SendBlocksAsync(
        string webhookUrl,
        object blocks,
        string? text = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[STUB SLACK BLOCKS] WebhookUrl: {WebhookUrl}, Text: {Text}, Blocks: {Blocks}",
            webhookUrl[..Math.Min(50, webhookUrl.Length)] + "...",
            text ?? "none",
            blocks.GetType().Name);

        return Task.FromResult(NotificationResult.Succeeded(
            messageId: $"stub-slack-{Guid.NewGuid():N}",
            metadata: new Dictionary<string, string>
            {
                ["provider"] = "stub",
                ["sentAt"] = DateTime.UtcNow.ToString("O")
            }));
    }
}
