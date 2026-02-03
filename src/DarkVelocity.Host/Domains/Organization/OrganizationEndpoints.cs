using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class OrganizationEndpoints
{
    public static WebApplication MapOrganizationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs").WithTags("Organizations");

        group.MapPost("/", async (
            [FromBody] CreateOrgRequest request,
            IGrainFactory grainFactory) =>
        {
            var orgId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
            var result = await grain.CreateAsync(new CreateOrganizationCommand(request.Name, request.Slug, request.Settings));

            return Results.Created($"/api/orgs/{orgId}", Hal.Resource(new
            {
                id = result.Id,
                slug = result.Slug,
                name = request.Name,
                createdAt = result.CreatedAt
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}" },
                ["sites"] = new { href = $"/api/orgs/{orgId}/sites" }
            }));
        });

        group.MapGet("/{orgId}", async (Guid orgId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Organization not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}" },
                ["sites"] = new { href = $"/api/orgs/{orgId}/sites" }
            }));
        });

        group.MapPatch("/{orgId}", async (
            Guid orgId,
            [FromBody] UpdateOrgRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Organization not found"));

            var result = await grain.UpdateAsync(new UpdateOrganizationCommand(request.Name, request.Settings));
            var state = await grain.GetStateAsync();

            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}" }
            }));
        });

        group.MapPost("/{orgId}/suspend", async (
            Guid orgId,
            [FromBody] SuspendOrgRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Organization not found"));

            await grain.SuspendAsync(request.Reason);
            return Results.Ok(new { message = "Organization suspended" });
        });

        return app;
    }
}
