namespace DarkVelocity.Orleans.Abstractions.Grains;

// ============================================================================
// Menu Category Grain
// ============================================================================

public record CreateMenuCategoryCommand(
    Guid LocationId,
    string Name,
    string? Description,
    int DisplayOrder,
    string? Color);

public record UpdateMenuCategoryCommand(
    string? Name,
    string? Description,
    int? DisplayOrder,
    string? Color,
    bool? IsActive);

public record MenuCategorySnapshot(
    Guid CategoryId,
    Guid LocationId,
    string Name,
    string? Description,
    int DisplayOrder,
    string? Color,
    bool IsActive,
    int ItemCount);

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

public record CreateMenuItemCommand(
    Guid LocationId,
    Guid CategoryId,
    Guid? AccountingGroupId,
    Guid? RecipeId,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    string? Sku,
    bool TrackInventory);

public record UpdateMenuItemCommand(
    Guid? CategoryId,
    Guid? AccountingGroupId,
    Guid? RecipeId,
    string? Name,
    string? Description,
    decimal? Price,
    string? ImageUrl,
    string? Sku,
    bool? IsActive,
    bool? TrackInventory);

public record MenuItemModifier(
    Guid ModifierId,
    string Name,
    decimal PriceAdjustment,
    bool IsRequired,
    int MinSelections,
    int MaxSelections,
    IReadOnlyList<MenuItemModifierOption> Options);

public record MenuItemModifierOption(
    Guid OptionId,
    string Name,
    decimal Price,
    bool IsDefault);

public record MenuItemSnapshot(
    Guid MenuItemId,
    Guid LocationId,
    Guid CategoryId,
    string CategoryName,
    Guid? AccountingGroupId,
    Guid? RecipeId,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    string? Sku,
    bool IsActive,
    bool TrackInventory,
    decimal? TheoreticalCost,
    decimal? CostPercent,
    IReadOnlyList<MenuItemModifier> Modifiers);

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

public record CreateMenuDefinitionCommand(
    Guid LocationId,
    string Name,
    string? Description,
    bool IsDefault);

public record UpdateMenuDefinitionCommand(
    string? Name,
    string? Description,
    bool? IsDefault,
    bool? IsActive);

public record MenuScreenDefinition(
    Guid ScreenId,
    string Name,
    int Position,
    string? Color,
    int Rows,
    int Columns,
    IReadOnlyList<MenuButtonDefinition> Buttons);

public record MenuButtonDefinition(
    Guid ButtonId,
    Guid? MenuItemId,
    Guid? SubScreenId,
    int Row,
    int Column,
    string? Label,
    string? Color,
    string ButtonType);

public record MenuDefinitionSnapshot(
    Guid MenuId,
    Guid LocationId,
    string Name,
    string? Description,
    bool IsDefault,
    bool IsActive,
    IReadOnlyList<MenuScreenDefinition> Screens);

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

public record CreateAccountingGroupCommand(
    Guid LocationId,
    string Name,
    string Code,
    string? Description,
    string? RevenueAccountCode,
    string? CogsAccountCode);

public record UpdateAccountingGroupCommand(
    string? Name,
    string? Code,
    string? Description,
    string? RevenueAccountCode,
    string? CogsAccountCode,
    bool? IsActive);

public record AccountingGroupSnapshot(
    Guid AccountingGroupId,
    Guid LocationId,
    string Name,
    string Code,
    string? Description,
    string? RevenueAccountCode,
    string? CogsAccountCode,
    bool IsActive,
    int ItemCount);

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
