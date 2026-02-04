using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class ScheduleEndpoints
{
    public static WebApplication MapScheduleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/schedules").WithTags("Schedules");

        // Create a new schedule (weekly rota)
        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] CreateScheduleRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IScheduleGrain>(GrainKeys.Schedule(orgId, siteId, request.WeekStartDate));
            var result = await grain.CreateAsync(new CreateScheduleCommand(siteId, request.WeekStartDate.ToDateTime(TimeOnly.MinValue), request.Notes));

            return Results.Created(
                $"/api/orgs/{orgId}/sites/{siteId}/schedules/{request.WeekStartDate:yyyy-MM-dd}",
                Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/schedules/{request.WeekStartDate:yyyy-MM-dd}" },
                    ["shifts"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/schedules/{request.WeekStartDate:yyyy-MM-dd}/shifts" },
                    ["publish"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/schedules/{request.WeekStartDate:yyyy-MM-dd}/publish" }
                }));
        });

        // Get a schedule by week start date
        group.MapGet("/{weekStartDate}", async (Guid orgId, Guid siteId, DateOnly weekStartDate, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IScheduleGrain>(GrainKeys.Schedule(orgId, siteId, weekStartDate));
            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/schedules/{weekStartDate:yyyy-MM-dd}" },
                    ["shifts"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/schedules/{weekStartDate:yyyy-MM-dd}/shifts" },
                    ["publish"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/schedules/{weekStartDate:yyyy-MM-dd}/publish" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Schedule not found"));
            }
        });

        // Publish a schedule
        group.MapPost("/{weekStartDate}/publish", async (
            Guid orgId,
            Guid siteId,
            DateOnly weekStartDate,
            [FromBody] PublishScheduleRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IScheduleGrain>(GrainKeys.Schedule(orgId, siteId, weekStartDate));
            try
            {
                var result = await grain.PublishAsync(new PublishScheduleCommand(request.PublishedByUserId));
                return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/schedules/{weekStartDate:yyyy-MM-dd}" }
                }));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Lock a schedule
        group.MapPost("/{weekStartDate}/lock", async (
            Guid orgId,
            Guid siteId,
            DateOnly weekStartDate,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IScheduleGrain>(GrainKeys.Schedule(orgId, siteId, weekStartDate));
            try
            {
                await grain.LockAsync();
                return Results.Ok(new { message = "Schedule locked" });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Add a shift to a schedule
        group.MapPost("/{weekStartDate}/shifts", async (
            Guid orgId,
            Guid siteId,
            DateOnly weekStartDate,
            [FromBody] AddShiftRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IScheduleGrain>(GrainKeys.Schedule(orgId, siteId, weekStartDate));
            var shiftId = Guid.NewGuid();
            try
            {
                await grain.AddShiftAsync(new AddShiftCommand(
                    shiftId,
                    request.EmployeeId,
                    request.RoleId,
                    request.Date.ToDateTime(TimeOnly.MinValue),
                    request.StartTime,
                    request.EndTime,
                    request.BreakMinutes,
                    request.HourlyRate,
                    request.Notes));

                return Results.Created(
                    $"/api/orgs/{orgId}/sites/{siteId}/schedules/{weekStartDate:yyyy-MM-dd}/shifts/{shiftId}",
                    new
                    {
                        shiftId,
                        _links = new
                        {
                            self = new { href = $"/api/orgs/{orgId}/sites/{siteId}/schedules/{weekStartDate:yyyy-MM-dd}/shifts/{shiftId}" },
                            schedule = new { href = $"/api/orgs/{orgId}/sites/{siteId}/schedules/{weekStartDate:yyyy-MM-dd}" }
                        }
                    });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Update a shift
        group.MapPatch("/{weekStartDate}/shifts/{shiftId}", async (
            Guid orgId,
            Guid siteId,
            DateOnly weekStartDate,
            Guid shiftId,
            [FromBody] UpdateShiftRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IScheduleGrain>(GrainKeys.Schedule(orgId, siteId, weekStartDate));
            try
            {
                await grain.UpdateShiftAsync(new UpdateShiftCommand(
                    shiftId,
                    request.StartTime,
                    request.EndTime,
                    request.BreakMinutes,
                    request.EmployeeId,
                    request.RoleId,
                    request.Notes));

                return Results.Ok(new { message = "Shift updated" });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Remove a shift
        group.MapDelete("/{weekStartDate}/shifts/{shiftId}", async (
            Guid orgId,
            Guid siteId,
            DateOnly weekStartDate,
            Guid shiftId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IScheduleGrain>(GrainKeys.Schedule(orgId, siteId, weekStartDate));
            try
            {
                await grain.RemoveShiftAsync(shiftId);
                return Results.NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Get shifts for a specific date
        group.MapGet("/{weekStartDate}/shifts/by-date/{date}", async (
            Guid orgId,
            Guid siteId,
            DateOnly weekStartDate,
            DateOnly date,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IScheduleGrain>(GrainKeys.Schedule(orgId, siteId, weekStartDate));
            try
            {
                var shifts = await grain.GetShiftsForDateAsync(date.ToDateTime(TimeOnly.MinValue));
                return Results.Ok(Hal.Collection(
                    $"/api/orgs/{orgId}/sites/{siteId}/schedules/{weekStartDate:yyyy-MM-dd}/shifts/by-date/{date:yyyy-MM-dd}",
                    shifts.Select(s => (object)s),
                    shifts.Count));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Schedule not found"));
            }
        });

        // Get shifts for a specific employee
        group.MapGet("/{weekStartDate}/shifts/by-employee/{employeeId}", async (
            Guid orgId,
            Guid siteId,
            DateOnly weekStartDate,
            Guid employeeId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IScheduleGrain>(GrainKeys.Schedule(orgId, siteId, weekStartDate));
            try
            {
                var shifts = await grain.GetShiftsForEmployeeAsync(employeeId);
                return Results.Ok(Hal.Collection(
                    $"/api/orgs/{orgId}/sites/{siteId}/schedules/{weekStartDate:yyyy-MM-dd}/shifts/by-employee/{employeeId}",
                    shifts.Select(s => (object)s),
                    shifts.Count));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Schedule not found"));
            }
        });

        // Get labor cost for a schedule
        group.MapGet("/{weekStartDate}/labor-cost", async (
            Guid orgId,
            Guid siteId,
            DateOnly weekStartDate,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IScheduleGrain>(GrainKeys.Schedule(orgId, siteId, weekStartDate));
            try
            {
                var totalCost = await grain.GetTotalLaborCostAsync();
                return Results.Ok(new { totalLaborCost = totalCost });
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Schedule not found"));
            }
        });

        return app;
    }
}
