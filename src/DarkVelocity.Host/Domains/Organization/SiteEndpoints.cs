using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class SiteEndpoints
{
    public static WebApplication MapSiteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites").WithTags("Sites");

        group.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateSiteRequest request,
            IGrainFactory grainFactory) =>
        {
            var siteId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));
            var result = await grain.CreateAsync(new CreateSiteCommand(
                orgId, request.Name, request.Code, request.Address, request.Timezone, request.Currency));

            var orgGrain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
            await orgGrain.AddSiteAsync(siteId);

            return Results.Created($"/api/orgs/{orgId}/sites/{siteId}", Hal.Resource(new
            {
                id = result.Id,
                code = result.Code,
                name = request.Name,
                createdAt = result.CreatedAt
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                ["organization"] = new { href = $"/api/orgs/{orgId}" },
                ["orders"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders" },
                ["menu"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu" }
            }));
        });

        group.MapGet("/", async (Guid orgId, IGrainFactory grainFactory) =>
        {
            var orgGrain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
            if (!await orgGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Organization not found"));

            var siteIds = await orgGrain.GetSiteIdsAsync();
            var sites = new List<object>();

            foreach (var siteId in siteIds)
            {
                var siteGrain = grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));
                if (await siteGrain.ExistsAsync())
                {
                    var state = await siteGrain.GetStateAsync();
                    sites.Add(Hal.Resource(state, new Dictionary<string, object>
                    {
                        ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" }
                    }));
                }
            }

            return Results.Ok(Hal.Collection("/api/orgs/{orgId}/sites", sites, sites.Count));
        });

        group.MapGet("/{siteId}", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Site not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
                ["organization"] = new { href = $"/api/orgs/{orgId}" },
                ["orders"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders" },
                ["menu"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu" },
                ["customers"] = new { href = $"/api/orgs/{orgId}/customers" },
                ["inventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory" },
                ["bookings"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings" }
            }));
        });

        group.MapPatch("/{siteId}", async (
            Guid orgId,
            Guid siteId,
            [FromBody] UpdateSiteRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Site not found"));

            await grain.UpdateAsync(new UpdateSiteCommand(request.Name, request.Address, request.OperatingHours, request.Settings));
            var state = await grain.GetStateAsync();

            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" }
            }));
        });

        group.MapPost("/{siteId}/open", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Site not found"));

            await grain.OpenAsync();
            return Results.Ok(new { message = "Site opened" });
        });

        group.MapPost("/{siteId}/close", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Site not found"));

            await grain.CloseAsync();
            return Results.Ok(new { message = "Site closed" });
        });

        return app;
    }
}
