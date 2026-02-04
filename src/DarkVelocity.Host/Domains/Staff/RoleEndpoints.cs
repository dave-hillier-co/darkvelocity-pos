using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class RoleEndpoints
{
    public static WebApplication MapRoleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/roles").WithTags("Roles");

        group.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateRoleRequest request,
            IGrainFactory grainFactory) =>
        {
            var roleId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IRoleGrain>(GrainKeys.Role(orgId, roleId));
            var result = await grain.CreateAsync(new CreateRoleCommand(
                request.Name,
                request.Department,
                request.DefaultHourlyRate,
                request.Color,
                request.SortOrder,
                request.RequiredCertifications ?? []));

            return Results.Created($"/api/orgs/{orgId}/roles/{roleId}", Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/roles/{roleId}" }
            }));
        });

        group.MapGet("/{roleId}", async (Guid orgId, Guid roleId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoleGrain>(GrainKeys.Role(orgId, roleId));
            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/roles/{roleId}" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Role not found"));
            }
        });

        group.MapPatch("/{roleId}", async (
            Guid orgId,
            Guid roleId,
            [FromBody] UpdateRoleRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IRoleGrain>(GrainKeys.Role(orgId, roleId));
            try
            {
                var result = await grain.UpdateAsync(new UpdateRoleCommand(
                    request.Name,
                    request.Department,
                    request.DefaultHourlyRate,
                    request.Color,
                    request.SortOrder,
                    request.IsActive));

                return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/roles/{roleId}" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Role not found"));
            }
        });

        return app;
    }
}
