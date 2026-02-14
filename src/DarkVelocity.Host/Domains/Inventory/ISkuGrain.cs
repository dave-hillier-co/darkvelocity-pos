using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record RegisterSkuCommand(
    [property: Id(0)] Guid OrgId,
    [property: Id(1)] Guid ProductId,
    [property: Id(2)] string Code,
    [property: Id(3)] string Description,
    [property: Id(4)] ContainerDefinition Container,
    [property: Id(5)] string? Barcode = null,
    [property: Id(6)] Guid? DefaultSupplierId = null);

[GenerateSerializer]
public record UpdateSkuCommand(
    [property: Id(0)] string? Code = null,
    [property: Id(1)] string? Description = null,
    [property: Id(2)] ContainerDefinition? Container = null,
    [property: Id(3)] Guid? DefaultSupplierId = null);

[GenerateSerializer]
public record SkuSnapshot(
    [property: Id(0)] Guid SkuId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] Guid ProductId,
    [property: Id(3)] string Code,
    [property: Id(4)] string? Barcode,
    [property: Id(5)] string Description,
    [property: Id(6)] ContainerDefinition Container,
    [property: Id(7)] Guid? DefaultSupplierId,
    [property: Id(8)] bool IsActive,
    [property: Id(9)] DateTime CreatedAt,
    [property: Id(10)] decimal BaseUnitQuantity,
    [property: Id(11)] string LeafUnit);

/// <summary>
/// Grain representing a purchasable form of a product (product + packaging).
/// Key: "{orgId}:sku:{skuId}"
/// </summary>
public interface ISkuGrain : IGrainWithStringKey
{
    Task<SkuSnapshot> RegisterAsync(RegisterSkuCommand command);
    Task<SkuSnapshot> UpdateAsync(UpdateSkuCommand command);
    Task DeactivateAsync(string reason);
    Task AssignBarcodeAsync(string barcode);
    Task<SkuSnapshot> GetSnapshotAsync();
    Task<decimal> GetBaseUnitQuantityAsync();
    Task<bool> ExistsAsync();
}
