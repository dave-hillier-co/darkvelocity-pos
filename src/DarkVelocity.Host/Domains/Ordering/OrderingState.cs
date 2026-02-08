using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

// ============================================================================
// Ordering Link State
// ============================================================================

[GenerateSerializer]
public sealed class OrderingLinkState
{
    [Id(0)] public Guid LinkId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public OrderingLinkType Type { get; set; }
    [Id(4)] public string Name { get; set; } = string.Empty;
    [Id(5)] public string ShortCode { get; set; } = string.Empty;
    [Id(6)] public bool IsActive { get; set; } = true;
    [Id(7)] public Guid? TableId { get; set; }
    [Id(8)] public string? TableNumber { get; set; }
    [Id(9)] public DateTime CreatedAt { get; set; }
    [Id(10)] public int Version { get; set; }
}

// ============================================================================
// Guest Session State
// ============================================================================

[GenerateSerializer]
public sealed class GuestSessionState
{
    [Id(0)] public Guid SessionId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public Guid LinkId { get; set; }
    [Id(4)] public GuestSessionStatus Status { get; set; }
    [Id(5)] public List<GuestCartItemState> CartItems { get; set; } = [];
    [Id(6)] public Guid? OrderId { get; set; }
    [Id(7)] public string? OrderNumber { get; set; }
    [Id(8)] public string? GuestName { get; set; }
    [Id(9)] public string? GuestPhone { get; set; }
    [Id(10)] public OrderingLinkType OrderingType { get; set; }
    [Id(11)] public Guid? TableId { get; set; }
    [Id(12)] public string? TableNumber { get; set; }
    [Id(13)] public DateTime CreatedAt { get; set; }
    [Id(14)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class GuestCartItemState
{
    [Id(0)] public Guid CartItemId { get; set; }
    [Id(1)] public Guid MenuItemId { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public int Quantity { get; set; }
    [Id(4)] public decimal UnitPrice { get; set; }
    [Id(5)] public string? Notes { get; set; }
    [Id(6)] public List<GuestCartModifierState> Modifiers { get; set; } = [];
}

[GenerateSerializer]
public sealed class GuestCartModifierState
{
    [Id(0)] public Guid ModifierId { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public decimal PriceAdjustment { get; set; }
}

// ============================================================================
// Ordering Link Registry State
// ============================================================================

[GenerateSerializer]
public sealed class OrderingLinkRegistryState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public bool IsCreated { get; set; }
    [Id(3)] public List<OrderingLinkRegistryEntry> Links { get; set; } = [];
}

[GenerateSerializer]
public sealed class OrderingLinkRegistryEntry
{
    [Id(0)] public Guid LinkId { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public OrderingLinkType Type { get; set; }
    [Id(3)] public string ShortCode { get; set; } = string.Empty;
    [Id(4)] public bool IsActive { get; set; } = true;
    [Id(5)] public Guid? TableId { get; set; }
    [Id(6)] public string? TableNumber { get; set; }
}
