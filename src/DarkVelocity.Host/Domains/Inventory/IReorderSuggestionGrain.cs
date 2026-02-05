namespace DarkVelocity.Host.Grains;

/// <summary>
/// Urgency level for reorder suggestions.
/// </summary>
public enum ReorderUrgency
{
    /// <summary>Stock is adequate, no immediate action needed.</summary>
    Low,
    /// <summary>Approaching reorder point, plan to order soon.</summary>
    Medium,
    /// <summary>Below reorder point, order recommended.</summary>
    High,
    /// <summary>Critical low stock, order immediately.</summary>
    Critical,
    /// <summary>Out of stock, emergency order needed.</summary>
    OutOfStock
}

/// <summary>
/// Status of a reorder suggestion.
/// </summary>
public enum SuggestionStatus
{
    /// <summary>Suggestion is pending review.</summary>
    Pending,
    /// <summary>Suggestion has been approved.</summary>
    Approved,
    /// <summary>Suggestion has been converted to a purchase order.</summary>
    Ordered,
    /// <summary>Suggestion was rejected/dismissed.</summary>
    Dismissed,
    /// <summary>Suggestion expired (stock was replenished through other means).</summary>
    Expired
}

[GenerateSerializer]
public record ReorderSettings(
    [property: Id(0)] int DefaultLeadTimeDays = 7,
    [property: Id(1)] decimal SafetyStockMultiplier = 1.5m,
    [property: Id(2)] bool UseAbcClassification = true,
    [property: Id(3)] int AnalysisPeriodDays = 30,
    [property: Id(4)] bool AutoGeneratePO = false,
    [property: Id(5)] decimal MinimumOrderValue = 0,
    [property: Id(6)] bool ConsolidateBySupplier = true);

[GenerateSerializer]
public record ReorderSuggestion
{
    [Id(0)] public Guid SuggestionId { get; init; }
    [Id(1)] public Guid IngredientId { get; init; }
    [Id(2)] public string IngredientName { get; init; } = string.Empty;
    [Id(3)] public string Sku { get; init; } = string.Empty;
    [Id(4)] public string Category { get; init; } = string.Empty;
    [Id(5)] public string Unit { get; init; } = string.Empty;

    // Current state
    [Id(6)] public decimal CurrentQuantity { get; init; }
    [Id(7)] public decimal ReorderPoint { get; init; }
    [Id(8)] public decimal ParLevel { get; init; }

    // Calculated values
    [Id(9)] public decimal SuggestedQuantity { get; init; }
    [Id(10)] public decimal EstimatedCost { get; init; }
    [Id(11)] public decimal DailyUsage { get; init; }
    [Id(12)] public int DaysOfSupply { get; init; }
    [Id(13)] public int LeadTimeDays { get; init; }

    // Status
    [Id(14)] public ReorderUrgency Urgency { get; init; }
    [Id(15)] public SuggestionStatus Status { get; init; }
    [Id(16)] public DateTime CreatedAt { get; init; }
    [Id(17)] public DateTime? ExpiresAt { get; init; }

    // Supplier info
    [Id(18)] public Guid? PreferredSupplierId { get; init; }
    [Id(19)] public string? PreferredSupplierName { get; init; }
    [Id(20)] public decimal? LastPurchasePrice { get; init; }

    // ABC classification integration
    [Id(21)] public AbcClass? AbcClassification { get; init; }
}

[GenerateSerializer]
public record ReorderReport
{
    [Id(0)] public DateTime GeneratedAt { get; init; }
    [Id(1)] public Guid SiteId { get; init; }

    // Summary
    [Id(2)] public int TotalSuggestions { get; init; }
    [Id(3)] public decimal TotalEstimatedCost { get; init; }

    // By urgency
    [Id(4)] public int OutOfStockCount { get; init; }
    [Id(5)] public int CriticalCount { get; init; }
    [Id(6)] public int HighCount { get; init; }
    [Id(7)] public int MediumCount { get; init; }

    // Suggestions
    [Id(8)] public List<ReorderSuggestion> OutOfStockItems { get; init; } = [];
    [Id(9)] public List<ReorderSuggestion> CriticalItems { get; init; } = [];
    [Id(10)] public List<ReorderSuggestion> HighPriorityItems { get; init; } = [];
    [Id(11)] public List<ReorderSuggestion> MediumPriorityItems { get; init; } = [];

    // By supplier
    [Id(12)] public Dictionary<Guid, SupplierReorderSummary> BySupplier { get; init; } = new();
}

[GenerateSerializer]
public record SupplierReorderSummary
{
    [Id(0)] public Guid SupplierId { get; init; }
    [Id(1)] public string SupplierName { get; init; } = string.Empty;
    [Id(2)] public int ItemCount { get; init; }
    [Id(3)] public decimal TotalValue { get; init; }
    [Id(4)] public List<ReorderSuggestion> Items { get; init; } = [];
}

[GenerateSerializer]
public record PurchaseOrderDraft
{
    [Id(0)] public Guid DraftId { get; init; }
    [Id(1)] public Guid? SupplierId { get; init; }
    [Id(2)] public string? SupplierName { get; init; }
    [Id(3)] public DateTime CreatedAt { get; init; }
    [Id(4)] public List<PurchaseOrderDraftLine> Lines { get; init; } = [];
    [Id(5)] public decimal TotalValue { get; init; }
    [Id(6)] public DateTime? RequestedDeliveryDate { get; init; }
}

[GenerateSerializer]
public record PurchaseOrderDraftLine
{
    [Id(0)] public Guid IngredientId { get; init; }
    [Id(1)] public string IngredientName { get; init; } = string.Empty;
    [Id(2)] public string Sku { get; init; } = string.Empty;
    [Id(3)] public decimal Quantity { get; init; }
    [Id(4)] public string Unit { get; init; } = string.Empty;
    [Id(5)] public decimal EstimatedUnitCost { get; init; }
    [Id(6)] public decimal EstimatedLineCost { get; init; }
    [Id(7)] public Guid? FromSuggestionId { get; init; }
}

/// <summary>
/// Grain for generating automatic reorder suggestions.
/// One per site.
/// </summary>
public interface IReorderSuggestionGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initializes the reorder suggestion grain for a site.
    /// </summary>
    Task InitializeAsync(Guid organizationId, Guid siteId);

    /// <summary>
    /// Configures reorder settings.
    /// </summary>
    Task ConfigureAsync(ReorderSettings settings);

    /// <summary>
    /// Gets current settings.
    /// </summary>
    Task<ReorderSettings> GetSettingsAsync();

    /// <summary>
    /// Registers an ingredient for reorder monitoring.
    /// </summary>
    Task RegisterIngredientAsync(
        Guid ingredientId,
        string ingredientName,
        string sku,
        string category,
        string unit,
        Guid? preferredSupplierId = null,
        string? preferredSupplierName = null,
        int? leadTimeDays = null);

    /// <summary>
    /// Removes an ingredient from reorder monitoring.
    /// </summary>
    Task UnregisterIngredientAsync(Guid ingredientId);

    /// <summary>
    /// Generates reorder suggestions for all registered ingredients.
    /// </summary>
    Task<ReorderReport> GenerateSuggestionsAsync();

    /// <summary>
    /// Gets all pending suggestions.
    /// </summary>
    Task<IReadOnlyList<ReorderSuggestion>> GetPendingSuggestionsAsync();

    /// <summary>
    /// Gets suggestions by urgency level.
    /// </summary>
    Task<IReadOnlyList<ReorderSuggestion>> GetSuggestionsByUrgencyAsync(ReorderUrgency urgency);

    /// <summary>
    /// Gets the full reorder report.
    /// </summary>
    Task<ReorderReport> GetReportAsync();

    /// <summary>
    /// Approves a suggestion.
    /// </summary>
    Task ApproveSuggestionAsync(Guid suggestionId, Guid approvedBy);

    /// <summary>
    /// Dismisses a suggestion.
    /// </summary>
    Task DismissSuggestionAsync(Guid suggestionId, Guid dismissedBy, string reason);

    /// <summary>
    /// Marks a suggestion as ordered (linked to a PO).
    /// </summary>
    Task MarkAsOrderedAsync(Guid suggestionId, Guid purchaseOrderId);

    /// <summary>
    /// Generates a purchase order draft from suggestions.
    /// </summary>
    Task<PurchaseOrderDraft> GeneratePurchaseOrderDraftAsync(IEnumerable<Guid>? suggestionIds = null, Guid? supplierId = null);

    /// <summary>
    /// Generates consolidated purchase order drafts by supplier.
    /// </summary>
    Task<IReadOnlyList<PurchaseOrderDraft>> GenerateConsolidatedDraftsAsync();

    /// <summary>
    /// Updates supplier and lead time for an ingredient.
    /// </summary>
    Task UpdateIngredientSupplierAsync(Guid ingredientId, Guid supplierId, string supplierName, int leadTimeDays);

    /// <summary>
    /// Calculates optimal order quantity based on Economic Order Quantity (EOQ) principles.
    /// </summary>
    Task<decimal> CalculateOptimalOrderQuantityAsync(Guid ingredientId, decimal? orderingCost = null, decimal? holdingCostPercentage = null);

    /// <summary>
    /// Checks if the grain is initialized.
    /// </summary>
    Task<bool> ExistsAsync();
}
