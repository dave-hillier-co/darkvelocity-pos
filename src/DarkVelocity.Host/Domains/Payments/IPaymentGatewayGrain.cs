namespace DarkVelocity.Host.Grains;

// ============================================================================
// Merchant Grain
// ============================================================================

[GenerateSerializer]
public record CreateMerchantCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] string Email,
    [property: Id(2)] string BusinessName,
    [property: Id(3)] string? BusinessType,
    [property: Id(4)] string Country,
    [property: Id(5)] string DefaultCurrency,
    [property: Id(6)] string? StatementDescriptor,
    [property: Id(7)] string? AddressLine1,
    [property: Id(8)] string? AddressLine2,
    [property: Id(9)] string? City,
    [property: Id(10)] string? State,
    [property: Id(11)] string? PostalCode,
    [property: Id(12)] Dictionary<string, string>? Metadata);

[GenerateSerializer]
public record UpdateMerchantCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] string? BusinessName,
    [property: Id(2)] string? BusinessType,
    [property: Id(3)] string? StatementDescriptor,
    [property: Id(4)] string? AddressLine1,
    [property: Id(5)] string? AddressLine2,
    [property: Id(6)] string? City,
    [property: Id(7)] string? State,
    [property: Id(8)] string? PostalCode,
    [property: Id(9)] Dictionary<string, string>? Metadata);

[GenerateSerializer]
public record MerchantSnapshot(
    [property: Id(0)] Guid MerchantId,
    [property: Id(1)] string Name,
    [property: Id(2)] string Email,
    [property: Id(3)] string BusinessName,
    [property: Id(4)] string? BusinessType,
    [property: Id(5)] string Country,
    [property: Id(6)] string DefaultCurrency,
    [property: Id(7)] string Status,
    [property: Id(8)] bool PayoutsEnabled,
    [property: Id(9)] bool ChargesEnabled,
    [property: Id(10)] string? StatementDescriptor,
    [property: Id(11)] string? AddressLine1,
    [property: Id(12)] string? AddressLine2,
    [property: Id(13)] string? City,
    [property: Id(14)] string? State,
    [property: Id(15)] string? PostalCode,
    [property: Id(16)] DateTime CreatedAt,
    [property: Id(17)] DateTime? UpdatedAt);

[GenerateSerializer]
public record ApiKeySnapshot(
    [property: Id(0)] Guid KeyId,
    [property: Id(1)] string Name,
    [property: Id(2)] string KeyType,
    [property: Id(3)] string KeyPrefix,
    [property: Id(4)] string KeyHint,
    [property: Id(5)] bool IsLive,
    [property: Id(6)] bool IsActive,
    [property: Id(7)] DateTime? LastUsedAt,
    [property: Id(8)] DateTime? ExpiresAt,
    [property: Id(9)] DateTime CreatedAt);

/// <summary>
/// Grain for merchant management.
/// Key: "{orgId}:merchant:{merchantId}"
/// </summary>
public interface IMerchantGrain : IGrainWithStringKey
{
    Task<MerchantSnapshot> CreateAsync(CreateMerchantCommand command);
    Task<MerchantSnapshot> UpdateAsync(UpdateMerchantCommand command);
    Task<MerchantSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();

    // API Key management
    Task<ApiKeySnapshot> CreateApiKeyAsync(string name, string keyType, bool isLive, DateTime? expiresAt);
    Task<IReadOnlyList<ApiKeySnapshot>> GetApiKeysAsync();
    Task RevokeApiKeyAsync(Guid keyId);
    Task<ApiKeySnapshot> RollApiKeyAsync(Guid keyId, DateTime? expiresAt);
    Task<bool> ValidateApiKeyAsync(string keyHash);

    // Status management
    Task EnableChargesAsync();
    Task DisableChargesAsync();
    Task EnablePayoutsAsync();
    Task DisablePayoutsAsync();
}

// ============================================================================
// Terminal Grain
// ============================================================================

public enum TerminalStatus
{
    Active,
    Inactive,
    Offline
}

[GenerateSerializer]
public record RegisterTerminalCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] string Label,
    [property: Id(2)] string? DeviceType,
    [property: Id(3)] string? SerialNumber,
    [property: Id(4)] Dictionary<string, string>? Metadata);

[GenerateSerializer]
public record UpdateTerminalCommand(
    [property: Id(0)] string? Label,
    [property: Id(1)] Guid? LocationId,
    [property: Id(2)] Dictionary<string, string>? Metadata,
    [property: Id(3)] TerminalStatus? Status);

[GenerateSerializer]
public record TerminalSnapshot(
    [property: Id(0)] Guid TerminalId,
    [property: Id(1)] Guid MerchantId,
    [property: Id(2)] Guid LocationId,
    [property: Id(3)] string Label,
    [property: Id(4)] string? DeviceType,
    [property: Id(5)] string? SerialNumber,
    [property: Id(6)] TerminalStatus Status,
    [property: Id(7)] DateTime? LastSeenAt,
    [property: Id(8)] string? IpAddress,
    [property: Id(9)] string? SoftwareVersion,
    [property: Id(10)] DateTime CreatedAt,
    [property: Id(11)] DateTime? UpdatedAt);

/// <summary>
/// Grain for payment terminal management.
/// Key: "{orgId}:terminal:{terminalId}"
/// </summary>
public interface ITerminalGrain : IGrainWithStringKey
{
    Task<TerminalSnapshot> RegisterAsync(RegisterTerminalCommand command);
    Task<TerminalSnapshot> UpdateAsync(UpdateTerminalCommand command);
    Task<TerminalSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();
    Task DeactivateAsync();

    // Status management
    Task HeartbeatAsync(string? ipAddress, string? softwareVersion);
    Task<bool> IsOnlineAsync();
}

// ============================================================================
// Refund Grain
// ============================================================================

public enum RefundStatus
{
    Pending,
    Succeeded,
    Failed,
    Cancelled
}

[GenerateSerializer]
public record CreateRefundCommand(
    [property: Id(0)] Guid PaymentIntentId,
    [property: Id(1)] long? Amount,
    [property: Id(2)] string Currency,
    [property: Id(3)] string? Reason,
    [property: Id(4)] Dictionary<string, string>? Metadata);

[GenerateSerializer]
public record RefundSnapshot(
    [property: Id(0)] Guid RefundId,
    [property: Id(1)] Guid MerchantId,
    [property: Id(2)] Guid PaymentIntentId,
    [property: Id(3)] long Amount,
    [property: Id(4)] string Currency,
    [property: Id(5)] RefundStatus Status,
    [property: Id(6)] string? Reason,
    [property: Id(7)] string? ReceiptNumber,
    [property: Id(8)] string? FailureReason,
    [property: Id(9)] DateTime CreatedAt,
    [property: Id(10)] DateTime? SucceededAt);

/// <summary>
/// Grain for refund management.
/// Key: "{orgId}:refund:{refundId}"
/// </summary>
public interface IRefundGrain : IGrainWithStringKey
{
    Task<RefundSnapshot> CreateAsync(CreateRefundCommand command);
    Task<RefundSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();

    Task<RefundSnapshot> ProcessAsync();
    Task<RefundSnapshot> FailAsync(string reason);
    Task<RefundSnapshot> CancelAsync();
    Task<RefundStatus> GetStatusAsync();
}

// ============================================================================
// Webhook Grain
// ============================================================================

[GenerateSerializer]
public record CreateWebhookEndpointCommand(
    [property: Id(0)] string Url,
    [property: Id(1)] string? Description,
    [property: Id(2)] IReadOnlyList<string> EnabledEvents,
    [property: Id(3)] string? Secret);

[GenerateSerializer]
public record UpdateWebhookEndpointCommand(
    [property: Id(0)] string? Url,
    [property: Id(1)] string? Description,
    [property: Id(2)] IReadOnlyList<string>? EnabledEvents,
    [property: Id(3)] bool? Enabled);

[GenerateSerializer]
public record WebhookDeliveryAttempt(
    [property: Id(0)] DateTime AttemptedAt,
    [property: Id(1)] int StatusCode,
    [property: Id(2)] bool Success,
    [property: Id(3)] string? Error);

[GenerateSerializer]
public record WebhookEndpointSnapshot(
    [property: Id(0)] Guid EndpointId,
    [property: Id(1)] Guid MerchantId,
    [property: Id(2)] string Url,
    [property: Id(3)] string? Description,
    [property: Id(4)] IReadOnlyList<string> EnabledEvents,
    [property: Id(5)] bool Enabled,
    [property: Id(6)] string Status,
    [property: Id(7)] DateTime? LastDeliveryAt,
    [property: Id(8)] IReadOnlyList<WebhookDeliveryAttempt> RecentDeliveries,
    [property: Id(9)] DateTime CreatedAt,
    [property: Id(10)] DateTime? UpdatedAt);

/// <summary>
/// Grain for webhook endpoint management.
/// Key: "{orgId}:webhook:{endpointId}"
/// </summary>
public interface IWebhookEndpointGrain : IGrainWithStringKey
{
    Task<WebhookEndpointSnapshot> CreateAsync(CreateWebhookEndpointCommand command);
    Task<WebhookEndpointSnapshot> UpdateAsync(UpdateWebhookEndpointCommand command);
    Task<WebhookEndpointSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();
    Task DeleteAsync();

    // Delivery management
    Task RecordDeliveryAttemptAsync(int statusCode, bool success, string? error);
    Task EnableAsync();
    Task DisableAsync();
    Task<bool> ShouldReceiveEventAsync(string eventType);
}
