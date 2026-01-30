using DarkVelocity.OrdersGateway.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.OrdersGateway.Api.Dtos;

/// <summary>
/// Response DTO for a delivery platform.
/// </summary>
public class DeliveryPlatformDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string PlatformType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PlatformStatus Status { get; set; }
    public string? MerchantId { get; set; }
    public PlatformSettings Settings { get; set; } = new();
    public DateTime? ConnectedAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public DateTime? LastOrderAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request to connect a new delivery platform.
/// </summary>
public record ConnectPlatformRequest(
    string PlatformType,
    string Name,
    string? MerchantId = null,
    PlatformCredentials? Credentials = null,
    PlatformSettings? Settings = null);

/// <summary>
/// Credentials for connecting to a delivery platform.
/// </summary>
public record PlatformCredentials(
    string? ApiKey = null,
    string? ApiSecret = null,
    string? ClientId = null,
    string? ClientSecret = null,
    string? AccessToken = null,
    string? RefreshToken = null,
    string? WebhookSecret = null);

/// <summary>
/// Request to update platform settings.
/// </summary>
public record UpdatePlatformRequest(
    string? Name = null,
    PlatformSettings? Settings = null);

/// <summary>
/// Response for platform connection status/health.
/// </summary>
public class PlatformStatusDto : HalResource
{
    public Guid Id { get; set; }
    public string PlatformType { get; set; } = string.Empty;
    public PlatformStatus Status { get; set; }
    public bool IsConnected { get; set; }
    public bool IsReceivingOrders { get; set; }
    public DateTime? LastOrderAt { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
