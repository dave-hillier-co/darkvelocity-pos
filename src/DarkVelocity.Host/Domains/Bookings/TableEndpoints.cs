using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Hubs;
using DarkVelocity.Host.State;
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

            // Auto-register with optimizer
            var optimizerGrain = grainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
                GrainKeys.TableAssignmentOptimizer(orgId, siteId));
            if (!await optimizerGrain.ExistsAsync())
                await optimizerGrain.InitializeAsync(orgId, siteId);
            await optimizerGrain.RegisterTableAsync(
                tableId, request.Number, request.MinCapacity, request.MaxCapacity,
                isCombinable: true);

            var state = await grain.GetStateAsync();
            var links = BuildTableLinks(orgId, siteId, tableId, state);

            return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/tables/{tableId}", Hal.Resource(new
            {
                id = result.Id,
                number = result.Number,
                createdAt = result.CreatedAt
            }, links));
        });

        group.MapGet("/{tableId}", async (Guid orgId, Guid siteId, Guid tableId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, siteId, tableId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Table not found"));

            var state = await grain.GetStateAsync();
            var links = BuildTableLinks(orgId, siteId, tableId, state);

            return Results.Ok(Hal.Resource(state, links));
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
                request.Shape, request.Position, request.IsCombinable, request.SortOrder,
                request.SectionId));
            var state = await grain.GetStateAsync();

            // Update optimizer if capacity or combinability changed
            if (request.MinCapacity.HasValue || request.MaxCapacity.HasValue ||
                request.IsCombinable.HasValue || request.Number != null)
            {
                var optimizerGrain = grainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
                    GrainKeys.TableAssignmentOptimizer(orgId, siteId));
                if (await optimizerGrain.ExistsAsync())
                {
                    await optimizerGrain.RegisterTableAsync(
                        tableId, state.Number, state.MinCapacity, state.MaxCapacity,
                        state.IsCombinable, state.Tags);
                }
            }

            var links = BuildTableLinks(orgId, siteId, tableId, state);
            return Results.Ok(Hal.Resource(state, links));
        });

        group.MapPost("/{tableId}/seat", async (
            Guid orgId, Guid siteId, Guid tableId,
            [FromBody] SeatTableRequest request,
            IGrainFactory grainFactory,
            FloorPlanNotifier notifier) =>
        {
            var grain = grainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, siteId, tableId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Table not found"));

            await grain.SeatAsync(new SeatTableCommand(request.BookingId, request.OrderId, request.GuestName, request.GuestCount, request.ServerId));
            var state = await grain.GetStateAsync();
            var links = BuildTableLinks(orgId, siteId, tableId, state);

            // Update optimizer with table usage
            var optimizerGrain = grainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
                GrainKeys.TableAssignmentOptimizer(orgId, siteId));
            if (await optimizerGrain.ExistsAsync())
                await optimizerGrain.RecordTableUsageAsync(tableId, request.ServerId ?? Guid.Empty, request.GuestCount);

            // Push real-time update
            await notifier.NotifyTableStatusChanged(orgId, siteId, tableId, state.Number, state.Status, state.CurrentOccupancy);

            return Results.Ok(Hal.Resource(new { status = state.Status, occupancy = state.CurrentOccupancy }, links));
        });

        group.MapPost("/{tableId}/clear", async (Guid orgId, Guid siteId, Guid tableId,
            IGrainFactory grainFactory, FloorPlanNotifier notifier) =>
        {
            var grain = grainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, siteId, tableId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Table not found"));

            await grain.ClearAsync();
            var state = await grain.GetStateAsync();
            var links = BuildTableLinks(orgId, siteId, tableId, state);

            // Clear optimizer usage
            var optimizerGrain = grainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
                GrainKeys.TableAssignmentOptimizer(orgId, siteId));
            if (await optimizerGrain.ExistsAsync())
                await optimizerGrain.ClearTableUsageAsync(tableId);

            // Push real-time update
            await notifier.NotifyTableStatusChanged(orgId, siteId, tableId, state.Number, state.Status, null);

            return Results.Ok(Hal.Resource(new { status = state.Status }, links));
        });

        group.MapPost("/{tableId}/status", async (
            Guid orgId, Guid siteId, Guid tableId,
            [FromBody] SetTableStatusRequest request,
            IGrainFactory grainFactory,
            FloorPlanNotifier notifier) =>
        {
            var grain = grainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, siteId, tableId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Table not found"));

            await grain.SetStatusAsync(request.Status);
            var state = await grain.GetStateAsync();
            var links = BuildTableLinks(orgId, siteId, tableId, state);

            // Push real-time update
            await notifier.NotifyTableStatusChanged(orgId, siteId, tableId, state.Number, state.Status, state.CurrentOccupancy);

            return Results.Ok(Hal.Resource(new { status = state.Status }, links));
        });

        group.MapDelete("/{tableId}", async (Guid orgId, Guid siteId, Guid tableId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITableGrain>(GrainKeys.Table(orgId, siteId, tableId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Table not found"));

            await grain.DeleteAsync();

            // Unregister from optimizer
            var optimizerGrain = grainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
                GrainKeys.TableAssignmentOptimizer(orgId, siteId));
            if (await optimizerGrain.ExistsAsync())
                await optimizerGrain.UnregisterTableAsync(tableId);

            return Results.NoContent();
        });

        return app;
    }

    /// <summary>
    /// Builds HAL links for a table resource with cross-domain relationships.
    /// </summary>
    private static Dictionary<string, object> BuildTableLinks(
        Guid orgId,
        Guid siteId,
        Guid tableId,
        TableState state)
    {
        var basePath = $"/api/orgs/{orgId}/sites/{siteId}/tables/{tableId}";
        var sitePath = $"/api/orgs/{orgId}/sites/{siteId}";

        var links = new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["site"] = new { href = sitePath },
            ["bookings"] = new { href = $"{sitePath}/bookings{{?tableId}}", templated = true },
            ["waitlist"] = new { href = $"{sitePath}/waitlist" }
        };

        // Add floor-plan link if the table is assigned to a floor plan
        if (state.FloorPlanId.HasValue)
        {
            links["floor-plan"] = new { href = $"{sitePath}/floor-plans/{state.FloorPlanId.Value}" };
        }

        // Add current-order link if table is occupied with an order
        if (state.CurrentOccupancy?.OrderId.HasValue == true)
        {
            links["current-order"] = new { href = $"{sitePath}/orders/{state.CurrentOccupancy.OrderId.Value}" };
        }

        // Add current-booking link if table has an active booking
        if (state.CurrentOccupancy?.BookingId.HasValue == true)
        {
            links["current-booking"] = new { href = $"{sitePath}/bookings/{state.CurrentOccupancy.BookingId.Value}" };
        }

        // Add action links based on table status
        switch (state.Status)
        {
            case TableStatus.Available:
            case TableStatus.Reserved:
            case TableStatus.Dirty:
                // Table can be assigned to a booking/order
                links["assign"] = new { href = $"{basePath}/seat" };
                break;

            case TableStatus.Occupied:
                // Table can be released
                links["release"] = new { href = $"{basePath}/clear" };
                break;

            // Blocked and OutOfService tables don't have action links
        }

        return links;
    }
}
