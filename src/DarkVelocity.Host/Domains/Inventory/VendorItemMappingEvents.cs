using DarkVelocity.Host.State;
using Orleans;

namespace DarkVelocity.Host.Events;

// ============================================================================
// Vendor Item Mapping Events
// ============================================================================

/// <summary>
/// A vendor mapping record was initialized.
/// </summary>
[GenerateSerializer]
public sealed record VendorMappingInitialized : DomainEvent
{
    public override string EventType => "vendor-mapping.initialized";
    public override string AggregateType => "VendorItemMapping";
    public override Guid AggregateId => Guid.Empty; // String key based

    [Id(100)] public required Guid OrganizationId { get; init; }
    [Id(101)] public required string VendorId { get; init; }
    [Id(102)] public required string VendorName { get; init; }
    [Id(103)] public required VendorType VendorType { get; init; }
}

/// <summary>
/// A mapping was learned from a confirmed purchase document.
/// </summary>
[GenerateSerializer]
public sealed record ItemMappingLearned : DomainEvent
{
    public override string EventType => "vendor-mapping.item.learned";
    public override string AggregateType => "VendorItemMapping";
    public override Guid AggregateId => Guid.Empty;

    [Id(100)] public required Guid OrganizationId { get; init; }
    [Id(101)] public required string VendorId { get; init; }
    [Id(102)] public required string VendorDescription { get; init; }
    [Id(103)] public string? VendorProductCode { get; init; }
    [Id(104)] public required Guid IngredientId { get; init; }
    [Id(105)] public required string IngredientName { get; init; }
    [Id(106)] public required string IngredientSku { get; init; }
    [Id(107)] public required MappingSource MappingOrigin { get; init; }
    [Id(108)] public required decimal Confidence { get; init; }
    [Id(109)] public Guid? LearnedFromDocumentId { get; init; }
    [Id(110)] public Guid? ProductId { get; init; }
    [Id(111)] public Guid? SkuId { get; init; }
}

/// <summary>
/// A mapping was manually set or updated.
/// </summary>
[GenerateSerializer]
public sealed record ItemMappingSet : DomainEvent
{
    public override string EventType => "vendor-mapping.item.set";
    public override string AggregateType => "VendorItemMapping";
    public override Guid AggregateId => Guid.Empty;

    [Id(100)] public required Guid OrganizationId { get; init; }
    [Id(101)] public required string VendorId { get; init; }
    [Id(102)] public required string VendorDescription { get; init; }
    [Id(103)] public string? VendorProductCode { get; init; }
    [Id(104)] public required Guid IngredientId { get; init; }
    [Id(105)] public required string IngredientName { get; init; }
    [Id(106)] public required string IngredientSku { get; init; }
    [Id(107)] public required Guid SetBy { get; init; }
    [Id(108)] public decimal? ExpectedUnitPrice { get; init; }
    [Id(109)] public Guid? ProductId { get; init; }
    [Id(110)] public Guid? SkuId { get; init; }
}

/// <summary>
/// A mapping was deleted.
/// </summary>
[GenerateSerializer]
public sealed record ItemMappingDeleted : DomainEvent
{
    public override string EventType => "vendor-mapping.item.deleted";
    public override string AggregateType => "VendorItemMapping";
    public override Guid AggregateId => Guid.Empty;

    [Id(100)] public required Guid OrganizationId { get; init; }
    [Id(101)] public required string VendorId { get; init; }
    [Id(102)] public required string VendorDescription { get; init; }
    [Id(103)] public required Guid DeletedBy { get; init; }
}

/// <summary>
/// A mapping was used (usage count incremented).
/// </summary>
[GenerateSerializer]
public sealed record ItemMappingUsed : DomainEvent
{
    public override string EventType => "vendor-mapping.item.used";
    public override string AggregateType => "VendorItemMapping";
    public override Guid AggregateId => Guid.Empty;

    [Id(100)] public required Guid OrganizationId { get; init; }
    [Id(101)] public required string VendorId { get; init; }
    [Id(102)] public required string VendorDescription { get; init; }
    [Id(103)] public required Guid DocumentId { get; init; }
}

/// <summary>
/// A learned pattern was added or reinforced.
/// </summary>
[GenerateSerializer]
public sealed record PatternLearned : DomainEvent
{
    public override string EventType => "vendor-mapping.pattern.learned";
    public override string AggregateType => "VendorItemMapping";
    public override Guid AggregateId => Guid.Empty;

    [Id(100)] public required Guid OrganizationId { get; init; }
    [Id(101)] public required string VendorId { get; init; }
    [Id(102)] public required IReadOnlyList<string> Tokens { get; init; }
    [Id(103)] public required Guid IngredientId { get; init; }
    [Id(104)] public required string IngredientName { get; init; }
    [Id(105)] public bool IsReinforcement { get; init; }
}
