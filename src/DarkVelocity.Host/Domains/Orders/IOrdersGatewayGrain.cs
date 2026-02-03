namespace DarkVelocity.Host.Grains;

// ============================================================================
// Channel Integration Types
// ============================================================================

/// <summary>
/// Defines how a channel integrates with the POS system.
/// </summary>
public enum IntegrationType
{
    /// <summary>
    /// Direct API integration with delivery platforms (UberEats, Deliveroo, JustEat).
    /// We implement their specific API contracts.
    /// </summary>
    Direct,

    /// <summary>
    /// Integration through an aggregator service (Deliverect, Otter).
    /// Aggregator normalizes multiple platform APIs into a single interface.
    /// </summary>
    Aggregator,

    /// <summary>
    /// Internal channels (local website, kiosk, phone orders).
    /// We control the full stack with no external dependencies.
    /// </summary>
    Internal
}

// ============================================================================
// Delivery Platform Grain
// ============================================================================

public enum DeliveryPlatformType
{
    // Direct integrations
    UberEats,
    DoorDash,
    Deliveroo,
    JustEat,
    Wolt,
    GrubHub,
    Postmates,

    // Aggregator platforms
    Deliverect,
    Otter,

    // Internal channels
    LocalWebsite,
    Kiosk,
    PhoneOrder,

    Custom
}

public enum DeliveryPlatformStatus
{
    Active,
    Paused,
    Disconnected,
    Error
}

[GenerateSerializer]
public record ConnectDeliveryPlatformCommand(
    [property: Id(0)] DeliveryPlatformType PlatformType,
    [property: Id(1)] IntegrationType IntegrationType,
    [property: Id(2)] string Name,
    [property: Id(3)] string? ApiCredentialsEncrypted,
    [property: Id(4)] string? WebhookSecret,
    [property: Id(5)] string? MerchantId,
    [property: Id(6)] string? Settings);

[GenerateSerializer]
public record UpdateDeliveryPlatformCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] DeliveryPlatformStatus? Status,
    [property: Id(2)] string? ApiCredentialsEncrypted,
    [property: Id(3)] string? WebhookSecret,
    [property: Id(4)] string? Settings);

[GenerateSerializer]
public record PlatformLocationMapping(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] string PlatformStoreId,
    [property: Id(2)] bool IsActive,
    [property: Id(3)] string? OperatingHoursOverride);

[GenerateSerializer]
public record DeliveryPlatformSnapshot(
    [property: Id(0)] Guid DeliveryPlatformId,
    [property: Id(1)] DeliveryPlatformType PlatformType,
    [property: Id(2)] IntegrationType IntegrationType,
    [property: Id(3)] string Name,
    [property: Id(4)] DeliveryPlatformStatus Status,
    [property: Id(5)] string? MerchantId,
    [property: Id(6)] DateTime? ConnectedAt,
    [property: Id(7)] DateTime? LastSyncAt,
    [property: Id(8)] DateTime? LastOrderAt,
    [property: Id(9)] IReadOnlyList<PlatformLocationMapping> Locations,
    [property: Id(10)] int TotalOrdersToday,
    [property: Id(11)] decimal TotalRevenueToday);

/// <summary>
/// Grain for delivery platform management.
/// Key: "{orgId}:deliveryplatform:{platformId}"
/// </summary>
public interface IDeliveryPlatformGrain : IGrainWithStringKey
{
    Task<DeliveryPlatformSnapshot> ConnectAsync(ConnectDeliveryPlatformCommand command);
    Task<DeliveryPlatformSnapshot> UpdateAsync(UpdateDeliveryPlatformCommand command);
    Task DisconnectAsync();
    Task PauseAsync();
    Task ResumeAsync();
    Task AddLocationMappingAsync(PlatformLocationMapping mapping);
    Task RemoveLocationMappingAsync(Guid locationId);
    Task<DeliveryPlatformSnapshot> GetSnapshotAsync();
    Task RecordOrderAsync(decimal orderTotal);
    Task RecordSyncAsync();
}

// ============================================================================
// External Order Grain
// ============================================================================

public enum ExternalOrderStatus
{
    Pending,
    Accepted,
    Rejected,
    Preparing,
    Ready,
    PickedUp,
    Delivered,
    Cancelled,
    Failed
}

public enum ExternalOrderType
{
    Delivery,
    Pickup,
    DineIn
}

[GenerateSerializer]
public record DeliveryAddress(
    [property: Id(0)] string Street,
    [property: Id(1)] string? PostalCode,
    [property: Id(2)] string City,
    [property: Id(3)] string Country,
    [property: Id(4)] string? ExtraAddressInfo);

[GenerateSerializer]
public record ExternalOrderCustomer(
    [property: Id(0)] string Name,
    [property: Id(1)] string? Phone,
    [property: Id(2)] string? Email,
    [property: Id(3)] DeliveryAddress? DeliveryAddress);

[GenerateSerializer]
public record CourierInfo(
    [property: Id(0)] string? FirstName,
    [property: Id(1)] string? LastName,
    [property: Id(2)] string? PhoneNumber,
    [property: Id(3)] string? Provider,
    [property: Id(4)] int? Status);

public enum DiscountProvider
{
    Restaurant,
    Channel
}

[GenerateSerializer]
public record ExternalOrderDiscount(
    [property: Id(0)] string Type,
    [property: Id(1)] DiscountProvider Provider,
    [property: Id(2)] string Name,
    [property: Id(3)] decimal Amount);

[GenerateSerializer]
public record PackagingPreferences(
    [property: Id(0)] bool IncludeCutlery,
    [property: Id(1)] bool IsReusable,
    [property: Id(2)] decimal? BagFee);

[GenerateSerializer]
public record ExternalOrderItem(
    [property: Id(0)] string PlatformItemId,
    [property: Id(1)] Guid? InternalMenuItemId,
    [property: Id(2)] string Name,
    [property: Id(3)] int Quantity,
    [property: Id(4)] decimal UnitPrice,
    [property: Id(5)] decimal TotalPrice,
    [property: Id(6)] string? SpecialInstructions,
    [property: Id(7)] IReadOnlyList<ExternalOrderModifier>? Modifiers);

[GenerateSerializer]
public record ExternalOrderModifier(
    [property: Id(0)] string Name,
    [property: Id(1)] decimal Price);

/// <summary>
/// Represents an order received from an external delivery platform.
/// This is an observed fact, not a command - the order already exists on the platform.
/// </summary>
[GenerateSerializer]
public record ExternalOrderReceived(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] Guid DeliveryPlatformId,
    [property: Id(2)] string PlatformOrderId,
    [property: Id(3)] string PlatformOrderNumber,
    [property: Id(4)] string? ChannelDisplayId,
    [property: Id(5)] ExternalOrderType OrderType,
    [property: Id(6)] DateTime PlacedAt,
    [property: Id(7)] DateTime? ScheduledPickupAt,
    [property: Id(8)] DateTime? ScheduledDeliveryAt,
    [property: Id(9)] bool IsAsapDelivery,
    [property: Id(10)] ExternalOrderCustomer Customer,
    [property: Id(11)] CourierInfo? Courier,
    [property: Id(12)] IReadOnlyList<ExternalOrderItem> Items,
    [property: Id(13)] decimal Subtotal,
    [property: Id(14)] decimal DeliveryFee,
    [property: Id(15)] decimal ServiceFee,
    [property: Id(16)] decimal Tax,
    [property: Id(17)] decimal Tip,
    [property: Id(18)] decimal Total,
    [property: Id(19)] string Currency,
    [property: Id(20)] IReadOnlyList<ExternalOrderDiscount>? Discounts,
    [property: Id(21)] PackagingPreferences? Packaging,
    [property: Id(22)] string? SpecialInstructions,
    [property: Id(23)] string? PlatformRawPayload);

[GenerateSerializer]
public record ExternalOrderSnapshot(
    [property: Id(0)] Guid ExternalOrderId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] Guid DeliveryPlatformId,
    [property: Id(3)] string PlatformOrderId,
    [property: Id(4)] string PlatformOrderNumber,
    [property: Id(5)] string? ChannelDisplayId,
    [property: Id(6)] Guid? InternalOrderId,
    [property: Id(7)] ExternalOrderStatus Status,
    [property: Id(8)] ExternalOrderType OrderType,
    [property: Id(9)] DateTime PlacedAt,
    [property: Id(10)] DateTime? ScheduledPickupAt,
    [property: Id(11)] DateTime? ScheduledDeliveryAt,
    [property: Id(12)] bool IsAsapDelivery,
    [property: Id(13)] DateTime? AcceptedAt,
    [property: Id(14)] DateTime? EstimatedPickupAt,
    [property: Id(15)] DateTime? ActualPickupAt,
    [property: Id(16)] ExternalOrderCustomer Customer,
    [property: Id(17)] CourierInfo? Courier,
    [property: Id(18)] IReadOnlyList<ExternalOrderItem> Items,
    [property: Id(19)] decimal Subtotal,
    [property: Id(20)] decimal DeliveryFee,
    [property: Id(21)] decimal ServiceFee,
    [property: Id(22)] decimal Tax,
    [property: Id(23)] decimal Tip,
    [property: Id(24)] decimal Total,
    [property: Id(25)] string Currency,
    [property: Id(26)] IReadOnlyList<ExternalOrderDiscount>? Discounts,
    [property: Id(27)] PackagingPreferences? Packaging,
    [property: Id(28)] string? SpecialInstructions,
    [property: Id(29)] string? ErrorMessage,
    [property: Id(30)] int RetryCount);

/// <summary>
/// Grain for external order management.
/// Key: "{orgId}:externalorder:{externalOrderId}"
/// </summary>
public interface IExternalOrderGrain : IGrainWithStringKey
{
    /// <summary>
    /// Records an order received from an external delivery platform.
    /// </summary>
    Task<ExternalOrderSnapshot> ReceiveAsync(ExternalOrderReceived order);
    Task<ExternalOrderSnapshot> AcceptAsync(DateTime? estimatedPickupAt);
    Task<ExternalOrderSnapshot> RejectAsync(string reason);
    Task SetPreparingAsync();
    Task SetReadyAsync();
    Task SetPickedUpAsync();
    Task SetDeliveredAsync();
    Task CancelAsync(string reason);
    Task LinkInternalOrderAsync(Guid internalOrderId);
    Task<ExternalOrderSnapshot> GetSnapshotAsync();
    Task MarkFailedAsync(string errorMessage);
    Task IncrementRetryAsync();

    /// <summary>
    /// Updates courier information received from the delivery platform.
    /// </summary>
    Task UpdateCourierAsync(CourierInfo courier);
}

// ============================================================================
// Menu Sync Grain
// ============================================================================

public enum MenuSyncStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

[GenerateSerializer]
public record StartMenuSyncCommand(
    [property: Id(0)] Guid DeliveryPlatformId,
    [property: Id(1)] Guid? LocationId);

[GenerateSerializer]
public record MenuItemMappingRecord(
    [property: Id(0)] Guid InternalMenuItemId,
    [property: Id(1)] string PlatformItemId,
    [property: Id(2)] string? PlatformCategoryId,
    [property: Id(3)] decimal? PriceOverride,
    [property: Id(4)] bool IsAvailable);

[GenerateSerializer]
public record MenuSyncSnapshot(
    [property: Id(0)] Guid MenuSyncId,
    [property: Id(1)] Guid DeliveryPlatformId,
    [property: Id(2)] Guid? LocationId,
    [property: Id(3)] MenuSyncStatus Status,
    [property: Id(4)] DateTime StartedAt,
    [property: Id(5)] DateTime? CompletedAt,
    [property: Id(6)] int ItemsSynced,
    [property: Id(7)] int ItemsFailed,
    [property: Id(8)] IReadOnlyList<string> Errors);

/// <summary>
/// Grain for menu sync management.
/// Key: "{orgId}:menusync:{syncId}"
/// </summary>
public interface IMenuSyncGrain : IGrainWithStringKey
{
    Task<MenuSyncSnapshot> StartAsync(StartMenuSyncCommand command);
    Task RecordItemSyncedAsync(MenuItemMappingRecord mapping);
    Task RecordItemFailedAsync(Guid menuItemId, string error);
    Task CompleteAsync();
    Task FailAsync(string error);
    Task<MenuSyncSnapshot> GetSnapshotAsync();
}

// ============================================================================
// Platform Payout Grain
// ============================================================================

public enum PayoutStatus
{
    Pending,
    Processing,
    Completed,
    Failed
}

/// <summary>
/// Represents a payout received/reported from an external delivery platform.
/// This is an observed fact - the payout was initiated by the platform.
/// </summary>
[GenerateSerializer]
public record PayoutReceived(
    [property: Id(0)] Guid DeliveryPlatformId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] DateTime PeriodStart,
    [property: Id(3)] DateTime PeriodEnd,
    [property: Id(4)] decimal GrossAmount,
    [property: Id(5)] decimal PlatformFees,
    [property: Id(6)] decimal NetAmount,
    [property: Id(7)] string Currency,
    [property: Id(8)] string? PayoutReference);

[GenerateSerializer]
public record PayoutSnapshot(
    [property: Id(0)] Guid PayoutId,
    [property: Id(1)] Guid DeliveryPlatformId,
    [property: Id(2)] Guid LocationId,
    [property: Id(3)] DateTime PeriodStart,
    [property: Id(4)] DateTime PeriodEnd,
    [property: Id(5)] decimal GrossAmount,
    [property: Id(6)] decimal PlatformFees,
    [property: Id(7)] decimal NetAmount,
    [property: Id(8)] string Currency,
    [property: Id(9)] PayoutStatus Status,
    [property: Id(10)] string? PayoutReference,
    [property: Id(11)] DateTime? ProcessedAt);

/// <summary>
/// Grain for platform payout management.
/// Key: "{orgId}:payout:{payoutId}"
/// </summary>
public interface IPlatformPayoutGrain : IGrainWithStringKey
{
    /// <summary>
    /// Records a payout received from an external delivery platform.
    /// </summary>
    Task<PayoutSnapshot> ReceiveAsync(PayoutReceived payout);
    Task SetProcessingAsync();
    Task CompleteAsync(DateTime processedAt);
    Task FailAsync(string reason);
    Task<PayoutSnapshot> GetSnapshotAsync();
}
