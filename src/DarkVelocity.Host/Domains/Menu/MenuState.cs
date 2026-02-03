using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

// Note: PricingType is defined in DarkVelocity.Host.Grains.IMenuGrain

// ============================================================================
// Menu Category State
// ============================================================================

[GenerateSerializer]
public sealed class MenuCategoryState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid CategoryId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public string? Description { get; set; }
    [Id(5)] public int DisplayOrder { get; set; }
    [Id(6)] public string? Color { get; set; }
    [Id(7)] public bool IsActive { get; set; } = true;
    [Id(8)] public int ItemCount { get; set; }
    [Id(9)] public int Version { get; set; }
}

// ============================================================================
// Menu Item State
// ============================================================================

[GenerateSerializer]
public sealed class MenuItemState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid MenuItemId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public Guid CategoryId { get; set; }
    [Id(4)] public string CategoryName { get; set; } = string.Empty;
    [Id(5)] public Guid? AccountingGroupId { get; set; }
    [Id(6)] public Guid? RecipeId { get; set; }
    [Id(7)] public string Name { get; set; } = string.Empty;
    [Id(8)] public string? Description { get; set; }
    [Id(9)] public decimal Price { get; set; }
    [Id(10)] public string? ImageUrl { get; set; }
    [Id(11)] public string? Sku { get; set; }
    [Id(12)] public bool IsActive { get; set; } = true;
    [Id(13)] public bool TrackInventory { get; set; }
    [Id(14)] public decimal? TheoreticalCost { get; set; }
    [Id(15)] public List<MenuItemModifierState> Modifiers { get; set; } = [];
    [Id(16)] public int Version { get; set; }
    [Id(17)] public bool IsSnoozed { get; set; }
    [Id(18)] public DateTime? SnoozedUntil { get; set; }
    [Id(19)] public List<ProductTagState> ProductTags { get; set; } = [];
    [Id(20)] public ContextualTaxRatesState? TaxRates { get; set; }
    [Id(21)] public List<MenuItemVariationState> Variations { get; set; } = [];
}

[GenerateSerializer]
public sealed class ProductTagState
{
    [Id(0)] public int TagId { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
}

[GenerateSerializer]
public sealed class ContextualTaxRatesState
{
    [Id(0)] public decimal DeliveryTaxPercent { get; set; }
    [Id(1)] public decimal TakeawayTaxPercent { get; set; }
    [Id(2)] public decimal DineInTaxPercent { get; set; }
}

[GenerateSerializer]
public sealed class MenuItemVariationState
{
    [Id(0)] public Guid VariationId { get; set; }
    [Id(1)] public Guid MenuItemId { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public PricingType PricingType { get; set; }
    [Id(4)] public decimal? Price { get; set; }
    [Id(5)] public string? Sku { get; set; }
    [Id(6)] public int DisplayOrder { get; set; }
    [Id(7)] public bool IsActive { get; set; } = true;
    [Id(8)] public bool TrackInventory { get; set; }
    [Id(9)] public Guid? InventoryItemId { get; set; }
    [Id(10)] public decimal? InventoryQuantityPerSale { get; set; }
    [Id(11)] public decimal? TheoreticalCost { get; set; }
}

[GenerateSerializer]
public sealed class MenuItemModifierState
{
    [Id(0)] public Guid ModifierId { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public decimal PriceAdjustment { get; set; }
    [Id(3)] public bool IsRequired { get; set; }
    [Id(4)] public int MinSelections { get; set; }
    [Id(5)] public int MaxSelections { get; set; }
    [Id(6)] public List<MenuItemModifierOptionState> Options { get; set; } = [];
}

[GenerateSerializer]
public sealed class MenuItemModifierOptionState
{
    [Id(0)] public Guid OptionId { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public decimal Price { get; set; }
    [Id(3)] public bool IsDefault { get; set; }

    /// <summary>
    /// Serving size for inventory consumption (e.g., 568 for a UK pint, 125 for small wine glass).
    /// When null, uses the base recipe quantity (multiplier of 1.0).
    /// </summary>
    [Id(4)] public decimal? ServingSize { get; set; }

    /// <summary>
    /// Unit of measure for serving size (e.g., "ml", "g", "oz", "cl").
    /// Should match the inventory tracking unit for the ingredient.
    /// </summary>
    [Id(5)] public string? ServingUnit { get; set; }
}

// ============================================================================
// Menu Definition State
// ============================================================================

[GenerateSerializer]
public sealed class MenuDefinitionState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid MenuId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public string? Description { get; set; }
    [Id(5)] public bool IsDefault { get; set; }
    [Id(6)] public bool IsActive { get; set; } = true;
    [Id(7)] public List<MenuScreenState> Screens { get; set; } = [];
    [Id(8)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class MenuScreenState
{
    [Id(0)] public Guid ScreenId { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public int Position { get; set; }
    [Id(3)] public string? Color { get; set; }
    [Id(4)] public int Rows { get; set; } = 4;
    [Id(5)] public int Columns { get; set; } = 5;
    [Id(6)] public List<MenuButtonState> Buttons { get; set; } = [];
}

[GenerateSerializer]
public sealed class MenuButtonState
{
    [Id(0)] public Guid ButtonId { get; set; }
    [Id(1)] public Guid? MenuItemId { get; set; }
    [Id(2)] public Guid? SubScreenId { get; set; }
    [Id(3)] public int Row { get; set; }
    [Id(4)] public int Column { get; set; }
    [Id(5)] public string? Label { get; set; }
    [Id(6)] public string? Color { get; set; }
    [Id(7)] public string ButtonType { get; set; } = "item";
}

// ============================================================================
// Accounting Group State
// ============================================================================

[GenerateSerializer]
public sealed class AccountingGroupState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid AccountingGroupId { get; set; }
    [Id(2)] public Guid LocationId { get; set; }
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public string Code { get; set; } = string.Empty;
    [Id(5)] public string? Description { get; set; }
    [Id(6)] public string? RevenueAccountCode { get; set; }
    [Id(7)] public string? CogsAccountCode { get; set; }
    [Id(8)] public bool IsActive { get; set; } = true;
    [Id(9)] public int ItemCount { get; set; }
    [Id(10)] public int Version { get; set; }
}
