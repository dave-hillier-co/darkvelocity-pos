using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class TipPoolEndpoints
{
    public static WebApplication MapTipPoolEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/tip-pools").WithTags("TipPools");

        // Create a tip pool
        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] CreateTipPoolRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITipPoolGrain>(GrainKeys.TipPool(orgId, siteId, request.BusinessDate, request.Name));
            var result = await grain.CreateAsync(new CreateTipPoolCommand(
                siteId,
                request.BusinessDate.ToDateTime(TimeOnly.MinValue),
                request.Name,
                request.Method,
                request.EligibleRoleIds ?? []));

            return Results.Created(
                $"/api/orgs/{orgId}/sites/{siteId}/tip-pools/{request.BusinessDate:yyyy-MM-dd}/{request.Name}",
                Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tip-pools/{request.BusinessDate:yyyy-MM-dd}/{request.Name}" },
                    ["add-tips"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tip-pools/{request.BusinessDate:yyyy-MM-dd}/{request.Name}/tips" },
                    ["distribute"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tip-pools/{request.BusinessDate:yyyy-MM-dd}/{request.Name}/distribute" }
                }));
        });

        // Get a tip pool
        group.MapGet("/{date}/{poolName}", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            string poolName,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITipPoolGrain>(GrainKeys.TipPool(orgId, siteId, date, poolName));
            try
            {
                var snapshot = await grain.GetSnapshotAsync();
                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tip-pools/{date:yyyy-MM-dd}/{poolName}" },
                    ["add-tips"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tip-pools/{date:yyyy-MM-dd}/{poolName}/tips" },
                    ["distribute"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tip-pools/{date:yyyy-MM-dd}/{poolName}/distribute" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Tip pool not found"));
            }
        });

        // Add tips to the pool
        group.MapPost("/{date}/{poolName}/tips", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            string poolName,
            [FromBody] AddTipsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITipPoolGrain>(GrainKeys.TipPool(orgId, siteId, date, poolName));
            try
            {
                await grain.AddTipsAsync(new AddTipsCommand(request.Amount, request.Source));
                return Results.Ok(new { message = "Tips added", amount = request.Amount, source = request.Source });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Add a participant to the pool
        group.MapPost("/{date}/{poolName}/participants", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            string poolName,
            [FromBody] AddParticipantRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITipPoolGrain>(GrainKeys.TipPool(orgId, siteId, date, poolName));
            try
            {
                await grain.AddParticipantAsync(request.EmployeeId, request.HoursWorked, request.Points);
                return Results.Ok(new { message = "Participant added", employeeId = request.EmployeeId });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        // Distribute tips
        group.MapPost("/{date}/{poolName}/distribute", async (
            Guid orgId,
            Guid siteId,
            DateOnly date,
            string poolName,
            [FromBody] DistributeTipsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITipPoolGrain>(GrainKeys.TipPool(orgId, siteId, date, poolName));
            try
            {
                var result = await grain.DistributeAsync(new DistributeTipsCommand(request.DistributedByUserId));
                return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tip-pools/{date:yyyy-MM-dd}/{poolName}" }
                }));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(Hal.Error("invalid_operation", ex.Message));
            }
        });

        return app;
    }
}
