using DarkVelocity.Host.Auth;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DarkVelocity.Host.Endpoints;

public static class OAuthEndpoints
{
    public static WebApplication MapOAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/oauth").WithTags("OAuth");

        group.MapGet("/login/{provider}", (string provider, string? returnUrl) =>
        {
            var redirectUri = returnUrl ?? "/";
            var properties = new AuthenticationProperties { RedirectUri = $"/api/oauth/callback?returnUrl={Uri.EscapeDataString(redirectUri)}" };

            return provider.ToLowerInvariant() switch
            {
                "google" => Results.Challenge(properties, ["Google"]),
                "microsoft" => Results.Challenge(properties, ["Microsoft"]),
                _ => Results.BadRequest(new { error = "invalid_provider", error_description = "Supported providers: google, microsoft" })
            };
        });

        group.MapGet("/callback", async (
            HttpContext context,
            IGrainFactory grainFactory,
            JwtTokenService tokenService,
            string? returnUrl) =>
        {
            var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded || result.Principal == null)
            {
                return Results.Redirect($"{returnUrl ?? "/"}?error=auth_failed");
            }

            var claims = result.Principal.Claims.ToList();
            var email = claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Email)?.Value;
            var name = claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name)?.Value;
            var externalId = claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(externalId))
            {
                return Results.Redirect($"{returnUrl ?? "/"}?error=missing_claims");
            }

            var orgId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var userId = Guid.NewGuid();

            var (accessToken, expires) = tokenService.GenerateAccessToken(
                userId,
                name ?? email,
                orgId,
                roles: ["admin", "backoffice"]
            );
            var refreshToken = tokenService.GenerateRefreshToken();

            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var redirectTarget = returnUrl ?? "http://localhost:5174";
            var separator = redirectTarget.Contains('#') ? "&" : "#";
            return Results.Redirect($"{redirectTarget}{separator}access_token={accessToken}&refresh_token={refreshToken}&expires_in={(int)(expires - DateTime.UtcNow).TotalSeconds}&user_id={userId}&display_name={Uri.EscapeDataString(name ?? email)}");
        });

        group.MapGet("/userinfo", (HttpContext context) =>
        {
            if (context.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            var claims = context.User.Claims.ToList();
            return Results.Ok(new
            {
                sub = claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                name = claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Name)?.Value,
                org_id = claims.FirstOrDefault(c => c.Type == "org_id")?.Value,
                site_id = claims.FirstOrDefault(c => c.Type == "site_id")?.Value,
                roles = claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToArray()
            });
        }).RequireAuthorization();

        return app;
    }
}
