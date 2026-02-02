using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events;

// ============================================================================
// Vendor Item Mapping Events
// ============================================================================

/// <summary>
/// A vendor mapping record was initialized.
/// </summary>
public sealed record VendorMappingInitialized : DomainEvent
{
    public override string EventType => "vendor-mapping.initialized";
    public override string AggregateType => "VendorItemMapping";
    public override Guid AggregateId => Guid.Empty; // String key based

    public required Guid OrganizationId { get; init; }
    public required string VendorId { get; init; }
    public required string VendorName { get; init; }
    public required VendorType VendorType { get; init; }
}

/// <summary>
/// A mapping was learned from a confirmed purchase document.
/// </summary>
public sealed record ItemMappingLearned : DomainEvent
{
    public override string EventType => "vendor-mapping.item.learned";
    public override string AggregateType => "VendorItemMapping";
    public override Guid AggregateId => Guid.Empty;

    public required Guid OrganizationId { get; init; }
    public required string VendorId { get; init; }
    public required string VendorDescription { get; init; }
    public string? VendorProductCode { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required string IngredientSku { get; init; }
    public required MappingSource Source { get; init; }
    public required decimal Confidence { get; init; }
    public Guid? LearnedFromDocumentId { get; init; }
}

/// <summary>
/// A mapping was manually set or updated.
/// </summary>
public sealed record ItemMappingSet : DomainEvent
{
    public override string EventType => "vendor-mapping.item.set";
    public override string AggregateType => "VendorItemMapping";
    public override Guid AggregateId => Guid.Empty;

    public required Guid OrganizationId { get; init; }
    public required string VendorId { get; init; }
    public required string VendorDescription { get; init; }
    public string? VendorProductCode { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public required string IngredientSku { get; init; }
    public required Guid SetBy { get; init; }
    public decimal? ExpectedUnitPrice { get; init; }
}

/// <summary>
/// A mapping was deleted.
/// </summary>
public sealed record ItemMappingDeleted : DomainEvent
{
    public override string EventType => "vendor-mapping.item.deleted";
    public override string AggregateType => "VendorItemMapping";
    public override Guid AggregateId => Guid.Empty;

    public required Guid OrganizationId { get; init; }
    public required string VendorId { get; init; }
    public required string VendorDescription { get; init; }
    public required Guid DeletedBy { get; init; }
}

/// <summary>
/// A mapping was used (usage count incremented).
/// </summary>
public sealed record ItemMappingUsed : DomainEvent
{
    public override string EventType => "vendor-mapping.item.used";
    public override string AggregateType => "VendorItemMapping";
    public override Guid AggregateId => Guid.Empty;

    public required Guid OrganizationId { get; init; }
    public required string VendorId { get; init; }
    public required string VendorDescription { get; init; }
    public required Guid DocumentId { get; init; }
}

/// <summary>
/// A learned pattern was added or reinforced.
/// </summary>
public sealed record PatternLearned : DomainEvent
{
    public override string EventType => "vendor-mapping.pattern.learned";
    public override string AggregateType => "VendorItemMapping";
    public override Guid AggregateId => Guid.Empty;

    public required Guid OrganizationId { get; init; }
    public required string VendorId { get; init; }
    public required IReadOnlyList<string> Tokens { get; init; }
    public required Guid IngredientId { get; init; }
    public required string IngredientName { get; init; }
    public bool IsReinforcement { get; init; }
}
