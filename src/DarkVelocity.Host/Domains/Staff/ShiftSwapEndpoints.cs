using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class ShiftSwapEndpoints
{
    public static WebApplication MapShiftSwapEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/shift-swaps").WithTags("ShiftSwaps");

        // Create a shift swap request
        group.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateShiftSwapRequest request,
            IGrainFactory grainFactory) =>
        {
            var requestId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IShiftSwapGrain>(GrainKeys.ShiftSwapRequest(orgId, requestId));

            var result = await grain.CreateAsync(new CreateShiftSwapCommand(
                request.RequestingEmployeeId,
                request.RequestingShiftId,
                request.TargetEmployeeId,
                request.TargetShiftId,
                request.Type,
                request.Reason));

            return Results.Created($"/api/orgs/{orgId}/shift-swaps/{requestId}", Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/shift-swaps/{requestId}" },
                ["approve"] = new { href = $"/api/orgs/{orgId}/shift-swaps/{requestId}/approve" },
                ["reject"] = new { href = $"/api/orgs/{orgId}/shift-swaps/{requestId}/reject" },
                ["cancel"] = new { href = $"/api/orgs/{orgId}/shift-swaps/{requestId}/cancel" }
            }));
        });

        // Get a shift swap request
        group.MapGet("/{requestId}", async (Guid orgId, Guid requestId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IShiftSwapGrain>(GrainKeys.ShiftSwapRequest(orgId, requestId));
            if (!await grain.ExistsAsync())
            {
                return Results.NotFound(Hal.Error("not_found", "Shift swap request not found"));
            }

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/shift-swaps/{requestId}" },
                ["approve"] = new { href = $"/api/orgs/{orgId}/shift-swaps/{requestId}/approve" },
                ["reject"] = new { href = $"/api/orgs/{orgId}/shift-swaps/{requestId}/reject" },
                ["cancel"] = new { href = $"/api/orgs/{orgId}/shift-swaps/{requestId}/cancel" }
            }));
        });

        // Get shift swap request status
        group.MapGet("/{requestId}/status", async (Guid orgId, Guid requestId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IShiftSwapGrain>(GrainKeys.ShiftSwapRequest(orgId, requestId));
            if (!await grain.ExistsAsync())
            {
                return Results.NotFound(Hal.Error("not_found", "Shift swap request not found"));
            }

            var status = await grain.GetStatusAsync();
            return Results.Ok(new { requestId, status = status.ToString() });
        });

        // Approve a shift swap request (manager)
        group.MapPost("/{requestId}/approve", async (
            Guid orgId,
            Guid requestId,
            [FromBody] RespondToShiftSwapRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IShiftSwapGrain>(GrainKeys.ShiftSwapRequest(orgId, requestId));
            if (!await grain.ExistsAsync())
            {
                return Results.NotFound(Hal.Error("not_found", "Shift swap request not found"));
            }

            try
            {
                var result = await grain.ApproveAsync(new RespondToShiftSwapCommand(
                    request.RespondingUserId,
                    request.Notes));

                return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/shift-swaps/{requestId}" }
                }));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Reject a shift swap request
        group.MapPost("/{requestId}/reject", async (
            Guid orgId,
            Guid requestId,
            [FromBody] RespondToShiftSwapRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IShiftSwapGrain>(GrainKeys.ShiftSwapRequest(orgId, requestId));
            if (!await grain.ExistsAsync())
            {
                return Results.NotFound(Hal.Error("not_found", "Shift swap request not found"));
            }

            try
            {
                var result = await grain.RejectAsync(new RespondToShiftSwapCommand(
                    request.RespondingUserId,
                    request.Notes));

                return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/shift-swaps/{requestId}" }
                }));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Cancel a shift swap request (by the requester)
        group.MapPost("/{requestId}/cancel", async (Guid orgId, Guid requestId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IShiftSwapGrain>(GrainKeys.ShiftSwapRequest(orgId, requestId));
            if (!await grain.ExistsAsync())
            {
                return Results.NotFound(Hal.Error("not_found", "Shift swap request not found"));
            }

            try
            {
                var result = await grain.CancelAsync();
                return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/shift-swaps/{requestId}" }
                }));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        return app;
    }
}
