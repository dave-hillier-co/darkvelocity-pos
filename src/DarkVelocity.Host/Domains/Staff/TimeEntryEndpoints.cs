using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class TimeEntryEndpoints
{
    public static WebApplication MapTimeEntryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/time-entries").WithTags("TimeEntries");

        // Clock in (create a new time entry)
        group.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateTimeEntryRequest request,
            IGrainFactory grainFactory) =>
        {
            var timeEntryId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<ITimeEntryGrain>(GrainKeys.TimeEntry(orgId, timeEntryId));
            var result = await grain.ClockInAsync(new TimeEntryClockInCommand(
                request.EmployeeId,
                request.SiteId,
                request.RoleId,
                request.ShiftId,
                request.Method,
                request.Notes));

            return Results.Created($"/api/orgs/{orgId}/time-entries/{timeEntryId}", Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/time-entries/{timeEntryId}" },
                ["clock-out"] = new { href = $"/api/orgs/{orgId}/time-entries/{timeEntryId}/clock-out" }
            }));
        });

        // Get a time entry
        group.MapGet("/{timeEntryId}", async (Guid orgId, Guid timeEntryId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITimeEntryGrain>(GrainKeys.TimeEntry(orgId, timeEntryId));
            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/time-entries/{timeEntryId}" },
                    ["clock-out"] = new { href = $"/api/orgs/{orgId}/time-entries/{timeEntryId}/clock-out" },
                    ["adjust"] = new { href = $"/api/orgs/{orgId}/time-entries/{timeEntryId}/adjust" },
                    ["approve"] = new { href = $"/api/orgs/{orgId}/time-entries/{timeEntryId}/approve" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Time entry not found"));
            }
        });

        // Clock out
        group.MapPost("/{timeEntryId}/clock-out", async (
            Guid orgId,
            Guid timeEntryId,
            [FromBody] ClockOutTimeEntryRequest? request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITimeEntryGrain>(GrainKeys.TimeEntry(orgId, timeEntryId));
            try
            {
                var result = await grain.ClockOutAsync(new TimeEntryClockOutCommand(
                    request?.Method ?? ClockMethod.Pin,
                    request?.Notes));

                return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/time-entries/{timeEntryId}" }
                }));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Add a break
        group.MapPost("/{timeEntryId}/breaks", async (
            Guid orgId,
            Guid timeEntryId,
            [FromBody] AddBreakRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITimeEntryGrain>(GrainKeys.TimeEntry(orgId, timeEntryId));
            try
            {
                await grain.AddBreakAsync(new AddBreakCommand(
                    request.BreakStart,
                    request.BreakEnd,
                    request.IsPaid));

                return Results.Ok(new { message = "Break added" });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Adjust a time entry (manager override)
        group.MapPost("/{timeEntryId}/adjust", async (
            Guid orgId,
            Guid timeEntryId,
            [FromBody] AdjustTimeEntryRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITimeEntryGrain>(GrainKeys.TimeEntry(orgId, timeEntryId));
            try
            {
                var result = await grain.AdjustAsync(new AdjustTimeEntryCommand(
                    request.AdjustedByUserId,
                    request.ClockInAt,
                    request.ClockOutAt,
                    request.BreakMinutes,
                    request.Reason));

                return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/time-entries/{timeEntryId}" }
                }));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Approve a time entry
        group.MapPost("/{timeEntryId}/approve", async (
            Guid orgId,
            Guid timeEntryId,
            [FromBody] ApproveTimeEntryRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITimeEntryGrain>(GrainKeys.TimeEntry(orgId, timeEntryId));
            try
            {
                var result = await grain.ApproveAsync(new ApproveTimeEntryCommand(request.ApprovedByUserId));
                return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/time-entries/{timeEntryId}" }
                }));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Check if currently active
        group.MapGet("/{timeEntryId}/is-active", async (Guid orgId, Guid timeEntryId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITimeEntryGrain>(GrainKeys.TimeEntry(orgId, timeEntryId));
            var isActive = await grain.IsActiveAsync();
            return Results.Ok(new { isActive });
        });

        return app;
    }
}
