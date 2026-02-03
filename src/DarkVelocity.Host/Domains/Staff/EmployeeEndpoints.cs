using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class EmployeeEndpoints
{
    public static WebApplication MapEmployeeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/employees").WithTags("Employees");

        group.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateEmployeeRequest request,
            IGrainFactory grainFactory) =>
        {
            var employeeId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
            var result = await grain.CreateAsync(new CreateEmployeeCommand(
                orgId, request.UserId, request.DefaultSiteId, request.EmployeeNumber, request.FirstName, request.LastName, request.Email, request.EmploymentType, request.HireDate));

            return Results.Created($"/api/orgs/{orgId}/employees/{employeeId}", Hal.Resource(new
            {
                id = result.Id,
                employeeNumber = result.EmployeeNumber,
                createdAt = result.CreatedAt
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}" },
                ["clock-in"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}/clock-in" }
            }));
        });

        group.MapGet("/{employeeId}", async (Guid orgId, Guid employeeId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Employee not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}" },
                ["clock-in"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}/clock-in" },
                ["clock-out"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}/clock-out" }
            }));
        });

        group.MapPatch("/{employeeId}", async (
            Guid orgId,
            Guid employeeId,
            [FromBody] UpdateEmployeeRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Employee not found"));

            await grain.UpdateAsync(new UpdateEmployeeCommand(
                request.FirstName, request.LastName, request.Email, request.HourlyRate, request.SalaryAmount, request.PayFrequency));
            var state = await grain.GetStateAsync();

            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}" }
            }));
        });

        group.MapPost("/{employeeId}/clock-in", async (
            Guid orgId,
            Guid employeeId,
            [FromBody] ClockInRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Employee not found"));

            var result = await grain.ClockInAsync(new ClockInCommand(request.SiteId, request.ShiftId));
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["employee"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}" },
                ["clock-out"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}/clock-out" }
            }));
        });

        group.MapPost("/{employeeId}/clock-out", async (
            Guid orgId,
            Guid employeeId,
            [FromBody] ClockOutRequest? request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Employee not found"));

            var result = await grain.ClockOutAsync(new ClockOutCommand(request?.Notes));
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["employee"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}" }
            }));
        });

        group.MapPost("/{employeeId}/roles", async (
            Guid orgId,
            Guid employeeId,
            [FromBody] AssignRoleRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Employee not found"));

            await grain.AssignRoleAsync(new AssignRoleCommand(request.RoleId, request.RoleName, request.Department, request.IsPrimary, request.HourlyRateOverride));
            return Results.Ok(new { message = "Role assigned" });
        });

        group.MapDelete("/{employeeId}/roles/{roleId}", async (Guid orgId, Guid employeeId, Guid roleId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Employee not found"));

            await grain.RemoveRoleAsync(roleId);
            return Results.NoContent();
        });

        return app;
    }
}
