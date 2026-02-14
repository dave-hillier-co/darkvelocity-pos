namespace DarkVelocity.Host.State;

[GenerateSerializer]
public sealed class LocationNode
{
    [Id(0)] public Guid LocationId { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public Guid? ParentId { get; set; }
    [Id(3)] public string Path { get; set; } = string.Empty;
    [Id(4)] public int SortOrder { get; set; }
    [Id(5)] public bool IsActive { get; set; } = true;
}

[GenerateSerializer]
public sealed class LocationRegistryState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public Dictionary<Guid, LocationNode> Locations { get; set; } = [];
    [Id(3)] public List<Guid> RootLocationIds { get; set; } = [];
}
