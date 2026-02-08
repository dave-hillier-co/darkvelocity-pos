namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Floor Plan events used in event sourcing.
/// </summary>
public interface IFloorPlanEvent
{
    Guid FloorPlanId { get; }
    DateTimeOffset OccurredAt { get; }
}

/// <summary>
/// Domain event: A floor plan was created.
/// </summary>
[GenerateSerializer]
public sealed record FloorPlanCreated(
    [property: Id(0)] Guid FloorPlanId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid OrgId,
    [property: Id(3)] Guid SiteId,
    [property: Id(4)] string Name,
    [property: Id(5)] bool IsDefault,
    [property: Id(6)] int Width,
    [property: Id(7)] int Height,
    [property: Id(8)] Guid? CreatedBy = null
) : IFloorPlanEvent;

/// <summary>
/// Domain event: A floor plan was updated.
/// </summary>
[GenerateSerializer]
public sealed record FloorPlanUpdated(
    [property: Id(0)] Guid FloorPlanId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] string? Name = null,
    [property: Id(3)] int? Width = null,
    [property: Id(4)] int? Height = null,
    [property: Id(5)] string? BackgroundImageUrl = null,
    [property: Id(6)] bool? IsActive = null
) : IFloorPlanEvent;

/// <summary>
/// Domain event: A table was added to the floor plan.
/// </summary>
[GenerateSerializer]
public sealed record FloorPlanTableAdded(
    [property: Id(0)] Guid FloorPlanId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid TableId
) : IFloorPlanEvent;

/// <summary>
/// Domain event: A table was removed from the floor plan.
/// </summary>
[GenerateSerializer]
public sealed record FloorPlanTableRemoved(
    [property: Id(0)] Guid FloorPlanId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid TableId
) : IFloorPlanEvent;

/// <summary>
/// Domain event: A section was added to the floor plan.
/// </summary>
[GenerateSerializer]
public sealed record FloorPlanSectionAdded(
    [property: Id(0)] Guid FloorPlanId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid SectionId,
    [property: Id(3)] string Name,
    [property: Id(4)] string? Color = null,
    [property: Id(5)] int SortOrder = 0
) : IFloorPlanEvent;

/// <summary>
/// Domain event: A section was removed from the floor plan.
/// </summary>
[GenerateSerializer]
public sealed record FloorPlanSectionRemoved(
    [property: Id(0)] Guid FloorPlanId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid SectionId
) : IFloorPlanEvent;

/// <summary>
/// Domain event: A section was updated on the floor plan.
/// </summary>
[GenerateSerializer]
public sealed record FloorPlanSectionUpdated(
    [property: Id(0)] Guid FloorPlanId,
    [property: Id(1)] DateTimeOffset OccurredAt,
    [property: Id(2)] Guid SectionId,
    [property: Id(3)] string? Name = null,
    [property: Id(4)] string? Color = null,
    [property: Id(5)] int? SortOrder = null
) : IFloorPlanEvent;

/// <summary>
/// Domain event: The floor plan was set as the default.
/// </summary>
[GenerateSerializer]
public sealed record FloorPlanDefaultSet(
    [property: Id(0)] Guid FloorPlanId,
    [property: Id(1)] DateTimeOffset OccurredAt
) : IFloorPlanEvent;

/// <summary>
/// Domain event: The floor plan was activated.
/// </summary>
[GenerateSerializer]
public sealed record FloorPlanActivated(
    [property: Id(0)] Guid FloorPlanId,
    [property: Id(1)] DateTimeOffset OccurredAt
) : IFloorPlanEvent;

/// <summary>
/// Domain event: The floor plan was deactivated.
/// </summary>
[GenerateSerializer]
public sealed record FloorPlanDeactivated(
    [property: Id(0)] Guid FloorPlanId,
    [property: Id(1)] DateTimeOffset OccurredAt
) : IFloorPlanEvent;
