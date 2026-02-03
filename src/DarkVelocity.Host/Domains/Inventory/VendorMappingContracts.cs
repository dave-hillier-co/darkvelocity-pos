using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

/// <summary>
/// Request to initialize a vendor mapping record.
/// </summary>
public record InitializeVendorMappingRequest
{
    /// <summary>Display name for the vendor</summary>
    public string? VendorName { get; init; }

    /// <summary>Type of vendor (Supplier, RetailStore)</summary>
    public VendorType? VendorType { get; init; }
}

/// <summary>
/// Request to set or update a mapping.
/// </summary>
public record SetMappingRequest
{
    /// <summary>Internal ingredient ID to map to</summary>
    public required Guid IngredientId { get; init; }

    /// <summary>Internal ingredient name</summary>
    public required string IngredientName { get; init; }

    /// <summary>Internal ingredient SKU</summary>
    public required string IngredientSku { get; init; }

    /// <summary>User who is setting this mapping</summary>
    public required Guid SetBy { get; init; }

    /// <summary>Vendor's product code if available</summary>
    public string? VendorProductCode { get; init; }

    /// <summary>Expected unit price for variance detection</summary>
    public decimal? ExpectedUnitPrice { get; init; }

    /// <summary>Unit of measure</summary>
    public string? Unit { get; init; }
}

/// <summary>
/// A single mapping for bulk import.
/// </summary>
public record BulkMappingItem
{
    /// <summary>Vendor's item description</summary>
    public required string VendorDescription { get; init; }

    /// <summary>Internal ingredient ID to map to</summary>
    public required Guid IngredientId { get; init; }

    /// <summary>Internal ingredient name</summary>
    public required string IngredientName { get; init; }

    /// <summary>Internal ingredient SKU</summary>
    public required string IngredientSku { get; init; }

    /// <summary>Vendor's product code if available</summary>
    public string? VendorProductCode { get; init; }

    /// <summary>Expected unit price for variance detection</summary>
    public decimal? ExpectedUnitPrice { get; init; }

    /// <summary>Unit of measure</summary>
    public string? Unit { get; init; }
}

/// <summary>
/// Request to bulk import mappings.
/// </summary>
public record BulkImportMappingsRequest
{
    /// <summary>Display name for the vendor (used if initializing)</summary>
    public string? VendorName { get; init; }

    /// <summary>Type of vendor (used if initializing)</summary>
    public VendorType? VendorType { get; init; }

    /// <summary>User performing the import</summary>
    public required Guid ImportedBy { get; init; }

    /// <summary>Mappings to import</summary>
    public required IReadOnlyList<BulkMappingItem> Mappings { get; init; }
}

/// <summary>
/// Request to learn a mapping from a confirmed document.
/// </summary>
public record LearnMappingRequest
{
    /// <summary>Vendor's item description</summary>
    public required string VendorDescription { get; init; }

    /// <summary>Internal ingredient ID to map to</summary>
    public required Guid IngredientId { get; init; }

    /// <summary>Internal ingredient name</summary>
    public required string IngredientName { get; init; }

    /// <summary>Internal ingredient SKU</summary>
    public required string IngredientSku { get; init; }

    /// <summary>Vendor's product code if available</summary>
    public string? VendorProductCode { get; init; }

    /// <summary>Document ID this mapping was learned from</summary>
    public Guid? LearnedFromDocumentId { get; init; }

    /// <summary>Unit price from the document</summary>
    public decimal? UnitPrice { get; init; }

    /// <summary>Unit of measure from the document</summary>
    public string? Unit { get; init; }
}
