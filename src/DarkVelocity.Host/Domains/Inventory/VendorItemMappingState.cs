using DarkVelocity.Host.Events;

namespace DarkVelocity.Host.State;

/// <summary>
/// State for vendor-specific item mappings.
/// Maps vendor item descriptions to internal ingredient SKUs.
/// </summary>
[GenerateSerializer]
public sealed class VendorItemMappingState
{
    [Id(0)] public Guid OrganizationId { get; set; }

    /// <summary>
    /// Vendor ID (supplier ID or store identifier).
    /// For retail stores, this is a normalized store name (e.g., "costco", "walmart").
    /// </summary>
    [Id(1)] public string VendorId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the vendor.
    /// </summary>
    [Id(2)] public string VendorName { get; set; } = string.Empty;

    /// <summary>
    /// Type of vendor for context-specific matching.
    /// </summary>
    [Id(3)] public VendorType VendorType { get; set; } = VendorType.Supplier;

    /// <summary>
    /// Exact mappings: normalized description → mapping record.
    /// Key is lowercased, trimmed description.
    /// </summary>
    [Id(4)] public Dictionary<string, VendorItemMapping> ExactMappings { get; set; } = [];

    /// <summary>
    /// Product code mappings: vendor product code → mapping record.
    /// Used for suppliers that provide stable SKU codes.
    /// </summary>
    [Id(5)] public Dictionary<string, VendorItemMapping> ProductCodeMappings { get; set; } = [];

    /// <summary>
    /// Learned patterns for fuzzy matching.
    /// Extracted from confirmed mappings to help match similar items.
    /// </summary>
    [Id(6)] public List<LearnedPattern> LearnedPatterns { get; set; } = [];

    /// <summary>
    /// Statistics for mapping accuracy.
    /// </summary>
    [Id(7)] public int TotalMappingsCreated { get; set; }
    [Id(8)] public int TotalAutoMappings { get; set; }
    [Id(9)] public int TotalManualMappings { get; set; }
    [Id(10)] public DateTime? LastMappingAt { get; set; }

    [Id(11)] public int Version { get; set; }
    [Id(12)] public DateTime CreatedAt { get; set; }
    [Id(13)] public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Type of vendor.
/// </summary>
public enum VendorType
{
    /// <summary>Established supplier with account (Sysco, US Foods)</summary>
    Supplier,
    /// <summary>Retail store for ad-hoc purchases (Costco, Walmart)</summary>
    RetailStore,
    /// <summary>Unknown or generic vendor</summary>
    Unknown
}

/// <summary>
/// A confirmed mapping from vendor item to internal ingredient.
/// </summary>
[GenerateSerializer]
public sealed record VendorItemMapping
{
    /// <summary>Original vendor description (preserved for display)</summary>
    [Id(0)] public required string VendorDescription { get; init; }

    /// <summary>Normalized description (lowercased, trimmed)</summary>
    [Id(1)] public required string NormalizedDescription { get; init; }

    /// <summary>Vendor's product code if available</summary>
    [Id(2)] public string? VendorProductCode { get; init; }

    /// <summary>Mapped internal ingredient ID</summary>
    [Id(3)] public required Guid IngredientId { get; init; }

    /// <summary>Mapped internal ingredient name</summary>
    [Id(4)] public required string IngredientName { get; init; }

    /// <summary>Mapped internal ingredient SKU</summary>
    [Id(5)] public required string IngredientSku { get; init; }

    /// <summary>How this mapping was created</summary>
    [Id(6)] public required MappingSource Source { get; init; }

    /// <summary>Confidence score (1.0 for manual, varies for auto)</summary>
    [Id(7)] public required decimal Confidence { get; init; }

    /// <summary>When this mapping was created</summary>
    [Id(8)] public required DateTime CreatedAt { get; init; }

    /// <summary>Who created this mapping</summary>
    [Id(9)] public Guid? CreatedBy { get; init; }

    /// <summary>Number of times this mapping has been used</summary>
    [Id(10)] public int UsageCount { get; init; }

    /// <summary>Last time this mapping was used</summary>
    [Id(11)] public DateTime? LastUsedAt { get; init; }

    /// <summary>Expected unit price for variance detection</summary>
    [Id(12)] public decimal? ExpectedUnitPrice { get; init; }

    /// <summary>Unit of measure for this item</summary>
    [Id(13)] public string? Unit { get; init; }
}

/// <summary>
/// A learned pattern for fuzzy matching.
/// Extracted from confirmed mappings to help match similar items.
/// </summary>
[GenerateSerializer]
public sealed record LearnedPattern
{
    /// <summary>Significant tokens from the description</summary>
    [Id(0)] public required IReadOnlyList<string> Tokens { get; init; }

    /// <summary>The ingredient this pattern maps to</summary>
    [Id(1)] public required Guid IngredientId { get; init; }

    /// <summary>The ingredient name</summary>
    [Id(2)] public required string IngredientName { get; init; }

    /// <summary>The ingredient SKU</summary>
    [Id(3)] public required string IngredientSku { get; init; }

    /// <summary>
    /// Weight based on how often this pattern has been confirmed.
    /// Higher weight = more reliable pattern.
    /// </summary>
    [Id(4)] public int Weight { get; init; } = 1;

    /// <summary>When this pattern was first learned</summary>
    [Id(5)] public required DateTime LearnedAt { get; init; }

    /// <summary>When this pattern was last reinforced</summary>
    [Id(6)] public DateTime? LastReinforcedAt { get; init; }
}
