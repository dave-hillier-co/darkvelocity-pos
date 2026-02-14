namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record RegisterProductCommand(
    [property: Id(0)] Guid OrgId,
    [property: Id(1)] string Name,
    [property: Id(2)] string BaseUnit,
    [property: Id(3)] string Category,
    [property: Id(4)] string? Description = null,
    [property: Id(5)] List<string>? Tags = null,
    [property: Id(6)] int? ShelfLifeDays = null,
    [property: Id(7)] string? StorageRequirements = null);

[GenerateSerializer]
public record UpdateProductCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] string? Description = null,
    [property: Id(2)] string? Category = null,
    [property: Id(3)] List<string>? Tags = null,
    [property: Id(4)] int? ShelfLifeDays = null,
    [property: Id(5)] string? StorageRequirements = null);

[GenerateSerializer]
public record ProductSnapshot(
    [property: Id(0)] Guid ProductId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] string Name,
    [property: Id(3)] string? Description,
    [property: Id(4)] string BaseUnit,
    [property: Id(5)] string Category,
    [property: Id(6)] IReadOnlyList<string> Tags,
    [property: Id(7)] int? ShelfLifeDays,
    [property: Id(8)] string? StorageRequirements,
    [property: Id(9)] IReadOnlyList<string> Allergens,
    [property: Id(10)] bool IsActive,
    [property: Id(11)] DateTime CreatedAt);

/// <summary>
/// Grain representing a canonical product identity.
/// Key: "{orgId}:product:{productId}"
/// </summary>
public interface IProductGrain : IGrainWithStringKey
{
    Task<ProductSnapshot> RegisterAsync(RegisterProductCommand command);
    Task<ProductSnapshot> UpdateAsync(UpdateProductCommand command);
    Task DeactivateAsync(string reason);
    Task ReactivateAsync();
    Task UpdateAllergensAsync(List<string> allergens);
    Task<ProductSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();
}
