namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Product events used in event sourcing.
/// </summary>
public interface IProductEvent
{
    Guid ProductId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record ProductRegistered : IProductEvent
{
    [Id(0)] public Guid ProductId { get; init; }
    [Id(1)] public Guid OrgId { get; init; }
    [Id(2)] public string Name { get; init; } = "";
    [Id(3)] public string? Description { get; init; }
    [Id(4)] public string BaseUnit { get; init; } = "";
    [Id(5)] public string Category { get; init; } = "";
    [Id(6)] public List<string> Tags { get; init; } = [];
    [Id(7)] public int? ShelfLifeDays { get; init; }
    [Id(8)] public string? StorageRequirements { get; init; }
    [Id(9)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ProductUpdated : IProductEvent
{
    [Id(0)] public Guid ProductId { get; init; }
    [Id(1)] public string? Name { get; init; }
    [Id(2)] public string? Description { get; init; }
    [Id(3)] public string? Category { get; init; }
    [Id(4)] public List<string>? Tags { get; init; }
    [Id(5)] public int? ShelfLifeDays { get; init; }
    [Id(6)] public string? StorageRequirements { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ProductDeactivated : IProductEvent
{
    [Id(0)] public Guid ProductId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ProductReactivated : IProductEvent
{
    [Id(0)] public Guid ProductId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ProductAllergensUpdated : IProductEvent
{
    [Id(0)] public Guid ProductId { get; init; }
    [Id(1)] public List<string> Allergens { get; init; } = [];
    [Id(2)] public DateTime OccurredAt { get; init; }
}
