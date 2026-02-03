namespace DarkVelocity.Host.State;

/// <summary>
/// Represents a line item in an ordered collection.
/// Domain-agnostic - can represent order lines, purchase document lines, etc.
/// </summary>
[GenerateSerializer]
public record LineItem
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public int Index { get; init; }
    [Id(2)] public string ItemType { get; init; } = string.Empty;
    [Id(3)] public decimal Quantity { get; init; }
    [Id(4)] public decimal UnitPrice { get; init; }
    [Id(5)] public decimal ExtendedPrice { get; init; }
    [Id(6)] public Dictionary<string, string> Metadata { get; init; } = [];
    [Id(7)] public bool IsVoided { get; init; }
    [Id(8)] public Guid? VoidedBy { get; init; }
    [Id(9)] public DateTime? VoidedAt { get; init; }
    [Id(10)] public string? VoidReason { get; init; }
    [Id(11)] public DateTime CreatedAt { get; init; }
    [Id(12)] public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// State for the LineItemsGrain.
/// Manages an ordered collection of line items with void tracking.
/// </summary>
[GenerateSerializer]
public sealed class LineItemsState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public string OwnerType { get; set; } = string.Empty;
    [Id(2)] public Guid OwnerId { get; set; }
    [Id(3)] public List<LineItem> Lines { get; set; } = [];
    [Id(4)] public int NextIndex { get; set; }
    [Id(5)] public int Version { get; set; }
    [Id(6)] public DateTime CreatedAt { get; set; }
    [Id(7)] public DateTime? UpdatedAt { get; set; }
}
