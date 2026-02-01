namespace DarkVelocity.Host.Grains;

// ============================================================================
// Menu Category Grain
// ============================================================================

[GenerateSerializer]
public record CreateMenuCategoryCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] string Name,
    [property: Id(2)] string? Description,
    [property: Id(3)] int DisplayOrder,
    [property: Id(4)] string? Color);

[GenerateSerializer]
public record UpdateMenuCategoryCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] string? Description,
    [property: Id(2)] int? DisplayOrder,
    [property: Id(3)] string? Color,
    [property: Id(4)] bool? IsActive);

[GenerateSerializer]
public record MenuCategorySnapshot(
    [property: Id(0)] Guid CategoryId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] string Name,
    [property: Id(3)] string? Description,
    [property: Id(4)] int DisplayOrder,
    [property: Id(5)] string? Color,
    [property: Id(6)] bool IsActive,
    [property: Id(7)] int ItemCount);

/// <summary>
/// Grain for menu category management.
/// Key: "{orgId}:menucategory:{categoryId}"
/// </summary>
public interface IMenuCategoryGrain : IGrainWithStringKey
{
    Task<MenuCategorySnapshot> CreateAsync(CreateMenuCategoryCommand command);
    Task<MenuCategorySnapshot> UpdateAsync(UpdateMenuCategoryCommand command);
    Task DeactivateAsync();
    Task<MenuCategorySnapshot> GetSnapshotAsync();
    Task IncrementItemCountAsync();
    Task DecrementItemCountAsync();
}

// ============================================================================
// Menu Item Grain
// ============================================================================

[GenerateSerializer]
public record CreateMenuItemCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] Guid CategoryId,
    [property: Id(2)] Guid? AccountingGroupId,
    [property: Id(3)] Guid? RecipeId,
    [property: Id(4)] string Name,
    [property: Id(5)] string? Description,
    [property: Id(6)] decimal Price,
    [property: Id(7)] string? ImageUrl,
    [property: Id(8)] string? Sku,
    [property: Id(9)] bool TrackInventory);

[GenerateSerializer]
public record UpdateMenuItemCommand(
    [property: Id(0)] Guid? CategoryId,
    [property: Id(1)] Guid? AccountingGroupId,
    [property: Id(2)] Guid? RecipeId,
    [property: Id(3)] string? Name,
    [property: Id(4)] string? Description,
    [property: Id(5)] decimal? Price,
    [property: Id(6)] string? ImageUrl,
    [property: Id(7)] string? Sku,
    [property: Id(8)] bool? IsActive,
    [property: Id(9)] bool? TrackInventory);

[GenerateSerializer]
public record MenuItemModifier(
    [property: Id(0)] Guid ModifierId,
    [property: Id(1)] string Name,
    [property: Id(2)] decimal PriceAdjustment,
    [property: Id(3)] bool IsRequired,
    [property: Id(4)] int MinSelections,
    [property: Id(5)] int MaxSelections,
    [property: Id(6)] IReadOnlyList<MenuItemModifierOption> Options);

/// <summary>
/// Modifier option with optional serving size for inventory consumption.
/// ServingSize/ServingUnit enable accurate beverage inventory tracking (e.g., pint=568ml, half=284ml).
/// </summary>
[GenerateSerializer]
public record MenuItemModifierOption(
    [property: Id(0)] Guid OptionId,
    [property: Id(1)] string Name,
    [property: Id(2)] decimal Price,
    [property: Id(3)] bool IsDefault,
    [property: Id(4)] decimal? ServingSize = null,
    [property: Id(5)] string? ServingUnit = null);

[GenerateSerializer]
public record MenuItemSnapshot(
    [property: Id(0)] Guid MenuItemId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] Guid CategoryId,
    [property: Id(3)] string CategoryName,
    [property: Id(4)] Guid? AccountingGroupId,
    [property: Id(5)] Guid? RecipeId,
    [property: Id(6)] string Name,
    [property: Id(7)] string? Description,
    [property: Id(8)] decimal Price,
    [property: Id(9)] string? ImageUrl,
    [property: Id(10)] string? Sku,
    [property: Id(11)] bool IsActive,
    [property: Id(12)] bool TrackInventory,
    [property: Id(13)] decimal? TheoreticalCost,
    [property: Id(14)] decimal? CostPercent,
    [property: Id(15)] IReadOnlyList<MenuItemModifier> Modifiers);

/// <summary>
/// Grain for menu item management.
/// Key: "{orgId}:menuitem:{itemId}"
/// </summary>
public interface IMenuItemGrain : IGrainWithStringKey
{
    Task<MenuItemSnapshot> CreateAsync(CreateMenuItemCommand command);
    Task<MenuItemSnapshot> UpdateAsync(UpdateMenuItemCommand command);
    Task DeactivateAsync();
    Task<MenuItemSnapshot> GetSnapshotAsync();
    Task<decimal> GetPriceAsync();
    Task AddModifierAsync(MenuItemModifier modifier);
    Task RemoveModifierAsync(Guid modifierId);
    Task UpdateCostAsync(decimal theoreticalCost);
}

// ============================================================================
// Menu Definition Grain
// ============================================================================

[GenerateSerializer]
public record CreateMenuDefinitionCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] string Name,
    [property: Id(2)] string? Description,
    [property: Id(3)] bool IsDefault);

[GenerateSerializer]
public record UpdateMenuDefinitionCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] string? Description,
    [property: Id(2)] bool? IsDefault,
    [property: Id(3)] bool? IsActive);

[GenerateSerializer]
public record MenuScreenDefinition(
    [property: Id(0)] Guid ScreenId,
    [property: Id(1)] string Name,
    [property: Id(2)] int Position,
    [property: Id(3)] string? Color,
    [property: Id(4)] int Rows,
    [property: Id(5)] int Columns,
    [property: Id(6)] IReadOnlyList<MenuButtonDefinition> Buttons);

[GenerateSerializer]
public record MenuButtonDefinition(
    [property: Id(0)] Guid ButtonId,
    [property: Id(1)] Guid? MenuItemId,
    [property: Id(2)] Guid? SubScreenId,
    [property: Id(3)] int Row,
    [property: Id(4)] int Column,
    [property: Id(5)] string? Label,
    [property: Id(6)] string? Color,
    [property: Id(7)] string ButtonType);

[GenerateSerializer]
public record MenuDefinitionSnapshot(
    [property: Id(0)] Guid MenuId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] string Name,
    [property: Id(3)] string? Description,
    [property: Id(4)] bool IsDefault,
    [property: Id(5)] bool IsActive,
    [property: Id(6)] IReadOnlyList<MenuScreenDefinition> Screens);

/// <summary>
/// Grain for menu definition management.
/// Key: "{orgId}:menudef:{menuId}"
/// </summary>
public interface IMenuDefinitionGrain : IGrainWithStringKey
{
    Task<MenuDefinitionSnapshot> CreateAsync(CreateMenuDefinitionCommand command);
    Task<MenuDefinitionSnapshot> UpdateAsync(UpdateMenuDefinitionCommand command);
    Task AddScreenAsync(MenuScreenDefinition screen);
    Task UpdateScreenAsync(Guid screenId, string? name, string? color, int? rows, int? columns);
    Task RemoveScreenAsync(Guid screenId);
    Task AddButtonAsync(Guid screenId, MenuButtonDefinition button);
    Task RemoveButtonAsync(Guid screenId, Guid buttonId);
    Task<MenuDefinitionSnapshot> GetSnapshotAsync();
    Task SetAsDefaultAsync();
}

// ============================================================================
// Accounting Group Grain
// ============================================================================

[GenerateSerializer]
public record CreateAccountingGroupCommand(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] string Name,
    [property: Id(2)] string Code,
    [property: Id(3)] string? Description,
    [property: Id(4)] string? RevenueAccountCode,
    [property: Id(5)] string? CogsAccountCode);

[GenerateSerializer]
public record UpdateAccountingGroupCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] string? Code,
    [property: Id(2)] string? Description,
    [property: Id(3)] string? RevenueAccountCode,
    [property: Id(4)] string? CogsAccountCode,
    [property: Id(5)] bool? IsActive);

[GenerateSerializer]
public record AccountingGroupSnapshot(
    [property: Id(0)] Guid AccountingGroupId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] string Name,
    [property: Id(3)] string Code,
    [property: Id(4)] string? Description,
    [property: Id(5)] string? RevenueAccountCode,
    [property: Id(6)] string? CogsAccountCode,
    [property: Id(7)] bool IsActive,
    [property: Id(8)] int ItemCount);

/// <summary>
/// Grain for accounting group management.
/// Key: "{orgId}:accountinggroup:{groupId}"
/// </summary>
public interface IAccountingGroupGrain : IGrainWithStringKey
{
    Task<AccountingGroupSnapshot> CreateAsync(CreateAccountingGroupCommand command);
    Task<AccountingGroupSnapshot> UpdateAsync(UpdateAccountingGroupCommand command);
    Task<AccountingGroupSnapshot> GetSnapshotAsync();
    Task IncrementItemCountAsync();
    Task DecrementItemCountAsync();
}
