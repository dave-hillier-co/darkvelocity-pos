using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class FloorPlanEndpoints
{
    public static WebApplication MapFloorPlanEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/floor-plans").WithTags("FloorPlans");

        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] CreateFloorPlanRequest request,
            IGrainFactory grainFactory) =>
        {
            var floorPlanId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IFloorPlanGrain>(GrainKeys.FloorPlan(orgId, siteId, floorPlanId));
            var result = await grain.CreateAsync(new CreateFloorPlanCommand(
                orgId, siteId, request.Name, request.IsDefault, request.Width, request.Height));

            return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/floor-plans/{floorPlanId}", Hal.Resource(new
            {
                id = result.Id,
                name = result.Name,
                createdAt = result.CreatedAt
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/floor-plans/{floorPlanId}" },
                ["tables"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/floor-plans/{floorPlanId}/tables" }
            }));
        });

        group.MapGet("/{floorPlanId}", async (Guid orgId, Guid siteId, Guid floorPlanId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IFloorPlanGrain>(GrainKeys.FloorPlan(orgId, siteId, floorPlanId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Floor plan not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/floor-plans/{floorPlanId}" },
                ["tables"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/floor-plans/{floorPlanId}/tables" }
            }));
        });

        group.MapPatch("/{floorPlanId}", async (
            Guid orgId, Guid siteId, Guid floorPlanId,
            [FromBody] UpdateFloorPlanRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IFloorPlanGrain>(GrainKeys.FloorPlan(orgId, siteId, floorPlanId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Floor plan not found"));

            await grain.UpdateAsync(new UpdateFloorPlanCommand(request.Name, request.Width, request.Height, request.BackgroundImageUrl, request.IsActive));
            var state = await grain.GetStateAsync();

            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/floor-plans/{floorPlanId}" }
            }));
        });

        group.MapPost("/{floorPlanId}/tables", async (
            Guid orgId, Guid siteId, Guid floorPlanId,
            [FromBody] AddTableToFloorPlanRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IFloorPlanGrain>(GrainKeys.FloorPlan(orgId, siteId, floorPlanId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Floor plan not found"));

            await grain.AddTableAsync(request.TableId);
            return Results.Ok(new { message = "Table added to floor plan" });
        });

        group.MapDelete("/{floorPlanId}/tables/{tableId}", async (Guid orgId, Guid siteId, Guid floorPlanId, Guid tableId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IFloorPlanGrain>(GrainKeys.FloorPlan(orgId, siteId, floorPlanId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Floor plan not found"));

            await grain.RemoveTableAsync(tableId);
            return Results.NoContent();
        });

        return app;
    }
}
