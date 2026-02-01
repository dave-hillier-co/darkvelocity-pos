using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class TableEndpoints
{
    public static WebApplication MapTableEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/tables").WithTags("Tables");

        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] CreateTableRequest request,
            IGrainFactory grainFactory) =>
        {
            var tableId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, siteId, tableId));
            var result = await grain.CreateAsync(new CreateTableCommand(
                orgId, siteId, request.Number, request.MinCapacity, request.MaxCapacity,
                request.Name, request.Shape, request.FloorPlanId));

            return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/tables/{tableId}", Hal.Resource(new
            {
                id = result.Id,
                number = result.Number,
                createdAt = result.CreatedAt
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/{tableId}" },
                ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" }
            }));
        });

        group.MapGet("/{tableId}", async (Guid orgId, Guid siteId, Guid tableId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, siteId, tableId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Table not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/{tableId}" },
                ["seat"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/{tableId}/seat" },
                ["clear"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/{tableId}/clear" }
            }));
        });

        group.MapPatch("/{tableId}", async (
            Guid orgId, Guid siteId, Guid tableId,
            [FromBody] UpdateTableRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, siteId, tableId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Table not found"));

            await grain.UpdateAsync(new UpdateTableCommand(
                request.Number, request.Name, request.MinCapacity, request.MaxCapacity,
                request.Shape, request.Position, request.IsCombinable, request.SortOrder));
            var state = await grain.GetStateAsync();

            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/{tableId}" }
            }));
        });

        group.MapPost("/{tableId}/seat", async (
            Guid orgId, Guid siteId, Guid tableId,
            [FromBody] SeatTableRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, siteId, tableId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Table not found"));

            await grain.SeatAsync(new SeatTableCommand(request.BookingId, request.OrderId, request.GuestName, request.GuestCount, request.ServerId));
            var state = await grain.GetStateAsync();

            return Results.Ok(Hal.Resource(new { status = state.Status, occupancy = state.CurrentOccupancy }, new Dictionary<string, object>
            {
                ["table"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/{tableId}" },
                ["clear"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/{tableId}/clear" }
            }));
        });

        group.MapPost("/{tableId}/clear", async (Guid orgId, Guid siteId, Guid tableId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, siteId, tableId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Table not found"));

            await grain.ClearAsync();
            return Results.Ok(new { message = "Table cleared" });
        });

        group.MapPost("/{tableId}/status", async (
            Guid orgId, Guid siteId, Guid tableId,
            [FromBody] SetTableStatusRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, siteId, tableId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Table not found"));

            await grain.SetStatusAsync(request.Status);
            return Results.Ok(new { status = request.Status });
        });

        group.MapDelete("/{tableId}", async (Guid orgId, Guid siteId, Guid tableId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, siteId, tableId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Table not found"));

            await grain.DeleteAsync();
            return Results.NoContent();
        });

        return app;
    }
}
