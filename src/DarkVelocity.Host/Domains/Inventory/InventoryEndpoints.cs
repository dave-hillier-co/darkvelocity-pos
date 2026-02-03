using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class InventoryEndpoints
{
    public static WebApplication MapInventoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/inventory").WithTags("Inventory");

        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] InitializeInventoryRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, request.IngredientId));
            await grain.InitializeAsync(new InitializeInventoryCommand(
                orgId, siteId, request.IngredientId, request.IngredientName, request.Sku, request.Unit, request.Category, request.ReorderPoint, request.ParLevel));

            return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/inventory/{request.IngredientId}", Hal.Resource(new
            {
                ingredientId = request.IngredientId,
                ingredientName = request.IngredientName
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{request.IngredientId}" },
                ["receive"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{request.IngredientId}/receive" }
            }));
        });

        group.MapGet("/{ingredientId}", async (Guid orgId, Guid siteId, Guid ingredientId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}" },
                ["receive"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/receive" },
                ["consume"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/consume" },
                ["adjust"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/adjust" }
            }));
        });

        group.MapPost("/{ingredientId}/receive", async (
            Guid orgId, Guid siteId, Guid ingredientId,
            [FromBody] ReceiveBatchRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

            var result = await grain.ReceiveBatchAsync(new ReceiveBatchCommand(
                request.BatchNumber, request.Quantity, request.UnitCost, request.ExpiryDate, request.SupplierId, request.DeliveryId, request.Location, request.Notes, request.ReceivedBy));

            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["inventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}" }
            }));
        });

        group.MapPost("/{ingredientId}/consume", async (
            Guid orgId, Guid siteId, Guid ingredientId,
            [FromBody] ConsumeStockRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

            var result = await grain.ConsumeAsync(new ConsumeStockCommand(request.Quantity, request.Reason, request.OrderId, request.PerformedBy));
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["inventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}" }
            }));
        });

        group.MapPost("/{ingredientId}/adjust", async (
            Guid orgId, Guid siteId, Guid ingredientId,
            [FromBody] AdjustInventoryRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

            await grain.AdjustQuantityAsync(new AdjustQuantityCommand(request.NewQuantity, request.Reason, request.AdjustedBy, request.ApprovedBy));
            var level = await grain.GetLevelInfoAsync();

            return Results.Ok(Hal.Resource(level, new Dictionary<string, object>
            {
                ["inventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}" }
            }));
        });

        group.MapGet("/{ingredientId}/level", async (Guid orgId, Guid siteId, Guid ingredientId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

            var level = await grain.GetLevelInfoAsync();
            return Results.Ok(Hal.Resource(level, new Dictionary<string, object>
            {
                ["inventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}" }
            }));
        });

        return app;
    }
}
