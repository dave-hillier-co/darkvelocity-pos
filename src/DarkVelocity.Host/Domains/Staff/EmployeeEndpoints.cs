using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
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

            var state = await grain.GetStateAsync();
            var links = BuildEmployeeLinks(orgId, employeeId, state);

            return Results.Created($"/api/orgs/{orgId}/employees/{employeeId}", Hal.Resource(new
            {
                id = result.Id,
                employeeNumber = result.EmployeeNumber,
                createdAt = result.CreatedAt
            }, links));
        });

        group.MapGet("/{employeeId}", async (Guid orgId, Guid employeeId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Employee not found"));

            var state = await grain.GetStateAsync();
            var links = BuildEmployeeLinks(orgId, employeeId, state);

            return Results.Ok(Hal.Resource(state, links));
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
            var links = BuildEmployeeLinks(orgId, employeeId, state);

            return Results.Ok(Hal.Resource(state, links));
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
            var state = await grain.GetStateAsync();
            var links = BuildEmployeeLinks(orgId, employeeId, state);
            return Results.Ok(Hal.Resource(new { roleId = request.RoleId, roleName = request.RoleName, assigned = true }, links));
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

    /// <summary>
    /// Builds HAL links for an employee resource with cross-domain relationships.
    /// </summary>
    private static Dictionary<string, object> BuildEmployeeLinks(
        Guid orgId,
        Guid employeeId,
        EmployeeState state)
    {
        var basePath = $"/api/orgs/{orgId}/employees/{employeeId}";

        var links = new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["organization"] = new { href = $"/api/orgs/{orgId}" },
            ["roles"] = new { href = $"{basePath}/roles" },
            ["time-entries"] = new { href = $"{basePath}/time-entries" },
            // Schedules are site-scoped, so use templated link with siteId parameter
            ["schedules"] = new { href = $"/api/orgs/{orgId}/sites/{{siteId}}/schedules{{?weekStartDate}}", templated = true },
            ["availability"] = new { href = $"{basePath}/availability" },
            ["shift-swaps"] = new { href = $"{basePath}/shift-swaps" },
            ["time-off"] = new { href = $"{basePath}/time-off" },
            ["payroll"] = new { href = $"/api/orgs/{orgId}/payroll{{?employeeId}}", templated = true }
        };

        // Add clock-in/clock-out links based on current state
        if (state.IsClockedIn)
        {
            links["clock-out"] = new { href = $"{basePath}/clock-out" };
        }
        else
        {
            links["clock-in"] = new { href = $"{basePath}/clock-in" };
        }

        // Add links to assigned sites
        var assignedSites = new List<object>();

        // Always include the default site
        assignedSites.Add(new { href = $"/api/orgs/{orgId}/sites/{state.DefaultSiteId}", title = "default" });

        // Add additional allowed sites
        foreach (var siteId in state.AllowedSiteIds.Where(s => s != state.DefaultSiteId))
        {
            assignedSites.Add(new { href = $"/api/orgs/{orgId}/sites/{siteId}" });
        }

        links["assigned-sites"] = assignedSites;

        // Add certifications link if employee has certifications
        if (state.Certifications.Count > 0)
        {
            links["certifications"] = new { href = $"{basePath}/certifications" };
        }

        return links;
    }
}
