using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class TimeOffEndpoints
{
    public static WebApplication MapTimeOffEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/time-off").WithTags("TimeOff");

        // Create a time off request
        group.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateTimeOffRequest request,
            IGrainFactory grainFactory) =>
        {
            var requestId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<ITimeOffGrain>(GrainKeys.TimeOffRequest(orgId, requestId));

            try
            {
                var result = await grain.CreateAsync(new CreateTimeOffCommand(
                    request.EmployeeId,
                    request.Type,
                    request.StartDate,
                    request.EndDate,
                    request.Reason));

                return Results.Created($"/api/orgs/{orgId}/time-off/{requestId}", Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/time-off/{requestId}" },
                    ["approve"] = new { href = $"/api/orgs/{orgId}/time-off/{requestId}/approve" },
                    ["reject"] = new { href = $"/api/orgs/{orgId}/time-off/{requestId}/reject" },
                    ["cancel"] = new { href = $"/api/orgs/{orgId}/time-off/{requestId}/cancel" }
                }));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_argument", ex.Message));
            }
        });

        // Get a time off request
        group.MapGet("/{requestId}", async (Guid orgId, Guid requestId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITimeOffGrain>(GrainKeys.TimeOffRequest(orgId, requestId));
            if (!await grain.ExistsAsync())
            {
                return Results.NotFound(Hal.Error("not_found", "Time off request not found"));
            }

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/time-off/{requestId}" },
                ["approve"] = new { href = $"/api/orgs/{orgId}/time-off/{requestId}/approve" },
                ["reject"] = new { href = $"/api/orgs/{orgId}/time-off/{requestId}/reject" },
                ["cancel"] = new { href = $"/api/orgs/{orgId}/time-off/{requestId}/cancel" }
            }));
        });

        // Get time off request status
        group.MapGet("/{requestId}/status", async (Guid orgId, Guid requestId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITimeOffGrain>(GrainKeys.TimeOffRequest(orgId, requestId));
            if (!await grain.ExistsAsync())
            {
                return Results.NotFound(Hal.Error("not_found", "Time off request not found"));
            }

            var status = await grain.GetStatusAsync();
            return Results.Ok(new { requestId, status = status.ToString() });
        });

        // Approve a time off request
        group.MapPost("/{requestId}/approve", async (
            Guid orgId,
            Guid requestId,
            [FromBody] RespondToTimeOffRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITimeOffGrain>(GrainKeys.TimeOffRequest(orgId, requestId));
            if (!await grain.ExistsAsync())
            {
                return Results.NotFound(Hal.Error("not_found", "Time off request not found"));
            }

            try
            {
                var result = await grain.ApproveAsync(new RespondToTimeOffCommand(
                    request.ReviewedByUserId,
                    request.Notes));

                return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/time-off/{requestId}" }
                }));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Reject a time off request
        group.MapPost("/{requestId}/reject", async (
            Guid orgId,
            Guid requestId,
            [FromBody] RespondToTimeOffRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITimeOffGrain>(GrainKeys.TimeOffRequest(orgId, requestId));
            if (!await grain.ExistsAsync())
            {
                return Results.NotFound(Hal.Error("not_found", "Time off request not found"));
            }

            try
            {
                var result = await grain.RejectAsync(new RespondToTimeOffCommand(
                    request.ReviewedByUserId,
                    request.Notes));

                return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/time-off/{requestId}" }
                }));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Cancel a time off request
        group.MapPost("/{requestId}/cancel", async (Guid orgId, Guid requestId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITimeOffGrain>(GrainKeys.TimeOffRequest(orgId, requestId));
            if (!await grain.ExistsAsync())
            {
                return Results.NotFound(Hal.Error("not_found", "Time off request not found"));
            }

            try
            {
                var result = await grain.CancelAsync();
                return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/time-off/{requestId}" }
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
