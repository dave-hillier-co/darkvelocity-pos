namespace DarkVelocity.Host.Contracts;

public record CreateMenuCategoryRequest(
    Guid LocationId,
    string Name,
    string? Description,
    int DisplayOrder,
    string? Color);

public record CreateMenuItemRequest(
    Guid LocationId,
    Guid CategoryId,
    string Name,
    decimal Price,
    Guid? AccountingGroupId = null,
    Guid? RecipeId = null,
    string? Description = null,
    string? ImageUrl = null,
    string? Sku = null,
    bool TrackInventory = false);

public record UpdateMenuItemRequest(
    Guid? CategoryId = null,
    Guid? AccountingGroupId = null,
    Guid? RecipeId = null,
    string? Name = null,
    string? Description = null,
    decimal? Price = null,
    string? ImageUrl = null,
    string? Sku = null,
    bool? IsActive = null,
    bool? TrackInventory = null);
