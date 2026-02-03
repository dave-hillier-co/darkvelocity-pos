using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class DeviceEndpoints
{
    public static WebApplication MapDeviceEndpoints(this WebApplication app)
    {
        MapStationsEndpoints(app);
        MapDeviceAuthEndpoints(app);
        MapDeviceManagementEndpoints(app);

        return app;
    }

    private static void MapStationsEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/stations").WithTags("Stations");

        group.MapGet("/{orgId}/{siteId}", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
        {
            var stations = new[]
            {
                new { id = Guid.NewGuid(), name = "Grill Station", siteId, orderTypes = new[] { "hot", "grill" } },
                new { id = Guid.NewGuid(), name = "Cold Station", siteId, orderTypes = new[] { "cold", "salad" } },
                new { id = Guid.NewGuid(), name = "Expeditor", siteId, orderTypes = new[] { "all" } },
                new { id = Guid.NewGuid(), name = "Bar", siteId, orderTypes = new[] { "drinks", "bar" } },
            };
            return Results.Ok(new { items = stations });
        });

        group.MapPost("/{orgId}/{siteId}/select", async (
            Guid orgId,
            Guid siteId,
            [FromBody] SelectStationRequest request,
            IGrainFactory grainFactory) =>
        {
            var deviceGrain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, request.DeviceId));
            if (!await deviceGrain.ExistsAsync())
                return Results.NotFound(new { error = "device_not_found" });

            return Results.Ok(new
            {
                message = "Station selected",
                deviceId = request.DeviceId,
                stationId = request.StationId,
                stationName = request.StationName
            });
        });
    }

    private static void MapDeviceAuthEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/device").WithTags("DeviceAuth");

        group.MapPost("/code", async (
            [FromBody] DeviceCodeApiRequest request,
            IGrainFactory grainFactory,
            HttpContext httpContext) =>
        {
            var userCode = GrainKeys.GenerateUserCode();
            var grain = grainFactory.GetGrain<IDeviceAuthGrain>(userCode);

            var response = await grain.InitiateAsync(new DeviceCodeRequest(
                request.ClientId,
                request.Scope ?? "device",
                request.DeviceFingerprint,
                httpContext.Connection.RemoteIpAddress?.ToString()
            ));

            return Results.Ok(response);
        });

        group.MapPost("/token", async (
            [FromBody] DeviceTokenApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceAuthGrain>(request.UserCode.Replace("-", "").ToUpperInvariant());
            var status = await grain.GetStatusAsync();

            return status switch
            {
                DeviceAuthStatus.Pending => Results.BadRequest(new { error = "authorization_pending", error_description = "The authorization request is still pending" }),
                DeviceAuthStatus.Expired => Results.BadRequest(new { error = "expired_token", error_description = "The device code has expired" }),
                DeviceAuthStatus.Denied => Results.BadRequest(new { error = "access_denied", error_description = "The authorization request was denied" }),
                DeviceAuthStatus.Authorized => Results.Ok(await grain.GetTokenAsync(request.DeviceCode)),
                _ => Results.BadRequest(new { error = "invalid_request" })
            };
        });

        group.MapPost("/authorize", async (
            [FromBody] AuthorizeDeviceApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceAuthGrain>(request.UserCode.Replace("-", "").ToUpperInvariant());

            await grain.AuthorizeAsync(new AuthorizeDeviceCommand(
                request.AuthorizedBy,
                request.OrganizationId,
                request.SiteId,
                request.DeviceName,
                request.AppType
            ));

            return Results.Ok(new { message = "Device authorized successfully" });
        });

        group.MapPost("/deny", async (
            [FromBody] DenyDeviceApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceAuthGrain>(request.UserCode.Replace("-", "").ToUpperInvariant());
            await grain.DenyAsync(request.Reason ?? "User denied authorization");
            return Results.Ok(new { message = "Device authorization denied" });
        });
    }

    private static void MapDeviceManagementEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/devices").WithTags("Devices");

        group.MapGet("/{orgId}/{deviceId}", async (
            Guid orgId,
            Guid deviceId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(snapshot);
        });

        group.MapPost("/{orgId}/{deviceId}/heartbeat", async (
            Guid orgId,
            Guid deviceId,
            [FromBody] DeviceHeartbeatRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.RecordHeartbeatAsync(request.AppVersion);
            return Results.Ok();
        });

        group.MapPost("/{orgId}/{deviceId}/suspend", async (
            Guid orgId,
            Guid deviceId,
            [FromBody] SuspendDeviceRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.SuspendAsync(request.Reason);
            return Results.Ok(new { message = "Device suspended" });
        });

        group.MapPost("/{orgId}/{deviceId}/revoke", async (
            Guid orgId,
            Guid deviceId,
            [FromBody] RevokeDeviceRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, deviceId));
            if (!await grain.ExistsAsync())
                return Results.NotFound();

            await grain.RevokeAsync(request.Reason);
            return Results.Ok(new { message = "Device revoked" });
        });
    }
}
