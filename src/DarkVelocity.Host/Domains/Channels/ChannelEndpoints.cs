using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class ChannelEndpoints
{
    public static WebApplication MapChannelEndpoints(this WebApplication app)
    {
        var channelGroup = app.MapGroup("/api/orgs/{orgId}/channels").WithTags("Channels");
        var statusMappingGroup = app.MapGroup("/api/orgs/{orgId}/status-mappings").WithTags("Status Mappings");

        // ============================================================================
        // Channel Registry Endpoints
        // ============================================================================

        channelGroup.MapGet("/", async (Guid orgId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelRegistryGrain>(GrainKeys.ChannelRegistry(orgId));
            var channels = await grain.GetAllChannelsAsync();

            var items = channels.Select(c => Hal.Resource(c, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/channels/{c.ChannelId}" }
            }));

            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/channels", items, channels.Count));
        })
        .WithName("ListChannels")
        .WithSummary("List all channels for an organization");

        channelGroup.MapGet("/by-type/{integrationType}", async (
            Guid orgId,
            IntegrationType integrationType,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelRegistryGrain>(GrainKeys.ChannelRegistry(orgId));
            var channels = await grain.GetChannelsByTypeAsync(integrationType);

            var items = channels.Select(c => Hal.Resource(c, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/channels/{c.ChannelId}" }
            }));

            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/channels/by-type/{integrationType}", items, channels.Count));
        })
        .WithName("ListChannelsByType")
        .WithSummary("List channels filtered by integration type");

        channelGroup.MapGet("/by-platform/{platformType}", async (
            Guid orgId,
            DeliveryPlatformType platformType,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelRegistryGrain>(GrainKeys.ChannelRegistry(orgId));
            var channels = await grain.GetChannelsByPlatformAsync(platformType);

            var items = channels.Select(c => Hal.Resource(c, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/channels/{c.ChannelId}" }
            }));

            return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/channels/by-platform/{platformType}", items, channels.Count));
        })
        .WithName("ListChannelsByPlatform")
        .WithSummary("List channels filtered by platform type");

        // ============================================================================
        // Channel Grain Endpoints
        // ============================================================================

        channelGroup.MapPost("/", async (
            Guid orgId,
            [FromBody] ConnectChannelRequest request,
            IGrainFactory grainFactory) =>
        {
            var channelId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));

            var result = await grain.ConnectAsync(new ConnectChannelCommand(
                request.PlatformType,
                request.IntegrationType,
                request.Name,
                request.ApiCredentialsEncrypted,
                request.WebhookSecret,
                request.ExternalChannelId,
                request.Settings));

            // Register in registry
            var registry = grainFactory.GetGrain<IChannelRegistryGrain>(GrainKeys.ChannelRegistry(orgId));
            await registry.RegisterChannelAsync(channelId, request.PlatformType, request.IntegrationType, request.Name);

            return Results.Created($"/api/orgs/{orgId}/channels/{channelId}", Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/channels/{channelId}" },
                ["organization"] = new { href = $"/api/orgs/{orgId}" },
                ["locations"] = new { href = $"/api/orgs/{orgId}/channels/{channelId}/locations" },
                ["pause"] = new { href = $"/api/orgs/{orgId}/channels/{channelId}/pause" },
                ["resume"] = new { href = $"/api/orgs/{orgId}/channels/{channelId}/resume" }
            }));
        })
        .WithName("ConnectChannel")
        .WithSummary("Connect a new delivery/order channel");

        channelGroup.MapGet("/{channelId}", async (Guid orgId, Guid channelId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));
            var snapshot = await grain.GetSnapshotAsync();

            if (snapshot.ChannelId == Guid.Empty)
                return Results.NotFound(Hal.Error("not_found", "Channel not found"));

            return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/channels/{channelId}" },
                ["organization"] = new { href = $"/api/orgs/{orgId}" },
                ["locations"] = new { href = $"/api/orgs/{orgId}/channels/{channelId}/locations" },
                ["pause"] = new { href = $"/api/orgs/{orgId}/channels/{channelId}/pause" },
                ["resume"] = new { href = $"/api/orgs/{orgId}/channels/{channelId}/resume" }
            }));
        })
        .WithName("GetChannel")
        .WithSummary("Get channel details");

        channelGroup.MapPatch("/{channelId}", async (
            Guid orgId,
            Guid channelId,
            [FromBody] UpdateChannelRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));

            try
            {
                var result = await grain.UpdateAsync(new UpdateChannelCommand(
                    request.Name,
                    request.Status,
                    request.ApiCredentialsEncrypted,
                    request.WebhookSecret,
                    request.Settings));

                return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/channels/{channelId}" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Channel not found"));
            }
        })
        .WithName("UpdateChannel")
        .WithSummary("Update channel configuration");

        channelGroup.MapDelete("/{channelId}", async (Guid orgId, Guid channelId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));
            await grain.DisconnectAsync();

            var registry = grainFactory.GetGrain<IChannelRegistryGrain>(GrainKeys.ChannelRegistry(orgId));
            await registry.UnregisterChannelAsync(channelId);

            return Results.NoContent();
        })
        .WithName("DisconnectChannel")
        .WithSummary("Disconnect and remove a channel");

        channelGroup.MapPost("/{channelId}/pause", async (
            Guid orgId,
            Guid channelId,
            [FromBody] PauseChannelRequest? request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));
            await grain.PauseAsync(request?.Reason);

            var registry = grainFactory.GetGrain<IChannelRegistryGrain>(GrainKeys.ChannelRegistry(orgId));
            await registry.UpdateChannelStatusAsync(channelId, ChannelStatus.Paused);

            return Results.Ok(new { status = "paused", reason = request?.Reason });
        })
        .WithName("PauseChannel")
        .WithSummary("Pause order acceptance on a channel");

        channelGroup.MapPost("/{channelId}/resume", async (Guid orgId, Guid channelId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));
            await grain.ResumeAsync();

            var registry = grainFactory.GetGrain<IChannelRegistryGrain>(GrainKeys.ChannelRegistry(orgId));
            await registry.UpdateChannelStatusAsync(channelId, ChannelStatus.Active);

            return Results.Ok(new { status = "active" });
        })
        .WithName("ResumeChannel")
        .WithSummary("Resume order acceptance on a channel");

        channelGroup.MapGet("/{channelId}/accepting-orders", async (Guid orgId, Guid channelId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));
            var accepting = await grain.IsAcceptingOrdersAsync();

            return Results.Ok(new { acceptingOrders = accepting });
        })
        .WithName("CheckChannelAcceptingOrders")
        .WithSummary("Check if channel is accepting orders");

        // ============================================================================
        // Channel Location Mapping Endpoints
        // ============================================================================

        channelGroup.MapPost("/{channelId}/locations", async (
            Guid orgId,
            Guid channelId,
            [FromBody] AddChannelLocationRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));

            await grain.AddLocationMappingAsync(new ChannelLocationMapping(
                request.LocationId,
                request.ExternalStoreId,
                request.IsActive,
                request.MenuId,
                request.OperatingHoursOverride));

            return Results.Created($"/api/orgs/{orgId}/channels/{channelId}/locations/{request.LocationId}",
                new { locationId = request.LocationId, externalStoreId = request.ExternalStoreId });
        })
        .WithName("AddChannelLocation")
        .WithSummary("Map a location to this channel");

        channelGroup.MapDelete("/{channelId}/locations/{locationId}", async (
            Guid orgId,
            Guid channelId,
            Guid locationId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));
            await grain.RemoveLocationMappingAsync(locationId);

            return Results.NoContent();
        })
        .WithName("RemoveChannelLocation")
        .WithSummary("Remove a location mapping from this channel");

        // ============================================================================
        // Channel Metrics Endpoints
        // ============================================================================

        channelGroup.MapPost("/{channelId}/orders", async (
            Guid orgId,
            Guid channelId,
            [FromBody] RecordOrderRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));
            await grain.RecordOrderAsync(request.OrderTotal);

            return Results.Ok(new { recorded = true });
        })
        .WithName("RecordChannelOrder")
        .WithSummary("Record an order received from this channel");

        channelGroup.MapPost("/{channelId}/heartbeat", async (Guid orgId, Guid channelId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));
            await grain.RecordHeartbeatAsync();

            return Results.Ok(new { timestamp = DateTime.UtcNow });
        })
        .WithName("RecordChannelHeartbeat")
        .WithSummary("Record a heartbeat from the channel integration");

        channelGroup.MapPost("/{channelId}/sync", async (Guid orgId, Guid channelId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));
            await grain.RecordSyncAsync();

            return Results.Ok(new { syncedAt = DateTime.UtcNow });
        })
        .WithName("RecordChannelSync")
        .WithSummary("Record a successful sync with the external platform");

        channelGroup.MapPost("/{channelId}/error", async (
            Guid orgId,
            Guid channelId,
            [FromBody] RecordErrorRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));
            await grain.RecordErrorAsync(request.ErrorMessage);

            var registry = grainFactory.GetGrain<IChannelRegistryGrain>(GrainKeys.ChannelRegistry(orgId));
            await registry.UpdateChannelStatusAsync(channelId, ChannelStatus.Error);

            return Results.Ok(new { status = "error", message = request.ErrorMessage });
        })
        .WithName("RecordChannelError")
        .WithSummary("Record an error that occurred on this channel");

        // ============================================================================
        // Status Mapping Endpoints
        // ============================================================================

        statusMappingGroup.MapPost("/{platformType}", async (
            Guid orgId,
            DeliveryPlatformType platformType,
            [FromBody] ConfigureStatusMappingRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IStatusMappingGrain>(GrainKeys.StatusMapping(orgId, platformType));

            var mappings = request.Mappings.Select(m => new StatusMappingEntry(
                m.ExternalStatusCode,
                m.ExternalStatusName,
                m.InternalStatus,
                m.TriggersPosAction,
                m.PosActionType)).ToList();

            var result = await grain.ConfigureAsync(new ConfigureStatusMappingCommand(platformType, mappings));

            return Results.Created($"/api/orgs/{orgId}/status-mappings/{platformType}", Hal.Resource(result, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/status-mappings/{platformType}" }
            }));
        })
        .WithName("ConfigureStatusMapping")
        .WithSummary("Configure status mappings for a platform");

        statusMappingGroup.MapGet("/{platformType}", async (
            Guid orgId,
            DeliveryPlatformType platformType,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IStatusMappingGrain>(GrainKeys.StatusMapping(orgId, platformType));
            var snapshot = await grain.GetSnapshotAsync();

            return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/status-mappings/{platformType}" }
            }));
        })
        .WithName("GetStatusMapping")
        .WithSummary("Get status mappings for a platform");

        statusMappingGroup.MapPost("/{platformType}/mappings", async (
            Guid orgId,
            DeliveryPlatformType platformType,
            [FromBody] AddStatusMappingRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IStatusMappingGrain>(GrainKeys.StatusMapping(orgId, platformType));

            await grain.AddMappingAsync(new StatusMappingEntry(
                request.ExternalStatusCode,
                request.ExternalStatusName,
                request.InternalStatus,
                request.TriggersPosAction,
                request.PosActionType));

            return Results.Created($"/api/orgs/{orgId}/status-mappings/{platformType}/mappings/{request.ExternalStatusCode}",
                new { externalStatusCode = request.ExternalStatusCode, internalStatus = request.InternalStatus });
        })
        .WithName("AddStatusMappingEntry")
        .WithSummary("Add or update a status mapping entry");

        statusMappingGroup.MapDelete("/{platformType}/mappings/{externalStatusCode}", async (
            Guid orgId,
            DeliveryPlatformType platformType,
            string externalStatusCode,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IStatusMappingGrain>(GrainKeys.StatusMapping(orgId, platformType));
            await grain.RemoveMappingAsync(externalStatusCode);

            return Results.NoContent();
        })
        .WithName("RemoveStatusMappingEntry")
        .WithSummary("Remove a status mapping entry");

        statusMappingGroup.MapGet("/{platformType}/translate/{externalStatusCode}", async (
            Guid orgId,
            DeliveryPlatformType platformType,
            string externalStatusCode,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IStatusMappingGrain>(GrainKeys.StatusMapping(orgId, platformType));
            var internalStatus = await grain.GetInternalStatusAsync(externalStatusCode);

            if (internalStatus == null)
                return Results.NotFound(Hal.Error("not_found", $"No mapping found for status code: {externalStatusCode}"));

            await grain.RecordUsageAsync(externalStatusCode);

            return Results.Ok(new
            {
                externalStatusCode,
                internalStatus = internalStatus.Value.ToString(),
                internalStatusCode = (int)internalStatus.Value
            });
        })
        .WithName("TranslateStatus")
        .WithSummary("Translate an external status code to internal status");

        // ============================================================================
        // Menu Sync Endpoints
        // ============================================================================

        channelGroup.MapPost("/{channelId}/menu-sync", async (
            Guid orgId,
            Guid channelId,
            [FromBody] TriggerMenuSyncRequest? request,
            IGrainFactory grainFactory) =>
        {
            var channelGrain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));
            var channelSnapshot = await channelGrain.GetSnapshotAsync();

            if (channelSnapshot.ChannelId == Guid.Empty)
                return Results.NotFound(Hal.Error("not_found", "Channel not found"));

            // Create a new menu sync
            var syncId = Guid.NewGuid();
            var syncGrain = grainFactory.GetGrain<IMenuSyncGrain>(GrainKeys.MenuSync(orgId, syncId));

            var command = new StartMenuSyncCommand(channelId, request?.LocationId);
            var snapshot = await syncGrain.StartAsync(command);

            // Record sync on channel
            await channelGrain.RecordSyncAsync();

            return Results.Accepted($"/api/orgs/{orgId}/channels/{channelId}/menu-sync/{syncId}", Hal.Resource(snapshot, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/channels/{channelId}/menu-sync/{syncId}" },
                ["channel"] = new { href = $"/api/orgs/{orgId}/channels/{channelId}" }
            }));
        })
        .WithName("TriggerMenuSync")
        .WithSummary("Trigger a menu sync to the channel");

        channelGroup.MapGet("/{channelId}/menu-sync/{syncId}", async (
            Guid orgId,
            Guid channelId,
            Guid syncId,
            IGrainFactory grainFactory) =>
        {
            var syncGrain = grainFactory.GetGrain<IMenuSyncGrain>(GrainKeys.MenuSync(orgId, syncId));

            try
            {
                var snapshot = await syncGrain.GetSnapshotAsync();

                return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/channels/{channelId}/menu-sync/{syncId}" },
                    ["channel"] = new { href = $"/api/orgs/{orgId}/channels/{channelId}" }
                }));
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound(Hal.Error("not_found", "Menu sync not found"));
            }
        })
        .WithName("GetMenuSyncStatus")
        .WithSummary("Get the status of a menu sync operation");

        // ============================================================================
        // Store Status Endpoints
        // ============================================================================

        channelGroup.MapPost("/{channelId}/locations/{locationId}/busy", async (
            Guid orgId,
            Guid channelId,
            Guid locationId,
            [FromBody] SetBusyModeRequest request,
            IGrainFactory grainFactory) =>
        {
            var channelGrain = grainFactory.GetGrain<IChannelGrain>(GrainKeys.Channel(orgId, channelId));
            var channelSnapshot = await channelGrain.GetSnapshotAsync();

            if (channelSnapshot.ChannelId == Guid.Empty)
                return Results.NotFound(Hal.Error("not_found", "Channel not found"));

            // Find the location mapping
            var locationMapping = channelSnapshot.Locations.FirstOrDefault(l => l.LocationId == locationId);
            if (locationMapping == null)
                return Results.NotFound(Hal.Error("not_found", "Location not mapped to this channel"));

            // Note: In a real implementation, this would call the platform adapter
            // to update the store's busy mode

            return Results.Ok(new
            {
                channelId,
                locationId,
                busyMode = request.IsBusy,
                additionalPrepTime = request.AdditionalPrepMinutes,
                effectiveUntil = request.Duration.HasValue ? DateTime.UtcNow.Add(request.Duration.Value) : (DateTime?)null
            });
        })
        .WithName("SetChannelLocationBusyMode")
        .WithSummary("Set busy mode for a location on this channel");

        return app;
    }
}

// ============================================================================
// Request DTOs
// ============================================================================

public record TriggerMenuSyncRequest(Guid? LocationId);

public record SetBusyModeRequest(bool IsBusy, int? AdditionalPrepMinutes, TimeSpan? Duration);
