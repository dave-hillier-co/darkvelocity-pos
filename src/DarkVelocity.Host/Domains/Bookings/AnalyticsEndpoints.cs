using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class AnalyticsEndpoints
{
    public static WebApplication MapTurnTimeAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/analytics/turn-times").WithTags("Analytics");

        // Get overall turn time stats
        group.MapGet("/", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITurnTimeAnalyticsGrain>(
                GrainKeys.TurnTimeAnalytics(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.Ok(Hal.Resource(new { message = "No turn time data" },
                    new Dictionary<string, object> { ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/analytics/turn-times" } }));

            var stats = await grain.GetOverallStatsAsync();
            return Results.Ok(Hal.Resource(stats, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/analytics/turn-times" },
                ["by-party-size"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/analytics/turn-times/by-party-size" },
                ["by-day"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/analytics/turn-times/by-day" },
                ["by-time-of-day"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/analytics/turn-times/by-time-of-day" },
                ["active-seatings"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/analytics/active-seatings" }
            }));
        });

        // Get stats by party size
        group.MapGet("/by-party-size", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITurnTimeAnalyticsGrain>(
                GrainKeys.TurnTimeAnalytics(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/analytics/turn-times/by-party-size", new List<object>(), 0));

            var stats = await grain.GetStatsByPartySizeAsync();
            var items = stats.Select(s => Hal.Resource(s, new Dictionary<string, object>())).ToList();
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/analytics/turn-times/by-party-size", items, items.Count));
        });

        // Get stats by day of week
        group.MapGet("/by-day", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITurnTimeAnalyticsGrain>(
                GrainKeys.TurnTimeAnalytics(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/analytics/turn-times/by-day", new List<object>(), 0));

            var stats = await grain.GetStatsByDayAsync();
            var items = stats.Select(s => Hal.Resource(s, new Dictionary<string, object>())).ToList();
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/analytics/turn-times/by-day", items, items.Count));
        });

        // Get stats by time of day (meal period)
        group.MapGet("/by-time-of-day", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITurnTimeAnalyticsGrain>(
                GrainKeys.TurnTimeAnalytics(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/analytics/turn-times/by-time-of-day", new List<object>(), 0));

            var stats = await grain.GetStatsByTimeOfDayAsync();
            var items = stats.Select(s => Hal.Resource(s, new Dictionary<string, object>())).ToList();
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/analytics/turn-times/by-time-of-day", items, items.Count));
        });

        // Get recent turn time records
        group.MapGet("/recent", async (
            Guid orgId, Guid siteId,
            [FromQuery] int? limit,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITurnTimeAnalyticsGrain>(
                GrainKeys.TurnTimeAnalytics(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/analytics/turn-times/recent", new List<object>(), 0));

            var records = await grain.GetRecentRecordsAsync(limit ?? 100);
            var items = records.Select(r => Hal.Resource(r, new Dictionary<string, object>())).ToList();
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/analytics/turn-times/recent", items, items.Count));
        });

        // Get active seatings
        app.MapGet("/api/orgs/{orgId}/sites/{siteId}/analytics/active-seatings", async (
            Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITurnTimeAnalyticsGrain>(
                GrainKeys.TurnTimeAnalytics(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/analytics/active-seatings", new List<object>(), 0));

            var seatings = await grain.GetActiveSeatingsAsync();
            var items = seatings.Select(s => Hal.Resource(s, new Dictionary<string, object>())).ToList();
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/analytics/active-seatings", items, items.Count));
        }).WithTags("Analytics");

        // Get long-running tables
        app.MapGet("/api/orgs/{orgId}/sites/{siteId}/analytics/long-running", async (
            Guid orgId, Guid siteId,
            [FromQuery] int? thresholdMinutes,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<ITurnTimeAnalyticsGrain>(
                GrainKeys.TurnTimeAnalytics(orgId, siteId));
            if (!await grain.ExistsAsync())
                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/analytics/long-running", new List<object>(), 0));

            var threshold = TimeSpan.FromMinutes(thresholdMinutes ?? 30);
            var alerts = await grain.GetLongRunningTablesAsync(threshold);
            var items = alerts.Select(a => Hal.Resource(a, new Dictionary<string, object>())).ToList();
            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/analytics/long-running", items, items.Count));
        }).WithTags("Analytics");

        return app;
    }
}
