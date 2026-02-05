namespace DarkVelocity.Host.State;

public enum WebhookStatus
{
    Active,
    Paused,
    Failed,
    Deleted
}

[GenerateSerializer]
public record WebhookEvent
{
    [Id(0)] public string EventType { get; init; } = string.Empty;
    [Id(1)] public bool IsEnabled { get; init; } = true;
}

[GenerateSerializer]
public record WebhookDelivery
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public string EventType { get; init; } = string.Empty;
    [Id(2)] public DateTime AttemptedAt { get; init; }
    [Id(3)] public int StatusCode { get; init; }
    [Id(4)] public bool Success { get; init; }
    [Id(5)] public string? ErrorMessage { get; init; }
    [Id(6)] public int RetryCount { get; init; }
}

[GenerateSerializer]
public sealed class WebhookSubscriptionState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }

    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string Url { get; set; } = string.Empty;
    [Id(4)] public string? Secret { get; set; }
    [Id(5)] public WebhookStatus Status { get; set; } = WebhookStatus.Active;

    [Id(6)] public List<WebhookEvent> Events { get; set; } = [];
    [Id(7)] public Dictionary<string, string> Headers { get; set; } = [];

    [Id(8)] public int MaxRetries { get; set; } = 3;
    [Id(9)] public int ConsecutiveFailures { get; set; }
    [Id(10)] public DateTime? LastDeliveryAt { get; set; }
    [Id(11)] public DateTime? PausedAt { get; set; }

    [Id(12)] public List<WebhookDelivery> RecentDeliveries { get; set; } = [];

    [Id(13)] public DateTime CreatedAt { get; set; }
    [Id(14)] public DateTime? UpdatedAt { get; set; }
    [Id(15)] public int Version { get; set; }
}
