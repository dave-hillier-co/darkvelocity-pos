using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Hal;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class ProductEndpoints
{
    public static WebApplication MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/products").WithTags("Products");

        group.MapPost("/", async (
            Guid orgId,
            [FromBody] RegisterProductRequest request,
            IGrainFactory grainFactory) =>
        {
            var productId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IProductGrain>(GrainKeys.Product(orgId, productId));
            var snapshot = await grain.RegisterAsync(new RegisterProductCommand(
                orgId, request.Name, request.BaseUnit, request.Category,
                request.Description, request.Tags, request.ShelfLifeDays, request.StorageRequirements));

            return Results.Created(
                $"/api/orgs/{orgId}/products/{productId}",
                Hal.Resource(snapshot, BuildProductLinks(orgId, productId)));
        });

        group.MapGet("/{productId}", async (Guid orgId, Guid productId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IProductGrain>(GrainKeys.Product(orgId, productId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Product not found"));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildProductLinks(orgId, productId)));
        });

        group.MapPut("/{productId}", async (
            Guid orgId, Guid productId,
            [FromBody] UpdateProductRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IProductGrain>(GrainKeys.Product(orgId, productId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Product not found"));

            var snapshot = await grain.UpdateAsync(new UpdateProductCommand(
                request.Name, request.Description, request.Category,
                request.Tags, request.ShelfLifeDays, request.StorageRequirements));

            return Results.Ok(Hal.Resource(snapshot, BuildProductLinks(orgId, productId)));
        });

        group.MapPost("/{productId}/deactivate", async (
            Guid orgId, Guid productId,
            [FromBody] DeactivateRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IProductGrain>(GrainKeys.Product(orgId, productId));
            await grain.DeactivateAsync(request.Reason);
            return Results.Ok();
        });

        group.MapPost("/{productId}/reactivate", async (
            Guid orgId, Guid productId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IProductGrain>(GrainKeys.Product(orgId, productId));
            await grain.ReactivateAsync();
            return Results.Ok();
        });

        group.MapPut("/{productId}/allergens", async (
            Guid orgId, Guid productId,
            [FromBody] UpdateAllergensRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IProductGrain>(GrainKeys.Product(orgId, productId));
            await grain.UpdateAllergensAsync(request.Allergens);
            return Results.Ok();
        });

        // SKU endpoints nested under product
        var skuGroup = app.MapGroup("/api/orgs/{orgId}/products/{productId}/skus").WithTags("SKUs");

        skuGroup.MapPost("/", async (
            Guid orgId, Guid productId,
            [FromBody] RegisterSkuRequest request,
            IGrainFactory grainFactory) =>
        {
            var skuId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<ISkuGrain>(GrainKeys.Sku(orgId, skuId));
            var snapshot = await grain.RegisterAsync(new RegisterSkuCommand(
                orgId, productId, request.Code, request.Description,
                request.Container, request.Barcode, request.DefaultSupplierId));

            return Results.Created(
                $"/api/orgs/{orgId}/products/{productId}/skus/{skuId}",
                Hal.Resource(snapshot, BuildSkuLinks(orgId, productId, skuId)));
        });

        skuGroup.MapGet("/{skuId}", async (
            Guid orgId, Guid productId, Guid skuId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISkuGrain>(GrainKeys.Sku(orgId, skuId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "SKU not found"));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildSkuLinks(orgId, productId, skuId)));
        });

        return app;
    }

    private static Dictionary<string, object> BuildProductLinks(Guid orgId, Guid productId)
    {
        var basePath = $"/api/orgs/{orgId}/products/{productId}";
        return new Dictionary<string, object>
        {
            ["self"] = new HalLink(basePath),
            ["skus"] = new HalLink($"{basePath}/skus", Title: "SKUs for this product"),
            ["deactivate"] = new HalLink($"{basePath}/deactivate", Title: "Deactivate product"),
            ["allergens"] = new HalLink($"{basePath}/allergens", Title: "Update allergens")
        };
    }

    private static Dictionary<string, object> BuildSkuLinks(Guid orgId, Guid productId, Guid skuId)
    {
        return new Dictionary<string, object>
        {
            ["self"] = new HalLink($"/api/orgs/{orgId}/products/{productId}/skus/{skuId}"),
            ["product"] = new HalLink($"/api/orgs/{orgId}/products/{productId}", Title: "Parent product")
        };
    }
}

// Request DTOs
public record RegisterProductRequest(
    string Name, string BaseUnit, string Category,
    string? Description = null, List<string>? Tags = null,
    int? ShelfLifeDays = null, string? StorageRequirements = null);

public record UpdateProductRequest(
    string? Name = null, string? Description = null, string? Category = null,
    List<string>? Tags = null, int? ShelfLifeDays = null, string? StorageRequirements = null);

public record DeactivateRequest(string Reason);
public record UpdateAllergensRequest(List<string> Allergens);

public record RegisterSkuRequest(
    string Code, string Description,
    DarkVelocity.Host.State.ContainerDefinition Container,
    string? Barcode = null, Guid? DefaultSupplierId = null);
