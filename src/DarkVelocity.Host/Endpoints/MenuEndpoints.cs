using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class MenuEndpoints
{
    public static WebApplication MapMenuEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/menu").WithTags("Menu");

        group.MapPost("/categories", async (
            Guid orgId,
            [FromBody] CreateMenuCategoryRequest request,
            IGrainFactory grainFactory) =>
        {
            var categoryId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IMenuCategoryGrain>(GrainKeys.MenuCategory(orgId, categoryId));
            var result = await grain.CreateAsync(new CreateMenuCategoryCommand(
                request.LocationId, request.Name, request.Description, request.DisplayOrder, request.Color));

            return Results.Created($"/api/orgs/{orgId}/menu/categories/{categoryId}", Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/categories/{categoryId}" },
                ["items"] = new { href = $"/api/orgs/{orgId}/menu/categories/{categoryId}/items" }
            }));
        });

        group.MapGet("/categories/{categoryId}", async (Guid orgId, Guid categoryId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuCategoryGrain>(GrainKeys.MenuCategory(orgId, categoryId));
            var snapshot = await grain.GetSnapshotAsync();

            return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/categories/{categoryId}" },
                ["items"] = new { href = $"/api/orgs/{orgId}/menu/categories/{categoryId}/items" }
            }));
        });

        group.MapPost("/items", async (
            Guid orgId,
            [FromBody] CreateMenuItemRequest request,
            IGrainFactory grainFactory) =>
        {
            var itemId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IMenuItemGrain>(GrainKeys.MenuItem(orgId, itemId));
            var result = await grain.CreateAsync(new CreateMenuItemCommand(
                request.LocationId, request.CategoryId, request.AccountingGroupId, request.RecipeId,
                request.Name, request.Description, request.Price, request.ImageUrl, request.Sku, request.TrackInventory));

            var categoryGrain = grainFactory.GetGrain<IMenuCategoryGrain>(GrainKeys.MenuCategory(orgId, request.CategoryId));
            await categoryGrain.IncrementItemCountAsync();

            return Results.Created($"/api/orgs/{orgId}/menu/items/{itemId}", Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/items/{itemId}" },
                ["category"] = new { href = $"/api/orgs/{orgId}/menu/categories/{request.CategoryId}" }
            }));
        });

        group.MapGet("/items/{itemId}", async (Guid orgId, Guid itemId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuItemGrain>(GrainKeys.MenuItem(orgId, itemId));
            var snapshot = await grain.GetSnapshotAsync();

            return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/items/{itemId}" },
                ["category"] = new { href = $"/api/orgs/{orgId}/menu/categories/{snapshot.CategoryId}" }
            }));
        });

        group.MapPatch("/items/{itemId}", async (
            Guid orgId,
            Guid itemId,
            [FromBody] UpdateMenuItemRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IMenuItemGrain>(GrainKeys.MenuItem(orgId, itemId));
            var result = await grain.UpdateAsync(new UpdateMenuItemCommand(
                request.CategoryId, request.AccountingGroupId, request.RecipeId, request.Name, request.Description,
                request.Price, request.ImageUrl, request.Sku, request.IsActive, request.TrackInventory));

            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/menu/items/{itemId}" }
            }));
        });

        return app;
    }
}
