using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class EmployeeAvailabilityEndpoints
{
    public static WebApplication MapEmployeeAvailabilityEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/employees/{employeeId}/availability").WithTags("EmployeeAvailability");

        // Get employee's availability
        group.MapGet("/", async (Guid orgId, Guid employeeId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IEmployeeAvailabilityGrain>(GrainKeys.EmployeeAvailability(orgId, employeeId));
            if (!await grain.ExistsAsync())
            {
                // Initialize if not exists
                await grain.InitializeAsync(employeeId);
            }

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}/availability" },
                ["employee"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}" }
            }));
        });

        // Get current (active) availability
        group.MapGet("/current", async (Guid orgId, Guid employeeId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IEmployeeAvailabilityGrain>(GrainKeys.EmployeeAvailability(orgId, employeeId));
            if (!await grain.ExistsAsync())
            {
                await grain.InitializeAsync(employeeId);
            }

            var availabilities = await grain.GetCurrentAvailabilityAsync();
            return Results.Ok(Hal.Collection(
                $"/api/orgs/{orgId}/employees/{employeeId}/availability/current",
                availabilities.Select(a => (object)a),
                availabilities.Count));
        });

        // Set availability for a specific day
        group.MapPost("/", async (
            Guid orgId,
            Guid employeeId,
            [FromBody] SetAvailabilityRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IEmployeeAvailabilityGrain>(GrainKeys.EmployeeAvailability(orgId, employeeId));
            if (!await grain.ExistsAsync())
            {
                await grain.InitializeAsync(employeeId);
            }

            try
            {
                var result = await grain.SetAvailabilityAsync(new SetAvailabilityCommand(
                    request.DayOfWeek,
                    request.StartTime,
                    request.EndTime,
                    request.IsAvailable,
                    request.IsPreferred,
                    request.EffectiveFrom,
                    request.EffectiveTo,
                    request.Notes));

                return Results.Created(
                    $"/api/orgs/{orgId}/employees/{employeeId}/availability/{result.Id}",
                    Hal.Resource(result, new Dictionary<string, object>
                    {
                        ["self"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}/availability/{result.Id}" },
                        ["availability"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}/availability" }
                    }));
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_argument", ex.Message));
            }
        });

        // Update an availability entry
        group.MapPatch("/{availabilityId}", async (
            Guid orgId,
            Guid employeeId,
            Guid availabilityId,
            [FromBody] SetAvailabilityRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IEmployeeAvailabilityGrain>(GrainKeys.EmployeeAvailability(orgId, employeeId));
            if (!await grain.ExistsAsync())
            {
                return Results.NotFound(Hal.Error("not_found", "Employee availability not found"));
            }

            try
            {
                await grain.UpdateAvailabilityAsync(availabilityId, new SetAvailabilityCommand(
                    request.DayOfWeek,
                    request.StartTime,
                    request.EndTime,
                    request.IsAvailable,
                    request.IsPreferred,
                    request.EffectiveFrom,
                    request.EffectiveTo,
                    request.Notes));

                return Results.Ok(new { message = "Availability updated" });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Remove an availability entry
        group.MapDelete("/{availabilityId}", async (
            Guid orgId,
            Guid employeeId,
            Guid availabilityId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IEmployeeAvailabilityGrain>(GrainKeys.EmployeeAvailability(orgId, employeeId));
            if (!await grain.ExistsAsync())
            {
                return Results.NotFound(Hal.Error("not_found", "Employee availability not found"));
            }

            await grain.RemoveAvailabilityAsync(availabilityId);
            return Results.NoContent();
        });

        // Set availability for the whole week at once
        group.MapPut("/week", async (
            Guid orgId,
            Guid employeeId,
            [FromBody] SetWeekAvailabilityRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IEmployeeAvailabilityGrain>(GrainKeys.EmployeeAvailability(orgId, employeeId));
            if (!await grain.ExistsAsync())
            {
                await grain.InitializeAsync(employeeId);
            }

            var commands = request.Availabilities.Select(a => new SetAvailabilityCommand(
                a.DayOfWeek,
                a.StartTime,
                a.EndTime,
                a.IsAvailable,
                a.IsPreferred,
                a.EffectiveFrom,
                a.EffectiveTo,
                a.Notes)).ToList();

            await grain.SetWeekAvailabilityAsync(commands);
            var snapshot = await grain.GetSnapshotAsync();

            return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}/availability" }
            }));
        });

        // Check if employee is available at a specific time
        group.MapGet("/check", async (
            Guid orgId,
            Guid employeeId,
            [FromQuery] int dayOfWeek,
            [FromQuery] string time,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IEmployeeAvailabilityGrain>(GrainKeys.EmployeeAvailability(orgId, employeeId));
            if (!await grain.ExistsAsync())
            {
                return Results.NotFound(Hal.Error("not_found", "Employee availability not found"));
            }

            if (!TimeSpan.TryParse(time, out var timeSpan))
            {
                return Results.BadRequest(Hal.Error("invalid_argument", "Invalid time format. Use HH:mm:ss"));
            }

            var isAvailable = await grain.IsAvailableOnAsync(dayOfWeek, timeSpan);
            return Results.Ok(new { dayOfWeek, time = timeSpan, isAvailable });
        });

        return app;
    }
}
