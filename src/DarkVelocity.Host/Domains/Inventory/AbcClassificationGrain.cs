using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

public class AbcClassificationGrain : Grain, IAbcClassificationGrain
{
    private readonly IPersistentState<AbcClassificationState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<AbcClassificationGrain> _logger;

    public AbcClassificationGrain(
        [PersistentState("abcClassification", "OrleansStorage")] IPersistentState<AbcClassificationState> state,
        IGrainFactory grainFactory,
        ILogger<AbcClassificationGrain> logger)
    {
        _state = state;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async Task InitializeAsync(Guid organizationId, Guid siteId)
    {
        if (_state.State.OrganizationId != Guid.Empty)
            throw new InvalidOperationException("ABC classification already initialized");

        _state.State.OrganizationId = organizationId;
        _state.State.SiteId = siteId;
        _state.State.Settings = new AbcClassificationSettings();

        // Set default reorder policies
        _state.State.ReorderPolicies = new Dictionary<AbcClass, AbcReorderPolicy>
        {
            [AbcClass.A] = new AbcReorderPolicy
            {
                Classification = AbcClass.A,
                SafetyStockDays = 14,
                ReviewFrequencyDays = 7,
                OrderFrequencyDays = 7,
                RequiresApproval = true,
                MaxOrderValueWithoutApproval = 0 // Always requires approval
            },
            [AbcClass.B] = new AbcReorderPolicy
            {
                Classification = AbcClass.B,
                SafetyStockDays = 21,
                ReviewFrequencyDays = 14,
                OrderFrequencyDays = 14,
                RequiresApproval = false,
                MaxOrderValueWithoutApproval = 5000
            },
            [AbcClass.C] = new AbcReorderPolicy
            {
                Classification = AbcClass.C,
                SafetyStockDays = 30,
                ReviewFrequencyDays = 30,
                OrderFrequencyDays = 30,
                RequiresApproval = false,
                MaxOrderValueWithoutApproval = 10000
            }
        };

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "ABC classification initialized for site {SiteId}",
            siteId);
    }

    public async Task ConfigureAsync(AbcClassificationSettings settings)
    {
        EnsureExists();

        _state.State.Settings = settings;
        await _state.WriteStateAsync();

        _logger.LogInformation(
            "ABC classification configured: A={A}%, B={B}%, Method={Method}",
            settings.ClassAThreshold,
            settings.ClassBThreshold,
            settings.Method);
    }

    public Task<AbcClassificationSettings> GetSettingsAsync()
    {
        return Task.FromResult(_state.State.Settings);
    }

    public async Task RegisterIngredientAsync(Guid ingredientId, string ingredientName, string sku, string category)
    {
        EnsureExists();

        _state.State.Ingredients[ingredientId] = new AbcIngredientData
        {
            IngredientId = ingredientId,
            IngredientName = ingredientName,
            Sku = sku,
            Category = category,
            RegisteredAt = DateTime.UtcNow,
            Classification = AbcClass.Unclassified
        };

        await _state.WriteStateAsync();
    }

    public async Task UnregisterIngredientAsync(Guid ingredientId)
    {
        EnsureExists();

        _state.State.Ingredients.Remove(ingredientId);
        _state.State.Overrides.Remove(ingredientId);
        await _state.WriteStateAsync();
    }

    public async Task<AbcClassificationReport> ClassifyAsync()
    {
        EnsureExists();

        var now = DateTime.UtcNow;
        var settings = _state.State.Settings;
        var analysisData = new List<(AbcIngredientData Data, decimal Value)>();

        // Gather data for all ingredients
        foreach (var ingredient in _state.State.Ingredients.Values)
        {
            var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(
                GrainKeys.Inventory(_state.State.OrganizationId, _state.State.SiteId, ingredient.IngredientId));

            if (!await inventoryGrain.ExistsAsync())
                continue;

            var state = await inventoryGrain.GetStateAsync();
            var value = CalculateValue(state, settings);

            ingredient.CurrentValue = state.QuantityOnHand * state.WeightedAverageCost;
            ingredient.Velocity = CalculateVelocity(state, settings.AnalysisPeriodDays);
            ingredient.AnnualConsumptionValue = value;

            analysisData.Add((ingredient, value));
        }

        if (analysisData.Count == 0)
        {
            return CreateEmptyReport(now);
        }

        // Sort by value descending
        var sortedData = analysisData
            .OrderByDescending(x => x.Value)
            .ToList();

        var totalValue = sortedData.Sum(x => x.Value);
        var cumulativeValue = 0m;
        var rank = 0;

        // Classify items
        foreach (var (data, value) in sortedData)
        {
            rank++;
            cumulativeValue += value;
            var cumulativePercentage = totalValue > 0 ? (cumulativeValue / totalValue) * 100 : 0;

            data.PreviousClassification = data.Classification;
            data.CumulativePercentage = cumulativePercentage;
            data.Rank = rank;
            data.ClassifiedAt = now;

            // Check for manual override
            if (_state.State.Overrides.TryGetValue(data.IngredientId, out var overrideData))
            {
                data.Classification = overrideData.OverrideClassification;
            }
            else
            {
                // Apply ABC classification based on cumulative percentage
                if (cumulativePercentage <= settings.ClassAThreshold)
                {
                    data.Classification = AbcClass.A;
                }
                else if (cumulativePercentage <= settings.ClassBThreshold)
                {
                    data.Classification = AbcClass.B;
                }
                else
                {
                    data.Classification = AbcClass.C;
                }
            }
        }

        _state.State.LastClassifiedAt = now;

        // Build report
        var classAItems = _state.State.Ingredients.Values
            .Where(i => i.Classification == AbcClass.A)
            .Select(ToClassifiedItem)
            .OrderBy(i => i.Rank)
            .ToList();

        var classBItems = _state.State.Ingredients.Values
            .Where(i => i.Classification == AbcClass.B)
            .Select(ToClassifiedItem)
            .OrderBy(i => i.Rank)
            .ToList();

        var classCItems = _state.State.Ingredients.Values
            .Where(i => i.Classification == AbcClass.C)
            .Select(ToClassifiedItem)
            .OrderBy(i => i.Rank)
            .ToList();

        var reclassifiedItems = _state.State.Ingredients.Values
            .Where(i => i.PreviousClassification.HasValue &&
                       i.PreviousClassification != AbcClass.Unclassified &&
                       i.PreviousClassification != i.Classification)
            .Select(ToClassifiedItem)
            .ToList();

        var report = new AbcClassificationReport
        {
            GeneratedAt = now,
            SiteId = _state.State.SiteId,
            Method = settings.Method,
            AnalysisPeriodDays = settings.AnalysisPeriodDays,
            ClassACount = classAItems.Count,
            ClassAValue = classAItems.Sum(i => i.AnnualConsumptionValue),
            ClassAPercentage = totalValue > 0 ? (classAItems.Sum(i => i.AnnualConsumptionValue) / totalValue) * 100 : 0,
            ClassBCount = classBItems.Count,
            ClassBValue = classBItems.Sum(i => i.AnnualConsumptionValue),
            ClassBPercentage = totalValue > 0 ? (classBItems.Sum(i => i.AnnualConsumptionValue) / totalValue) * 100 : 0,
            ClassCCount = classCItems.Count,
            ClassCValue = classCItems.Sum(i => i.AnnualConsumptionValue),
            ClassCPercentage = totalValue > 0 ? (classCItems.Sum(i => i.AnnualConsumptionValue) / totalValue) * 100 : 0,
            TotalItems = _state.State.Ingredients.Count,
            TotalValue = totalValue,
            ClassAItems = classAItems,
            ClassBItems = classBItems,
            ClassCItems = classCItems,
            ReclassifiedItems = reclassifiedItems
        };

        // Cache report summary
        _state.State.CachedReport = new AbcReportCache
        {
            GeneratedAt = now,
            ClassACount = report.ClassACount,
            ClassAValue = report.ClassAValue,
            ClassBCount = report.ClassBCount,
            ClassBValue = report.ClassBValue,
            ClassCCount = report.ClassCCount,
            ClassCValue = report.ClassCValue,
            TotalItems = report.TotalItems,
            TotalValue = report.TotalValue
        };

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "ABC classification complete: A={ACount} ({AValue:C}), B={BCount} ({BValue:C}), C={CCount} ({CValue:C}). Reclassified: {Reclassified}",
            report.ClassACount, report.ClassAValue,
            report.ClassBCount, report.ClassBValue,
            report.ClassCCount, report.ClassCValue,
            reclassifiedItems.Count);

        return report;
    }

    public Task<ClassifiedItem?> GetClassificationAsync(Guid ingredientId)
    {
        EnsureExists();

        if (_state.State.Ingredients.TryGetValue(ingredientId, out var data))
        {
            return Task.FromResult<ClassifiedItem?>(ToClassifiedItem(data));
        }

        return Task.FromResult<ClassifiedItem?>(null);
    }

    public Task<IReadOnlyList<ClassifiedItem>> GetItemsByClassAsync(AbcClass classification)
    {
        EnsureExists();

        var items = _state.State.Ingredients.Values
            .Where(i => i.Classification == classification)
            .Select(ToClassifiedItem)
            .OrderBy(i => i.Rank)
            .ToList();

        return Task.FromResult<IReadOnlyList<ClassifiedItem>>(items);
    }

    public async Task<AbcClassificationReport> GetReportAsync()
    {
        // Re-run classification if needed
        if (!_state.State.LastClassifiedAt.HasValue ||
            (DateTime.UtcNow - _state.State.LastClassifiedAt.Value).TotalDays > _state.State.Settings.ReclassifyIntervalDays)
        {
            return await ClassifyAsync();
        }

        // Build report from cached data
        var classAItems = _state.State.Ingredients.Values
            .Where(i => i.Classification == AbcClass.A)
            .Select(ToClassifiedItem)
            .OrderBy(i => i.Rank)
            .ToList();

        var classBItems = _state.State.Ingredients.Values
            .Where(i => i.Classification == AbcClass.B)
            .Select(ToClassifiedItem)
            .OrderBy(i => i.Rank)
            .ToList();

        var classCItems = _state.State.Ingredients.Values
            .Where(i => i.Classification == AbcClass.C)
            .Select(ToClassifiedItem)
            .OrderBy(i => i.Rank)
            .ToList();

        var totalValue = classAItems.Sum(i => i.AnnualConsumptionValue) +
                        classBItems.Sum(i => i.AnnualConsumptionValue) +
                        classCItems.Sum(i => i.AnnualConsumptionValue);

        return new AbcClassificationReport
        {
            GeneratedAt = _state.State.LastClassifiedAt ?? DateTime.UtcNow,
            SiteId = _state.State.SiteId,
            Method = _state.State.Settings.Method,
            AnalysisPeriodDays = _state.State.Settings.AnalysisPeriodDays,
            ClassACount = classAItems.Count,
            ClassAValue = classAItems.Sum(i => i.AnnualConsumptionValue),
            ClassAPercentage = totalValue > 0 ? (classAItems.Sum(i => i.AnnualConsumptionValue) / totalValue) * 100 : 0,
            ClassBCount = classBItems.Count,
            ClassBValue = classBItems.Sum(i => i.AnnualConsumptionValue),
            ClassBPercentage = totalValue > 0 ? (classBItems.Sum(i => i.AnnualConsumptionValue) / totalValue) * 100 : 0,
            ClassCCount = classCItems.Count,
            ClassCValue = classCItems.Sum(i => i.AnnualConsumptionValue),
            ClassCPercentage = totalValue > 0 ? (classCItems.Sum(i => i.AnnualConsumptionValue) / totalValue) * 100 : 0,
            TotalItems = _state.State.Ingredients.Count,
            TotalValue = totalValue,
            ClassAItems = classAItems,
            ClassBItems = classBItems,
            ClassCItems = classCItems,
            ReclassifiedItems = []
        };
    }

    public async Task SetReorderPolicyAsync(AbcReorderPolicy policy)
    {
        EnsureExists();

        _state.State.ReorderPolicies[policy.Classification] = policy;
        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Reorder policy set for class {Class}: SafetyDays={Safety}, ReviewDays={Review}",
            policy.Classification,
            policy.SafetyStockDays,
            policy.ReviewFrequencyDays);
    }

    public Task<AbcReorderPolicy?> GetReorderPolicyAsync(AbcClass classification)
    {
        EnsureExists();

        if (_state.State.ReorderPolicies.TryGetValue(classification, out var policy))
        {
            return Task.FromResult<AbcReorderPolicy?>(policy);
        }

        return Task.FromResult<AbcReorderPolicy?>(null);
    }

    public Task<IReadOnlyList<AbcReorderPolicy>> GetAllReorderPoliciesAsync()
    {
        EnsureExists();

        return Task.FromResult<IReadOnlyList<AbcReorderPolicy>>(
            _state.State.ReorderPolicies.Values.ToList());
    }

    public Task<IReadOnlyList<ClassifiedItem>> GetReclassifiedItemsAsync()
    {
        EnsureExists();

        var reclassified = _state.State.Ingredients.Values
            .Where(i => i.PreviousClassification.HasValue &&
                       i.PreviousClassification != AbcClass.Unclassified &&
                       i.PreviousClassification != i.Classification)
            .Select(ToClassifiedItem)
            .ToList();

        return Task.FromResult<IReadOnlyList<ClassifiedItem>>(reclassified);
    }

    public async Task OverrideClassificationAsync(Guid ingredientId, AbcClass classification, string reason)
    {
        EnsureExists();

        if (!_state.State.Ingredients.ContainsKey(ingredientId))
            throw new InvalidOperationException($"Ingredient {ingredientId} not registered");

        _state.State.Overrides[ingredientId] = new AbcClassificationOverride
        {
            IngredientId = ingredientId,
            OverrideClassification = classification,
            Reason = reason,
            OverriddenAt = DateTime.UtcNow
        };

        // Apply override immediately
        _state.State.Ingredients[ingredientId].PreviousClassification =
            _state.State.Ingredients[ingredientId].Classification;
        _state.State.Ingredients[ingredientId].Classification = classification;

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Classification override set for ingredient {IngredientId} to {Class}. Reason: {Reason}",
            ingredientId,
            classification,
            reason);
    }

    public async Task ClearOverrideAsync(Guid ingredientId)
    {
        EnsureExists();

        _state.State.Overrides.Remove(ingredientId);
        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Classification override cleared for ingredient {IngredientId}",
            ingredientId);
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.OrganizationId != Guid.Empty);

    private void EnsureExists()
    {
        if (_state.State.OrganizationId == Guid.Empty)
            throw new InvalidOperationException("ABC classification not initialized");
    }

    private static decimal CalculateValue(InventoryState state, AbcClassificationSettings settings)
    {
        return settings.Method switch
        {
            ClassificationMethod.CurrentValue => state.QuantityOnHand * state.WeightedAverageCost,
            ClassificationMethod.Velocity => CalculateVelocity(state, settings.AnalysisPeriodDays),
            ClassificationMethod.Combined =>
                (state.QuantityOnHand * state.WeightedAverageCost) +
                (CalculateVelocity(state, settings.AnalysisPeriodDays) * state.WeightedAverageCost * settings.AnalysisPeriodDays),
            _ => CalculateAnnualConsumptionValue(state, settings.AnalysisPeriodDays)
        };
    }

    private static decimal CalculateAnnualConsumptionValue(InventoryState state, int analysisPeriodDays)
    {
        var consumptions = state.RecentMovements
            .Where(m => m.Type == MovementType.Consumption && m.Timestamp >= DateTime.UtcNow.AddDays(-analysisPeriodDays))
            .ToList();

        var totalConsumed = consumptions.Sum(m => Math.Abs(m.Quantity));
        var totalCost = consumptions.Sum(m => m.TotalCost);

        // Annualize if less than a year of data
        var daysCovered = consumptions.Count > 0
            ? Math.Max((DateTime.UtcNow - consumptions.Min(m => m.Timestamp)).TotalDays, 1)
            : 1;

        return totalCost * (decimal)(365.0 / daysCovered);
    }

    private static decimal CalculateVelocity(InventoryState state, int analysisPeriodDays)
    {
        var consumptions = state.RecentMovements
            .Where(m => m.Type == MovementType.Consumption && m.Timestamp >= DateTime.UtcNow.AddDays(-analysisPeriodDays))
            .ToList();

        var totalConsumed = consumptions.Sum(m => Math.Abs(m.Quantity));
        var daysCovered = consumptions.Count > 0
            ? Math.Max((DateTime.UtcNow - consumptions.Min(m => m.Timestamp)).TotalDays, 1)
            : 1;

        return totalConsumed / (decimal)daysCovered;
    }

    private static ClassifiedItem ToClassifiedItem(AbcIngredientData data)
    {
        return new ClassifiedItem
        {
            IngredientId = data.IngredientId,
            IngredientName = data.IngredientName,
            Sku = data.Sku,
            Category = data.Category,
            Classification = data.Classification,
            AnnualConsumptionValue = data.AnnualConsumptionValue,
            CurrentValue = data.CurrentValue,
            Velocity = data.Velocity,
            CumulativePercentage = data.CumulativePercentage,
            Rank = data.Rank,
            ClassifiedAt = data.ClassifiedAt ?? DateTime.MinValue,
            PreviousClassification = data.PreviousClassification
        };
    }

    private AbcClassificationReport CreateEmptyReport(DateTime now)
    {
        return new AbcClassificationReport
        {
            GeneratedAt = now,
            SiteId = _state.State.SiteId,
            Method = _state.State.Settings.Method,
            AnalysisPeriodDays = _state.State.Settings.AnalysisPeriodDays,
            TotalItems = 0,
            TotalValue = 0
        };
    }
}
