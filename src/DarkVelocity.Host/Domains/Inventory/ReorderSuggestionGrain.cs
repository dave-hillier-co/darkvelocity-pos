using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

public class ReorderSuggestionGrain : Grain, IReorderSuggestionGrain
{
    private readonly IPersistentState<ReorderSuggestionState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ReorderSuggestionGrain> _logger;
    private Lazy<IAsyncStream<IStreamEvent>>? _alertStream;

    public ReorderSuggestionGrain(
        [PersistentState("reorderSuggestion", "OrleansStorage")] IPersistentState<ReorderSuggestionState> state,
        IGrainFactory grainFactory,
        ILogger<ReorderSuggestionGrain> logger)
    {
        _state = state;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.OrganizationId != Guid.Empty)
        {
            InitializeLazyFields();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    private void InitializeLazyFields()
    {
        var orgId = _state.State.OrganizationId;

        _alertStream = new Lazy<IAsyncStream<IStreamEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.AlertStreamNamespace, orgId.ToString());
            return streamProvider.GetStream<IStreamEvent>(streamId);
        });
    }

    private IAsyncStream<IStreamEvent> AlertStream => _alertStream!.Value;

    public async Task InitializeAsync(Guid organizationId, Guid siteId)
    {
        if (_state.State.OrganizationId != Guid.Empty)
            throw new InvalidOperationException("Reorder suggestion grain already initialized");

        _state.State.OrganizationId = organizationId;
        _state.State.SiteId = siteId;
        _state.State.Settings = new ReorderSettings();

        await _state.WriteStateAsync();
        InitializeLazyFields();

        _logger.LogInformation(
            "Reorder suggestion grain initialized for site {SiteId}",
            siteId);
    }

    public async Task ConfigureAsync(ReorderSettings settings)
    {
        EnsureExists();

        _state.State.Settings = settings;
        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Reorder settings configured: LeadTime={LeadTime}d, SafetyMultiplier={Safety}, UseABC={UseAbc}",
            settings.DefaultLeadTimeDays,
            settings.SafetyStockMultiplier,
            settings.UseAbcClassification);
    }

    public Task<ReorderSettings> GetSettingsAsync()
    {
        return Task.FromResult(_state.State.Settings);
    }

    public async Task RegisterIngredientAsync(
        Guid ingredientId,
        string ingredientName,
        string sku,
        string category,
        string unit,
        Guid? preferredSupplierId = null,
        string? preferredSupplierName = null,
        int? leadTimeDays = null)
    {
        EnsureExists();

        _state.State.Ingredients[ingredientId] = new ReorderIngredientData
        {
            IngredientId = ingredientId,
            IngredientName = ingredientName,
            Sku = sku,
            Category = category,
            Unit = unit,
            RegisteredAt = DateTime.UtcNow,
            PreferredSupplierId = preferredSupplierId,
            PreferredSupplierName = preferredSupplierName,
            LeadTimeDays = leadTimeDays ?? _state.State.Settings.DefaultLeadTimeDays
        };

        await _state.WriteStateAsync();
    }

    public async Task UnregisterIngredientAsync(Guid ingredientId)
    {
        EnsureExists();

        _state.State.Ingredients.Remove(ingredientId);

        // Remove any pending suggestions for this ingredient
        var toRemove = _state.State.Suggestions
            .Where(kvp => kvp.Value.IngredientId == ingredientId && kvp.Value.Status == SuggestionStatus.Pending)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var id in toRemove)
        {
            _state.State.Suggestions.Remove(id);
        }

        await _state.WriteStateAsync();
    }

    public async Task<ReorderReport> GenerateSuggestionsAsync()
    {
        EnsureExists();

        var now = DateTime.UtcNow;
        var settings = _state.State.Settings;
        var newSuggestions = new List<ReorderSuggestion>();

        // Expire old pending suggestions
        foreach (var suggestion in _state.State.Suggestions.Values
            .Where(s => s.Status == SuggestionStatus.Pending && s.ExpiresAt.HasValue && s.ExpiresAt < now))
        {
            suggestion.Status = SuggestionStatus.Expired;
        }

        // Get ABC classification if enabled
        Dictionary<Guid, AbcClass>? abcClassifications = null;
        Dictionary<AbcClass, AbcReorderPolicy>? abcPolicies = null;

        if (settings.UseAbcClassification)
        {
            var abcGrain = _grainFactory.GetGrain<IAbcClassificationGrain>(
                GrainKeys.AbcClassification(_state.State.OrganizationId, _state.State.SiteId));

            if (await abcGrain.ExistsAsync())
            {
                var abcReport = await abcGrain.GetReportAsync();
                abcClassifications = abcReport.ClassAItems
                    .Concat(abcReport.ClassBItems)
                    .Concat(abcReport.ClassCItems)
                    .ToDictionary(i => i.IngredientId, i => i.Classification);

                var policies = await abcGrain.GetAllReorderPoliciesAsync();
                abcPolicies = policies.ToDictionary(p => p.Classification);
            }
        }

        foreach (var ingredient in _state.State.Ingredients.Values)
        {
            var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(
                GrainKeys.Inventory(_state.State.OrganizationId, _state.State.SiteId, ingredient.IngredientId));

            if (!await inventoryGrain.ExistsAsync())
                continue;

            var state = await inventoryGrain.GetStateAsync();
            var levelInfo = await inventoryGrain.GetLevelInfoAsync();

            // Calculate daily usage
            var dailyUsage = CalculateDailyUsage(state, settings.AnalysisPeriodDays);

            // Get lead time (from ABC policy or ingredient default)
            var leadTimeDays = ingredient.LeadTimeDays;
            AbcClass? abcClass = null;

            if (abcClassifications != null && abcClassifications.TryGetValue(ingredient.IngredientId, out var classification))
            {
                abcClass = classification;
                if (abcPolicies != null && abcPolicies.TryGetValue(classification, out var policy))
                {
                    leadTimeDays = (int)policy.SafetyStockDays;
                }
            }

            // Calculate days of supply
            var daysOfSupply = dailyUsage > 0
                ? (int)(state.QuantityOnHand / dailyUsage)
                : int.MaxValue;

            // Determine urgency
            var urgency = CalculateUrgency(state, dailyUsage, leadTimeDays);

            // Check if we need to create a suggestion
            if (urgency >= ReorderUrgency.Medium)
            {
                // Check for existing pending suggestion
                var existingSuggestion = _state.State.Suggestions.Values
                    .FirstOrDefault(s => s.IngredientId == ingredient.IngredientId && s.Status == SuggestionStatus.Pending);

                if (existingSuggestion == null)
                {
                    // Calculate suggested order quantity
                    var suggestedQty = CalculateSuggestedQuantity(
                        state.QuantityOnHand,
                        state.ParLevel,
                        dailyUsage,
                        leadTimeDays,
                        settings.SafetyStockMultiplier);

                    var estimatedCost = suggestedQty * (ingredient.LastPurchasePrice ?? state.WeightedAverageCost);

                    var suggestionId = Guid.NewGuid();
                    var suggestionData = new ReorderSuggestionData
                    {
                        SuggestionId = suggestionId,
                        IngredientId = ingredient.IngredientId,
                        Status = SuggestionStatus.Pending,
                        CreatedAt = now,
                        ExpiresAt = now.AddDays(7), // Suggestions expire after 7 days
                        CurrentQuantity = state.QuantityOnHand,
                        SuggestedQuantity = suggestedQty,
                        EstimatedCost = estimatedCost,
                        DailyUsage = dailyUsage,
                        DaysOfSupply = daysOfSupply,
                        Urgency = urgency
                    };

                    _state.State.Suggestions[suggestionId] = suggestionData;

                    var suggestion = ToReorderSuggestion(suggestionData, ingredient, state, abcClass);
                    newSuggestions.Add(suggestion);

                    // Publish alert for urgent items
                    if (urgency >= ReorderUrgency.High)
                    {
                        await AlertStream.OnNextAsync(new ReorderSuggestionGeneratedEvent(
                            ingredient.IngredientId,
                            _state.State.SiteId,
                            ingredient.IngredientName,
                            state.QuantityOnHand,
                            suggestedQty,
                            estimatedCost,
                            ingredient.PreferredSupplierId)
                        {
                            OrganizationId = _state.State.OrganizationId
                        });
                    }
                }
            }
        }

        _state.State.LastGeneratedAt = now;
        await _state.WriteStateAsync();

        // Build report
        var allPendingSuggestions = _state.State.Suggestions.Values
            .Where(s => s.Status == SuggestionStatus.Pending)
            .Select(s =>
            {
                var ing = _state.State.Ingredients.GetValueOrDefault(s.IngredientId);
                AbcClass? abc = null;
                if (abcClassifications != null && abcClassifications.TryGetValue(s.IngredientId, out var c))
                    abc = c;
                return ing != null ? ToReorderSuggestion(s, ing, null, abc) : null;
            })
            .Where(s => s != null)
            .Cast<ReorderSuggestion>()
            .ToList();

        var outOfStock = allPendingSuggestions.Where(s => s.Urgency == ReorderUrgency.OutOfStock).ToList();
        var critical = allPendingSuggestions.Where(s => s.Urgency == ReorderUrgency.Critical).ToList();
        var high = allPendingSuggestions.Where(s => s.Urgency == ReorderUrgency.High).ToList();
        var medium = allPendingSuggestions.Where(s => s.Urgency == ReorderUrgency.Medium).ToList();

        var bySupplier = allPendingSuggestions
            .Where(s => s.PreferredSupplierId.HasValue)
            .GroupBy(s => s.PreferredSupplierId!.Value)
            .ToDictionary(
                g => g.Key,
                g => new SupplierReorderSummary
                {
                    SupplierId = g.Key,
                    SupplierName = g.First().PreferredSupplierName ?? "",
                    ItemCount = g.Count(),
                    TotalValue = g.Sum(s => s.EstimatedCost),
                    Items = g.ToList()
                });

        var report = new ReorderReport
        {
            GeneratedAt = now,
            SiteId = _state.State.SiteId,
            TotalSuggestions = allPendingSuggestions.Count,
            TotalEstimatedCost = allPendingSuggestions.Sum(s => s.EstimatedCost),
            OutOfStockCount = outOfStock.Count,
            CriticalCount = critical.Count,
            HighCount = high.Count,
            MediumCount = medium.Count,
            OutOfStockItems = outOfStock,
            CriticalItems = critical,
            HighPriorityItems = high,
            MediumPriorityItems = medium,
            BySupplier = bySupplier
        };

        _logger.LogInformation(
            "Generated {NewCount} new reorder suggestions. Total pending: {Total} (OOS: {OOS}, Critical: {Critical}, High: {High})",
            newSuggestions.Count,
            allPendingSuggestions.Count,
            outOfStock.Count,
            critical.Count,
            high.Count);

        return report;
    }

    public Task<IReadOnlyList<ReorderSuggestion>> GetPendingSuggestionsAsync()
    {
        EnsureExists();

        var suggestions = _state.State.Suggestions.Values
            .Where(s => s.Status == SuggestionStatus.Pending)
            .Select(s =>
            {
                var ing = _state.State.Ingredients.GetValueOrDefault(s.IngredientId);
                return ing != null ? ToReorderSuggestion(s, ing, null, null) : null;
            })
            .Where(s => s != null)
            .Cast<ReorderSuggestion>()
            .OrderByDescending(s => s.Urgency)
            .ThenByDescending(s => s.EstimatedCost)
            .ToList();

        return Task.FromResult<IReadOnlyList<ReorderSuggestion>>(suggestions);
    }

    public Task<IReadOnlyList<ReorderSuggestion>> GetSuggestionsByUrgencyAsync(ReorderUrgency urgency)
    {
        EnsureExists();

        var suggestions = _state.State.Suggestions.Values
            .Where(s => s.Status == SuggestionStatus.Pending && s.Urgency == urgency)
            .Select(s =>
            {
                var ing = _state.State.Ingredients.GetValueOrDefault(s.IngredientId);
                return ing != null ? ToReorderSuggestion(s, ing, null, null) : null;
            })
            .Where(s => s != null)
            .Cast<ReorderSuggestion>()
            .OrderByDescending(s => s.EstimatedCost)
            .ToList();

        return Task.FromResult<IReadOnlyList<ReorderSuggestion>>(suggestions);
    }

    public async Task<ReorderReport> GetReportAsync()
    {
        return await GenerateSuggestionsAsync();
    }

    public async Task ApproveSuggestionAsync(Guid suggestionId, Guid approvedBy)
    {
        EnsureExists();

        if (!_state.State.Suggestions.TryGetValue(suggestionId, out var suggestion))
            throw new InvalidOperationException($"Suggestion {suggestionId} not found");

        if (suggestion.Status != SuggestionStatus.Pending)
            throw new InvalidOperationException($"Cannot approve suggestion in status {suggestion.Status}");

        suggestion.Status = SuggestionStatus.Approved;
        suggestion.ApprovedBy = approvedBy;
        suggestion.ApprovedAt = DateTime.UtcNow;

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Suggestion {SuggestionId} approved by {ApprovedBy}",
            suggestionId,
            approvedBy);
    }

    public async Task DismissSuggestionAsync(Guid suggestionId, Guid dismissedBy, string reason)
    {
        EnsureExists();

        if (!_state.State.Suggestions.TryGetValue(suggestionId, out var suggestion))
            throw new InvalidOperationException($"Suggestion {suggestionId} not found");

        if (suggestion.Status != SuggestionStatus.Pending)
            throw new InvalidOperationException($"Cannot dismiss suggestion in status {suggestion.Status}");

        suggestion.Status = SuggestionStatus.Dismissed;
        suggestion.DismissedBy = dismissedBy;
        suggestion.DismissedAt = DateTime.UtcNow;
        suggestion.DismissalReason = reason;

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Suggestion {SuggestionId} dismissed by {DismissedBy}. Reason: {Reason}",
            suggestionId,
            dismissedBy,
            reason);
    }

    public async Task MarkAsOrderedAsync(Guid suggestionId, Guid purchaseOrderId)
    {
        EnsureExists();

        if (!_state.State.Suggestions.TryGetValue(suggestionId, out var suggestion))
            throw new InvalidOperationException($"Suggestion {suggestionId} not found");

        suggestion.Status = SuggestionStatus.Ordered;
        suggestion.PurchaseOrderId = purchaseOrderId;
        suggestion.OrderedAt = DateTime.UtcNow;

        await _state.WriteStateAsync();

        _logger.LogInformation(
            "Suggestion {SuggestionId} linked to purchase order {PurchaseOrderId}",
            suggestionId,
            purchaseOrderId);
    }

    public Task<PurchaseOrderDraft> GeneratePurchaseOrderDraftAsync(IEnumerable<Guid>? suggestionIds = null, Guid? supplierId = null)
    {
        EnsureExists();

        var suggestions = suggestionIds != null
            ? _state.State.Suggestions.Values.Where(s => suggestionIds.Contains(s.SuggestionId))
            : _state.State.Suggestions.Values.Where(s => s.Status == SuggestionStatus.Pending || s.Status == SuggestionStatus.Approved);

        if (supplierId.HasValue)
        {
            suggestions = suggestions.Where(s =>
            {
                var ing = _state.State.Ingredients.GetValueOrDefault(s.IngredientId);
                return ing?.PreferredSupplierId == supplierId;
            });
        }

        var lines = suggestions.Select(s =>
        {
            var ing = _state.State.Ingredients.GetValueOrDefault(s.IngredientId);
            return new PurchaseOrderDraftLine
            {
                IngredientId = s.IngredientId,
                IngredientName = ing?.IngredientName ?? "",
                Sku = ing?.Sku ?? "",
                Quantity = s.SuggestedQuantity,
                Unit = ing?.Unit ?? "",
                EstimatedUnitCost = ing?.LastPurchasePrice ?? s.EstimatedCost / s.SuggestedQuantity,
                EstimatedLineCost = s.EstimatedCost,
                FromSuggestionId = s.SuggestionId
            };
        }).ToList();

        var supplierName = supplierId.HasValue
            ? _state.State.Ingredients.Values.FirstOrDefault(i => i.PreferredSupplierId == supplierId)?.PreferredSupplierName
            : null;

        var draft = new PurchaseOrderDraft
        {
            DraftId = Guid.NewGuid(),
            SupplierId = supplierId,
            SupplierName = supplierName,
            CreatedAt = DateTime.UtcNow,
            Lines = lines,
            TotalValue = lines.Sum(l => l.EstimatedLineCost),
            RequestedDeliveryDate = DateTime.UtcNow.AddDays(_state.State.Settings.DefaultLeadTimeDays)
        };

        return Task.FromResult(draft);
    }

    public async Task<IReadOnlyList<PurchaseOrderDraft>> GenerateConsolidatedDraftsAsync()
    {
        EnsureExists();

        var drafts = new List<PurchaseOrderDraft>();

        var supplierGroups = _state.State.Suggestions.Values
            .Where(s => s.Status == SuggestionStatus.Pending || s.Status == SuggestionStatus.Approved)
            .Select(s => new { Suggestion = s, Ingredient = _state.State.Ingredients.GetValueOrDefault(s.IngredientId) })
            .Where(x => x.Ingredient?.PreferredSupplierId != null)
            .GroupBy(x => x.Ingredient!.PreferredSupplierId!.Value);

        foreach (var group in supplierGroups)
        {
            var draft = await GeneratePurchaseOrderDraftAsync(
                group.Select(x => x.Suggestion.SuggestionId),
                group.Key);
            drafts.Add(draft);
        }

        // Also create a draft for items without a preferred supplier
        var noSupplierItems = _state.State.Suggestions.Values
            .Where(s => s.Status == SuggestionStatus.Pending || s.Status == SuggestionStatus.Approved)
            .Where(s =>
            {
                var ing = _state.State.Ingredients.GetValueOrDefault(s.IngredientId);
                return ing?.PreferredSupplierId == null;
            })
            .Select(s => s.SuggestionId);

        if (noSupplierItems.Any())
        {
            var draft = await GeneratePurchaseOrderDraftAsync(noSupplierItems, null);
            drafts.Add(draft);
        }

        return drafts;
    }

    public async Task UpdateIngredientSupplierAsync(Guid ingredientId, Guid supplierId, string supplierName, int leadTimeDays)
    {
        EnsureExists();

        if (!_state.State.Ingredients.TryGetValue(ingredientId, out var ingredient))
            throw new InvalidOperationException($"Ingredient {ingredientId} not registered");

        ingredient.PreferredSupplierId = supplierId;
        ingredient.PreferredSupplierName = supplierName;
        ingredient.LeadTimeDays = leadTimeDays;

        await _state.WriteStateAsync();
    }

    public async Task<decimal> CalculateOptimalOrderQuantityAsync(Guid ingredientId, decimal? orderingCost = null, decimal? holdingCostPercentage = null)
    {
        EnsureExists();

        if (!_state.State.Ingredients.TryGetValue(ingredientId, out var ingredient))
            throw new InvalidOperationException($"Ingredient {ingredientId} not registered");

        var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(
            GrainKeys.Inventory(_state.State.OrganizationId, _state.State.SiteId, ingredientId));

        if (!await inventoryGrain.ExistsAsync())
            throw new InvalidOperationException($"Inventory for ingredient {ingredientId} not found");

        var state = await inventoryGrain.GetStateAsync();
        var dailyUsage = CalculateDailyUsage(state, _state.State.Settings.AnalysisPeriodDays);
        var annualDemand = dailyUsage * 365;

        // Default ordering cost and holding cost if not provided
        var S = orderingCost ?? 50m; // Fixed cost per order
        var H = (holdingCostPercentage ?? 0.25m) * state.WeightedAverageCost; // Holding cost per unit per year

        if (annualDemand <= 0 || H <= 0)
            return state.ParLevel - state.QuantityOnHand;

        // EOQ formula: sqrt((2 * D * S) / H)
        var eoq = (decimal)Math.Sqrt((double)(2 * annualDemand * S / H));

        return Math.Max(eoq, 1);
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.OrganizationId != Guid.Empty);

    private void EnsureExists()
    {
        if (_state.State.OrganizationId == Guid.Empty)
            throw new InvalidOperationException("Reorder suggestion grain not initialized");
    }

    private static decimal CalculateDailyUsage(InventoryState state, int analysisPeriodDays)
    {
        var consumptions = state.RecentMovements
            .Where(m => m.Type == MovementType.Consumption && m.Timestamp >= DateTime.UtcNow.AddDays(-analysisPeriodDays))
            .ToList();

        if (consumptions.Count == 0)
            return 0;

        var totalConsumed = consumptions.Sum(m => Math.Abs(m.Quantity));
        var daysCovered = Math.Max((DateTime.UtcNow - consumptions.Min(m => m.Timestamp)).TotalDays, 1);

        return totalConsumed / (decimal)daysCovered;
    }

    private static ReorderUrgency CalculateUrgency(InventoryState state, decimal dailyUsage, int leadTimeDays)
    {
        if (state.QuantityOnHand <= 0)
            return ReorderUrgency.OutOfStock;

        if (dailyUsage <= 0)
            return ReorderUrgency.Low;

        var daysOfSupply = (int)(state.QuantityOnHand / dailyUsage);
        var leadTimeWithBuffer = leadTimeDays * 1.5m;

        if (daysOfSupply <= leadTimeDays / 2)
            return ReorderUrgency.Critical;

        if (daysOfSupply <= leadTimeDays)
            return ReorderUrgency.High;

        if (daysOfSupply <= (int)leadTimeWithBuffer)
            return ReorderUrgency.Medium;

        if (state.QuantityOnHand <= state.ReorderPoint)
            return ReorderUrgency.Medium;

        return ReorderUrgency.Low;
    }

    private static decimal CalculateSuggestedQuantity(
        decimal currentQuantity,
        decimal parLevel,
        decimal dailyUsage,
        int leadTimeDays,
        decimal safetyMultiplier)
    {
        // Calculate target quantity: par level + safety stock
        var safetyStock = dailyUsage * leadTimeDays * safetyMultiplier;
        var targetQuantity = Math.Max(parLevel, dailyUsage * leadTimeDays * 2) + safetyStock;

        // Order quantity is the difference
        var orderQuantity = Math.Max(targetQuantity - currentQuantity, 0);

        return Math.Ceiling(orderQuantity);
    }

    private static ReorderSuggestion ToReorderSuggestion(
        ReorderSuggestionData data,
        ReorderIngredientData ingredient,
        InventoryState? inventoryState,
        AbcClass? abcClass)
    {
        return new ReorderSuggestion
        {
            SuggestionId = data.SuggestionId,
            IngredientId = data.IngredientId,
            IngredientName = ingredient.IngredientName,
            Sku = ingredient.Sku,
            Category = ingredient.Category,
            Unit = ingredient.Unit,
            CurrentQuantity = data.CurrentQuantity,
            ReorderPoint = inventoryState?.ReorderPoint ?? 0,
            ParLevel = inventoryState?.ParLevel ?? 0,
            SuggestedQuantity = data.SuggestedQuantity,
            EstimatedCost = data.EstimatedCost,
            DailyUsage = data.DailyUsage,
            DaysOfSupply = data.DaysOfSupply,
            LeadTimeDays = ingredient.LeadTimeDays,
            Urgency = data.Urgency,
            Status = data.Status,
            CreatedAt = data.CreatedAt,
            ExpiresAt = data.ExpiresAt,
            PreferredSupplierId = ingredient.PreferredSupplierId,
            PreferredSupplierName = ingredient.PreferredSupplierName,
            LastPurchasePrice = ingredient.LastPurchasePrice,
            AbcClassification = abcClass
        };
    }
}
