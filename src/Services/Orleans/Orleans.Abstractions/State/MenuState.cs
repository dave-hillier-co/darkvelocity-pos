using DarkVelocity.Orleans.Abstractions.Grains;

namespace DarkVelocity.Orleans.Abstractions.State;

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
