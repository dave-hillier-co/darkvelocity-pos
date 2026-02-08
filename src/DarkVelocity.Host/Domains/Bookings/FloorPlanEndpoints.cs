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
                ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
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
            return Results.Ok(Hal.Resource(new { tableId = request.TableId, added = true }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/floor-plans/{floorPlanId}" },
                ["table"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/{request.TableId}" },
                ["tables"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/floor-plans/{floorPlanId}/tables" }
            }));
        });

        group.MapDelete("/{floorPlanId}/tables/{tableId}", async (Guid orgId, Guid siteId, Guid floorPlanId, Guid tableId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IFloorPlanGrain>(GrainKeys.FloorPlan(orgId, siteId, floorPlanId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Floor plan not found"));

            await grain.RemoveTableAsync(tableId);
            return Results.NoContent();
        });

        // Section-table assignment
        group.MapPost("/{floorPlanId}/sections/{sectionId}/tables/{tableId}", async (
            Guid orgId, Guid siteId, Guid floorPlanId, Guid sectionId, Guid tableId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IFloorPlanGrain>(GrainKeys.FloorPlan(orgId, siteId, floorPlanId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Floor plan not found"));

            await grain.AssignTableToSectionAsync(tableId, sectionId);
            return Results.Ok(Hal.Resource(new { tableId, sectionId, assigned = true }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/floor-plans/{floorPlanId}/sections/{sectionId}/tables/{tableId}" },
                ["table"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/{tableId}" },
                ["floor-plan"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/floor-plans/{floorPlanId}" }
            }));
        });

        group.MapDelete("/{floorPlanId}/sections/{sectionId}/tables/{tableId}", async (
            Guid orgId, Guid siteId, Guid floorPlanId, Guid sectionId, Guid tableId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IFloorPlanGrain>(GrainKeys.FloorPlan(orgId, siteId, floorPlanId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Floor plan not found"));

            await grain.UnassignTableFromSectionAsync(tableId);
            return Results.NoContent();
        });

        return app;
    }
}
