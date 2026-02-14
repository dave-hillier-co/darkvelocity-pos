using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class LocationRegistryGrain : JournaledGrain<LocationRegistryState, ILocationEvent>, ILocationRegistryGrain
{
    private readonly ILogger<LocationRegistryGrain> _logger;

    public LocationRegistryGrain(ILogger<LocationRegistryGrain> logger)
    {
        _logger = logger;
    }

    protected override void TransitionState(LocationRegistryState state, ILocationEvent @event)
    {
        switch (@event)
        {
            case LocationRegistryInitialized e:
                state.OrgId = e.OrgId;
                state.SiteId = e.SiteId;
                break;

            case LocationAdded e:
                var parentPath = "";
                if (e.ParentId.HasValue && state.Locations.TryGetValue(e.ParentId.Value, out var parent))
                {
                    parentPath = parent.Path;
                }
                var path = string.IsNullOrEmpty(parentPath) ? $"/{e.Name}" : $"{parentPath}/{e.Name}";

                state.Locations[e.LocationId] = new LocationNode
                {
                    LocationId = e.LocationId,
                    Name = e.Name,
                    ParentId = e.ParentId,
                    Path = path,
                    SortOrder = e.SortOrder,
                    IsActive = true
                };

                if (!e.ParentId.HasValue)
                {
                    state.RootLocationIds.Add(e.LocationId);
                }
                break;

            case LocationRenamed e:
                if (state.Locations.TryGetValue(e.LocationId, out var renamedNode))
                {
                    renamedNode.Name = e.NewName;
                    RebuildPaths(state, e.LocationId);
                }
                break;

            case LocationMoved e:
                if (state.Locations.TryGetValue(e.LocationId, out var movedNode))
                {
                    // Remove from old parent's root list if applicable
                    if (!movedNode.ParentId.HasValue)
                    {
                        state.RootLocationIds.Remove(e.LocationId);
                    }

                    movedNode.ParentId = e.NewParentId;

                    // Add to root if no parent
                    if (!e.NewParentId.HasValue)
                    {
                        state.RootLocationIds.Add(e.LocationId);
                    }

                    RebuildPaths(state, e.LocationId);
                }
                break;

            case LocationDeactivated e:
                if (state.Locations.TryGetValue(e.LocationId, out var deactivatedNode))
                {
                    deactivatedNode.IsActive = false;
                }
                break;
        }
    }

    private static void RebuildPaths(LocationRegistryState state, Guid startId)
    {
        if (!state.Locations.TryGetValue(startId, out var node)) return;

        var parentPath = "";
        if (node.ParentId.HasValue && state.Locations.TryGetValue(node.ParentId.Value, out var parent))
        {
            parentPath = parent.Path;
        }
        node.Path = string.IsNullOrEmpty(parentPath) ? $"/{node.Name}" : $"{parentPath}/{node.Name}";

        // Rebuild children paths
        foreach (var child in state.Locations.Values.Where(l => l.ParentId == startId))
        {
            RebuildPaths(state, child.LocationId);
        }
    }

    public async Task InitializeAsync(Guid orgId, Guid siteId)
    {
        RaiseEvent(new LocationRegistryInitialized
        {
            OrgId = orgId,
            SiteId = siteId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task<LocationSnapshot> AddLocationAsync(AddLocationCommand command)
    {
        if (command.ParentId.HasValue && !State.Locations.ContainsKey(command.ParentId.Value))
            throw new InvalidOperationException("Parent location not found");

        var locationId = Guid.NewGuid();

        RaiseEvent(new LocationAdded
        {
            SiteId = State.SiteId,
            LocationId = locationId,
            Name = command.Name,
            ParentId = command.ParentId,
            SortOrder = command.SortOrder,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogInformation("Location added: {Name} at site {SiteId}", command.Name, State.SiteId);
        return BuildSnapshot(locationId)!;
    }

    public async Task RenameLocationAsync(Guid locationId, string newName)
    {
        if (!State.Locations.ContainsKey(locationId))
            throw new InvalidOperationException("Location not found");

        RaiseEvent(new LocationRenamed
        {
            SiteId = State.SiteId,
            LocationId = locationId,
            NewName = newName,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task MoveLocationAsync(Guid locationId, Guid? newParentId)
    {
        if (!State.Locations.ContainsKey(locationId))
            throw new InvalidOperationException("Location not found");

        if (newParentId.HasValue)
        {
            if (!State.Locations.ContainsKey(newParentId.Value))
                throw new InvalidOperationException("New parent location not found");

            // Prevent circular references
            var subtreeIds = GetSubtreeIdsInternal(locationId);
            if (subtreeIds.Contains(newParentId.Value))
                throw new InvalidOperationException("Cannot move a location under its own subtree");
        }

        RaiseEvent(new LocationMoved
        {
            SiteId = State.SiteId,
            LocationId = locationId,
            NewParentId = newParentId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task DeactivateLocationAsync(Guid locationId)
    {
        if (!State.Locations.ContainsKey(locationId))
            throw new InvalidOperationException("Location not found");

        RaiseEvent(new LocationDeactivated
        {
            SiteId = State.SiteId,
            LocationId = locationId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<IReadOnlyList<LocationSnapshot>> GetTreeAsync()
    {
        var roots = State.RootLocationIds
            .Where(id => State.Locations.ContainsKey(id) && State.Locations[id].IsActive)
            .Select(id => BuildSnapshot(id)!)
            .OrderBy(s => s.SortOrder)
            .ToList();

        return Task.FromResult<IReadOnlyList<LocationSnapshot>>(roots);
    }

    public Task<LocationSnapshot?> GetLocationAsync(Guid locationId)
    {
        return Task.FromResult(BuildSnapshot(locationId));
    }

    public Task<IReadOnlyList<Guid>> GetSubtreeIdsAsync(Guid locationId)
    {
        return Task.FromResult<IReadOnlyList<Guid>>(GetSubtreeIdsInternal(locationId));
    }

    private List<Guid> GetSubtreeIdsInternal(Guid locationId)
    {
        var result = new List<Guid> { locationId };
        foreach (var child in State.Locations.Values.Where(l => l.ParentId == locationId))
        {
            result.AddRange(GetSubtreeIdsInternal(child.LocationId));
        }
        return result;
    }

    private LocationSnapshot? BuildSnapshot(Guid locationId)
    {
        if (!State.Locations.TryGetValue(locationId, out var node))
            return null;

        var children = State.Locations.Values
            .Where(l => l.ParentId == locationId && l.IsActive)
            .OrderBy(l => l.SortOrder)
            .Select(l => BuildSnapshot(l.LocationId)!)
            .ToList();

        return new LocationSnapshot(
            node.LocationId,
            node.Name,
            node.ParentId,
            node.Path,
            node.SortOrder,
            node.IsActive,
            children);
    }
}
