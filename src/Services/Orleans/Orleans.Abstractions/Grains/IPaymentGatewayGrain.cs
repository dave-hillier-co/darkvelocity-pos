namespace DarkVelocity.Orleans.Abstractions.Grains;

// ============================================================================
// Merchant Grain
// ============================================================================

public record CreateMerchantCommand(
    string Name,
    string Email,
    string BusinessName,
    string? BusinessType,
    string Country,
    string DefaultCurrency,
    string? StatementDescriptor,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    Dictionary<string, string>? Metadata);

public record UpdateMerchantCommand(
    string? Name,
    string? BusinessName,
    string? BusinessType,
    string? StatementDescriptor,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    Dictionary<string, string>? Metadata);

public record MerchantSnapshot(
    Guid MerchantId,
    string Name,
    string Email,
    string BusinessName,
    string? BusinessType,
    string Country,
    string DefaultCurrency,
    string Status,
    bool PayoutsEnabled,
    bool ChargesEnabled,
    string? StatementDescriptor,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record ApiKeySnapshot(
    Guid KeyId,
    string Name,
    string KeyType,
    string KeyPrefix,
    string KeyHint,
    bool IsLive,
    bool IsActive,
    DateTime? LastUsedAt,
    DateTime? ExpiresAt,
    DateTime CreatedAt);

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

public record RegisterTerminalCommand(
    Guid LocationId,
    string Label,
    string? DeviceType,
    string? SerialNumber,
    Dictionary<string, string>? Metadata);

public record UpdateTerminalCommand(
    string? Label,
    Guid? LocationId,
    Dictionary<string, string>? Metadata,
    TerminalStatus? Status);

public record TerminalSnapshot(
    Guid TerminalId,
    Guid MerchantId,
    Guid LocationId,
    string Label,
    string? DeviceType,
    string? SerialNumber,
    TerminalStatus Status,
    DateTime? LastSeenAt,
    string? IpAddress,
    string? SoftwareVersion,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

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

public record CreateRefundCommand(
    Guid PaymentIntentId,
    long? Amount,
    string Currency,
    string? Reason,
    Dictionary<string, string>? Metadata);

public record RefundSnapshot(
    Guid RefundId,
    Guid MerchantId,
    Guid PaymentIntentId,
    long Amount,
    string Currency,
    RefundStatus Status,
    string? Reason,
    string? ReceiptNumber,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? SucceededAt);

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

public record CreateWebhookEndpointCommand(
    string Url,
    string? Description,
    IReadOnlyList<string> EnabledEvents,
    string? Secret);

public record UpdateWebhookEndpointCommand(
    string? Url,
    string? Description,
    IReadOnlyList<string>? EnabledEvents,
    bool? Enabled);

public record WebhookDeliveryAttempt(
    DateTime AttemptedAt,
    int StatusCode,
    bool Success,
    string? Error);

public record WebhookEndpointSnapshot(
    Guid EndpointId,
    Guid MerchantId,
    string Url,
    string? Description,
    IReadOnlyList<string> EnabledEvents,
    bool Enabled,
    string Status,
    DateTime? LastDeliveryAt,
    IReadOnlyList<WebhookDeliveryAttempt> RecentDeliveries,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

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
