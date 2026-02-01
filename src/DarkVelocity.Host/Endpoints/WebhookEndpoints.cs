using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class WebhookEndpoints
{
    public static WebApplication MapWebhookEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/webhooks").WithTags("Webhooks");

        group.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateWebhookRequest request,
            IGrainFactory grainFactory) =>
        {
            var webhookId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IWebhookSubscriptionGrain>(GrainKeys.Webhook(orgId, webhookId));
            var result = await grain.CreateAsync(new CreateWebhookCommand(
                orgId, request.Name, request.Url, request.EventTypes, request.Secret, request.Headers));

            return Results.Created($"/api/orgs/{orgId}/webhooks/{webhookId}", Hal.Resource(new
            {
                id = result.Id,
                name = result.Name,
                createdAt = result.CreatedAt
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/webhooks/{webhookId}" },
                ["deliveries"] = new { href = $"/api/orgs/{orgId}/webhooks/{webhookId}/deliveries" }
            }));
        });

        group.MapGet("/{webhookId}", async (Guid orgId, Guid webhookId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IWebhookSubscriptionGrain>(GrainKeys.Webhook(orgId, webhookId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Webhook not found"));

            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/webhooks/{webhookId}" },
                ["deliveries"] = new { href = $"/api/orgs/{orgId}/webhooks/{webhookId}/deliveries" },
                ["pause"] = new { href = $"/api/orgs/{orgId}/webhooks/{webhookId}/pause" },
                ["resume"] = new { href = $"/api/orgs/{orgId}/webhooks/{webhookId}/resume" }
            }));
        });

        group.MapPatch("/{webhookId}", async (
            Guid orgId, Guid webhookId,
            [FromBody] UpdateWebhookRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IWebhookSubscriptionGrain>(GrainKeys.Webhook(orgId, webhookId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Webhook not found"));

            await grain.UpdateAsync(new UpdateWebhookCommand(request.Name, request.Url, request.Secret, request.EventTypes, request.Headers));
            var state = await grain.GetStateAsync();

            return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/webhooks/{webhookId}" }
            }));
        });

        group.MapDelete("/{webhookId}", async (Guid orgId, Guid webhookId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IWebhookSubscriptionGrain>(GrainKeys.Webhook(orgId, webhookId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Webhook not found"));

            await grain.DeleteAsync();
            return Results.NoContent();
        });

        group.MapPost("/{webhookId}/pause", async (Guid orgId, Guid webhookId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IWebhookSubscriptionGrain>(GrainKeys.Webhook(orgId, webhookId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Webhook not found"));

            await grain.PauseAsync();
            return Results.Ok(new { message = "Webhook paused" });
        });

        group.MapPost("/{webhookId}/resume", async (Guid orgId, Guid webhookId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IWebhookSubscriptionGrain>(GrainKeys.Webhook(orgId, webhookId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Webhook not found"));

            await grain.ResumeAsync();
            return Results.Ok(new { message = "Webhook resumed" });
        });

        group.MapGet("/{webhookId}/deliveries", async (Guid orgId, Guid webhookId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IWebhookSubscriptionGrain>(GrainKeys.Webhook(orgId, webhookId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Webhook not found"));

            var deliveries = await grain.GetRecentDeliveriesAsync();
            var items = deliveries.Select(d => Hal.Resource(d, new Dictionary<string, object>())).ToList();

            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/webhooks/{webhookId}/deliveries", items, items.Count));
        });

        return app;
    }
}
