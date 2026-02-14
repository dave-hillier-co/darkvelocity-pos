namespace DarkVelocity.Host.State;

[GenerateSerializer]
public sealed class ProductState
{
    [Id(0)] public Guid ProductId { get; set; }
    [Id(1)] public Guid OrgId { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string? Description { get; set; }
    [Id(4)] public string BaseUnit { get; set; } = string.Empty;
    [Id(5)] public string Category { get; set; } = string.Empty;
    [Id(6)] public List<string> Tags { get; set; } = [];
    [Id(7)] public int? ShelfLifeDays { get; set; }
    [Id(8)] public string? StorageRequirements { get; set; }
    [Id(9)] public List<string> Allergens { get; set; } = [];
    [Id(10)] public bool IsActive { get; set; } = true;
    [Id(11)] public DateTime CreatedAt { get; set; }
}
