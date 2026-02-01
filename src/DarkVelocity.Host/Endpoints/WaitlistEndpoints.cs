using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class WaitlistEndpoints
{
    public static WebApplication MapWaitlistEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/waitlist").WithTags("Waitlist");

        group.MapPost("/", async (
            Guid orgId,
            Guid siteId,
            [FromBody] AddToWaitlistRequest request,
            IGrainFactory grainFactory) =>
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var grain = grainFactory.GetGrain<IWaitlistGrain>(GrainKeys.Waitlist(orgId, siteId, date));
            if (!await grain.ExistsAsync())
                await grain.InitializeAsync(orgId, siteId, date);

            var result = await grain.AddEntryAsync(new AddToWaitlistCommand(
                request.Guest, request.PartySize, request.QuotedWait, request.TablePreferences, request.NotificationMethod));

            return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/waitlist/{result.EntryId}", Hal.Resource(new
            {
                entryId = result.EntryId,
                position = result.Position,
                quotedWait = result.QuotedWait
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/waitlist/{result.EntryId}" },
                ["notify"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/waitlist/{result.EntryId}/notify" },
                ["seat"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/waitlist/{result.EntryId}/seat" }
            }));
        });

        group.MapGet("/", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var grain = grainFactory.GetGrain<IWaitlistGrain>(GrainKeys.Waitlist(orgId, siteId, date));
            if (!await grain.ExistsAsync())
                return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/waitlist", new List<object>(), 0));

            var entries = await grain.GetEntriesAsync();
            var items = entries.Select(e => Hal.Resource(e, new Dictionary<string, object>
            {
                ["notify"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/waitlist/{e.Id}/notify" },
                ["seat"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/waitlist/{e.Id}/seat" }
            })).ToList();

            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/waitlist", items, items.Count));
        });

        group.MapGet("/estimate", async (Guid orgId, Guid siteId, [FromQuery] int partySize, IGrainFactory grainFactory) =>
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var grain = grainFactory.GetGrain<IWaitlistGrain>(GrainKeys.Waitlist(orgId, siteId, date));
            if (!await grain.ExistsAsync())
                return Results.Ok(new { estimatedWait = TimeSpan.FromMinutes(15), waitingCount = 0 });

            var estimatedWait = await grain.GetEstimatedWaitAsync(partySize);
            var waitingCount = await grain.GetWaitingCountAsync();

            return Results.Ok(new { estimatedWait, waitingCount });
        });

        group.MapPost("/{entryId}/notify", async (Guid orgId, Guid siteId, Guid entryId, IGrainFactory grainFactory) =>
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var grain = grainFactory.GetGrain<IWaitlistGrain>(GrainKeys.Waitlist(orgId, siteId, date));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Waitlist not found"));

            await grain.NotifyEntryAsync(entryId);
            return Results.Ok(new { message = "Guest notified" });
        });

        group.MapPost("/{entryId}/seat", async (
            Guid orgId, Guid siteId, Guid entryId,
            [FromBody] SeatWaitlistEntryRequest request,
            IGrainFactory grainFactory) =>
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var grain = grainFactory.GetGrain<IWaitlistGrain>(GrainKeys.Waitlist(orgId, siteId, date));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Waitlist not found"));

            await grain.SeatEntryAsync(entryId, request.TableId);
            return Results.Ok(new { message = "Guest seated" });
        });

        group.MapDelete("/{entryId}", async (
            Guid orgId, Guid siteId, Guid entryId,
            [FromQuery] string? reason,
            IGrainFactory grainFactory) =>
        {
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            var grain = grainFactory.GetGrain<IWaitlistGrain>(GrainKeys.Waitlist(orgId, siteId, date));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Waitlist not found"));

            await grain.RemoveEntryAsync(entryId, reason ?? "Removed");
            return Results.NoContent();
        });

        return app;
    }
}
