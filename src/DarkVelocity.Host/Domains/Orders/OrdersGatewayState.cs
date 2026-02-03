using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

// ============================================================================
// Delivery Platform State
// ============================================================================

[GenerateSerializer]
public sealed class DeliveryPlatformState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid DeliveryPlatformId { get; set; }
    [Id(2)] public DeliveryPlatformType PlatformType { get; set; }
    [Id(17)] public IntegrationType IntegrationType { get; set; }
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public DeliveryPlatformStatus Status { get; set; }
    [Id(5)] public string? ApiCredentialsEncrypted { get; set; }
    [Id(6)] public string? WebhookSecret { get; set; }
    [Id(7)] public string? MerchantId { get; set; }
    [Id(8)] public string? Settings { get; set; }
    [Id(9)] public DateTime? ConnectedAt { get; set; }
    [Id(10)] public DateTime? LastSyncAt { get; set; }
    [Id(11)] public DateTime? LastOrderAt { get; set; }
    [Id(12)] public List<PlatformLocationState> Locations { get; set; } = [];
    [Id(13)] public int TotalOrdersToday { get; set; }
    [Id(14)] public decimal TotalRevenueToday { get; set; }
    [Id(15)] public DateTime TodayDate { get; set; }
    [Id(16)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class PlatformLocationState
{
    [Id(0)] public Guid LocationId { get; set; }
    [Id(1)] public string PlatformStoreId { get; set; } = string.Empty;
    [Id(2)] public bool IsActive { get; set; }
    [Id(3)] public string? OperatingHoursOverride { get; set; }
}

// ============================================================================
// External Order State
// ============================================================================

[GenerateSerializer]
public sealed class ExternalOrderState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid ExternalOrderId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public Guid DeliveryPlatformId { get; set; }
    [Id(4)] public string PlatformOrderId { get; set; } = string.Empty;
    [Id(5)] public string PlatformOrderNumber { get; set; } = string.Empty;
    [Id(6)] public string? ChannelDisplayId { get; set; }
    [Id(7)] public Guid? InternalOrderId { get; set; }
    [Id(8)] public ExternalOrderStatus Status { get; set; }
    [Id(9)] public ExternalOrderType OrderType { get; set; }
    [Id(10)] public DateTime PlacedAt { get; set; }
    [Id(11)] public DateTime? ScheduledPickupAt { get; set; }
    [Id(12)] public DateTime? ScheduledDeliveryAt { get; set; }
    [Id(13)] public bool IsAsapDelivery { get; set; }
    [Id(14)] public DateTime? AcceptedAt { get; set; }
    [Id(15)] public DateTime? EstimatedPickupAt { get; set; }
    [Id(16)] public DateTime? ActualPickupAt { get; set; }
    [Id(17)] public ExternalOrderCustomerState Customer { get; set; } = new();
    [Id(18)] public CourierInfoState? Courier { get; set; }
    [Id(19)] public List<ExternalOrderItemState> Items { get; set; } = [];
    [Id(20)] public decimal Subtotal { get; set; }
    [Id(21)] public decimal DeliveryFee { get; set; }
    [Id(22)] public decimal ServiceFee { get; set; }
    [Id(23)] public decimal Tax { get; set; }
    [Id(24)] public decimal Tip { get; set; }
    [Id(25)] public decimal Total { get; set; }
    [Id(26)] public string Currency { get; set; } = "EUR";
    [Id(27)] public List<ExternalOrderDiscountState> Discounts { get; set; } = [];
    [Id(28)] public PackagingPreferencesState? Packaging { get; set; }
    [Id(29)] public string? SpecialInstructions { get; set; }
    [Id(30)] public string? PlatformRawPayload { get; set; }
    [Id(31)] public string? ErrorMessage { get; set; }
    [Id(32)] public int RetryCount { get; set; }
    [Id(33)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class ExternalOrderCustomerState
{
    [Id(0)] public string Name { get; set; } = string.Empty;
    [Id(1)] public string? Phone { get; set; }
    [Id(2)] public string? Email { get; set; }
    [Id(3)] public DeliveryAddressState? DeliveryAddress { get; set; }
}

[GenerateSerializer]
public sealed class DeliveryAddressState
{
    [Id(0)] public string Street { get; set; } = string.Empty;
    [Id(1)] public string? PostalCode { get; set; }
    [Id(2)] public string City { get; set; } = string.Empty;
    [Id(3)] public string Country { get; set; } = string.Empty;
    [Id(4)] public string? ExtraAddressInfo { get; set; }
}

[GenerateSerializer]
public sealed class CourierInfoState
{
    [Id(0)] public string? FirstName { get; set; }
    [Id(1)] public string? LastName { get; set; }
    [Id(2)] public string? PhoneNumber { get; set; }
    [Id(3)] public string? Provider { get; set; }
    [Id(4)] public int? Status { get; set; }
}

[GenerateSerializer]
public sealed class ExternalOrderDiscountState
{
    [Id(0)] public string Type { get; set; } = string.Empty;
    [Id(1)] public DiscountProvider Provider { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public decimal Amount { get; set; }
}

[GenerateSerializer]
public sealed class PackagingPreferencesState
{
    [Id(0)] public bool IncludeCutlery { get; set; }
    [Id(1)] public bool IsReusable { get; set; }
    [Id(2)] public decimal? BagFee { get; set; }
}

[GenerateSerializer]
public sealed class ExternalOrderItemState
{
    [Id(0)] public string PlatformItemId { get; set; } = string.Empty;
    [Id(1)] public Guid? InternalMenuItemId { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public int Quantity { get; set; }
    [Id(4)] public decimal UnitPrice { get; set; }
    [Id(5)] public decimal TotalPrice { get; set; }
    [Id(6)] public string? SpecialInstructions { get; set; }
    [Id(7)] public List<ExternalOrderModifierState> Modifiers { get; set; } = [];
}

[GenerateSerializer]
public sealed class ExternalOrderModifierState
{
    [Id(0)] public string Name { get; set; } = string.Empty;
    [Id(1)] public decimal Price { get; set; }
}

// ============================================================================
// Menu Sync State
// ============================================================================

[GenerateSerializer]
public sealed class MenuSyncState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid MenuSyncId { get; set; }
    [Id(2)] public Guid DeliveryPlatformId { get; set; }
    [Id(3)] public Guid? LocationId { get; set; }
    [Id(4)] public MenuSyncStatus Status { get; set; }
    [Id(5)] public DateTime StartedAt { get; set; }
    [Id(6)] public DateTime? CompletedAt { get; set; }
    [Id(7)] public int ItemsSynced { get; set; }
    [Id(8)] public int ItemsFailed { get; set; }
    [Id(9)] public List<string> Errors { get; set; } = [];
    [Id(10)] public List<MenuItemMappingState> Mappings { get; set; } = [];
    [Id(11)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class MenuItemMappingState
{
    [Id(0)] public Guid InternalMenuItemId { get; set; }
    [Id(1)] public string PlatformItemId { get; set; } = string.Empty;
    [Id(2)] public string? PlatformCategoryId { get; set; }
    [Id(3)] public decimal? PriceOverride { get; set; }
    [Id(4)] public bool IsAvailable { get; set; }
}

// ============================================================================
// Platform Payout State
// ============================================================================

[GenerateSerializer]
public sealed class PlatformPayoutState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid PayoutId { get; set; }
    [Id(2)] public Guid DeliveryPlatformId { get; set; }
    [Id(3)] public Guid LocationId { get; set; }
    [Id(4)] public DateTime PeriodStart { get; set; }
    [Id(5)] public DateTime PeriodEnd { get; set; }
    [Id(6)] public decimal GrossAmount { get; set; }
    [Id(7)] public decimal PlatformFees { get; set; }
    [Id(8)] public decimal NetAmount { get; set; }
    [Id(9)] public string Currency { get; set; } = "EUR";
    [Id(10)] public PayoutStatus Status { get; set; }
    [Id(11)] public string? PayoutReference { get; set; }
    [Id(12)] public DateTime? ProcessedAt { get; set; }
    [Id(13)] public int Version { get; set; }
}
