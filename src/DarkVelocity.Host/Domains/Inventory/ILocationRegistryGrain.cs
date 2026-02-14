using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record AddLocationCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] Guid? ParentId = null,
    [property: Id(2)] int SortOrder = 0);

[GenerateSerializer]
public record LocationSnapshot(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] string Name,
    [property: Id(2)] Guid? ParentId,
    [property: Id(3)] string Path,
    [property: Id(4)] int SortOrder,
    [property: Id(5)] bool IsActive,
    [property: Id(6)] IReadOnlyList<LocationSnapshot> Children);

/// <summary>
/// Grain for managing hierarchical locations within a site.
/// Key: "{orgId}:{siteId}:locations"
/// </summary>
public interface ILocationRegistryGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid orgId, Guid siteId);
    Task<LocationSnapshot> AddLocationAsync(AddLocationCommand command);
    Task RenameLocationAsync(Guid locationId, string newName);
    Task MoveLocationAsync(Guid locationId, Guid? newParentId);
    Task DeactivateLocationAsync(Guid locationId);
    Task<IReadOnlyList<LocationSnapshot>> GetTreeAsync();
    Task<LocationSnapshot?> GetLocationAsync(Guid locationId);
    Task<IReadOnlyList<Guid>> GetSubtreeIdsAsync(Guid locationId);
}
