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
            IGrainFactory grainFactory) =>
        {
            var deviceGrain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(request.OrganizationId, request.DeviceId));
            if (!await deviceGrain.IsAuthorizedAsync())
                return Results.Unauthorized();

            var pinHash = HashPin(request.Pin);

            var userLookupGrain = grainFactory.GetGrain<IUserLookupGrain>(GrainKeys.UserLookup(request.OrganizationId));
            var lookupResult = await userLookupGrain.FindByPinHashAsync(pinHash, request.SiteId);

            if (lookupResult == null)
                return Results.BadRequest(new { error = "invalid_pin", error_description = "Invalid PIN" });

            var userGrain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(request.OrganizationId, lookupResult.UserId));
            var authResult = await userGrain.VerifyPinAsync(request.Pin);

            if (!authResult.Success)
                return Results.BadRequest(new { error = "invalid_pin", error_description = authResult.Error });

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

            await deviceGrain.SetCurrentUserAsync(lookupResult.UserId);
            await userGrain.RecordLoginAsync();

            return Results.Ok(new PinLoginResponse(
                tokens.AccessToken,
                tokens.RefreshToken,
                (int)(tokens.AccessTokenExpires - DateTime.UtcNow).TotalSeconds,
                lookupResult.UserId,
                lookupResult.DisplayName
            ));
        });

        group.MapPost("/logout", async (
            [FromBody] LogoutApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var sessionGrain = grainFactory.GetGrain<ISessionGrain>(GrainKeys.Session(request.OrganizationId, request.SessionId));
            await sessionGrain.RevokeAsync();

            var deviceGrain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(request.OrganizationId, request.DeviceId));
            await deviceGrain.SetCurrentUserAsync(null);

            return Results.Ok(new { message = "Logged out successfully" });
        });

        group.MapPost("/refresh", async (
            [FromBody] RefreshTokenApiRequest request,
            IGrainFactory grainFactory) =>
        {
            var sessionGrain = grainFactory.GetGrain<ISessionGrain>(GrainKeys.Session(request.OrganizationId, request.SessionId));
            var result = await sessionGrain.RefreshAsync(request.RefreshToken);

            if (!result.Success)
                return Results.BadRequest(new { error = "invalid_token", error_description = result.Error });

            return Results.Ok(new RefreshTokenResponse(
                result.Tokens!.AccessToken,
                result.Tokens.RefreshToken,
                (int)(result.Tokens.AccessTokenExpires - DateTime.UtcNow).TotalSeconds
            ));
        });

        return app;
    }

    private static string HashPin(string pin)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(pin));
        return Convert.ToBase64String(bytes);
    }
}
