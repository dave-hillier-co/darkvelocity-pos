namespace DarkVelocity.Orleans.Abstractions.Grains;

// ============================================================================
// Delivery Platform Grain
// ============================================================================

public enum DeliveryPlatformType
{
    UberEats,
    DoorDash,
    Deliveroo,
    JustEat,
    Wolt,
    GrubHub,
    Postmates,
    Custom
}

public enum DeliveryPlatformStatus
{
    Active,
    Paused,
    Disconnected,
    Error
}

public record ConnectDeliveryPlatformCommand(
    DeliveryPlatformType PlatformType,
    string Name,
    string? ApiCredentialsEncrypted,
    string? WebhookSecret,
    string? MerchantId,
    string? Settings);

public record UpdateDeliveryPlatformCommand(
    string? Name,
    DeliveryPlatformStatus? Status,
    string? ApiCredentialsEncrypted,
    string? WebhookSecret,
    string? Settings);

public record PlatformLocationMapping(
    Guid LocationId,
    string PlatformStoreId,
    bool IsActive,
    string? OperatingHoursOverride);

public record DeliveryPlatformSnapshot(
    Guid DeliveryPlatformId,
    DeliveryPlatformType PlatformType,
    string Name,
    DeliveryPlatformStatus Status,
    string? MerchantId,
    DateTime? ConnectedAt,
    DateTime? LastSyncAt,
    DateTime? LastOrderAt,
    IReadOnlyList<PlatformLocationMapping> Locations,
    int TotalOrdersToday,
    decimal TotalRevenueToday);

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
    Pickup
}

public record ExternalOrderCustomer(
    string Name,
    string? Phone,
    string? DeliveryAddress);

public record ExternalOrderItem(
    string PlatformItemId,
    Guid? InternalMenuItemId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string? SpecialInstructions,
    IReadOnlyList<ExternalOrderModifier>? Modifiers);

public record ExternalOrderModifier(
    string Name,
    decimal Price);

public record CreateExternalOrderCommand(
    Guid LocationId,
    Guid DeliveryPlatformId,
    string PlatformOrderId,
    string PlatformOrderNumber,
    ExternalOrderType OrderType,
    DateTime PlacedAt,
    ExternalOrderCustomer Customer,
    IReadOnlyList<ExternalOrderItem> Items,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal ServiceFee,
    decimal Tax,
    decimal Tip,
    decimal Total,
    string Currency,
    string? SpecialInstructions,
    string? PlatformRawPayload);

public record ExternalOrderSnapshot(
    Guid ExternalOrderId,
    Guid LocationId,
    Guid DeliveryPlatformId,
    string PlatformOrderId,
    string PlatformOrderNumber,
    Guid? InternalOrderId,
    ExternalOrderStatus Status,
    ExternalOrderType OrderType,
    DateTime PlacedAt,
    DateTime? AcceptedAt,
    DateTime? EstimatedPickupAt,
    DateTime? ActualPickupAt,
    ExternalOrderCustomer Customer,
    IReadOnlyList<ExternalOrderItem> Items,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal ServiceFee,
    decimal Tax,
    decimal Tip,
    decimal Total,
    string Currency,
    string? SpecialInstructions,
    string? ErrorMessage,
    int RetryCount);

/// <summary>
/// Grain for external order management.
/// Key: "{orgId}:externalorder:{externalOrderId}"
/// </summary>
public interface IExternalOrderGrain : IGrainWithStringKey
{
    Task<ExternalOrderSnapshot> CreateAsync(CreateExternalOrderCommand command);
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

public record StartMenuSyncCommand(
    Guid DeliveryPlatformId,
    Guid? LocationId);

public record MenuItemMappingRecord(
    Guid InternalMenuItemId,
    string PlatformItemId,
    string? PlatformCategoryId,
    decimal? PriceOverride,
    bool IsAvailable);

public record MenuSyncSnapshot(
    Guid MenuSyncId,
    Guid DeliveryPlatformId,
    Guid? LocationId,
    MenuSyncStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    int ItemsSynced,
    int ItemsFailed,
    IReadOnlyList<string> Errors);

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

public record CreatePayoutCommand(
    Guid DeliveryPlatformId,
    Guid LocationId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal GrossAmount,
    decimal PlatformFees,
    decimal NetAmount,
    string Currency,
    string? PayoutReference);

public record PayoutSnapshot(
    Guid PayoutId,
    Guid DeliveryPlatformId,
    Guid LocationId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal GrossAmount,
    decimal PlatformFees,
    decimal NetAmount,
    string Currency,
    PayoutStatus Status,
    string? PayoutReference,
    DateTime? ProcessedAt);

/// <summary>
/// Grain for platform payout management.
/// Key: "{orgId}:payout:{payoutId}"
/// </summary>
public interface IPlatformPayoutGrain : IGrainWithStringKey
{
    Task<PayoutSnapshot> CreateAsync(CreatePayoutCommand command);
    Task SetProcessingAsync();
    Task CompleteAsync(DateTime processedAt);
    Task FailAsync(string reason);
    Task<PayoutSnapshot> GetSnapshotAsync();
}
