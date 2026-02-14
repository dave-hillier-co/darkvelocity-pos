namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Location events used in event sourcing.
/// </summary>
public interface ILocationEvent
{
    Guid SiteId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record LocationRegistryInitialized : ILocationEvent
{
    [Id(0)] public Guid OrgId { get; init; }
    [Id(1)] public Guid SiteId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record LocationAdded : ILocationEvent
{
    [Id(0)] public Guid SiteId { get; init; }
    [Id(1)] public Guid LocationId { get; init; }
    [Id(2)] public string Name { get; init; } = "";
    [Id(3)] public Guid? ParentId { get; init; }
    [Id(4)] public int SortOrder { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record LocationRenamed : ILocationEvent
{
    [Id(0)] public Guid SiteId { get; init; }
    [Id(1)] public Guid LocationId { get; init; }
    [Id(2)] public string NewName { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record LocationMoved : ILocationEvent
{
    [Id(0)] public Guid SiteId { get; init; }
    [Id(1)] public Guid LocationId { get; init; }
    [Id(2)] public Guid? NewParentId { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record LocationDeactivated : ILocationEvent
{
    [Id(0)] public Guid SiteId { get; init; }
    [Id(1)] public Guid LocationId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}
