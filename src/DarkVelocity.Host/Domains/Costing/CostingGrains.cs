using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Recipe Grain Implementation
// ============================================================================

public class RecipeGrain : Grain, IRecipeGrain
{
    private readonly IPersistentState<RecipeState> _state;
    private const int MaxCostSnapshots = 52; // One year of weekly snapshots

    public RecipeGrain(
        [PersistentState("recipe", "OrleansStorage")]
        IPersistentState<RecipeState> state)
    {
        _state = state;
    }

    public async Task<RecipeSnapshot> CreateAsync(CreateRecipeCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Recipe already exists");

        if (string.IsNullOrWhiteSpace(command.Code))
            throw new ArgumentException("Recipe code is required", nameof(command));

        var (orgId, _, recipeId) = GrainKeys.ParseOrgEntity(this.GetPrimaryKeyString());

        _state.State = new RecipeState
        {
            Id = recipeId,
            OrganizationId = orgId,
            MenuItemId = command.MenuItemId,
            MenuItemName = command.MenuItemName,
            Code = command.Code,
            CategoryId = command.CategoryId,
            CategoryName = command.CategoryName,
            Description = command.Description,
            PortionYield = command.PortionYield > 0 ? command.PortionYield : 1,
            PrepInstructions = command.PrepInstructions,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<RecipeSnapshot> UpdateAsync(UpdateRecipeCommand command)
    {
        EnsureExists();

        if (command.MenuItemName != null) _state.State.MenuItemName = command.MenuItemName;
        if (command.Code != null) _state.State.Code = command.Code;
        if (command.CategoryId.HasValue) _state.State.CategoryId = command.CategoryId;
        if (command.CategoryName != null) _state.State.CategoryName = command.CategoryName;
        if (command.Description != null) _state.State.Description = command.Description;
        if (command.PortionYield.HasValue && command.PortionYield.Value > 0)
            _state.State.PortionYield = command.PortionYield.Value;
        if (command.PrepInstructions != null) _state.State.PrepInstructions = command.PrepInstructions;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;

        _state.State.UpdatedAt = DateTime.UtcNow;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<RecipeSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.Id != Guid.Empty);
    }

    public async Task DeleteAsync()
    {
        EnsureExists();
        _state.State.IsActive = false;
        _state.State.UpdatedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddIngredientAsync(RecipeIngredientCommand command)
    {
        EnsureExists();

        var existing = _state.State.Ingredients.FirstOrDefault(i => i.IngredientId == command.IngredientId);
        if (existing != null)
            throw new InvalidOperationException("Ingredient already exists in recipe");

        var effectiveQty = command.Quantity * (1 + command.WastePercentage / 100);
        var lineCost = effectiveQty * command.CurrentUnitCost;

        var ingredient = new CostingRecipeIngredientState
        {
            Id = Guid.NewGuid(),
            IngredientId = command.IngredientId,
            IngredientName = command.IngredientName,
            Quantity = command.Quantity,
            UnitOfMeasure = command.UnitOfMeasure,
            WastePercentage = command.WastePercentage,
            CurrentUnitCost = command.CurrentUnitCost,
            CurrentLineCost = lineCost
        };

        _state.State.Ingredients.Add(ingredient);
        RecalculateCost();
        _state.State.UpdatedAt = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task UpdateIngredientAsync(Guid ingredientId, RecipeIngredientCommand command)
    {
        EnsureExists();

        var ingredient = _state.State.Ingredients.FirstOrDefault(i => i.IngredientId == ingredientId)
            ?? throw new InvalidOperationException("Ingredient not found");

        ingredient.IngredientName = command.IngredientName;
        ingredient.Quantity = command.Quantity;
        ingredient.UnitOfMeasure = command.UnitOfMeasure;
        ingredient.WastePercentage = command.WastePercentage;
        ingredient.CurrentUnitCost = command.CurrentUnitCost;

        var effectiveQty = command.Quantity * (1 + command.WastePercentage / 100);
        ingredient.CurrentLineCost = effectiveQty * command.CurrentUnitCost;

        RecalculateCost();
        _state.State.UpdatedAt = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task RemoveIngredientAsync(Guid ingredientId)
    {
        EnsureExists();

        var ingredient = _state.State.Ingredients.FirstOrDefault(i => i.IngredientId == ingredientId)
            ?? throw new InvalidOperationException("Ingredient not found");

        _state.State.Ingredients.Remove(ingredient);
        RecalculateCost();
        _state.State.UpdatedAt = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<CostingRecipeIngredientSnapshot>> GetIngredientsAsync()
    {
        EnsureExists();

        var totalCost = _state.State.Ingredients.Sum(i => i.CurrentLineCost);
        var snapshots = _state.State.Ingredients.Select(i =>
        {
            var effectiveQty = i.Quantity * (1 + i.WastePercentage / 100);
            return new CostingRecipeIngredientSnapshot(
                i.Id,
                i.IngredientId,
                i.IngredientName,
                i.Quantity,
                i.UnitOfMeasure,
                i.WastePercentage,
                effectiveQty,
                i.CurrentUnitCost,
                i.CurrentLineCost,
                totalCost > 0 ? (i.CurrentLineCost / totalCost) * 100 : 0);
        }).ToList();

        return Task.FromResult<IReadOnlyList<CostingRecipeIngredientSnapshot>>(snapshots);
    }

    public Task<RecipeCostCalculation> CalculateCostAsync(decimal? menuPrice = null)
    {
        EnsureExists();

        var totalCost = _state.State.Ingredients.Sum(i => i.CurrentLineCost);
        var costPerPortion = _state.State.PortionYield > 0
            ? totalCost / _state.State.PortionYield
            : totalCost;

        var ingredientSnapshots = _state.State.Ingredients
            .Select(i =>
            {
                var effectiveQty = i.Quantity * (1 + i.WastePercentage / 100);
                return new CostingRecipeIngredientSnapshot(
                    i.Id,
                    i.IngredientId,
                    i.IngredientName,
                    i.Quantity,
                    i.UnitOfMeasure,
                    i.WastePercentage,
                    effectiveQty,
                    i.CurrentUnitCost,
                    i.CurrentLineCost,
                    totalCost > 0 ? (i.CurrentLineCost / totalCost) * 100 : 0);
            })
            .OrderByDescending(i => i.CurrentLineCost)
            .ToList();

        decimal? costPercentage = null;
        decimal? grossMarginPercent = null;

        if (menuPrice.HasValue && menuPrice.Value > 0)
        {
            costPercentage = (costPerPortion / menuPrice.Value) * 100;
            grossMarginPercent = 100 - costPercentage;
        }

        return Task.FromResult(new RecipeCostCalculation(
            _state.State.Id,
            _state.State.MenuItemName,
            totalCost,
            costPerPortion,
            _state.State.PortionYield,
            menuPrice,
            costPercentage,
            grossMarginPercent,
            ingredientSnapshots));
    }

    public async Task<RecipeSnapshot> RecalculateFromPricesAsync(IReadOnlyDictionary<Guid, decimal> ingredientPrices)
    {
        EnsureExists();

        foreach (var ingredient in _state.State.Ingredients)
        {
            if (ingredientPrices.TryGetValue(ingredient.IngredientId, out var newPrice))
            {
                ingredient.CurrentUnitCost = newPrice;
                var effectiveQty = ingredient.Quantity * (1 + ingredient.WastePercentage / 100);
                ingredient.CurrentLineCost = effectiveQty * newPrice;
            }
        }

        RecalculateCost();
        _state.State.UpdatedAt = DateTime.UtcNow;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<RecipeCostSnapshotEntry> CreateCostSnapshotAsync(decimal? menuPrice, string? notes = null)
    {
        EnsureExists();

        var snapshot = new RecipeCostSnapshotState
        {
            Id = Guid.NewGuid(),
            SnapshotDate = DateTime.UtcNow,
            CostPerPortion = _state.State.CurrentCostPerPortion,
            MenuPrice = menuPrice,
            MarginPercent = menuPrice.HasValue && menuPrice.Value > 0
                ? 100 - (_state.State.CurrentCostPerPortion / menuPrice.Value * 100)
                : null,
            Notes = notes
        };

        _state.State.CostSnapshots.Add(snapshot);

        // Keep only recent snapshots
        if (_state.State.CostSnapshots.Count > MaxCostSnapshots)
        {
            _state.State.CostSnapshots = _state.State.CostSnapshots
                .OrderByDescending(s => s.SnapshotDate)
                .Take(MaxCostSnapshots)
                .ToList();
        }

        await _state.WriteStateAsync();

        return new RecipeCostSnapshotEntry(
            snapshot.Id,
            snapshot.SnapshotDate,
            snapshot.CostPerPortion,
            snapshot.MenuPrice,
            snapshot.MarginPercent,
            snapshot.Notes);
    }

    public Task<IReadOnlyList<RecipeCostSnapshotEntry>> GetCostHistoryAsync(int count = 10)
    {
        var history = _state.State.CostSnapshots
            .OrderByDescending(s => s.SnapshotDate)
            .Take(count)
            .Select(s => new RecipeCostSnapshotEntry(
                s.Id,
                s.SnapshotDate,
                s.CostPerPortion,
                s.MenuPrice,
                s.MarginPercent,
                s.Notes))
            .ToList();

        return Task.FromResult<IReadOnlyList<RecipeCostSnapshotEntry>>(history);
    }

    private void RecalculateCost()
    {
        var totalCost = _state.State.Ingredients.Sum(i => i.CurrentLineCost);
        _state.State.CurrentCostPerPortion = _state.State.PortionYield > 0
            ? totalCost / _state.State.PortionYield
            : totalCost;
        _state.State.CostCalculatedAt = DateTime.UtcNow;
    }

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Recipe not found");
    }

    private RecipeSnapshot CreateSnapshot()
    {
        var totalCost = _state.State.Ingredients.Sum(i => i.CurrentLineCost);
        var ingredientSnapshots = _state.State.Ingredients
            .Select(i =>
            {
                var effectiveQty = i.Quantity * (1 + i.WastePercentage / 100);
                return new CostingRecipeIngredientSnapshot(
                    i.Id,
                    i.IngredientId,
                    i.IngredientName,
                    i.Quantity,
                    i.UnitOfMeasure,
                    i.WastePercentage,
                    effectiveQty,
                    i.CurrentUnitCost,
                    i.CurrentLineCost,
                    totalCost > 0 ? (i.CurrentLineCost / totalCost) * 100 : 0);
            })
            .ToList();

        return new RecipeSnapshot(
            _state.State.Id,
            _state.State.MenuItemId,
            _state.State.MenuItemName,
            _state.State.Code,
            _state.State.CategoryId,
            _state.State.CategoryName,
            _state.State.Description,
            _state.State.PortionYield,
            _state.State.PrepInstructions,
            _state.State.CurrentCostPerPortion,
            _state.State.CostCalculatedAt,
            _state.State.IsActive,
            ingredientSnapshots);
    }
}

// ============================================================================
// Ingredient Price Grain Implementation
// ============================================================================

public class IngredientPriceGrain : Grain, IIngredientPriceGrain
{
    private readonly IPersistentState<IngredientPriceState> _state;
    private const int MaxPriceHistory = 100;

    public IngredientPriceGrain(
        [PersistentState("ingredientprice", "OrleansStorage")]
        IPersistentState<IngredientPriceState> state)
    {
        _state = state;
    }

    public async Task<IngredientPriceSnapshot> CreateAsync(CreateIngredientPriceCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Ingredient price already exists");

        var (orgId, _, ingredientId) = GrainKeys.ParseOrgEntity(this.GetPrimaryKeyString());

        var pricePerUnit = command.PackSize > 0 ? command.CurrentPrice / command.PackSize : command.CurrentPrice;

        _state.State = new IngredientPriceState
        {
            Id = ingredientId,
            OrganizationId = orgId,
            IngredientId = command.IngredientId,
            IngredientName = command.IngredientName,
            CurrentPrice = command.CurrentPrice,
            UnitOfMeasure = command.UnitOfMeasure,
            PackSize = command.PackSize > 0 ? command.PackSize : 1,
            PricePerUnit = pricePerUnit,
            PreferredSupplierId = command.PreferredSupplierId,
            PreferredSupplierName = command.PreferredSupplierName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Record initial price in history
        _state.State.PriceHistory.Add(new PriceHistoryEntryState
        {
            Timestamp = DateTime.UtcNow,
            Price = command.CurrentPrice,
            PricePerUnit = pricePerUnit,
            ChangePercent = 0,
            SupplierId = command.PreferredSupplierId,
            ChangeReason = "Initial price"
        });

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<IngredientPriceSnapshot> UpdateAsync(UpdateIngredientPriceCommand command)
    {
        EnsureExists();

        if (command.CurrentPrice.HasValue)
        {
            var previousPrice = _state.State.CurrentPrice;
            _state.State.PreviousPrice = previousPrice;
            _state.State.CurrentPrice = command.CurrentPrice.Value;
            _state.State.PriceChangedAt = DateTime.UtcNow;

            if (previousPrice > 0)
            {
                _state.State.PriceChangePercent = ((command.CurrentPrice.Value - previousPrice) / previousPrice) * 100;
            }

            var packSize = command.PackSize ?? _state.State.PackSize;
            _state.State.PricePerUnit = packSize > 0 ? command.CurrentPrice.Value / packSize : command.CurrentPrice.Value;
        }

        if (command.PackSize.HasValue && command.PackSize.Value > 0)
        {
            _state.State.PackSize = command.PackSize.Value;
            _state.State.PricePerUnit = _state.State.CurrentPrice / command.PackSize.Value;
        }

        if (command.PreferredSupplierId.HasValue) _state.State.PreferredSupplierId = command.PreferredSupplierId;
        if (command.PreferredSupplierName != null) _state.State.PreferredSupplierName = command.PreferredSupplierName;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;

        _state.State.UpdatedAt = DateTime.UtcNow;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<IngredientPriceSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.Id != Guid.Empty);
    }

    public async Task DeleteAsync()
    {
        EnsureExists();
        _state.State.IsActive = false;
        _state.State.UpdatedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task<IngredientPriceSnapshot> UpdatePriceAsync(decimal newPrice, string? changeReason = null)
    {
        EnsureExists();

        var previousPrice = _state.State.CurrentPrice;
        var changePercent = previousPrice > 0 ? ((newPrice - previousPrice) / previousPrice) * 100 : 0;

        _state.State.PreviousPrice = previousPrice;
        _state.State.CurrentPrice = newPrice;
        _state.State.PricePerUnit = _state.State.PackSize > 0 ? newPrice / _state.State.PackSize : newPrice;
        _state.State.PriceChangedAt = DateTime.UtcNow;
        _state.State.PriceChangePercent = changePercent;
        _state.State.UpdatedAt = DateTime.UtcNow;

        // Record in history
        _state.State.PriceHistory.Add(new PriceHistoryEntryState
        {
            Timestamp = DateTime.UtcNow,
            Price = newPrice,
            PricePerUnit = _state.State.PricePerUnit,
            ChangePercent = changePercent,
            SupplierId = _state.State.PreferredSupplierId,
            ChangeReason = changeReason
        });

        // Trim history if too long
        if (_state.State.PriceHistory.Count > MaxPriceHistory)
        {
            _state.State.PriceHistory = _state.State.PriceHistory
                .OrderByDescending(h => h.Timestamp)
                .Take(MaxPriceHistory)
                .ToList();
        }

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<decimal> GetPricePerUnitAsync()
    {
        EnsureExists();
        return Task.FromResult(_state.State.PricePerUnit);
    }

    public Task<IReadOnlyList<PriceHistoryEntry>> GetPriceHistoryAsync(int count = 20)
    {
        var history = _state.State.PriceHistory
            .OrderByDescending(h => h.Timestamp)
            .Take(count)
            .Select(h => new PriceHistoryEntry(
                h.Timestamp,
                h.Price,
                h.PricePerUnit,
                h.ChangePercent,
                h.SupplierId,
                h.ChangeReason))
            .ToList();

        return Task.FromResult<IReadOnlyList<PriceHistoryEntry>>(history);
    }

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Ingredient price not found");
    }

    private IngredientPriceSnapshot CreateSnapshot()
    {
        return new IngredientPriceSnapshot(
            _state.State.Id,
            _state.State.IngredientId,
            _state.State.IngredientName,
            _state.State.CurrentPrice,
            _state.State.UnitOfMeasure,
            _state.State.PackSize,
            _state.State.PricePerUnit,
            _state.State.PreferredSupplierId,
            _state.State.PreferredSupplierName,
            _state.State.PreviousPrice,
            _state.State.PriceChangedAt,
            _state.State.PriceChangePercent,
            _state.State.IsActive);
    }
}

// ============================================================================
// Cost Alert Grain Implementation
// ============================================================================

public class CostAlertGrain : Grain, ICostAlertGrain
{
    private readonly IPersistentState<CostAlertState> _state;

    public CostAlertGrain(
        [PersistentState("costalert", "OrleansStorage")]
        IPersistentState<CostAlertState> state)
    {
        _state = state;
    }

    public async Task<CostAlertSnapshot> CreateAsync(CreateCostAlertCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Cost alert already exists");

        var (orgId, _, alertId) = GrainKeys.ParseOrgEntity(this.GetPrimaryKeyString());

        var changePercent = command.PreviousValue != 0
            ? ((command.CurrentValue - command.PreviousValue) / Math.Abs(command.PreviousValue)) * 100
            : 0;

        _state.State = new CostAlertState
        {
            Id = alertId,
            OrganizationId = orgId,
            AlertType = command.AlertType,
            RecipeId = command.RecipeId,
            RecipeName = command.RecipeName,
            IngredientId = command.IngredientId,
            IngredientName = command.IngredientName,
            MenuItemId = command.MenuItemId,
            MenuItemName = command.MenuItemName,
            PreviousValue = command.PreviousValue,
            CurrentValue = command.CurrentValue,
            ChangePercent = changePercent,
            ThresholdValue = command.ThresholdValue,
            ImpactDescription = command.ImpactDescription,
            AffectedRecipeCount = command.AffectedRecipeCount,
            IsAcknowledged = false,
            ActionTaken = CostAlertAction.None,
            CreatedAt = DateTime.UtcNow
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<CostAlertSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.Id != Guid.Empty);
    }

    public async Task<CostAlertSnapshot> AcknowledgeAsync(AcknowledgeCostAlertCommand command)
    {
        EnsureExists();

        if (_state.State.IsAcknowledged)
            throw new InvalidOperationException("Alert has already been acknowledged");

        _state.State.IsAcknowledged = true;
        _state.State.AcknowledgedAt = DateTime.UtcNow;
        _state.State.AcknowledgedByUserId = command.AcknowledgedByUserId;
        _state.State.Notes = command.Notes;
        _state.State.ActionTaken = command.ActionTaken;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<bool> IsAcknowledgedAsync()
    {
        return Task.FromResult(_state.State.IsAcknowledged);
    }

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Cost alert not found");
    }

    private CostAlertSnapshot CreateSnapshot()
    {
        return new CostAlertSnapshot(
            _state.State.Id,
            _state.State.AlertType,
            _state.State.RecipeId,
            _state.State.RecipeName,
            _state.State.IngredientId,
            _state.State.IngredientName,
            _state.State.MenuItemId,
            _state.State.MenuItemName,
            _state.State.PreviousValue,
            _state.State.CurrentValue,
            _state.State.ChangePercent,
            _state.State.ThresholdValue,
            _state.State.ImpactDescription,
            _state.State.AffectedRecipeCount,
            _state.State.IsAcknowledged,
            _state.State.AcknowledgedAt,
            _state.State.AcknowledgedByUserId,
            _state.State.Notes,
            _state.State.ActionTaken,
            _state.State.CreatedAt);
    }
}

// ============================================================================
// Costing Settings Grain Implementation
// ============================================================================

public class CostingSettingsGrain : Grain, ICostingSettingsGrain
{
    private readonly IPersistentState<CostingSettingsState> _state;

    public CostingSettingsGrain(
        [PersistentState("costingsettings", "OrleansStorage")]
        IPersistentState<CostingSettingsState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(Guid locationId)
    {
        if (_state.State.Id != Guid.Empty)
            return; // Already initialized

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State = new CostingSettingsState
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            LocationId = locationId,
            TargetFoodCostPercent = 30,
            TargetBeverageCostPercent = 25,
            MinimumMarginPercent = 50,
            WarningMarginPercent = 60,
            PriceChangeAlertThreshold = 10,
            CostIncreaseAlertThreshold = 5,
            AutoRecalculateCosts = true,
            AutoCreateSnapshots = true,
            SnapshotFrequencyDays = 7,
            CreatedAt = DateTime.UtcNow
        };

        await _state.WriteStateAsync();
    }

    public Task<CostingSettingsSnapshot> GetSettingsAsync()
    {
        EnsureExists();
        return Task.FromResult(CreateSnapshot());
    }

    public async Task<CostingSettingsSnapshot> UpdateAsync(UpdateCostingSettingsCommand command)
    {
        EnsureExists();

        if (command.TargetFoodCostPercent.HasValue)
            _state.State.TargetFoodCostPercent = command.TargetFoodCostPercent.Value;
        if (command.TargetBeverageCostPercent.HasValue)
            _state.State.TargetBeverageCostPercent = command.TargetBeverageCostPercent.Value;
        if (command.MinimumMarginPercent.HasValue)
            _state.State.MinimumMarginPercent = command.MinimumMarginPercent.Value;
        if (command.WarningMarginPercent.HasValue)
            _state.State.WarningMarginPercent = command.WarningMarginPercent.Value;
        if (command.PriceChangeAlertThreshold.HasValue)
            _state.State.PriceChangeAlertThreshold = command.PriceChangeAlertThreshold.Value;
        if (command.CostIncreaseAlertThreshold.HasValue)
            _state.State.CostIncreaseAlertThreshold = command.CostIncreaseAlertThreshold.Value;
        if (command.AutoRecalculateCosts.HasValue)
            _state.State.AutoRecalculateCosts = command.AutoRecalculateCosts.Value;
        if (command.AutoCreateSnapshots.HasValue)
            _state.State.AutoCreateSnapshots = command.AutoCreateSnapshots.Value;
        if (command.SnapshotFrequencyDays.HasValue && command.SnapshotFrequencyDays.Value > 0)
            _state.State.SnapshotFrequencyDays = command.SnapshotFrequencyDays.Value;

        _state.State.UpdatedAt = DateTime.UtcNow;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.Id != Guid.Empty);
    }

    public Task<bool> ShouldAlertOnPriceChangeAsync(decimal changePercent)
    {
        return Task.FromResult(Math.Abs(changePercent) >= _state.State.PriceChangeAlertThreshold);
    }

    public Task<bool> ShouldAlertOnCostIncreaseAsync(decimal changePercent)
    {
        return Task.FromResult(changePercent >= _state.State.CostIncreaseAlertThreshold);
    }

    public Task<bool> IsMarginBelowMinimumAsync(decimal marginPercent)
    {
        return Task.FromResult(marginPercent < _state.State.MinimumMarginPercent);
    }

    public Task<bool> IsMarginBelowWarningAsync(decimal marginPercent)
    {
        return Task.FromResult(marginPercent < _state.State.WarningMarginPercent);
    }

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Costing settings not found - call InitializeAsync first");
    }

    private CostingSettingsSnapshot CreateSnapshot()
    {
        return new CostingSettingsSnapshot(
            _state.State.LocationId,
            _state.State.TargetFoodCostPercent,
            _state.State.TargetBeverageCostPercent,
            _state.State.MinimumMarginPercent,
            _state.State.WarningMarginPercent,
            _state.State.PriceChangeAlertThreshold,
            _state.State.CostIncreaseAlertThreshold,
            _state.State.AutoRecalculateCosts,
            _state.State.AutoCreateSnapshots,
            _state.State.SnapshotFrequencyDays);
    }
}
