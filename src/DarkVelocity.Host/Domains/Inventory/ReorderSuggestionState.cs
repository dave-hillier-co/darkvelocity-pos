using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

[GenerateSerializer]
public sealed class ReorderSuggestionState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }

    /// <summary>
    /// Reorder settings.
    /// </summary>
    [Id(2)] public ReorderSettings Settings { get; set; } = new();

    /// <summary>
    /// Registered ingredients for reorder monitoring.
    /// </summary>
    [Id(3)] public Dictionary<Guid, ReorderIngredientData> Ingredients { get; set; } = new();

    /// <summary>
    /// Active suggestions.
    /// </summary>
    [Id(4)] public Dictionary<Guid, ReorderSuggestionData> Suggestions { get; set; } = new();

    /// <summary>
    /// Last suggestion generation timestamp.
    /// </summary>
    [Id(5)] public DateTime? LastGeneratedAt { get; set; }
}

[GenerateSerializer]
public sealed class ReorderIngredientData
{
    [Id(0)] public Guid IngredientId { get; set; }
    [Id(1)] public string IngredientName { get; set; } = string.Empty;
    [Id(2)] public string Sku { get; set; } = string.Empty;
    [Id(3)] public string Category { get; set; } = string.Empty;
    [Id(4)] public string Unit { get; set; } = string.Empty;
    [Id(5)] public DateTime RegisteredAt { get; set; }

    // Supplier info
    [Id(6)] public Guid? PreferredSupplierId { get; set; }
    [Id(7)] public string? PreferredSupplierName { get; set; }
    [Id(8)] public int LeadTimeDays { get; set; } = 7;

    // Pricing
    [Id(9)] public decimal? LastPurchasePrice { get; set; }
    [Id(10)] public DateTime? LastPurchaseDate { get; set; }
}

[GenerateSerializer]
public sealed class ReorderSuggestionData
{
    [Id(0)] public Guid SuggestionId { get; set; }
    [Id(1)] public Guid IngredientId { get; set; }
    [Id(2)] public SuggestionStatus Status { get; set; } = SuggestionStatus.Pending;
    [Id(3)] public DateTime CreatedAt { get; set; }
    [Id(4)] public DateTime? ExpiresAt { get; set; }

    // Calculated values at time of creation
    [Id(5)] public decimal CurrentQuantity { get; set; }
    [Id(6)] public decimal SuggestedQuantity { get; set; }
    [Id(7)] public decimal EstimatedCost { get; set; }
    [Id(8)] public decimal DailyUsage { get; set; }
    [Id(9)] public int DaysOfSupply { get; set; }
    [Id(10)] public ReorderUrgency Urgency { get; set; }

    // Actions
    [Id(11)] public Guid? ApprovedBy { get; set; }
    [Id(12)] public DateTime? ApprovedAt { get; set; }
    [Id(13)] public Guid? DismissedBy { get; set; }
    [Id(14)] public DateTime? DismissedAt { get; set; }
    [Id(15)] public string? DismissalReason { get; set; }
    [Id(16)] public Guid? PurchaseOrderId { get; set; }
    [Id(17)] public DateTime? OrderedAt { get; set; }
}
