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

// Visit History for Customer
[GenerateSerializer]
public record CustomerVisit
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public string? SiteName { get; init; }
    [Id(3)] public DateTime VisitedAt { get; init; }
    [Id(4)] public Guid? OrderId { get; init; }
    [Id(5)] public Guid? BookingId { get; init; }
    [Id(6)] public decimal SpendAmount { get; init; }
    [Id(7)] public int PartySize { get; init; }
    [Id(8)] public int PointsEarned { get; init; }
    [Id(9)] public string? Notes { get; init; }
}

// Booking Calendar State (for listing bookings by date)
[GenerateSerializer]
public record BookingReference
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public string ConfirmationCode { get; init; } = string.Empty;
    [Id(2)] public TimeOnly Time { get; init; }
    [Id(3)] public int PartySize { get; init; }
    [Id(4)] public string GuestName { get; init; } = string.Empty;
    [Id(5)] public BookingStatus Status { get; init; }
    [Id(6)] public Guid? TableId { get; init; }
    [Id(7)] public string? TableNumber { get; init; }
}

[GenerateSerializer]
public sealed class BookingCalendarState
{
    [Id(0)] public Guid SiteId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public DateOnly Date { get; set; }
    [Id(3)] public List<BookingReference> Bookings { get; set; } = [];
    [Id(4)] public int TotalCovers { get; set; }
    [Id(5)] public int Version { get; set; }
}
