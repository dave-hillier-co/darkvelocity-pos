using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class OrganizationEndpoints
{
    /// <summary>
    /// Builds comprehensive HATEOAS links for an organization resource.
    /// </summary>
    private static Dictionary<string, object> BuildOrgLinks(Guid orgId)
    {
        return new Dictionary<string, object>
        {
            ["self"] = new { href = $"/api/orgs/{orgId}" },
            ["sites"] = new { href = $"/api/orgs/{orgId}/sites" },
            ["channels"] = new { href = $"/api/orgs/{orgId}/channels" },
            ["customers"] = new { href = $"/api/orgs/{orgId}/customers" },
            ["employees"] = new { href = $"/api/orgs/{orgId}/employees" },
            ["menu"] = new { href = $"/api/orgs/{orgId}/menu" },
            ["menu:categories"] = new { href = $"/api/orgs/{orgId}/menu/categories" },
            ["menu:items"] = new { href = $"/api/orgs/{orgId}/menu/items" },
            ["menu:cms"] = new { href = $"/api/orgs/{orgId}/menu/cms" },
            ["recipes"] = new { href = $"/api/orgs/{orgId}/recipes/cms" },
            ["webhooks"] = new { href = $"/api/orgs/{orgId}/webhooks" },
            ["search"] = new { href = $"/api/orgs/{orgId}/search" },
            ["users"] = new { href = $"/api/orgs/{orgId}/users" },
            ["groups"] = new { href = $"/api/orgs/{orgId}/groups" },
            ["roles"] = new { href = $"/api/orgs/{orgId}/roles" },
            ["api-keys"] = new { href = $"/api/orgs/{orgId}/api-keys" }
        };
    }

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
            }, BuildOrgLinks(orgId)));
        });

        group.MapGet("/{orgId}", async (Guid orgId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Organization not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, BuildOrgLinks(orgId)));
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

            return Results.Ok(Hal.Resource(state, BuildOrgLinks(orgId)));
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
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(new { status = "Suspended", reason = request.Reason }, BuildOrgLinks(orgId)));
        });

        return app;
    }
}
