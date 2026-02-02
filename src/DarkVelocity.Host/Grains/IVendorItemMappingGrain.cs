using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Commands
// ============================================================================

/// <summary>
/// Command to initialize a vendor mapping record.
/// </summary>
[GenerateSerializer]
public record InitializeVendorMappingCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] string VendorId,
    [property: Id(2)] string VendorName,
    [property: Id(3)] VendorType VendorType = VendorType.Unknown);

/// <summary>
/// Command to learn a mapping from a confirmed document.
/// </summary>
[GenerateSerializer]
public record LearnMappingCommand(
    [property: Id(0)] string VendorDescription,
    [property: Id(1)] Guid IngredientId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] string IngredientSku,
    [property: Id(4)] MappingSource Source,
    [property: Id(5)] decimal Confidence = 1.0m,
    [property: Id(6)] string? VendorProductCode = null,
    [property: Id(7)] Guid? LearnedFromDocumentId = null,
    [property: Id(8)] Guid? LearnedBy = null,
    [property: Id(9)] decimal? UnitPrice = null,
    [property: Id(10)] string? Unit = null);

/// <summary>
/// Command to manually set or update a mapping.
/// </summary>
[GenerateSerializer]
public record SetMappingCommand(
    [property: Id(0)] string VendorDescription,
    [property: Id(1)] Guid IngredientId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] string IngredientSku,
    [property: Id(4)] Guid SetBy,
    [property: Id(5)] string? VendorProductCode = null,
    [property: Id(6)] decimal? ExpectedUnitPrice = null,
    [property: Id(7)] string? Unit = null);

/// <summary>
/// Command to delete a mapping.
/// </summary>
[GenerateSerializer]
public record DeleteMappingCommand(
    [property: Id(0)] string VendorDescription,
    [property: Id(1)] Guid DeletedBy);

/// <summary>
/// Command to record usage of a mapping.
/// </summary>
[GenerateSerializer]
public record RecordMappingUsageCommand(
    [property: Id(0)] string VendorDescription,
    [property: Id(1)] Guid DocumentId);

// ============================================================================
// Results
// ============================================================================

/// <summary>
/// Result of looking up a mapping.
/// </summary>
[GenerateSerializer]
public record MappingLookupResult(
    [property: Id(0)] bool Found,
    [property: Id(1)] VendorItemMapping? Mapping,
    [property: Id(2)] MappingMatchType MatchType);

/// <summary>
/// How the mapping was matched.
/// </summary>
public enum MappingMatchType
{
    /// <summary>No match found</summary>
    None,
    /// <summary>Exact description match</summary>
    ExactDescription,
    /// <summary>Product code match</summary>
    ProductCode,
    /// <summary>Fuzzy match from learned patterns</summary>
    FuzzyPattern
}

/// <summary>
/// A suggested mapping with confidence score.
/// </summary>
[GenerateSerializer]
public record MappingSuggestion(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] string IngredientName,
    [property: Id(2)] string IngredientSku,
    [property: Id(3)] decimal Confidence,
    [property: Id(4)] string MatchReason,
    [property: Id(5)] MappingMatchType MatchType);

/// <summary>
/// Snapshot of vendor mapping state.
/// </summary>
[GenerateSerializer]
public record VendorMappingSnapshot(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] string VendorId,
    [property: Id(2)] string VendorName,
    [property: Id(3)] VendorType VendorType,
    [property: Id(4)] int TotalMappings,
    [property: Id(5)] int TotalPatterns,
    [property: Id(6)] DateTime? LastMappingAt,
    [property: Id(7)] int Version);

/// <summary>
/// Summary of a single mapping for listing.
/// </summary>
[GenerateSerializer]
public record MappingSummary(
    [property: Id(0)] string VendorDescription,
    [property: Id(1)] string? VendorProductCode,
    [property: Id(2)] Guid IngredientId,
    [property: Id(3)] string IngredientName,
    [property: Id(4)] string IngredientSku,
    [property: Id(5)] int UsageCount,
    [property: Id(6)] DateTime CreatedAt,
    [property: Id(7)] MappingSource Source);

// ============================================================================
// Grain Interface
// ============================================================================

/// <summary>
/// Grain for managing vendor-specific item mappings.
/// One grain per vendor per organization.
/// </summary>
public interface IVendorItemMappingGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initialize the vendor mapping record.
    /// </summary>
    Task<VendorMappingSnapshot> InitializeAsync(InitializeVendorMappingCommand command);

    /// <summary>
    /// Check if this vendor mapping exists.
    /// </summary>
    Task<bool> ExistsAsync();

    /// <summary>
    /// Get the mapping for a vendor item description.
    /// Returns null if no mapping exists.
    /// </summary>
    Task<MappingLookupResult> GetMappingAsync(string vendorDescription, string? vendorProductCode = null);

    /// <summary>
    /// Get suggested mappings for a vendor item description.
    /// Uses fuzzy matching against learned patterns.
    /// </summary>
    Task<IReadOnlyList<MappingSuggestion>> GetSuggestionsAsync(
        string vendorDescription,
        IReadOnlyList<IngredientInfo>? candidateIngredients = null,
        int maxSuggestions = 5);

    /// <summary>
    /// Learn a mapping from a confirmed document line.
    /// Called when a user confirms a purchase document.
    /// </summary>
    Task LearnMappingAsync(LearnMappingCommand command);

    /// <summary>
    /// Manually set or update a mapping.
    /// </summary>
    Task<VendorItemMapping> SetMappingAsync(SetMappingCommand command);

    /// <summary>
    /// Delete a mapping.
    /// </summary>
    Task DeleteMappingAsync(DeleteMappingCommand command);

    /// <summary>
    /// Record that a mapping was used (increments usage count).
    /// </summary>
    Task RecordUsageAsync(RecordMappingUsageCommand command);

    /// <summary>
    /// Get all mappings for this vendor.
    /// </summary>
    Task<IReadOnlyList<MappingSummary>> GetAllMappingsAsync();

    /// <summary>
    /// Get the current state snapshot.
    /// </summary>
    Task<VendorMappingSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Update vendor info (name, type).
    /// </summary>
    Task UpdateVendorInfoAsync(string? vendorName = null, VendorType? vendorType = null);
}

/// <summary>
/// Basic ingredient info for fuzzy matching.
/// </summary>
[GenerateSerializer]
public record IngredientInfo(
    [property: Id(0)] Guid Id,
    [property: Id(1)] string Name,
    [property: Id(2)] string Sku,
    [property: Id(3)] string? Category = null,
    [property: Id(4)] IReadOnlyList<string>? Aliases = null);
