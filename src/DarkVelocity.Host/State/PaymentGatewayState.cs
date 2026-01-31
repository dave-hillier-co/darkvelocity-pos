using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

[GenerateSerializer]
public sealed class MerchantState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid MerchantId { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string Email { get; set; } = string.Empty;
    [Id(4)] public string BusinessName { get; set; } = string.Empty;
    [Id(5)] public string? BusinessType { get; set; }
    [Id(6)] public string Country { get; set; } = string.Empty;
    [Id(7)] public string DefaultCurrency { get; set; } = "USD";
    [Id(8)] public string Status { get; set; } = "active";
    [Id(9)] public bool PayoutsEnabled { get; set; }
    [Id(10)] public bool ChargesEnabled { get; set; } = true;
    [Id(11)] public string? StatementDescriptor { get; set; }
    [Id(12)] public string? AddressLine1 { get; set; }
    [Id(13)] public string? AddressLine2 { get; set; }
    [Id(14)] public string? City { get; set; }
    [Id(15)] public string? State { get; set; }
    [Id(16)] public string? PostalCode { get; set; }
    [Id(17)] public string? Metadata { get; set; }
    [Id(18)] public List<ApiKeyState> ApiKeys { get; set; } = [];
    [Id(19)] public DateTime CreatedAt { get; set; }
    [Id(20)] public DateTime? UpdatedAt { get; set; }
    [Id(21)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class ApiKeyState
{
    [Id(0)] public Guid KeyId { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public string KeyType { get; set; } = string.Empty;
    [Id(3)] public string KeyPrefix { get; set; } = string.Empty;
    [Id(4)] public string KeyHash { get; set; } = string.Empty;
    [Id(5)] public string KeyHint { get; set; } = string.Empty;
    [Id(6)] public bool IsLive { get; set; }
    [Id(7)] public bool IsActive { get; set; } = true;
    [Id(8)] public DateTime? LastUsedAt { get; set; }
    [Id(9)] public DateTime? ExpiresAt { get; set; }
    [Id(10)] public DateTime? RevokedAt { get; set; }
    [Id(11)] public DateTime CreatedAt { get; set; }
}

[GenerateSerializer]
public sealed class TerminalState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid TerminalId { get; set; }
    [Id(2)] public Guid MerchantId { get; set; }
    [Id(3)] public Guid LocationId { get; set; }
    [Id(4)] public string Label { get; set; } = string.Empty;
    [Id(5)] public string? DeviceType { get; set; }
    [Id(6)] public string? SerialNumber { get; set; }
    [Id(7)] public TerminalStatus Status { get; set; } = TerminalStatus.Active;
    [Id(8)] public DateTime? LastSeenAt { get; set; }
    [Id(9)] public string? IpAddress { get; set; }
    [Id(10)] public string? SoftwareVersion { get; set; }
    [Id(11)] public string? Metadata { get; set; }
    [Id(12)] public DateTime CreatedAt { get; set; }
    [Id(13)] public DateTime? UpdatedAt { get; set; }
    [Id(14)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class RefundState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid RefundId { get; set; }
    [Id(2)] public Guid MerchantId { get; set; }
    [Id(3)] public Guid PaymentIntentId { get; set; }
    [Id(4)] public long Amount { get; set; }
    [Id(5)] public string Currency { get; set; } = "USD";
    [Id(6)] public RefundStatus Status { get; set; } = RefundStatus.Pending;
    [Id(7)] public string? Reason { get; set; }
    [Id(8)] public string? ReceiptNumber { get; set; }
    [Id(9)] public string? FailureReason { get; set; }
    [Id(10)] public string? Metadata { get; set; }
    [Id(11)] public DateTime CreatedAt { get; set; }
    [Id(12)] public DateTime? SucceededAt { get; set; }
    [Id(13)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class WebhookEndpointState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid EndpointId { get; set; }
    [Id(2)] public Guid MerchantId { get; set; }
    [Id(3)] public string Url { get; set; } = string.Empty;
    [Id(4)] public string? Description { get; set; }
    [Id(5)] public List<string> EnabledEvents { get; set; } = [];
    [Id(6)] public string? Secret { get; set; }
    [Id(7)] public bool Enabled { get; set; } = true;
    [Id(8)] public string Status { get; set; } = "enabled";
    [Id(9)] public DateTime? LastDeliveryAt { get; set; }
    [Id(10)] public List<WebhookDeliveryState> RecentDeliveries { get; set; } = [];
    [Id(11)] public DateTime CreatedAt { get; set; }
    [Id(12)] public DateTime? UpdatedAt { get; set; }
    [Id(13)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class WebhookDeliveryState
{
    [Id(0)] public DateTime AttemptedAt { get; set; }
    [Id(1)] public int StatusCode { get; set; }
    [Id(2)] public bool Success { get; set; }
    [Id(3)] public string? Error { get; set; }
}
