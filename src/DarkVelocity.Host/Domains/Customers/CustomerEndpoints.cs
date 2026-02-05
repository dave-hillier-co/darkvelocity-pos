using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class CustomerEndpoints
{
    public static WebApplication MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/customers").WithTags("Customers");

        group.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateCustomerRequest request,
            IGrainFactory grainFactory) =>
        {
            var customerId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
            var result = await grain.CreateAsync(new CreateCustomerCommand(
                orgId, request.FirstName, request.LastName, request.Email, request.Phone, request.Source));

            var state = await grain.GetStateAsync();
            var links = await BuildCustomerLinksAsync(orgId, customerId, state, grainFactory);

            return Results.Created($"/api/orgs/{orgId}/customers/{customerId}", Hal.Resource(new
            {
                id = result.Id,
                displayName = result.DisplayName,
                createdAt = result.CreatedAt
            }, links));
        });

        group.MapGet("/{customerId}", async (Guid orgId, Guid customerId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));

            var state = await grain.GetStateAsync();
            var links = await BuildCustomerLinksAsync(orgId, customerId, state, grainFactory);

            return Results.Ok(Hal.Resource(state, links));
        });

        group.MapPatch("/{customerId}", async (
            Guid orgId,
            Guid customerId,
            [FromBody] UpdateCustomerRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));

            await grain.UpdateAsync(new UpdateCustomerCommand(
                request.FirstName, request.LastName, request.Email, request.Phone, request.DateOfBirth, request.Preferences));
            var state = await grain.GetStateAsync();
            var links = await BuildCustomerLinksAsync(orgId, customerId, state, grainFactory);

            return Results.Ok(Hal.Resource(state, links));
        });

        group.MapPost("/{customerId}/loyalty/enroll", async (
            Guid orgId,
            Guid customerId,
            [FromBody] EnrollLoyaltyRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));

            await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(request.ProgramId, request.MemberNumber, request.InitialTierId, request.TierName));
            var state = await grain.GetStateAsync();
            var links = await BuildCustomerLinksAsync(orgId, customerId, state, grainFactory);
            return Results.Ok(Hal.Resource(new { enrolled = true, programId = request.ProgramId }, links));
        });

        group.MapPost("/{customerId}/loyalty/earn", async (
            Guid orgId,
            Guid customerId,
            [FromBody] EarnPointsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));

            var result = await grain.EarnPointsAsync(new EarnPointsCommand(request.Points, request.Reason, request.OrderId, request.SiteId, request.SpendAmount));
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["customer"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}" }
            }));
        });

        group.MapPost("/{customerId}/loyalty/redeem", async (
            Guid orgId,
            Guid customerId,
            [FromBody] RedeemPointsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));

            var result = await grain.RedeemPointsAsync(new RedeemPointsCommand(request.Points, request.OrderId, request.Reason));
            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["customer"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}" }
            }));
        });

        group.MapGet("/{customerId}/rewards", async (Guid orgId, Guid customerId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));

            var rewards = await grain.GetAvailableRewardsAsync();
            var items = rewards.Select(r => Hal.Resource(r, new Dictionary<string, object>
            {
                ["redeem"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}/rewards/{r.Id}/redeem" }
            })).ToList();

            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/customers/{customerId}/rewards", items, items.Count));
        });

        // Visit History endpoints
        group.MapGet("/{customerId}/visits", async (
            Guid orgId, Guid customerId,
            [FromQuery] int? limit,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));

            var visits = await grain.GetVisitHistoryAsync(limit ?? 50);
            var items = visits.Select(v =>
            {
                var visitLinks = new Dictionary<string, object>
                {
                    ["customer"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}" },
                    ["site"] = new { href = $"/api/orgs/{orgId}/sites/{v.SiteId}" }
                };
                if (v.OrderId != null)
                    visitLinks["order"] = new { href = $"/api/orgs/{orgId}/sites/{v.SiteId}/orders/{v.OrderId}" };
                return Hal.Resource(v, visitLinks);
            }).ToList();

            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/customers/{customerId}/visits", items, items.Count));
        });

        // Preferences endpoints
        group.MapGet("/{customerId}/preferences", async (Guid orgId, Guid customerId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state.Preferences, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}/preferences" },
                ["customer"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}" }
            }));
        });

        group.MapPatch("/{customerId}/preferences", async (
            Guid orgId, Guid customerId,
            [FromBody] UpdateCustomerPreferencesRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));

            await grain.UpdatePreferencesAsync(new UpdatePreferencesCommand(
                request.DietaryRestrictions, request.Allergens, request.SeatingPreference, request.Notes));
            var state = await grain.GetStateAsync();

            return Results.Ok(Hal.Resource(state.Preferences, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}/preferences" }
            }));
        });

        // Tags endpoints
        group.MapGet("/{customerId}/tags", async (Guid orgId, Guid customerId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/customers/{customerId}/tags" } },
                tags = state.Tags
            });
        });

        group.MapPost("/{customerId}/tags", async (
            Guid orgId, Guid customerId,
            [FromBody] AddTagRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));

            await grain.AddTagAsync(request.Tag);
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(new { tag = request.Tag, added = true }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}/tags" },
                ["customer"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}" }
            }));
        });

        group.MapDelete("/{customerId}/tags/{tag}", async (Guid orgId, Guid customerId, string tag, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));

            await grain.RemoveTagAsync(tag);
            return Results.NoContent();
        });

        return app;
    }

    /// <summary>
    /// Builds HAL links for a customer resource with cross-domain relationships.
    /// </summary>
    private static async Task<Dictionary<string, object>> BuildCustomerLinksAsync(
        Guid orgId,
        Guid customerId,
        CustomerState state,
        IGrainFactory grainFactory)
    {
        var basePath = $"/api/orgs/{orgId}/customers/{customerId}";

        var links = new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["organization"] = new { href = $"/api/orgs/{orgId}" },
            // Orders are site-scoped, so use templated link with siteId parameter
            ["orders"] = new { href = $"{basePath}/orders{{?siteId}}", templated = true },
            ["bookings"] = new { href = $"{basePath}/bookings" },
            ["visits"] = new { href = $"{basePath}/visits" },
            ["preferences"] = new { href = $"{basePath}/preferences" },
            ["tags"] = new { href = $"{basePath}/tags" },
            ["rewards"] = new { href = $"{basePath}/rewards" },
            ["gift-cards"] = new { href = $"{basePath}/gift-cards" }
        };

        // Add loyalty link if customer is enrolled in a loyalty program
        if (state.Loyalty != null)
        {
            links["loyalty"] = new { href = $"{basePath}/loyalty" };
            links["loyalty:earn"] = new { href = $"{basePath}/loyalty/earn" };
            links["loyalty:redeem"] = new { href = $"{basePath}/loyalty/redeem" };
        }
        else
        {
            // Link to enroll in loyalty
            links["loyalty:enroll"] = new { href = $"{basePath}/loyalty/enroll" };
        }

        // Check if spend projection exists and add link
        var spendGrain = grainFactory.GetGrain<ICustomerSpendProjectionGrain>(
            GrainKeys.CustomerSpendProjection(orgId, customerId));
        if (await spendGrain.ExistsAsync())
        {
            links["spend-history"] = new { href = $"{basePath}/spend" };
        }

        return links;
    }
}
