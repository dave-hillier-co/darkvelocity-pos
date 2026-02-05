using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Ingredient Grain Implementation (Event Sourced)
// ============================================================================

/// <summary>
/// Event-sourced grain for ingredient master data management.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class IngredientGrain : JournaledGrain<IngredientState, IIngredientEvent>, IIngredientGrain
{
    /// <summary>
    /// Applies domain events to mutate state.
    /// </summary>
    protected override void TransitionState(IngredientState state, IIngredientEvent @event)
    {
        switch (@event)
        {
            case IngredientCreated e:
                state.OrgId = e.OrgId;
                state.IngredientId = e.IngredientId;
                state.IsCreated = true;
                state.Name = e.Name;
                state.Description = e.Description;
                state.Sku = e.Sku;
                state.UnitOfMeasure = new IngredientUnitOfMeasure { BaseUnit = e.BaseUnit };
                state.DefaultCostPerUnit = e.DefaultCostPerUnit;
                state.CostUnit = e.CostUnit;
                state.LastCostUpdate = e.OccurredAt;
                state.Category = e.Category;
                state.Tags = e.Tags?.ToList() ?? [];
                state.CreatedAt = e.OccurredAt;

                if (e.Allergens != null)
                {
                    state.Allergens = e.Allergens.Select(a => new AllergenDeclaration
                    {
                        Allergen = a.Allergen,
                        DeclarationType = a.DeclarationType,
                        Notes = a.Notes
                    }).ToList();
                }

                if (e.Nutrition != null)
                {
                    state.Nutrition = new IngredientNutrition
                    {
                        CaloriesPer100g = e.Nutrition.CaloriesPer100g,
                        ProteinPer100g = e.Nutrition.ProteinPer100g,
                        CarbohydratesPer100g = e.Nutrition.CarbohydratesPer100g,
                        FatPer100g = e.Nutrition.FatPer100g,
                        SaturatedFatPer100g = e.Nutrition.SaturatedFatPer100g,
                        FiberPer100g = e.Nutrition.FiberPer100g,
                        SugarPer100g = e.Nutrition.SugarPer100g,
                        SodiumPer100g = e.Nutrition.SodiumPer100g,
                        IsPerMilliliter = e.Nutrition.IsPerMilliliter
                    };
                }

                state.CostHistory.Add(new IngredientCostHistory
                {
                    CostPerUnit = e.DefaultCostPerUnit,
                    Unit = e.CostUnit,
                    EffectiveDate = e.OccurredAt,
                    UpdatedBy = e.CreatedBy,
                    Source = "Initial creation"
                });
                break;

            case IngredientUpdated e:
                if (e.Name != null) state.Name = e.Name;
                if (e.Description != null) state.Description = e.Description;
                if (e.Sku != null) state.Sku = e.Sku;
                if (e.Category != null) state.Category = e.Category;
                if (e.Tags != null) state.Tags = e.Tags.ToList();
                break;

            case IngredientCostUpdated e:
                state.DefaultCostPerUnit = e.NewCost;
                state.CostUnit = e.CostUnit;
                state.LastCostUpdate = e.OccurredAt;
                state.CostHistory.Add(new IngredientCostHistory
                {
                    CostPerUnit = e.NewCost,
                    Unit = e.CostUnit,
                    EffectiveDate = e.OccurredAt,
                    SupplierId = e.SupplierId,
                    Source = e.Source,
                    UpdatedBy = e.UpdatedBy
                });
                break;

            case IngredientAllergensUpdated e:
                state.Allergens = e.Allergens.Select(a => new AllergenDeclaration
                {
                    Allergen = a.Allergen,
                    DeclarationType = a.DeclarationType,
                    Notes = a.Notes
                }).ToList();
                break;

            case IngredientNutritionUpdated e:
                if (e.Nutrition != null)
                {
                    state.Nutrition = new IngredientNutrition
                    {
                        CaloriesPer100g = e.Nutrition.CaloriesPer100g,
                        ProteinPer100g = e.Nutrition.ProteinPer100g,
                        CarbohydratesPer100g = e.Nutrition.CarbohydratesPer100g,
                        FatPer100g = e.Nutrition.FatPer100g,
                        SaturatedFatPer100g = e.Nutrition.SaturatedFatPer100g,
                        FiberPer100g = e.Nutrition.FiberPer100g,
                        SugarPer100g = e.Nutrition.SugarPer100g,
                        SodiumPer100g = e.Nutrition.SodiumPer100g,
                        IsPerMilliliter = e.Nutrition.IsPerMilliliter
                    };
                }
                else
                {
                    state.Nutrition = null;
                }
                break;

            case IngredientSupplierLinked e:
                var existingSupplier = state.Suppliers.FirstOrDefault(s => s.SupplierId == e.SupplierId);
                if (existingSupplier != null)
                {
                    existingSupplier.SupplierName = e.SupplierName;
                    existingSupplier.SupplierSku = e.SupplierSku;
                    existingSupplier.SupplierPrice = e.SupplierPrice;
                    existingSupplier.SupplierUnit = e.SupplierUnit;
                    existingSupplier.ConversionToBaseUnit = e.ConversionToBaseUnit;
                    existingSupplier.IsPreferred = e.IsPreferred;
                    existingSupplier.LastPriceUpdate = e.OccurredAt;
                }
                else
                {
                    // If this is preferred, unset any existing preferred supplier
                    if (e.IsPreferred)
                    {
                        foreach (var s in state.Suppliers)
                            s.IsPreferred = false;
                    }

                    state.Suppliers.Add(new IngredientSupplierLink
                    {
                        SupplierId = e.SupplierId,
                        SupplierName = e.SupplierName,
                        SupplierSku = e.SupplierSku,
                        SupplierPrice = e.SupplierPrice,
                        SupplierUnit = e.SupplierUnit,
                        ConversionToBaseUnit = e.ConversionToBaseUnit,
                        IsPreferred = e.IsPreferred,
                        LastPriceUpdate = e.OccurredAt
                    });
                }
                break;

            case IngredientSupplierUnlinked e:
                state.Suppliers.RemoveAll(s => s.SupplierId == e.SupplierId);
                break;

            case IngredientUnitConversionsUpdated e:
                state.UnitOfMeasure.BaseUnit = e.BaseUnit;
                state.UnitOfMeasure.Conversions = new Dictionary<string, decimal>(e.Conversions);
                break;

            case IngredientLinkedToSubRecipe e:
                state.ProducedByRecipeId = e.RecipeDocumentId;
                state.IsSubRecipeOutput = true;
                break;

            case IngredientUnlinkedFromSubRecipe e:
                state.ProducedByRecipeId = null;
                state.IsSubRecipeOutput = false;
                break;

            case IngredientArchived e:
                state.IsArchived = true;
                state.ArchivedAt = e.OccurredAt;
                break;

            case IngredientRestored e:
                state.IsArchived = false;
                state.ArchivedAt = null;
                break;
        }
    }

    public async Task<IngredientSnapshot> CreateAsync(CreateIngredientCommand command)
    {
        if (State.IsCreated)
            throw new InvalidOperationException("Ingredient already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var ingredientId = Guid.Parse(parts[2]);

        var allergenData = command.Allergens?.Select(a => new AllergenDeclarationData(
            a.Allergen, a.DeclarationType, a.Notes)).ToList();

        IngredientNutritionData? nutritionData = command.Nutrition != null
            ? new IngredientNutritionData(
                command.Nutrition.CaloriesPer100g,
                command.Nutrition.ProteinPer100g,
                command.Nutrition.CarbohydratesPer100g,
                command.Nutrition.FatPer100g,
                command.Nutrition.SaturatedFatPer100g,
                command.Nutrition.FiberPer100g,
                command.Nutrition.SugarPer100g,
                command.Nutrition.SodiumPer100g,
                command.Nutrition.IsPerMilliliter)
            : null;

        RaiseEvent(new IngredientCreated(
            IngredientId: ingredientId,
            OrgId: orgId,
            OccurredAt: DateTimeOffset.UtcNow,
            Name: command.Name,
            Description: command.Description,
            Sku: command.Sku,
            BaseUnit: command.BaseUnit,
            DefaultCostPerUnit: command.DefaultCostPerUnit,
            CostUnit: command.CostUnit,
            Allergens: allergenData,
            Nutrition: nutritionData,
            Category: command.Category,
            Tags: command.Tags?.ToList(),
            CreatedBy: command.CreatedBy
        ));

        await ConfirmEvents();
        return GetSnapshot();
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(State.IsCreated);
    }

    public Task<IngredientSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(GetSnapshot());
    }

    public async Task<IngredientSnapshot> UpdateAsync(UpdateIngredientCommand command)
    {
        EnsureInitialized();

        RaiseEvent(new IngredientUpdated(
            IngredientId: State.IngredientId,
            OccurredAt: DateTimeOffset.UtcNow,
            Name: command.Name,
            Description: command.Description,
            Sku: command.Sku,
            Category: command.Category,
            Tags: command.Tags?.ToList(),
            UpdatedBy: command.UpdatedBy
        ));

        await ConfirmEvents();
        return GetSnapshot();
    }

    public async Task UpdateCostAsync(UpdateIngredientCostCommand command)
    {
        EnsureInitialized();

        RaiseEvent(new IngredientCostUpdated(
            IngredientId: State.IngredientId,
            OccurredAt: DateTimeOffset.UtcNow,
            PreviousCost: State.DefaultCostPerUnit,
            NewCost: command.NewCost,
            CostUnit: command.CostUnit ?? State.CostUnit,
            SupplierId: command.SupplierId,
            Source: command.Source,
            UpdatedBy: command.UpdatedBy
        ));

        await ConfirmEvents();
    }

    public async Task UpdateAllergensAsync(IReadOnlyList<AllergenDeclarationCommand> allergens, Guid? updatedBy = null)
    {
        EnsureInitialized();

        var allergenData = allergens.Select(a => new AllergenDeclarationData(
            a.Allergen, a.DeclarationType, a.Notes)).ToList();

        RaiseEvent(new IngredientAllergensUpdated(
            IngredientId: State.IngredientId,
            OccurredAt: DateTimeOffset.UtcNow,
            Allergens: allergenData,
            UpdatedBy: updatedBy
        ));

        await ConfirmEvents();
    }

    public async Task UpdateNutritionAsync(IngredientNutritionCommand? nutrition, Guid? updatedBy = null)
    {
        EnsureInitialized();

        IngredientNutritionData? nutritionData = nutrition != null
            ? new IngredientNutritionData(
                nutrition.CaloriesPer100g,
                nutrition.ProteinPer100g,
                nutrition.CarbohydratesPer100g,
                nutrition.FatPer100g,
                nutrition.SaturatedFatPer100g,
                nutrition.FiberPer100g,
                nutrition.SugarPer100g,
                nutrition.SodiumPer100g,
                nutrition.IsPerMilliliter)
            : null;

        RaiseEvent(new IngredientNutritionUpdated(
            IngredientId: State.IngredientId,
            OccurredAt: DateTimeOffset.UtcNow,
            Nutrition: nutritionData,
            UpdatedBy: updatedBy
        ));

        await ConfirmEvents();
    }

    public async Task UpdateUnitConversionsAsync(UpdateUnitConversionsCommand command)
    {
        EnsureInitialized();

        RaiseEvent(new IngredientUnitConversionsUpdated(
            IngredientId: State.IngredientId,
            OccurredAt: DateTimeOffset.UtcNow,
            BaseUnit: command.BaseUnit,
            Conversions: new Dictionary<string, decimal>(command.Conversions)
        ));

        await ConfirmEvents();
    }

    public async Task LinkSupplierAsync(LinkSupplierCommand command)
    {
        EnsureInitialized();

        RaiseEvent(new IngredientSupplierLinked(
            IngredientId: State.IngredientId,
            OccurredAt: DateTimeOffset.UtcNow,
            SupplierId: command.SupplierId,
            SupplierName: command.SupplierName,
            SupplierSku: command.SupplierSku,
            SupplierPrice: command.SupplierPrice,
            SupplierUnit: command.SupplierUnit,
            ConversionToBaseUnit: command.ConversionToBaseUnit,
            IsPreferred: command.IsPreferred
        ));

        await ConfirmEvents();
    }

    public async Task UnlinkSupplierAsync(Guid supplierId)
    {
        EnsureInitialized();

        if (State.Suppliers.Any(s => s.SupplierId == supplierId))
        {
            RaiseEvent(new IngredientSupplierUnlinked(
                IngredientId: State.IngredientId,
                OccurredAt: DateTimeOffset.UtcNow,
                SupplierId: supplierId
            ));

            await ConfirmEvents();
        }
    }

    public Task<IReadOnlyList<IngredientSupplierSnapshot>> GetSuppliersAsync()
    {
        EnsureInitialized();
        var suppliers = State.Suppliers.Select(s => new IngredientSupplierSnapshot(
            SupplierId: s.SupplierId,
            SupplierName: s.SupplierName,
            SupplierSku: s.SupplierSku,
            SupplierPrice: s.SupplierPrice,
            SupplierUnit: s.SupplierUnit,
            ConversionToBaseUnit: s.ConversionToBaseUnit,
            IsPreferred: s.IsPreferred,
            LastPriceUpdate: s.LastPriceUpdate
        )).ToList();
        return Task.FromResult<IReadOnlyList<IngredientSupplierSnapshot>>(suppliers);
    }

    public async Task LinkToSubRecipeAsync(string recipeDocumentId)
    {
        EnsureInitialized();

        RaiseEvent(new IngredientLinkedToSubRecipe(
            IngredientId: State.IngredientId,
            OccurredAt: DateTimeOffset.UtcNow,
            RecipeDocumentId: recipeDocumentId
        ));

        await ConfirmEvents();
    }

    public async Task UnlinkFromSubRecipeAsync()
    {
        EnsureInitialized();

        if (State.ProducedByRecipeId != null)
        {
            RaiseEvent(new IngredientUnlinkedFromSubRecipe(
                IngredientId: State.IngredientId,
                OccurredAt: DateTimeOffset.UtcNow,
                RecipeDocumentId: State.ProducedByRecipeId
            ));

            await ConfirmEvents();
        }
    }

    public Task<IReadOnlyList<AllergenDeclarationSnapshot>> GetAllergensAsync()
    {
        EnsureInitialized();
        var allergens = State.Allergens.Select(a => new AllergenDeclarationSnapshot(
            Allergen: a.Allergen,
            DeclarationType: a.DeclarationType,
            Notes: a.Notes
        )).ToList();
        return Task.FromResult<IReadOnlyList<AllergenDeclarationSnapshot>>(allergens);
    }

    public Task<bool> ContainsAllergenAsync(string allergen)
    {
        EnsureInitialized();
        return Task.FromResult(State.Allergens.Any(a =>
            a.Allergen.Equals(allergen, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<IngredientNutritionSnapshot?> GetNutritionAsync()
    {
        EnsureInitialized();
        if (State.Nutrition == null)
            return Task.FromResult<IngredientNutritionSnapshot?>(null);

        return Task.FromResult<IngredientNutritionSnapshot?>(new IngredientNutritionSnapshot(
            CaloriesPer100g: State.Nutrition.CaloriesPer100g,
            ProteinPer100g: State.Nutrition.ProteinPer100g,
            CarbohydratesPer100g: State.Nutrition.CarbohydratesPer100g,
            FatPer100g: State.Nutrition.FatPer100g,
            SaturatedFatPer100g: State.Nutrition.SaturatedFatPer100g,
            FiberPer100g: State.Nutrition.FiberPer100g,
            SugarPer100g: State.Nutrition.SugarPer100g,
            SodiumPer100g: State.Nutrition.SodiumPer100g,
            IsPerMilliliter: State.Nutrition.IsPerMilliliter
        ));
    }

    public Task<decimal> GetCostInUnitAsync(string unit)
    {
        EnsureInitialized();

        if (unit == State.CostUnit)
            return Task.FromResult(State.DefaultCostPerUnit);

        // Convert using unit conversions
        try
        {
            var costInBase = State.DefaultCostPerUnit;

            // If cost unit is not base unit, convert to base first
            if (State.CostUnit != State.UnitOfMeasure.BaseUnit)
            {
                if (State.UnitOfMeasure.Conversions.TryGetValue(State.CostUnit, out var costFactor))
                    costInBase = State.DefaultCostPerUnit / costFactor;
            }

            // Convert from base to requested unit
            if (unit != State.UnitOfMeasure.BaseUnit)
            {
                if (State.UnitOfMeasure.Conversions.TryGetValue(unit, out var targetFactor))
                    return Task.FromResult(costInBase * targetFactor);
            }

            return Task.FromResult(costInBase);
        }
        catch
        {
            return Task.FromResult(State.DefaultCostPerUnit);
        }
    }

    public Task<IReadOnlyList<IngredientCostHistorySnapshot>> GetCostHistoryAsync(int take = 20)
    {
        EnsureInitialized();
        var history = State.CostHistory
            .OrderByDescending(h => h.EffectiveDate)
            .Take(take)
            .Select(h => new IngredientCostHistorySnapshot(
                CostPerUnit: h.CostPerUnit,
                Unit: h.Unit,
                EffectiveDate: h.EffectiveDate,
                SupplierId: h.SupplierId,
                Source: h.Source
            ))
            .ToList();
        return Task.FromResult<IReadOnlyList<IngredientCostHistorySnapshot>>(history);
    }

    public async Task ArchiveAsync(Guid? archivedBy = null, string? reason = null)
    {
        EnsureInitialized();

        RaiseEvent(new IngredientArchived(
            IngredientId: State.IngredientId,
            OccurredAt: DateTimeOffset.UtcNow,
            ArchivedBy: archivedBy,
            Reason: reason
        ));

        await ConfirmEvents();
    }

    public async Task RestoreAsync(Guid? restoredBy = null)
    {
        EnsureInitialized();

        RaiseEvent(new IngredientRestored(
            IngredientId: State.IngredientId,
            OccurredAt: DateTimeOffset.UtcNow,
            RestoredBy: restoredBy
        ));

        await ConfirmEvents();
    }

    public async Task<IReadOnlyList<IIngredientEvent>> GetEventHistoryAsync(int fromVersion = 0, int maxCount = 100)
    {
        var events = await RetrieveConfirmedEvents(fromVersion, maxCount);
        return events.ToList();
    }

    private IngredientSnapshot GetSnapshot()
    {
        return new IngredientSnapshot(
            IngredientId: State.IngredientId,
            OrgId: State.OrgId,
            Name: State.Name,
            Description: State.Description,
            Sku: State.Sku,
            BaseUnit: State.UnitOfMeasure.BaseUnit,
            DefaultCostPerUnit: State.DefaultCostPerUnit,
            CostUnit: State.CostUnit,
            LastCostUpdate: State.LastCostUpdate,
            Allergens: State.Allergens.Select(a => new AllergenDeclarationSnapshot(
                a.Allergen, a.DeclarationType, a.Notes)).ToList(),
            Nutrition: State.Nutrition != null ? new IngredientNutritionSnapshot(
                State.Nutrition.CaloriesPer100g,
                State.Nutrition.ProteinPer100g,
                State.Nutrition.CarbohydratesPer100g,
                State.Nutrition.FatPer100g,
                State.Nutrition.SaturatedFatPer100g,
                State.Nutrition.FiberPer100g,
                State.Nutrition.SugarPer100g,
                State.Nutrition.SodiumPer100g,
                State.Nutrition.IsPerMilliliter) : null,
            Suppliers: State.Suppliers.Select(s => new IngredientSupplierSnapshot(
                s.SupplierId, s.SupplierName, s.SupplierSku, s.SupplierPrice,
                s.SupplierUnit, s.ConversionToBaseUnit, s.IsPreferred, s.LastPriceUpdate)).ToList(),
            UnitConversions: State.UnitOfMeasure.Conversions,
            Category: State.Category,
            Tags: State.Tags,
            ProducedByRecipeId: State.ProducedByRecipeId,
            IsSubRecipeOutput: State.IsSubRecipeOutput,
            IsArchived: State.IsArchived,
            CreatedAt: State.CreatedAt
        );
    }

    private void EnsureInitialized()
    {
        if (!State.IsCreated)
            throw new InvalidOperationException("Ingredient not initialized");
    }
}

// ============================================================================
// Ingredient Registry Grain Implementation
// ============================================================================

/// <summary>
/// Grain for maintaining a registry of ingredients.
/// </summary>
public class IngredientRegistryGrain : Grain, IIngredientRegistryGrain
{
    private readonly IPersistentState<IngredientRegistryState> _state;

    private IngredientRegistryState State => _state.State;

    public IngredientRegistryGrain(
        [PersistentState("ingredientRegistry", "OrleansStorage")]
        IPersistentState<IngredientRegistryState> state)
    {
        _state = state;
    }

    public async Task RegisterIngredientAsync(IngredientSummary summary)
    {
        await EnsureInitializedAsync();

        State.Ingredients[summary.IngredientId] = new IngredientRegistryEntry
        {
            IngredientId = summary.IngredientId,
            Name = summary.Name,
            Sku = summary.Sku,
            Category = summary.Category,
            DefaultCostPerUnit = summary.DefaultCostPerUnit,
            BaseUnit = summary.BaseUnit,
            AllergenTags = summary.AllergenTags.ToList(),
            IsSubRecipeOutput = summary.IsSubRecipeOutput,
            IsArchived = summary.IsArchived,
            LastModified = DateTimeOffset.UtcNow
        };

        await _state.WriteStateAsync();
    }

    public async Task UpdateIngredientAsync(IngredientSummary summary)
    {
        await EnsureInitializedAsync();

        if (State.Ingredients.TryGetValue(summary.IngredientId, out var entry))
        {
            entry.Name = summary.Name;
            entry.Sku = summary.Sku;
            entry.Category = summary.Category;
            entry.DefaultCostPerUnit = summary.DefaultCostPerUnit;
            entry.BaseUnit = summary.BaseUnit;
            entry.AllergenTags = summary.AllergenTags.ToList();
            entry.IsSubRecipeOutput = summary.IsSubRecipeOutput;
            entry.IsArchived = summary.IsArchived;
            entry.LastModified = DateTimeOffset.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task UnregisterIngredientAsync(Guid ingredientId)
    {
        await EnsureInitializedAsync();
        State.Ingredients.Remove(ingredientId);
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<IngredientSummary>> GetIngredientsAsync(
        string? category = null,
        bool includeArchived = false,
        bool? isSubRecipeOutput = null)
    {
        var ingredients = State.Ingredients.Values
            .Where(i => includeArchived || !i.IsArchived)
            .Where(i => category == null || i.Category == category)
            .Where(i => isSubRecipeOutput == null || i.IsSubRecipeOutput == isSubRecipeOutput)
            .OrderBy(i => i.Name)
            .Select(i => new IngredientSummary(
                IngredientId: i.IngredientId,
                Name: i.Name,
                Sku: i.Sku,
                Category: i.Category,
                DefaultCostPerUnit: i.DefaultCostPerUnit,
                BaseUnit: i.BaseUnit,
                AllergenTags: i.AllergenTags,
                IsSubRecipeOutput: i.IsSubRecipeOutput,
                IsArchived: i.IsArchived,
                LastModified: i.LastModified))
            .ToList();

        return Task.FromResult<IReadOnlyList<IngredientSummary>>(ingredients);
    }

    public Task<IReadOnlyList<IngredientSummary>> SearchIngredientsAsync(string query, int take = 20)
    {
        var lowerQuery = query.ToLowerInvariant();
        var ingredients = State.Ingredients.Values
            .Where(i => !i.IsArchived)
            .Where(i => i.Name.ToLowerInvariant().Contains(lowerQuery) ||
                       (i.Sku?.ToLowerInvariant().Contains(lowerQuery) ?? false))
            .Take(take)
            .Select(i => new IngredientSummary(
                IngredientId: i.IngredientId,
                Name: i.Name,
                Sku: i.Sku,
                Category: i.Category,
                DefaultCostPerUnit: i.DefaultCostPerUnit,
                BaseUnit: i.BaseUnit,
                AllergenTags: i.AllergenTags,
                IsSubRecipeOutput: i.IsSubRecipeOutput,
                IsArchived: i.IsArchived,
                LastModified: i.LastModified))
            .ToList();

        return Task.FromResult<IReadOnlyList<IngredientSummary>>(ingredients);
    }

    public Task<IReadOnlyList<IngredientSummary>> GetIngredientsByAllergenAsync(string allergen)
    {
        var lowerAllergen = allergen.ToLowerInvariant();
        var ingredients = State.Ingredients.Values
            .Where(i => !i.IsArchived)
            .Where(i => i.AllergenTags.Any(a => a.ToLowerInvariant() == lowerAllergen))
            .OrderBy(i => i.Name)
            .Select(i => new IngredientSummary(
                IngredientId: i.IngredientId,
                Name: i.Name,
                Sku: i.Sku,
                Category: i.Category,
                DefaultCostPerUnit: i.DefaultCostPerUnit,
                BaseUnit: i.BaseUnit,
                AllergenTags: i.AllergenTags,
                IsSubRecipeOutput: i.IsSubRecipeOutput,
                IsArchived: i.IsArchived,
                LastModified: i.LastModified))
            .ToList();

        return Task.FromResult<IReadOnlyList<IngredientSummary>>(ingredients);
    }

    public Task<IReadOnlyList<IngredientSummary>> GetSubRecipeOutputsAsync()
    {
        var ingredients = State.Ingredients.Values
            .Where(i => !i.IsArchived && i.IsSubRecipeOutput)
            .OrderBy(i => i.Name)
            .Select(i => new IngredientSummary(
                IngredientId: i.IngredientId,
                Name: i.Name,
                Sku: i.Sku,
                Category: i.Category,
                DefaultCostPerUnit: i.DefaultCostPerUnit,
                BaseUnit: i.BaseUnit,
                AllergenTags: i.AllergenTags,
                IsSubRecipeOutput: i.IsSubRecipeOutput,
                IsArchived: i.IsArchived,
                LastModified: i.LastModified))
            .ToList();

        return Task.FromResult<IReadOnlyList<IngredientSummary>>(ingredients);
    }

    private async Task EnsureInitializedAsync()
    {
        if (!State.IsCreated)
        {
            var key = this.GetPrimaryKeyString();
            var parts = key.Split(':');
            State.OrgId = Guid.Parse(parts[0]);
            State.IsCreated = true;
            await _state.WriteStateAsync();
        }
    }
}
