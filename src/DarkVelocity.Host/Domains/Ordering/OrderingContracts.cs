namespace DarkVelocity.Host.Contracts;

// ============================================================================
// Admin Ordering Link Requests
// ============================================================================

public record CreateOrderingLinkRequest(
    string Name,
    string Type,
    Guid? TableId = null,
    string? TableNumber = null);

public record UpdateOrderingLinkRequest(
    string? Name = null,
    Guid? TableId = null,
    string? TableNumber = null);

// ============================================================================
// Public Guest Ordering Requests
// ============================================================================

public record StartSessionRequest(
    string? GuestName = null);

public record AddToCartRequest(
    Guid MenuItemId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    string? Notes = null,
    List<CartModifierRequest>? Modifiers = null);

public record CartModifierRequest(
    Guid ModifierId,
    string Name,
    decimal PriceAdjustment);

public record UpdateCartItemRequest(
    int? Quantity = null,
    string? Notes = null);

public record SubmitOrderRequest(
    string? GuestName = null,
    string? GuestPhone = null);
