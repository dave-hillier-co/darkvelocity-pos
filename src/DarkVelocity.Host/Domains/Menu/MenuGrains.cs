using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Menu Category Grain
// ============================================================================

/// <summary>
/// Grain for menu category management.
/// Manages menu categories for organizing menu items.
/// </summary>
public class MenuCategoryGrain : Grain, IMenuCategoryGrain
{
    private readonly IPersistentState<MenuCategoryState> _state;

    public MenuCategoryGrain(
        [PersistentState("menuCategory", "OrleansStorage")]
        IPersistentState<MenuCategoryState> state)
    {
        _state = state;
    }

    public async Task<MenuCategorySnapshot> CreateAsync(CreateMenuCategoryCommand command)
    {
        if (_state.State.CategoryId != Guid.Empty)
            throw new InvalidOperationException("Menu category already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var categoryId = Guid.Parse(parts[2]);

        _state.State = new MenuCategoryState
        {
            OrgId = orgId,
            CategoryId = categoryId,
            LocationId = command.LocationId,
            Name = command.Name,
            Description = command.Description,
            DisplayOrder = command.DisplayOrder,
            Color = command.Color,
            IsActive = true,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<MenuCategorySnapshot> UpdateAsync(UpdateMenuCategoryCommand command)
    {
        EnsureInitialized();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.Description != null) _state.State.Description = command.Description;
        if (command.DisplayOrder.HasValue) _state.State.DisplayOrder = command.DisplayOrder.Value;
        if (command.Color != null) _state.State.Color = command.Color;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task DeactivateAsync()
    {
        EnsureInitialized();
        _state.State.IsActive = false;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<MenuCategorySnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public async Task IncrementItemCountAsync()
    {
        EnsureInitialized();
        _state.State.ItemCount++;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task DecrementItemCountAsync()
    {
        EnsureInitialized();
        if (_state.State.ItemCount > 0)
            _state.State.ItemCount--;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private MenuCategorySnapshot CreateSnapshot()
    {
        return new MenuCategorySnapshot(
            CategoryId: _state.State.CategoryId,
            LocationId: _state.State.LocationId,
            Name: _state.State.Name,
            Description: _state.State.Description,
            DisplayOrder: _state.State.DisplayOrder,
            Color: _state.State.Color,
            IsActive: _state.State.IsActive,
            ItemCount: _state.State.ItemCount);
    }

    private void EnsureInitialized()
    {
        if (_state.State.CategoryId == Guid.Empty)
            throw new InvalidOperationException("Menu category grain not initialized");
    }
}

// ============================================================================
// Menu Item Grain
// ============================================================================

/// <summary>
/// Grain for menu item management.
/// Manages individual menu items with pricing and modifiers.
/// </summary>
public class MenuItemGrain : Grain, IMenuItemGrain
{
    private readonly IPersistentState<MenuItemState> _state;

    public MenuItemGrain(
        [PersistentState("menuItem", "OrleansStorage")]
        IPersistentState<MenuItemState> state)
    {
        _state = state;
    }

    public async Task<MenuItemSnapshot> CreateAsync(CreateMenuItemCommand command)
    {
        if (_state.State.MenuItemId != Guid.Empty)
            throw new InvalidOperationException("Menu item already exists");

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Name cannot be empty", nameof(command));

        if (command.Price < 0)
            throw new ArgumentException("Price cannot be negative", nameof(command));

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var itemId = Guid.Parse(parts[2]);

        _state.State = new MenuItemState
        {
            OrgId = orgId,
            MenuItemId = itemId,
            LocationId = command.LocationId,
            CategoryId = command.CategoryId,
            AccountingGroupId = command.AccountingGroupId,
            RecipeId = command.RecipeId,
            Name = command.Name,
            Description = command.Description,
            Price = command.Price,
            ImageUrl = command.ImageUrl,
            Sku = command.Sku,
            IsActive = true,
            TrackInventory = command.TrackInventory,
            Version = 1,
            TaxRates = command.TaxRates != null ? new ContextualTaxRatesState
            {
                DeliveryTaxPercent = command.TaxRates.DeliveryTaxPercent,
                TakeawayTaxPercent = command.TaxRates.TakeawayTaxPercent,
                DineInTaxPercent = command.TaxRates.DineInTaxPercent
            } : null,
            ProductTags = command.ProductTags?.Select(t => new ProductTagState
            {
                TagId = t.TagId,
                Name = t.Name
            }).ToList() ?? []
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<MenuItemSnapshot> UpdateAsync(UpdateMenuItemCommand command)
    {
        EnsureInitialized();

        if (command.CategoryId.HasValue) _state.State.CategoryId = command.CategoryId.Value;
        if (command.AccountingGroupId.HasValue) _state.State.AccountingGroupId = command.AccountingGroupId.Value;
        if (command.RecipeId.HasValue) _state.State.RecipeId = command.RecipeId.Value;
        if (command.Name != null) _state.State.Name = command.Name;
        if (command.Description != null) _state.State.Description = command.Description;
        if (command.Price.HasValue) _state.State.Price = command.Price.Value;
        if (command.ImageUrl != null) _state.State.ImageUrl = command.ImageUrl;
        if (command.Sku != null) _state.State.Sku = command.Sku;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;
        if (command.TrackInventory.HasValue) _state.State.TrackInventory = command.TrackInventory.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task DeactivateAsync()
    {
        EnsureInitialized();
        _state.State.IsActive = false;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<MenuItemSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<decimal> GetPriceAsync()
    {
        EnsureInitialized();
        return Task.FromResult(_state.State.Price);
    }

    public async Task AddModifierAsync(MenuItemModifier modifier)
    {
        EnsureInitialized();

        if (modifier.Options == null || modifier.Options.Count == 0)
            throw new ArgumentException("Modifier must have at least one option", nameof(modifier));

        if (modifier.MinSelections > modifier.MaxSelections)
            throw new ArgumentException("MinSelections cannot be greater than MaxSelections", nameof(modifier));

        var existing = _state.State.Modifiers.FirstOrDefault(m => m.ModifierId == modifier.ModifierId);
        if (existing != null)
        {
            existing.Name = modifier.Name;
            existing.PriceAdjustment = modifier.PriceAdjustment;
            existing.IsRequired = modifier.IsRequired;
            existing.MinSelections = modifier.MinSelections;
            existing.MaxSelections = modifier.MaxSelections;
            existing.Options = modifier.Options.Select(o => new MenuItemModifierOptionState
            {
                OptionId = o.OptionId,
                Name = o.Name,
                Price = o.Price,
                IsDefault = o.IsDefault,
                ServingSize = o.ServingSize,
                ServingUnit = o.ServingUnit
            }).ToList();
        }
        else
        {
            _state.State.Modifiers.Add(new MenuItemModifierState
            {
                ModifierId = modifier.ModifierId,
                Name = modifier.Name,
                PriceAdjustment = modifier.PriceAdjustment,
                IsRequired = modifier.IsRequired,
                MinSelections = modifier.MinSelections,
                MaxSelections = modifier.MaxSelections,
                Options = modifier.Options.Select(o => new MenuItemModifierOptionState
                {
                    OptionId = o.OptionId,
                    Name = o.Name,
                    Price = o.Price,
                    IsDefault = o.IsDefault,
                    ServingSize = o.ServingSize,
                    ServingUnit = o.ServingUnit
                }).ToList()
            });
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveModifierAsync(Guid modifierId)
    {
        EnsureInitialized();
        _state.State.Modifiers.RemoveAll(m => m.ModifierId == modifierId);
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task UpdateCostAsync(decimal theoreticalCost)
    {
        EnsureInitialized();
        _state.State.TheoreticalCost = theoreticalCost;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private MenuItemSnapshot CreateSnapshot()
    {
        var costPercent = _state.State.Price > 0 && _state.State.TheoreticalCost.HasValue
            ? _state.State.TheoreticalCost.Value / _state.State.Price * 100
            : (decimal?)null;

        var taxRates = _state.State.TaxRates != null
            ? new ContextualTaxRates(_state.State.TaxRates.DeliveryTaxPercent, _state.State.TaxRates.TakeawayTaxPercent, _state.State.TaxRates.DineInTaxPercent)
            : null;

        var productTags = _state.State.ProductTags
            .Select(t => new ProductTag(t.TagId, t.Name))
            .ToList();

        return new MenuItemSnapshot(
            MenuItemId: _state.State.MenuItemId,
            LocationId: _state.State.LocationId,
            CategoryId: _state.State.CategoryId,
            CategoryName: _state.State.CategoryName,
            AccountingGroupId: _state.State.AccountingGroupId,
            RecipeId: _state.State.RecipeId,
            Name: _state.State.Name,
            Description: _state.State.Description,
            Price: _state.State.Price,
            ImageUrl: _state.State.ImageUrl,
            Sku: _state.State.Sku,
            IsActive: _state.State.IsActive,
            TrackInventory: _state.State.TrackInventory,
            TheoreticalCost: _state.State.TheoreticalCost,
            CostPercent: costPercent,
            Modifiers: _state.State.Modifiers.Select(m => new MenuItemModifier(
                ModifierId: m.ModifierId,
                Name: m.Name,
                PriceAdjustment: m.PriceAdjustment,
                IsRequired: m.IsRequired,
                MinSelections: m.MinSelections,
                MaxSelections: m.MaxSelections,
                Options: m.Options.Select(o => new MenuItemModifierOption(
                    OptionId: o.OptionId,
                    Name: o.Name,
                    Price: o.Price,
                    IsDefault: o.IsDefault,
                    ServingSize: o.ServingSize,
                    ServingUnit: o.ServingUnit)).ToList())).ToList(),
            TaxRates: taxRates,
            ProductTags: productTags,
            IsSnoozed: _state.State.IsSnoozed,
            SnoozedUntil: _state.State.SnoozedUntil);
    }

    private void EnsureInitialized()
    {
        if (_state.State.MenuItemId == Guid.Empty)
            throw new InvalidOperationException("Menu item grain not initialized");
    }

    public async Task SetSnoozedAsync(bool snoozed, TimeSpan? duration = null)
    {
        EnsureInitialized();
        _state.State.IsSnoozed = snoozed;
        _state.State.SnoozedUntil = snoozed && duration.HasValue
            ? DateTime.UtcNow.Add(duration.Value)
            : null;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task AddProductTagAsync(ProductTag tag)
    {
        EnsureInitialized();
        var existing = _state.State.ProductTags.FirstOrDefault(t => t.TagId == tag.TagId);
        if (existing != null)
        {
            existing.Name = tag.Name;
        }
        else
        {
            _state.State.ProductTags.Add(new ProductTagState
            {
                TagId = tag.TagId,
                Name = tag.Name
            });
        }
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveProductTagAsync(int tagId)
    {
        EnsureInitialized();
        _state.State.ProductTags.RemoveAll(t => t.TagId == tagId);
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task UpdateTaxRatesAsync(ContextualTaxRates rates)
    {
        EnsureInitialized();
        _state.State.TaxRates = new ContextualTaxRatesState
        {
            DeliveryTaxPercent = rates.DeliveryTaxPercent,
            TakeawayTaxPercent = rates.TakeawayTaxPercent,
            DineInTaxPercent = rates.DineInTaxPercent
        };
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task<MenuItemVariationSnapshot> AddVariationAsync(CreateMenuItemVariationCommand command)
    {
        EnsureInitialized();
        var variationId = Guid.NewGuid();
        var variation = new MenuItemVariationState
        {
            VariationId = variationId,
            MenuItemId = _state.State.MenuItemId,
            Name = command.Name,
            PricingType = command.PricingType,
            Price = command.Price,
            Sku = command.Sku,
            DisplayOrder = command.DisplayOrder,
            IsActive = true,
            TrackInventory = command.TrackInventory,
            InventoryItemId = command.InventoryItemId,
            InventoryQuantityPerSale = command.InventoryQuantityPerSale
        };
        _state.State.Variations.Add(variation);
        _state.State.Version++;
        await _state.WriteStateAsync();

        return CreateVariationSnapshot(variation);
    }

    public async Task<MenuItemVariationSnapshot> UpdateVariationAsync(Guid variationId, UpdateMenuItemVariationCommand command)
    {
        EnsureInitialized();
        var variation = _state.State.Variations.FirstOrDefault(v => v.VariationId == variationId)
            ?? throw new InvalidOperationException("Variation not found");

        if (command.Name != null) variation.Name = command.Name;
        if (command.PricingType.HasValue) variation.PricingType = command.PricingType.Value;
        if (command.Price.HasValue) variation.Price = command.Price.Value;
        if (command.Sku != null) variation.Sku = command.Sku;
        if (command.DisplayOrder.HasValue) variation.DisplayOrder = command.DisplayOrder.Value;
        if (command.IsActive.HasValue) variation.IsActive = command.IsActive.Value;
        if (command.TrackInventory.HasValue) variation.TrackInventory = command.TrackInventory.Value;
        if (command.InventoryItemId.HasValue) variation.InventoryItemId = command.InventoryItemId;
        if (command.InventoryQuantityPerSale.HasValue) variation.InventoryQuantityPerSale = command.InventoryQuantityPerSale;

        _state.State.Version++;
        await _state.WriteStateAsync();

        return CreateVariationSnapshot(variation);
    }

    public async Task RemoveVariationAsync(Guid variationId)
    {
        EnsureInitialized();
        _state.State.Variations.RemoveAll(v => v.VariationId == variationId);
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<MenuItemVariationSnapshot>> GetVariationsAsync()
    {
        EnsureInitialized();
        var snapshots = _state.State.Variations
            .OrderBy(v => v.DisplayOrder)
            .Select(CreateVariationSnapshot)
            .ToList();
        return Task.FromResult<IReadOnlyList<MenuItemVariationSnapshot>>(snapshots);
    }

    private static MenuItemVariationSnapshot CreateVariationSnapshot(MenuItemVariationState v) =>
        new(v.VariationId, v.MenuItemId, v.Name, v.PricingType, v.Price, v.Sku,
            v.DisplayOrder, v.IsActive, v.TrackInventory, v.InventoryItemId,
            v.InventoryQuantityPerSale, v.TheoreticalCost);
}

// ============================================================================
// Menu Definition Grain
// ============================================================================

/// <summary>
/// Grain for menu definition management.
/// Manages POS menu layouts with screens and buttons.
/// </summary>
public class MenuDefinitionGrain : Grain, IMenuDefinitionGrain
{
    private readonly IPersistentState<MenuDefinitionState> _state;

    public MenuDefinitionGrain(
        [PersistentState("menuDefinition", "OrleansStorage")]
        IPersistentState<MenuDefinitionState> state)
    {
        _state = state;
    }

    public async Task<MenuDefinitionSnapshot> CreateAsync(CreateMenuDefinitionCommand command)
    {
        if (_state.State.MenuId != Guid.Empty)
            throw new InvalidOperationException("Menu definition already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var menuId = Guid.Parse(parts[2]);

        _state.State = new MenuDefinitionState
        {
            OrgId = orgId,
            MenuId = menuId,
            LocationId = command.LocationId,
            Name = command.Name,
            Description = command.Description,
            IsDefault = command.IsDefault,
            IsActive = true,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<MenuDefinitionSnapshot> UpdateAsync(UpdateMenuDefinitionCommand command)
    {
        EnsureInitialized();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.Description != null) _state.State.Description = command.Description;
        if (command.IsDefault.HasValue) _state.State.IsDefault = command.IsDefault.Value;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task AddScreenAsync(MenuScreenDefinition screen)
    {
        EnsureInitialized();

        _state.State.Screens.Add(new MenuScreenState
        {
            ScreenId = screen.ScreenId,
            Name = screen.Name,
            Position = screen.Position,
            Color = screen.Color,
            Rows = screen.Rows,
            Columns = screen.Columns,
            Buttons = screen.Buttons.Select(b => new MenuButtonState
            {
                ButtonId = b.ButtonId,
                MenuItemId = b.MenuItemId,
                SubScreenId = b.SubScreenId,
                Row = b.Row,
                Column = b.Column,
                Label = b.Label,
                Color = b.Color,
                ButtonType = b.ButtonType
            }).ToList()
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task UpdateScreenAsync(Guid screenId, string? name, string? color, int? rows, int? columns)
    {
        EnsureInitialized();

        var screen = _state.State.Screens.FirstOrDefault(s => s.ScreenId == screenId)
            ?? throw new InvalidOperationException("Screen not found");

        if (name != null) screen.Name = name;
        if (color != null) screen.Color = color;
        if (rows.HasValue) screen.Rows = rows.Value;
        if (columns.HasValue) screen.Columns = columns.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveScreenAsync(Guid screenId)
    {
        EnsureInitialized();
        _state.State.Screens.RemoveAll(s => s.ScreenId == screenId);
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task AddButtonAsync(Guid screenId, MenuButtonDefinition button)
    {
        EnsureInitialized();

        var screen = _state.State.Screens.FirstOrDefault(s => s.ScreenId == screenId)
            ?? throw new InvalidOperationException("Screen not found");

        screen.Buttons.Add(new MenuButtonState
        {
            ButtonId = button.ButtonId,
            MenuItemId = button.MenuItemId,
            SubScreenId = button.SubScreenId,
            Row = button.Row,
            Column = button.Column,
            Label = button.Label,
            Color = button.Color,
            ButtonType = button.ButtonType
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveButtonAsync(Guid screenId, Guid buttonId)
    {
        EnsureInitialized();

        var screen = _state.State.Screens.FirstOrDefault(s => s.ScreenId == screenId)
            ?? throw new InvalidOperationException("Screen not found");

        screen.Buttons.RemoveAll(b => b.ButtonId == buttonId);
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public Task<MenuDefinitionSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public async Task SetAsDefaultAsync()
    {
        EnsureInitialized();
        _state.State.IsDefault = true;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private MenuDefinitionSnapshot CreateSnapshot()
    {
        return new MenuDefinitionSnapshot(
            MenuId: _state.State.MenuId,
            LocationId: _state.State.LocationId,
            Name: _state.State.Name,
            Description: _state.State.Description,
            IsDefault: _state.State.IsDefault,
            IsActive: _state.State.IsActive,
            Screens: _state.State.Screens.Select(s => new MenuScreenDefinition(
                ScreenId: s.ScreenId,
                Name: s.Name,
                Position: s.Position,
                Color: s.Color,
                Rows: s.Rows,
                Columns: s.Columns,
                Buttons: s.Buttons.Select(b => new MenuButtonDefinition(
                    ButtonId: b.ButtonId,
                    MenuItemId: b.MenuItemId,
                    SubScreenId: b.SubScreenId,
                    Row: b.Row,
                    Column: b.Column,
                    Label: b.Label,
                    Color: b.Color,
                    ButtonType: b.ButtonType)).ToList())).ToList());
    }

    private void EnsureInitialized()
    {
        if (_state.State.MenuId == Guid.Empty)
            throw new InvalidOperationException("Menu definition grain not initialized");
    }
}

// ============================================================================
// Accounting Group Grain
// ============================================================================

/// <summary>
/// Grain for accounting group management.
/// Manages accounting groups for financial reporting.
/// </summary>
public class AccountingGroupGrain : Grain, IAccountingGroupGrain
{
    private readonly IPersistentState<AccountingGroupState> _state;

    public AccountingGroupGrain(
        [PersistentState("accountingGroup", "OrleansStorage")]
        IPersistentState<AccountingGroupState> state)
    {
        _state = state;
    }

    public async Task<AccountingGroupSnapshot> CreateAsync(CreateAccountingGroupCommand command)
    {
        if (_state.State.AccountingGroupId != Guid.Empty)
            throw new InvalidOperationException("Accounting group already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var groupId = Guid.Parse(parts[2]);

        _state.State = new AccountingGroupState
        {
            OrgId = orgId,
            AccountingGroupId = groupId,
            LocationId = command.LocationId,
            Name = command.Name,
            Code = command.Code,
            Description = command.Description,
            RevenueAccountCode = command.RevenueAccountCode,
            CogsAccountCode = command.CogsAccountCode,
            IsActive = true,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<AccountingGroupSnapshot> UpdateAsync(UpdateAccountingGroupCommand command)
    {
        EnsureInitialized();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.Code != null) _state.State.Code = command.Code;
        if (command.Description != null) _state.State.Description = command.Description;
        if (command.RevenueAccountCode != null) _state.State.RevenueAccountCode = command.RevenueAccountCode;
        if (command.CogsAccountCode != null) _state.State.CogsAccountCode = command.CogsAccountCode;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<AccountingGroupSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public async Task IncrementItemCountAsync()
    {
        EnsureInitialized();
        _state.State.ItemCount++;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task DecrementItemCountAsync()
    {
        EnsureInitialized();
        if (_state.State.ItemCount > 0)
            _state.State.ItemCount--;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private AccountingGroupSnapshot CreateSnapshot()
    {
        return new AccountingGroupSnapshot(
            AccountingGroupId: _state.State.AccountingGroupId,
            LocationId: _state.State.LocationId,
            Name: _state.State.Name,
            Code: _state.State.Code,
            Description: _state.State.Description,
            RevenueAccountCode: _state.State.RevenueAccountCode,
            CogsAccountCode: _state.State.CogsAccountCode,
            IsActive: _state.State.IsActive,
            ItemCount: _state.State.ItemCount);
    }

    private void EnsureInitialized()
    {
        if (_state.State.AccountingGroupId == Guid.Empty)
            throw new InvalidOperationException("Accounting group grain not initialized");
    }
}
