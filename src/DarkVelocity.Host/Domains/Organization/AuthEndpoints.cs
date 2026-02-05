using DarkVelocity.Host.Authorization;
using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class AuthEndpoints
{
    public static WebApplication MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/pin", async (
            [FromBody] PinLoginApiRequest request,
            IGrainFactory grainFactory,
            IAuthorizationService authService) =>
        {
            var deviceGrain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(request.OrganizationId, request.DeviceId));
            if (!await deviceGrain.IsAuthorizedAsync())
                return Results.Unauthorized();

            var pinHash = HashPin(request.Pin);

            var userLookupGrain = grainFactory.GetGrain<IUserLookupGrain>(GrainKeys.UserLookup(request.OrganizationId));
            var lookupResult = await userLookupGrain.FindByPinHashAsync(pinHash, request.SiteId);

            if (lookupResult == null)
                return Results.BadRequest(Hal.Error("invalid_pin", "Invalid PIN"));

            var userGrain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(request.OrganizationId, lookupResult.UserId));
            var authResult = await userGrain.VerifyPinAsync(request.Pin);

            if (!authResult.Success)
                return Results.BadRequest(Hal.Error("invalid_pin", authResult.Error ?? "Authentication failed"));

            var sessionId = Guid.NewGuid();
            var sessionGrain = grainFactory.GetGrain<ISessionGrain>(GrainKeys.Session(request.OrganizationId, sessionId));
            var tokens = await sessionGrain.CreateAsync(new CreateSessionCommand(
                lookupResult.UserId,
                request.OrganizationId,
                request.SiteId,
                request.DeviceId,
                "pin",
                null,
                null
            ));

            // Create SpiceDB session with PIN scope (restricted to POS operations)
            await authService.CreateSessionAsync(
                lookupResult.UserId,
                request.OrganizationId,
                request.SiteId,
                "pin");

            await deviceGrain.SetCurrentUserAsync(lookupResult.UserId);
            await userGrain.RecordLoginAsync();

            return Results.Ok(Hal.Resource(new PinLoginResponse(
                tokens.AccessToken,
                tokens.RefreshToken,
                (int)(tokens.AccessTokenExpires - DateTime.UtcNow).TotalSeconds,
                lookupResult.UserId,
                lookupResult.DisplayName
            ), new Dictionary<string, object>
            {
                ["self"] = new { href = "/api/auth/pin" },
                ["logout"] = new { href = "/api/auth/logout" },
                ["refresh"] = new { href = "/api/auth/refresh" },
                ["organization"] = new { href = $"/api/orgs/{request.OrganizationId}" },
                ["site"] = new { href = $"/api/orgs/{request.OrganizationId}/sites/{request.SiteId}" }
            }));
        });

        group.MapPost("/logout", async (
            [FromBody] LogoutApiRequest request,
            IGrainFactory grainFactory,
            IAuthorizationService authService) =>
        {
            var sessionGrain = grainFactory.GetGrain<ISessionGrain>(GrainKeys.Session(request.OrganizationId, request.SessionId));
            var sessionState = await sessionGrain.GetStateAsync();
            await sessionGrain.RevokeAsync();

            // Revoke SpiceDB session
            if (sessionState.SiteId.HasValue)
            {
                await authService.RevokeSessionAsync(
                    sessionState.UserId,
                    request.OrganizationId,
                    sessionState.SiteId.Value);
            }

            var deviceGrain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(request.OrganizationId, request.DeviceId));
            await deviceGrain.SetCurrentUserAsync(null);

            return Results.Ok(Hal.Resource(new { loggedOut = true }, new Dictionary<string, object>
            {
                ["login"] = new { href = "/api/auth/pin" }
            }));
        });

        group.MapPost("/refresh", async (
            [FromBody] RefreshTokenApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var sessionGrain = grainFactory.GetGrain<ISessionGrain>(GrainKeys.Session(request.OrganizationId, request.SessionId));
            var result = await sessionGrain.RefreshAsync(request.RefreshToken);

            if (!result.Success)
                return Results.BadRequest(Hal.Error("invalid_token", result.Error ?? "Token refresh failed"));

            return Results.Ok(Hal.Resource(new RefreshTokenResponse(
                result.Tokens!.AccessToken,
                result.Tokens.RefreshToken,
                (int)(result.Tokens.AccessTokenExpires - DateTime.UtcNow).TotalSeconds
            ), new Dictionary<string, object>
            {
                ["refresh"] = new { href = "/api/auth/refresh" },
                ["logout"] = new { href = "/api/auth/logout" }
            }));
        });

        return app;
    }

    private static string HashPin(string pin)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(pin));
        return Convert.ToBase64String(bytes);
    }
}
