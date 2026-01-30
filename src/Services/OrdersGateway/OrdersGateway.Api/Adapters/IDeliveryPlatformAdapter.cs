using DarkVelocity.OrdersGateway.Api.Dtos;
using DarkVelocity.OrdersGateway.Api.Entities;

namespace DarkVelocity.OrdersGateway.Api.Adapters;

/// <summary>
/// Interface for delivery platform adapters.
/// Each supported platform implements this interface.
/// </summary>
public interface IDeliveryPlatformAdapter
{
    /// <summary>
    /// The platform type this adapter handles (e.g., "UberEats", "DoorDash").
    /// </summary>
    string PlatformType { get; }

    #region Connection

    /// <summary>
    /// Tests the connection to the platform with the given credentials.
    /// </summary>
    Task<ConnectionResult> TestConnectionAsync(PlatformCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Connects to the platform and stores credentials.
    /// </summary>
    Task<ConnectionResult> ConnectAsync(DeliveryPlatform platform, PlatformCredentials credentials, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnects from the platform.
    /// </summary>
    Task DisconnectAsync(DeliveryPlatform platform, CancellationToken cancellationToken = default);

    #endregion

    #region Webhooks

    /// <summary>
    /// Parses and validates an incoming webhook payload.
    /// </summary>
    Task<WebhookParseResult> ParseWebhookAsync(string payload, string? signature, DeliveryPlatform platform, CancellationToken cancellationToken = default);

    #endregion

    #region Order Management

    /// <summary>
    /// Accepts an order on the platform.
    /// </summary>
    Task<OrderActionResult> AcceptOrderAsync(DeliveryPlatform platform, string platformOrderId, int prepTimeMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects an order on the platform.
    /// </summary>
    Task<OrderActionResult> RejectOrderAsync(DeliveryPlatform platform, string platformOrderId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an order as ready for pickup.
    /// </summary>
    Task<OrderActionResult> MarkReadyAsync(DeliveryPlatform platform, string platformOrderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an order on the platform.
    /// </summary>
    Task<OrderActionResult> CancelOrderAsync(DeliveryPlatform platform, string platformOrderId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the preparation time for an order.
    /// </summary>
    Task<OrderActionResult> UpdatePrepTimeAsync(DeliveryPlatform platform, string platformOrderId, int newPrepTimeMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets delivery tracking information for an order.
    /// </summary>
    Task<OrderTrackingDto?> GetTrackingAsync(DeliveryPlatform platform, string platformOrderId, CancellationToken cancellationToken = default);

    #endregion

    #region Menu Management

    /// <summary>
    /// Syncs menu items to the platform.
    /// </summary>
    Task<MenuSyncResult> SyncMenuAsync(DeliveryPlatform platform, Guid locationId, IEnumerable<MenuItemForSync> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates item availability on the platform.
    /// </summary>
    Task<bool> UpdateItemAvailabilityAsync(DeliveryPlatform platform, string platformItemId, bool isAvailable, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates item price on the platform.
    /// </summary>
    Task<bool> UpdateItemPriceAsync(DeliveryPlatform platform, string platformItemId, decimal price, CancellationToken cancellationToken = default);

    #endregion

    #region Store Management

    /// <summary>
    /// Sets the store's open/closed status on the platform.
    /// </summary>
    Task<bool> SetStoreStatusAsync(DeliveryPlatform platform, string platformStoreId, bool isOpen, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets busy mode on the platform (increased prep times, may reduce order volume).
    /// </summary>
    Task<bool> SetBusyModeAsync(DeliveryPlatform platform, string platformStoreId, bool isBusy, int? additionalPrepMinutes, CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
/// Result of a connection attempt.
/// </summary>
public record ConnectionResult(
    bool Success,
    string? ErrorMessage = null,
    string? MerchantId = null,
    Dictionary<string, string>? Metadata = null);

/// <summary>
/// Result of parsing a webhook.
/// </summary>
public record WebhookParseResult(
    bool Success,
    WebhookEventType EventType,
    ExternalOrder? Order = null,
    string? ErrorMessage = null,
    string? RawPayload = null);

/// <summary>
/// Types of webhook events from delivery platforms.
/// </summary>
public enum WebhookEventType
{
    Unknown,
    OrderCreated,
    OrderUpdated,
    OrderCancelled,
    OrderPickedUp,
    OrderDelivered,
    MenuItemUnavailable,
    StoreStatusChanged
}

/// <summary>
/// Result of an order action (accept, reject, etc.).
/// </summary>
public record OrderActionResult(
    bool Success,
    string? ErrorMessage = null,
    string? ErrorCode = null);

/// <summary>
/// Result of a menu sync operation.
/// </summary>
public record MenuSyncResult(
    bool Success,
    int ItemsSynced,
    int ItemsFailed,
    List<MenuSyncError>? Errors = null);

/// <summary>
/// Menu item data for syncing to a platform.
/// </summary>
public record MenuItemForSync(
    Guid InternalId,
    string Name,
    string? Description,
    decimal Price,
    string? CategoryName,
    string? ImageUrl,
    bool IsAvailable,
    List<MenuModifierForSync>? Modifiers);

/// <summary>
/// Modifier data for syncing to a platform.
/// </summary>
public record MenuModifierForSync(
    Guid InternalId,
    string Name,
    decimal Price,
    bool IsRequired,
    int? MinSelections,
    int? MaxSelections);
