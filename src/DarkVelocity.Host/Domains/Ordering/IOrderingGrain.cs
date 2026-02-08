namespace DarkVelocity.Host.Grains;

// ============================================================================
// Ordering Link Grain - Manages QR codes and kiosk ordering links
// ============================================================================

public enum OrderingLinkType
{
    TableQr,
    TakeOut,
    Kiosk
}

[GenerateSerializer]
public record CreateOrderingLinkCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] OrderingLinkType Type,
    [property: Id(3)] string Name,
    [property: Id(4)] Guid? TableId = null,
    [property: Id(5)] string? TableNumber = null);

[GenerateSerializer]
public record UpdateOrderingLinkCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] Guid? TableId = null,
    [property: Id(2)] string? TableNumber = null);

[GenerateSerializer]
public record OrderingLinkSnapshot(
    [property: Id(0)] Guid LinkId,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] OrderingLinkType Type,
    [property: Id(4)] string Name,
    [property: Id(5)] string ShortCode,
    [property: Id(6)] bool IsActive,
    [property: Id(7)] Guid? TableId,
    [property: Id(8)] string? TableNumber,
    [property: Id(9)] DateTime CreatedAt);

/// <summary>
/// Grain for managing ordering links (QR codes, kiosk URLs).
/// Key: "{orgId}:orderinglink:{linkId}"
/// </summary>
public interface IOrderingLinkGrain : IGrainWithStringKey
{
    Task<OrderingLinkSnapshot> CreateAsync(CreateOrderingLinkCommand command);
    Task<OrderingLinkSnapshot> UpdateAsync(UpdateOrderingLinkCommand command);
    Task DeactivateAsync();
    Task ActivateAsync();
    Task<OrderingLinkSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();
}

// ============================================================================
// Guest Session Grain - Manages anonymous guest ordering sessions
// ============================================================================

public enum GuestSessionStatus
{
    Active,
    Submitted,
    Completed,
    Abandoned
}

[GenerateSerializer]
public record StartGuestSessionCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid LinkId,
    [property: Id(3)] OrderingLinkType OrderingType,
    [property: Id(4)] Guid? TableId = null,
    [property: Id(5)] string? TableNumber = null);

[GenerateSerializer]
public record GuestCartItem(
    [property: Id(0)] Guid CartItemId,
    [property: Id(1)] Guid MenuItemId,
    [property: Id(2)] string Name,
    [property: Id(3)] int Quantity,
    [property: Id(4)] decimal UnitPrice,
    [property: Id(5)] string? Notes = null,
    [property: Id(6)] List<GuestCartModifier>? Modifiers = null);

[GenerateSerializer]
public record GuestCartModifier(
    [property: Id(0)] Guid ModifierId,
    [property: Id(1)] string Name,
    [property: Id(2)] decimal PriceAdjustment);

[GenerateSerializer]
public record AddToCartCommand(
    [property: Id(0)] Guid MenuItemId,
    [property: Id(1)] string Name,
    [property: Id(2)] int Quantity,
    [property: Id(3)] decimal UnitPrice,
    [property: Id(4)] string? Notes = null,
    [property: Id(5)] List<GuestCartModifier>? Modifiers = null);

[GenerateSerializer]
public record UpdateCartItemCommand(
    [property: Id(0)] Guid CartItemId,
    [property: Id(1)] int? Quantity = null,
    [property: Id(2)] string? Notes = null);

[GenerateSerializer]
public record SubmitGuestOrderCommand(
    [property: Id(0)] string? GuestName = null,
    [property: Id(1)] string? GuestPhone = null);

[GenerateSerializer]
public record GuestOrderResult(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] string OrderNumber,
    [property: Id(2)] DateTime SubmittedAt);

[GenerateSerializer]
public record GuestSessionSnapshot(
    [property: Id(0)] Guid SessionId,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] Guid LinkId,
    [property: Id(4)] GuestSessionStatus Status,
    [property: Id(5)] IReadOnlyList<GuestCartItem> CartItems,
    [property: Id(6)] decimal CartTotal,
    [property: Id(7)] Guid? OrderId,
    [property: Id(8)] string? OrderNumber,
    [property: Id(9)] string? GuestName,
    [property: Id(10)] string? GuestPhone,
    [property: Id(11)] OrderingLinkType OrderingType,
    [property: Id(12)] Guid? TableId,
    [property: Id(13)] string? TableNumber,
    [property: Id(14)] DateTime CreatedAt);

/// <summary>
/// Grain for managing a guest's ordering session (cart to order lifecycle).
/// Key: "{orgId}:{siteId}:guestsession:{sessionId}"
/// </summary>
public interface IGuestSessionGrain : IGrainWithStringKey
{
    Task<GuestSessionSnapshot> StartAsync(StartGuestSessionCommand command);
    Task<GuestSessionSnapshot> AddToCartAsync(AddToCartCommand command);
    Task<GuestSessionSnapshot> UpdateCartItemAsync(UpdateCartItemCommand command);
    Task<GuestSessionSnapshot> RemoveFromCartAsync(Guid cartItemId);
    Task<GuestOrderResult> SubmitOrderAsync(SubmitGuestOrderCommand command);
    Task<GuestSessionSnapshot> GetSnapshotAsync();
    Task<GuestSessionStatus> GetStatusAsync();
}

// ============================================================================
// Ordering Link Registry - Tracks all links for a site + short code lookup
// ============================================================================

[GenerateSerializer]
public record OrderingLinkSummary(
    [property: Id(0)] Guid LinkId,
    [property: Id(1)] string Name,
    [property: Id(2)] OrderingLinkType Type,
    [property: Id(3)] string ShortCode,
    [property: Id(4)] bool IsActive,
    [property: Id(5)] Guid? TableId,
    [property: Id(6)] string? TableNumber);

/// <summary>
/// Registry grain for listing and looking up ordering links by short code.
/// Key: "{orgId}:{siteId}:orderinglinkregistry"
/// </summary>
public interface IOrderingLinkRegistryGrain : IGrainWithStringKey
{
    Task RegisterLinkAsync(OrderingLinkSummary summary);
    Task UpdateLinkAsync(Guid linkId, string? name, bool? isActive);
    Task<IReadOnlyList<OrderingLinkSummary>> GetLinksAsync(bool includeInactive = false);
    Task<OrderingLinkSummary?> FindByShortCodeAsync(string shortCode);
}
