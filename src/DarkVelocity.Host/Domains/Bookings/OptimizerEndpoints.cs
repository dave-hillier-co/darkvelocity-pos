using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class OptimizerEndpoints
{
    public static WebApplication MapOptimizerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/tables").WithTags("Tables");

        // Get table recommendations for a booking
        group.MapGet("/recommendations", async (
            Guid orgId, Guid siteId,
            [FromQuery] int partySize,
            [FromQuery] DateTime? bookingTime,
            [FromQuery] string? preference,
            [FromQuery] Guid? bookingId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
                GrainKeys.TableAssignmentOptimizer(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.Ok(Hal.Resource(new { success = false, message = "Optimizer not initialized", recommendations = Array.Empty<object>() },
                    new Dictionary<string, object> { ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/recommendations" } }));

            var result = await grain.GetRecommendationsAsync(new TableAssignmentRequest(
                BookingId: bookingId ?? Guid.Empty,
                PartySize: partySize,
                BookingTime: bookingTime ?? DateTime.UtcNow,
                Duration: TimeSpan.FromMinutes(90),
                SeatingPreference: preference));

            return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/recommendations?partySize={partySize}" }
            }));
        });

        // Auto-assign a table for a booking
        group.MapPost("/auto-assign", async (
            Guid orgId, Guid siteId,
            [FromBody] AutoAssignTableRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
                GrainKeys.TableAssignmentOptimizer(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.BadRequest(Hal.Error("not_initialized", "Optimizer not initialized"));

            var recommendation = await grain.AutoAssignAsync(new TableAssignmentRequest(
                BookingId: request.BookingId,
                PartySize: request.PartySize,
                BookingTime: request.BookingTime,
                Duration: request.Duration ?? TimeSpan.FromMinutes(90),
                SeatingPreference: request.SeatingPreference,
                IsVip: request.IsVip,
                PreferredServerId: request.PreferredServerId,
                PreferredTableId: request.PreferredTableId));

            if (recommendation == null)
                return Results.Ok(Hal.Resource(new { success = false, message = "No suitable table found" },
                    new Dictionary<string, object> { ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/auto-assign" } }));

            return Results.Ok(Hal.Resource(new { success = true, recommendation }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/auto-assign" },
                ["table"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/tables/{recommendation.TableId}" }
            }));
        });

        // Get server workloads
        group.MapGet("/server-workloads", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
                GrainKeys.TableAssignmentOptimizer(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/tables/server-workloads", new List<object>(), 0));

            var workloads = await grain.GetServerWorkloadsAsync();
            var items = workloads.Select(w => Hal.Resource(w, new Dictionary<string, object>())).ToList();
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/tables/server-workloads", items, items.Count));
        });

        // Update server sections
        group.MapPost("/server-sections", async (
            Guid orgId, Guid siteId,
            [FromBody] UpdateServerSectionCommand command,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
                GrainKeys.TableAssignmentOptimizer(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.BadRequest(Hal.Error("not_initialized", "Optimizer not initialized"));

            await grain.UpdateServerSectionAsync(command);
            var sections = await grain.GetServerSectionsAsync();
            var items = sections.Select(s => Hal.Resource(s, new Dictionary<string, object>())).ToList();
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/tables/server-sections", items, items.Count));
        });

        // Get server sections
        group.MapGet("/server-sections", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITableAssignmentOptimizerGrain>(
                GrainKeys.TableAssignmentOptimizer(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/tables/server-sections", new List<object>(), 0));

            var sections = await grain.GetServerSectionsAsync();
            var items = sections.Select(s => Hal.Resource(s, new Dictionary<string, object>())).ToList();
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/tables/server-sections", items, items.Count));
        });

        return app;
    }
}

public record AutoAssignTableRequest(
    Guid BookingId,
    int PartySize,
    DateTime BookingTime,
    TimeSpan? Duration = null,
    string? SeatingPreference = null,
    bool IsVip = false,
    Guid? PreferredServerId = null,
    Guid? PreferredTableId = null);
