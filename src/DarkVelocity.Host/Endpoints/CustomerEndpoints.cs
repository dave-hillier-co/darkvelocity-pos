using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
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

            return Results.Created($"/api/orgs/{orgId}/customers/{customerId}", Hal.Resource(new
            {
                id = result.Id,
                displayName = result.DisplayName,
                createdAt = result.CreatedAt
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}" },
                ["loyalty"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}/loyalty" }
            }));
        });

        group.MapGet("/{customerId}", async (Guid orgId, Guid customerId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Customer not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}" },
                ["loyalty"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}/loyalty" },
                ["rewards"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}/rewards" }
            }));
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

            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}" }
            }));
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
            return Results.Ok(new { message = "Enrolled in loyalty program" });
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
                ["redeem"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}/rewards/{r.RewardId}/redeem" }
            })).ToList();

            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/customers/{customerId}/rewards", items, items.Count));
        });

        return app;
    }
}
