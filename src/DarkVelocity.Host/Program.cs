using DarkVelocity.Host;
using DarkVelocity.Host.Auth;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Configure Orleans Silo
builder.Host.UseOrleans(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
    siloBuilder.AddMemoryGrainStorageAsDefault();
    siloBuilder.AddMemoryGrainStorage("PersistentStorage");
    siloBuilder.AddMemoryStreams("StreamProvider");
    siloBuilder.UseDashboard(options =>
    {
        options.Port = 8888;
        options.HostSelf = true;
    });
});

builder.Services.AddEndpointsApiExplorer();

// Configure JWT Settings
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<JwtTokenService>();

// Configure OAuth Settings
var oauthSettings = builder.Configuration.GetSection("OAuth").Get<OAuthSettings>() ?? new OAuthSettings();
builder.Services.AddSingleton(oauthSettings);

// Configure Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    var tokenService = new JwtTokenService(jwtSettings);
    options.TokenValidationParameters = tokenService.GetValidationParameters();
})
.AddCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
})
.AddGoogle(options =>
{
    options.ClientId = oauthSettings.Google.ClientId;
    options.ClientSecret = oauthSettings.Google.ClientSecret;
    options.CallbackPath = "/signin-google";
    options.SaveTokens = true;
})
.AddMicrosoftAccount(options =>
{
    options.ClientId = oauthSettings.Microsoft.ClientId;
    options.ClientSecret = oauthSettings.Microsoft.ClientSecret;
    options.CallbackPath = "/signin-microsoft";
    options.SaveTokens = true;
});

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173",  // POS dev
                "http://localhost:5174",  // Back Office dev
                "http://localhost:5175"   // KDS dev
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// ============================================================================
// OAuth Authentication Endpoints
// ============================================================================

var oauthGroup = app.MapGroup("/api/oauth").WithTags("OAuth");

// GET /api/oauth/login/{provider} - Initiate OAuth login
oauthGroup.MapGet("/login/{provider}", (string provider, string? returnUrl) =>
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

// GET /api/oauth/callback - OAuth callback handler
oauthGroup.MapGet("/callback", async (
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

    // For demo purposes, use a fixed org ID. In production, this would:
    // 1. Look up or create user by email
    // 2. Determine their organization
    // 3. Create/update the user record
    var orgId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    var userId = Guid.NewGuid(); // In production: lookup by email or externalId

    var (accessToken, expires) = tokenService.GenerateAccessToken(
        userId,
        name ?? email,
        orgId,
        roles: ["admin", "backoffice"]
    );
    var refreshToken = tokenService.GenerateRefreshToken();

    // Sign out of the cookie scheme
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    // Redirect with tokens in fragment (for SPA to pick up)
    var redirectTarget = returnUrl ?? "http://localhost:5174";
    var separator = redirectTarget.Contains('#') ? "&" : "#";
    return Results.Redirect($"{redirectTarget}{separator}access_token={accessToken}&refresh_token={refreshToken}&expires_in={(int)(expires - DateTime.UtcNow).TotalSeconds}&user_id={userId}&display_name={Uri.EscapeDataString(name ?? email)}");
});

// GET /api/oauth/userinfo - Get current user info (requires auth)
oauthGroup.MapGet("/userinfo", (HttpContext context) =>
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

// ============================================================================
// Station API (for KDS)
// ============================================================================

var stationsGroup = app.MapGroup("/api/stations").WithTags("Stations");

// GET /api/stations/{orgId}/{siteId} - List stations for a site
stationsGroup.MapGet("/{orgId}/{siteId}", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
{
    // In production, this would query a StationRegistryGrain or database
    // For now, return mock data
    var stations = new[]
    {
        new { id = Guid.NewGuid(), name = "Grill Station", siteId, orderTypes = new[] { "hot", "grill" } },
        new { id = Guid.NewGuid(), name = "Cold Station", siteId, orderTypes = new[] { "cold", "salad" } },
        new { id = Guid.NewGuid(), name = "Expeditor", siteId, orderTypes = new[] { "all" } },
        new { id = Guid.NewGuid(), name = "Bar", siteId, orderTypes = new[] { "drinks", "bar" } },
    };
    return Results.Ok(new { items = stations });
});

// POST /api/stations/{orgId}/{siteId}/select - Select station for KDS device
stationsGroup.MapPost("/{orgId}/{siteId}/select", async (
    Guid orgId,
    Guid siteId,
    [FromBody] SelectStationRequest request,
    IGrainFactory grainFactory) =>
{
    // Update device with selected station
    var deviceGrain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(orgId, request.DeviceId));
    if (!await deviceGrain.ExistsAsync())
        return Results.NotFound(new { error = "device_not_found" });

    // In production, this would update the device's station assignment
    return Results.Ok(new
    {
        message = "Station selected",
        deviceId = request.DeviceId,
        stationId = request.StationId,
        stationName = request.StationName
    });
});

// ============================================================================
// Device Authorization API (OAuth 2.0 Device Flow - RFC 8628)
// ============================================================================

var deviceAuthGroup = app.MapGroup("/api/device").WithTags("DeviceAuth");

// POST /api/device/code - Request a device code for authorization
deviceAuthGroup.MapPost("/code", async (
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

// POST /api/device/token - Poll for token (device polls this after showing code)
deviceAuthGroup.MapPost("/token", async (
    [FromBody] DeviceTokenApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IDeviceAuthGrain>(request.UserCode.Replace("-", "").ToUpperInvariant());
    var status = await grain.GetStatusAsync();

    return status switch
    {
        DarkVelocity.Host.State.DeviceAuthStatus.Pending => Results.BadRequest(new { error = "authorization_pending", error_description = "The authorization request is still pending" }),
        DarkVelocity.Host.State.DeviceAuthStatus.Expired => Results.BadRequest(new { error = "expired_token", error_description = "The device code has expired" }),
        DarkVelocity.Host.State.DeviceAuthStatus.Denied => Results.BadRequest(new { error = "access_denied", error_description = "The authorization request was denied" }),
        DarkVelocity.Host.State.DeviceAuthStatus.Authorized => Results.Ok(await grain.GetTokenAsync(request.DeviceCode)),
        _ => Results.BadRequest(new { error = "invalid_request" })
    };
});

// POST /api/device/authorize - User authorizes the device (from browser, requires auth)
deviceAuthGroup.MapPost("/authorize", async (
    [FromBody] AuthorizeDeviceApiRequest request,
    IGrainFactory grainFactory) =>
{
    // Note: In production, this should use RequireAuthorization() and get user from claims
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

// POST /api/device/deny - User denies the device authorization
deviceAuthGroup.MapPost("/deny", async (
    [FromBody] DenyDeviceApiRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IDeviceAuthGrain>(request.UserCode.Replace("-", "").ToUpperInvariant());
    await grain.DenyAsync(request.Reason ?? "User denied authorization");
    return Results.Ok(new { message = "Device authorization denied" });
});

// ============================================================================
// PIN Authentication API (for authenticated devices)
// ============================================================================

var pinAuthGroup = app.MapGroup("/api/auth").WithTags("Auth");

// POST /api/auth/pin - PIN login on an authenticated device
pinAuthGroup.MapPost("/pin", async (
    [FromBody] PinLoginApiRequest request,
    IGrainFactory grainFactory) =>
{
    // Verify device is authorized
    var deviceGrain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(request.OrganizationId, request.DeviceId));
    if (!await deviceGrain.IsAuthorizedAsync())
        return Results.Unauthorized();

    // Hash the PIN for lookup
    var pinHash = HashPin(request.Pin);

    // Find user by PIN within organization
    var userLookupGrain = grainFactory.GetGrain<IUserLookupGrain>(GrainKeys.UserLookup(request.OrganizationId));
    var lookupResult = await userLookupGrain.FindByPinHashAsync(pinHash, request.SiteId);

    if (lookupResult == null)
        return Results.BadRequest(new { error = "invalid_pin", error_description = "Invalid PIN" });

    // Verify PIN directly on user grain
    var userGrain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(request.OrganizationId, lookupResult.UserId));
    var authResult = await userGrain.VerifyPinAsync(request.Pin);

    if (!authResult.Success)
        return Results.BadRequest(new { error = "invalid_pin", error_description = authResult.Error });

    // Create session
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

    // Update device current user
    await deviceGrain.SetCurrentUserAsync(lookupResult.UserId);

    // Record login
    await userGrain.RecordLoginAsync();

    return Results.Ok(new PinLoginResponse(
        tokens.AccessToken,
        tokens.RefreshToken,
        (int)(tokens.AccessTokenExpires - DateTime.UtcNow).TotalSeconds,
        lookupResult.UserId,
        lookupResult.DisplayName
    ));
});

// POST /api/auth/logout - Logout from device
pinAuthGroup.MapPost("/logout", async (
    [FromBody] LogoutApiRequest request,
    IGrainFactory grainFactory) =>
{
    // Revoke session
    var sessionGrain = grainFactory.GetGrain<ISessionGrain>(GrainKeys.Session(request.OrganizationId, request.SessionId));
    await sessionGrain.RevokeAsync();

    // Clear current user from device
    var deviceGrain = grainFactory.GetGrain<IDeviceGrain>(GrainKeys.Device(request.OrganizationId, request.DeviceId));
    await deviceGrain.SetCurrentUserAsync(null);

    return Results.Ok(new { message = "Logged out successfully" });
});

// POST /api/auth/refresh - Refresh access token
pinAuthGroup.MapPost("/refresh", async (
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

// ============================================================================
// Device Management API
// ============================================================================

var devicesGroup = app.MapGroup("/api/devices").WithTags("Devices");

// GET /api/devices/{orgId}/{deviceId} - Get device info
devicesGroup.MapGet("/{orgId}/{deviceId}", async (
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

// POST /api/devices/{orgId}/{deviceId}/heartbeat - Device heartbeat
devicesGroup.MapPost("/{orgId}/{deviceId}/heartbeat", async (
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

// POST /api/devices/{orgId}/{deviceId}/suspend - Suspend device
devicesGroup.MapPost("/{orgId}/{deviceId}/suspend", async (
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

// POST /api/devices/{orgId}/{deviceId}/revoke - Revoke device
devicesGroup.MapPost("/{orgId}/{deviceId}/revoke", async (
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

app.Run();

// ============================================================================
// Device Auth Request/Response DTOs (Active)
// ============================================================================

// Device code flow
public record DeviceCodeApiRequest(string ClientId, string? Scope, string? DeviceFingerprint);
public record DeviceTokenApiRequest(string UserCode, string DeviceCode);
public record AuthorizeDeviceApiRequest(
    string UserCode,
    Guid AuthorizedBy,
    Guid OrganizationId,
    Guid SiteId,
    string DeviceName,
    DarkVelocity.Host.State.DeviceType AppType);
public record DenyDeviceApiRequest(string UserCode, string? Reason);

// PIN authentication
public record PinLoginApiRequest(string Pin, Guid OrganizationId, Guid SiteId, Guid DeviceId);
public record PinLoginResponse(string AccessToken, string RefreshToken, int ExpiresIn, Guid UserId, string DisplayName);
public record LogoutApiRequest(Guid OrganizationId, Guid DeviceId, Guid SessionId);
public record RefreshTokenApiRequest(Guid OrganizationId, Guid SessionId, string RefreshToken);
public record RefreshTokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);

// Device management
public record DeviceHeartbeatRequest(string? AppVersion);
public record SuspendDeviceRequest(string Reason);
public record RevokeDeviceRequest(string Reason);

// Station selection (KDS)
public record SelectStationRequest(Guid DeviceId, Guid StationId, string StationName);

// Helper function for PIN hashing
static string HashPin(string pin)
{
    var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(pin));
    return Convert.ToBase64String(bytes);
}
