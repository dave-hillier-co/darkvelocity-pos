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

// ============================================================================
// Organizations API
// ============================================================================

var orgsGroup = app.MapGroup("/api/orgs").WithTags("Organizations");

// POST /api/orgs - Create organization
orgsGroup.MapPost("/", async (
    [FromBody] CreateOrgRequest request,
    IGrainFactory grainFactory) =>
{
    var orgId = Guid.NewGuid();
    var grain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
    var result = await grain.CreateAsync(new CreateOrganizationCommand(request.Name, request.Slug, request.Settings));

    return Results.Created($"/api/orgs/{orgId}", Hal.Resource(new
    {
        id = result.Id,
        slug = result.Slug,
        name = request.Name,
        createdAt = result.CreatedAt
    }, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}" },
        ["sites"] = new { href = $"/api/orgs/{orgId}/sites" }
    }));
});

// GET /api/orgs/{orgId} - Get organization
orgsGroup.MapGet("/{orgId}", async (Guid orgId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Organization not found"));

    var state = await grain.GetStateAsync();
    return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}" },
        ["sites"] = new { href = $"/api/orgs/{orgId}/sites" }
    }));
});

// PATCH /api/orgs/{orgId} - Update organization
orgsGroup.MapPatch("/{orgId}", async (
    Guid orgId,
    [FromBody] UpdateOrgRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Organization not found"));

    var result = await grain.UpdateAsync(new UpdateOrganizationCommand(request.Name, request.Settings));
    var state = await grain.GetStateAsync();

    return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}" }
    }));
});

// POST /api/orgs/{orgId}/suspend - Suspend organization
orgsGroup.MapPost("/{orgId}/suspend", async (
    Guid orgId,
    [FromBody] SuspendOrgRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Organization not found"));

    await grain.SuspendAsync(request.Reason);
    return Results.Ok(new { message = "Organization suspended" });
});

// ============================================================================
// Sites API
// ============================================================================

var sitesGroup = app.MapGroup("/api/orgs/{orgId}/sites").WithTags("Sites");

// POST /api/orgs/{orgId}/sites - Create site
sitesGroup.MapPost("/", async (
    Guid orgId,
    [FromBody] CreateSiteRequest request,
    IGrainFactory grainFactory) =>
{
    var siteId = Guid.NewGuid();
    var grain = grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));
    var result = await grain.CreateAsync(new CreateSiteCommand(
        orgId, request.Name, request.Code, request.Address, request.Timezone, request.Currency));

    // Register site with organization
    var orgGrain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
    await orgGrain.AddSiteAsync(siteId);

    return Results.Created($"/api/orgs/{orgId}/sites/{siteId}", Hal.Resource(new
    {
        id = result.Id,
        code = result.Code,
        name = request.Name,
        createdAt = result.CreatedAt
    }, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
        ["organization"] = new { href = $"/api/orgs/{orgId}" },
        ["orders"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders" },
        ["menu"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu" }
    }));
});

// GET /api/orgs/{orgId}/sites - List sites
sitesGroup.MapGet("/", async (Guid orgId, IGrainFactory grainFactory) =>
{
    var orgGrain = grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
    if (!await orgGrain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Organization not found"));

    var siteIds = await orgGrain.GetSiteIdsAsync();
    var sites = new List<object>();

    foreach (var siteId in siteIds)
    {
        var siteGrain = grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));
        if (await siteGrain.ExistsAsync())
        {
            var state = await siteGrain.GetStateAsync();
            sites.Add(Hal.Resource(state, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" }
            }));
        }
    }

    return Results.Ok(Hal.Collection("/api/orgs/{orgId}/sites", sites, sites.Count));
});

// GET /api/orgs/{orgId}/sites/{siteId} - Get site
sitesGroup.MapGet("/{siteId}", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Site not found"));

    var state = await grain.GetStateAsync();
    return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
        ["organization"] = new { href = $"/api/orgs/{orgId}" },
        ["orders"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders" },
        ["menu"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/menu" },
        ["customers"] = new { href = $"/api/orgs/{orgId}/customers" },
        ["inventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory" },
        ["bookings"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings" }
    }));
});

// PATCH /api/orgs/{orgId}/sites/{siteId} - Update site
sitesGroup.MapPatch("/{siteId}", async (
    Guid orgId,
    Guid siteId,
    [FromBody] UpdateSiteRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Site not found"));

    await grain.UpdateAsync(new UpdateSiteCommand(request.Name, request.Address, request.OperatingHours, request.Settings));
    var state = await grain.GetStateAsync();

    return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" }
    }));
});

// POST /api/orgs/{orgId}/sites/{siteId}/open - Open site
sitesGroup.MapPost("/{siteId}/open", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Site not found"));

    await grain.OpenAsync();
    return Results.Ok(new { message = "Site opened" });
});

// POST /api/orgs/{orgId}/sites/{siteId}/close - Close site
sitesGroup.MapPost("/{siteId}/close", async (Guid orgId, Guid siteId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Site not found"));

    await grain.CloseAsync();
    return Results.Ok(new { message = "Site closed" });
});

// ============================================================================
// Orders API
// ============================================================================

var ordersGroup = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/orders").WithTags("Orders");

// POST /api/orgs/{orgId}/sites/{siteId}/orders - Create order
ordersGroup.MapPost("/", async (
    Guid orgId,
    Guid siteId,
    [FromBody] CreateOrderRequest request,
    IGrainFactory grainFactory) =>
{
    var orderId = Guid.NewGuid();
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
    var result = await grain.CreateAsync(new CreateOrderCommand(
        orgId, siteId, request.CreatedBy, request.Type, request.TableId, request.TableNumber, request.CustomerId, request.GuestCount));

    return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}", Hal.Resource(new
    {
        id = result.Id,
        orderNumber = result.OrderNumber,
        createdAt = result.CreatedAt
    }, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" },
        ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
        ["lines"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines" }
    }));
});

// GET /api/orgs/{orgId}/sites/{siteId}/orders/{orderId} - Get order
ordersGroup.MapGet("/{orderId}", async (Guid orgId, Guid siteId, Guid orderId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Order not found"));

    var state = await grain.GetStateAsync();
    return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" },
        ["site"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}" },
        ["lines"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines" },
        ["send"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/send" },
        ["close"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/close" }
    }));
});

// POST /api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines - Add line to order
ordersGroup.MapPost("/{orderId}/lines", async (
    Guid orgId,
    Guid siteId,
    Guid orderId,
    [FromBody] AddLineRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Order not found"));

    var result = await grain.AddLineAsync(new AddLineCommand(
        request.MenuItemId, request.Name, request.Quantity, request.UnitPrice, request.Notes, request.Modifiers));

    return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines/{result.LineId}",
        Hal.Resource(result, new Dictionary<string, object>
        {
            ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines/{result.LineId}" },
            ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
        }));
});

// GET /api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines - Get order lines
ordersGroup.MapGet("/{orderId}/lines", async (Guid orgId, Guid siteId, Guid orderId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Order not found"));

    var lines = await grain.GetLinesAsync();
    var items = lines.Select(l => Hal.Resource(l, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines/{l.Id}" }
    })).ToList();

    return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines", items, items.Count));
});

// DELETE /api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/lines/{lineId} - Remove line
ordersGroup.MapDelete("/{orderId}/lines/{lineId}", async (
    Guid orgId, Guid siteId, Guid orderId, Guid lineId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Order not found"));

    await grain.RemoveLineAsync(lineId);
    return Results.NoContent();
});

// POST /api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/send - Send order to kitchen
ordersGroup.MapPost("/{orderId}/send", async (
    Guid orgId, Guid siteId, Guid orderId,
    [FromBody] SendOrderRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Order not found"));

    await grain.SendAsync(request.SentBy);
    var state = await grain.GetStateAsync();

    return Results.Ok(Hal.Resource(new { status = state.Status, sentAt = state.SentAt }, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
    }));
});

// POST /api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/close - Close order
ordersGroup.MapPost("/{orderId}/close", async (
    Guid orgId, Guid siteId, Guid orderId,
    [FromBody] CloseOrderRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Order not found"));

    await grain.CloseAsync(request.ClosedBy);
    return Results.Ok(new { message = "Order closed" });
});

// POST /api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/void - Void order
ordersGroup.MapPost("/{orderId}/void", async (
    Guid orgId, Guid siteId, Guid orderId,
    [FromBody] VoidOrderRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Order not found"));

    await grain.VoidAsync(new VoidOrderCommand(request.VoidedBy, request.Reason));
    return Results.Ok(new { message = "Order voided" });
});

// POST /api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/discounts - Apply discount
ordersGroup.MapPost("/{orderId}/discounts", async (
    Guid orgId, Guid siteId, Guid orderId,
    [FromBody] ApplyDiscountRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Order not found"));

    await grain.ApplyDiscountAsync(new ApplyDiscountCommand(
        request.Name, request.Type, request.Value, request.AppliedBy, request.DiscountId, request.Reason, request.ApprovedBy));
    var totals = await grain.GetTotalsAsync();

    return Results.Ok(Hal.Resource(totals, new Dictionary<string, object>
    {
        ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
    }));
});

// GET /api/orgs/{orgId}/sites/{siteId}/orders/{orderId}/totals - Get order totals
ordersGroup.MapGet("/{orderId}/totals", async (Guid orgId, Guid siteId, Guid orderId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, orderId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Order not found"));

    var totals = await grain.GetTotalsAsync();
    return Results.Ok(Hal.Resource(totals, new Dictionary<string, object>
    {
        ["order"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/orders/{orderId}" }
    }));
});

// ============================================================================
// Payments API
// ============================================================================

var paymentsGroup = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/payments").WithTags("Payments");

// POST /api/orgs/{orgId}/sites/{siteId}/payments - Initiate payment
paymentsGroup.MapPost("/", async (
    Guid orgId,
    Guid siteId,
    [FromBody] InitiatePaymentRequest request,
    IGrainFactory grainFactory) =>
{
    var paymentId = Guid.NewGuid();
    var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
    var result = await grain.InitiateAsync(new InitiatePaymentCommand(
        orgId, siteId, request.OrderId, request.Method, request.Amount, request.CashierId, request.CustomerId, request.DrawerId));

    return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}", Hal.Resource(new
    {
        id = result.Id,
        createdAt = result.CreatedAt
    }, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}" },
        ["complete-cash"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}/complete-cash" },
        ["complete-card"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}/complete-card" }
    }));
});

// GET /api/orgs/{orgId}/sites/{siteId}/payments/{paymentId} - Get payment
paymentsGroup.MapGet("/{paymentId}", async (Guid orgId, Guid siteId, Guid paymentId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Payment not found"));

    var state = await grain.GetStateAsync();
    return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}" }
    }));
});

// POST /api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}/complete-cash - Complete cash payment
paymentsGroup.MapPost("/{paymentId}/complete-cash", async (
    Guid orgId, Guid siteId, Guid paymentId,
    [FromBody] CompleteCashRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Payment not found"));

    var result = await grain.CompleteCashAsync(new CompleteCashPaymentCommand(request.AmountTendered, request.TipAmount));

    // Record payment on order
    var state = await grain.GetStateAsync();
    var orderGrain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, state.OrderId));
    await orderGrain.RecordPaymentAsync(paymentId, result.TotalAmount, request.TipAmount, "cash");

    return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
    {
        ["payment"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}" }
    }));
});

// POST /api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}/complete-card - Complete card payment
paymentsGroup.MapPost("/{paymentId}/complete-card", async (
    Guid orgId, Guid siteId, Guid paymentId,
    [FromBody] CompleteCardRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Payment not found"));

    var result = await grain.CompleteCardAsync(new ProcessCardPaymentCommand(
        request.GatewayReference, request.AuthorizationCode, request.CardInfo, request.GatewayName, request.TipAmount));

    // Record payment on order
    var state = await grain.GetStateAsync();
    var orderGrain = grainFactory.GetGrain<IOrderGrain>(GrainKeys.Order(orgId, siteId, state.OrderId));
    await orderGrain.RecordPaymentAsync(paymentId, result.TotalAmount, request.TipAmount, "card");

    return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
    {
        ["payment"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}" }
    }));
});

// POST /api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}/void - Void payment
paymentsGroup.MapPost("/{paymentId}/void", async (
    Guid orgId, Guid siteId, Guid paymentId,
    [FromBody] VoidPaymentRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Payment not found"));

    await grain.VoidAsync(new DarkVelocity.Host.Grains.VoidPaymentCommand(request.VoidedBy, request.Reason));
    return Results.Ok(new { message = "Payment voided" });
});

// POST /api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}/refund - Refund payment
paymentsGroup.MapPost("/{paymentId}/refund", async (
    Guid orgId, Guid siteId, Guid paymentId,
    [FromBody] RefundPaymentRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IPaymentGrain>(GrainKeys.Payment(orgId, siteId, paymentId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Payment not found"));

    var result = await grain.RefundAsync(new RefundPaymentCommand(request.Amount, request.Reason, request.IssuedBy));
    return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
    {
        ["payment"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/payments/{paymentId}" }
    }));
});

// ============================================================================
// Menu API
// ============================================================================

var menuGroup = app.MapGroup("/api/orgs/{orgId}/menu").WithTags("Menu");

// POST /api/orgs/{orgId}/menu/categories - Create category
menuGroup.MapPost("/categories", async (
    Guid orgId,
    [FromBody] CreateMenuCategoryRequest request,
    IGrainFactory grainFactory) =>
{
    var categoryId = Guid.NewGuid();
    var grain = grainFactory.GetGrain<IMenuCategoryGrain>(GrainKeys.MenuCategory(orgId, categoryId));
    var result = await grain.CreateAsync(new CreateMenuCategoryCommand(
        request.LocationId, request.Name, request.Description, request.DisplayOrder, request.Color));

    return Results.Created($"/api/orgs/{orgId}/menu/categories/{categoryId}", Hal.Resource(result, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/menu/categories/{categoryId}" },
        ["items"] = new { href = $"/api/orgs/{orgId}/menu/categories/{categoryId}/items" }
    }));
});

// GET /api/orgs/{orgId}/menu/categories/{categoryId} - Get category
menuGroup.MapGet("/categories/{categoryId}", async (Guid orgId, Guid categoryId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IMenuCategoryGrain>(GrainKeys.MenuCategory(orgId, categoryId));
    var snapshot = await grain.GetSnapshotAsync();

    return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/menu/categories/{categoryId}" },
        ["items"] = new { href = $"/api/orgs/{orgId}/menu/categories/{categoryId}/items" }
    }));
});

// POST /api/orgs/{orgId}/menu/items - Create menu item
menuGroup.MapPost("/items", async (
    Guid orgId,
    [FromBody] CreateMenuItemRequest request,
    IGrainFactory grainFactory) =>
{
    var itemId = Guid.NewGuid();
    var grain = grainFactory.GetGrain<IMenuItemGrain>(GrainKeys.MenuItem(orgId, itemId));
    var result = await grain.CreateAsync(new CreateMenuItemCommand(
        request.LocationId, request.CategoryId, request.AccountingGroupId, request.RecipeId,
        request.Name, request.Description, request.Price, request.ImageUrl, request.Sku, request.TrackInventory));

    // Increment category item count
    var categoryGrain = grainFactory.GetGrain<IMenuCategoryGrain>(GrainKeys.MenuCategory(orgId, request.CategoryId));
    await categoryGrain.IncrementItemCountAsync();

    return Results.Created($"/api/orgs/{orgId}/menu/items/{itemId}", Hal.Resource(result, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/menu/items/{itemId}" },
        ["category"] = new { href = $"/api/orgs/{orgId}/menu/categories/{request.CategoryId}" }
    }));
});

// GET /api/orgs/{orgId}/menu/items/{itemId} - Get menu item
menuGroup.MapGet("/items/{itemId}", async (Guid orgId, Guid itemId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IMenuItemGrain>(GrainKeys.MenuItem(orgId, itemId));
    var snapshot = await grain.GetSnapshotAsync();

    return Results.Ok(Hal.Resource(snapshot, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/menu/items/{itemId}" },
        ["category"] = new { href = $"/api/orgs/{orgId}/menu/categories/{snapshot.CategoryId}" }
    }));
});

// PATCH /api/orgs/{orgId}/menu/items/{itemId} - Update menu item
menuGroup.MapPatch("/items/{itemId}", async (
    Guid orgId,
    Guid itemId,
    [FromBody] UpdateMenuItemRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IMenuItemGrain>(GrainKeys.MenuItem(orgId, itemId));
    var result = await grain.UpdateAsync(new UpdateMenuItemCommand(
        request.CategoryId, request.AccountingGroupId, request.RecipeId, request.Name, request.Description,
        request.Price, request.ImageUrl, request.Sku, request.IsActive, request.TrackInventory));

    return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/menu/items/{itemId}" }
    }));
});

// ============================================================================
// Customers API
// ============================================================================

var customersGroup = app.MapGroup("/api/orgs/{orgId}/customers").WithTags("Customers");

// POST /api/orgs/{orgId}/customers - Create customer
customersGroup.MapPost("/", async (
    Guid orgId,
    [FromBody] CreateCustomerRequest request,
    IGrainFactory grainFactory) =>
{
    var customerId = Guid.NewGuid();
    var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
    var result = await grain.CreateAsync(new CreateCustomerCommand(
        orgId, request.FirstName, request.LastName, request.Email, request.Phone, request.Source));

    return Results.Created($"/api/orgs/{orgId}/customers/{customerId}", Hal.Resource(new
    {
        id = result.Id,
        displayName = result.DisplayName,
        createdAt = result.CreatedAt
    }, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}" },
        ["loyalty"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}/loyalty" }
    }));
});

// GET /api/orgs/{orgId}/customers/{customerId} - Get customer
customersGroup.MapGet("/{customerId}", async (Guid orgId, Guid customerId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Customer not found"));

    var state = await grain.GetStateAsync();
    return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}" },
        ["loyalty"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}/loyalty" },
        ["rewards"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}/rewards" }
    }));
});

// PATCH /api/orgs/{orgId}/customers/{customerId} - Update customer
customersGroup.MapPatch("/{customerId}", async (
    Guid orgId,
    Guid customerId,
    [FromBody] UpdateCustomerRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Customer not found"));

    await grain.UpdateAsync(new UpdateCustomerCommand(
        request.FirstName, request.LastName, request.Email, request.Phone, request.DateOfBirth, request.Preferences));
    var state = await grain.GetStateAsync();

    return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}" }
    }));
});

// POST /api/orgs/{orgId}/customers/{customerId}/loyalty/enroll - Enroll in loyalty
customersGroup.MapPost("/{customerId}/loyalty/enroll", async (
    Guid orgId,
    Guid customerId,
    [FromBody] EnrollLoyaltyRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Customer not found"));

    await grain.EnrollInLoyaltyAsync(new EnrollLoyaltyCommand(request.ProgramId, request.MemberNumber, request.InitialTierId, request.TierName));
    return Results.Ok(new { message = "Enrolled in loyalty program" });
});

// POST /api/orgs/{orgId}/customers/{customerId}/loyalty/earn - Earn points
customersGroup.MapPost("/{customerId}/loyalty/earn", async (
    Guid orgId,
    Guid customerId,
    [FromBody] EarnPointsRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Customer not found"));

    var result = await grain.EarnPointsAsync(new EarnPointsCommand(request.Points, request.Reason, request.OrderId, request.SiteId, request.SpendAmount));
    return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
    {
        ["customer"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}" }
    }));
});

// POST /api/orgs/{orgId}/customers/{customerId}/loyalty/redeem - Redeem points
customersGroup.MapPost("/{customerId}/loyalty/redeem", async (
    Guid orgId,
    Guid customerId,
    [FromBody] RedeemPointsRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Customer not found"));

    var result = await grain.RedeemPointsAsync(new RedeemPointsCommand(request.Points, request.OrderId, request.Reason));
    return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
    {
        ["customer"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}" }
    }));
});

// GET /api/orgs/{orgId}/customers/{customerId}/rewards - Get available rewards
customersGroup.MapGet("/{customerId}/rewards", async (Guid orgId, Guid customerId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<ICustomerGrain>(GrainKeys.Customer(orgId, customerId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Customer not found"));

    var rewards = await grain.GetAvailableRewardsAsync();
    var items = rewards.Select(r => Hal.Resource(r, new Dictionary<string, object>
    {
        ["redeem"] = new { href = $"/api/orgs/{orgId}/customers/{customerId}/rewards/{r.RewardId}/redeem" }
    })).ToList();

    return Results.Ok(Hal.Collection($"/api/orgs/{orgId}/customers/{customerId}/rewards", items, items.Count));
});

// ============================================================================
// Inventory API
// ============================================================================

var inventoryGroup = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/inventory").WithTags("Inventory");

// POST /api/orgs/{orgId}/sites/{siteId}/inventory - Initialize inventory item
inventoryGroup.MapPost("/", async (
    Guid orgId,
    Guid siteId,
    [FromBody] InitializeInventoryRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, request.IngredientId));
    await grain.InitializeAsync(new InitializeInventoryCommand(
        orgId, siteId, request.IngredientId, request.IngredientName, request.Sku, request.Unit, request.Category, request.ReorderPoint, request.ParLevel));

    return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/inventory/{request.IngredientId}", Hal.Resource(new
    {
        ingredientId = request.IngredientId,
        ingredientName = request.IngredientName
    }, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{request.IngredientId}" },
        ["receive"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{request.IngredientId}/receive" }
    }));
});

// GET /api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId} - Get inventory
inventoryGroup.MapGet("/{ingredientId}", async (Guid orgId, Guid siteId, Guid ingredientId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

    var state = await grain.GetStateAsync();
    return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}" },
        ["receive"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/receive" },
        ["consume"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/consume" },
        ["adjust"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/adjust" }
    }));
});

// POST /api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/receive - Receive batch
inventoryGroup.MapPost("/{ingredientId}/receive", async (
    Guid orgId, Guid siteId, Guid ingredientId,
    [FromBody] ReceiveBatchRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

    var result = await grain.ReceiveBatchAsync(new ReceiveBatchCommand(
        request.BatchNumber, request.Quantity, request.UnitCost, request.ExpiryDate, request.SupplierId, request.DeliveryId, request.Location, request.Notes, request.ReceivedBy));

    return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
    {
        ["inventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}" }
    }));
});

// POST /api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/consume - Consume stock
inventoryGroup.MapPost("/{ingredientId}/consume", async (
    Guid orgId, Guid siteId, Guid ingredientId,
    [FromBody] ConsumeStockRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

    var result = await grain.ConsumeAsync(new ConsumeStockCommand(request.Quantity, request.Reason, request.OrderId, request.PerformedBy));
    return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
    {
        ["inventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}" }
    }));
});

// POST /api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/adjust - Adjust quantity
inventoryGroup.MapPost("/{ingredientId}/adjust", async (
    Guid orgId, Guid siteId, Guid ingredientId,
    [FromBody] AdjustInventoryRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

    await grain.AdjustQuantityAsync(new AdjustQuantityCommand(request.NewQuantity, request.Reason, request.AdjustedBy, request.ApprovedBy));
    var level = await grain.GetLevelInfoAsync();

    return Results.Ok(Hal.Resource(level, new Dictionary<string, object>
    {
        ["inventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}" }
    }));
});

// GET /api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}/level - Get inventory level
inventoryGroup.MapGet("/{ingredientId}/level", async (Guid orgId, Guid siteId, Guid ingredientId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IInventoryGrain>(GrainKeys.Inventory(orgId, siteId, ingredientId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Inventory item not found"));

    var level = await grain.GetLevelInfoAsync();
    return Results.Ok(Hal.Resource(level, new Dictionary<string, object>
    {
        ["inventory"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/inventory/{ingredientId}" }
    }));
});

// ============================================================================
// Bookings API
// ============================================================================

var bookingsGroup = app.MapGroup("/api/orgs/{orgId}/sites/{siteId}/bookings").WithTags("Bookings");

// POST /api/orgs/{orgId}/sites/{siteId}/bookings - Request booking
bookingsGroup.MapPost("/", async (
    Guid orgId,
    Guid siteId,
    [FromBody] RequestBookingRequest request,
    IGrainFactory grainFactory) =>
{
    var bookingId = Guid.NewGuid();
    var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
    var result = await grain.RequestAsync(new RequestBookingCommand(
        orgId, siteId, request.Guest, request.RequestedTime, request.PartySize, request.Duration, request.SpecialRequests, request.Occasion, request.Source, request.ExternalRef, request.CustomerId));

    return Results.Created($"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}", Hal.Resource(new
    {
        id = result.Id,
        confirmationCode = result.ConfirmationCode,
        createdAt = result.CreatedAt
    }, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}" },
        ["confirm"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/confirm" },
        ["cancel"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/cancel" }
    }));
});

// GET /api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId} - Get booking
bookingsGroup.MapGet("/{bookingId}", async (Guid orgId, Guid siteId, Guid bookingId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Booking not found"));

    var state = await grain.GetStateAsync();
    return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}" },
        ["confirm"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/confirm" },
        ["cancel"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/cancel" },
        ["checkin"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/checkin" }
    }));
});

// POST /api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/confirm - Confirm booking
bookingsGroup.MapPost("/{bookingId}/confirm", async (
    Guid orgId, Guid siteId, Guid bookingId,
    [FromBody] ConfirmBookingRequest? request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Booking not found"));

    var result = await grain.ConfirmAsync(request?.ConfirmedTime);
    return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
    {
        ["booking"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}" }
    }));
});

// POST /api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/cancel - Cancel booking
bookingsGroup.MapPost("/{bookingId}/cancel", async (
    Guid orgId, Guid siteId, Guid bookingId,
    [FromBody] CancelBookingRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Booking not found"));

    await grain.CancelAsync(new CancelBookingCommand(request.Reason, request.CancelledBy));
    return Results.Ok(new { message = "Booking cancelled" });
});

// POST /api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/checkin - Check in guest
bookingsGroup.MapPost("/{bookingId}/checkin", async (
    Guid orgId, Guid siteId, Guid bookingId,
    [FromBody] CheckInRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Booking not found"));

    var arrivedAt = await grain.RecordArrivalAsync(new RecordArrivalCommand(request.CheckedInBy));
    return Results.Ok(Hal.Resource(new { arrivedAt }, new Dictionary<string, object>
    {
        ["booking"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}" },
        ["seat"] = new { href = $"/api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/seat" }
    }));
});

// POST /api/orgs/{orgId}/sites/{siteId}/bookings/{bookingId}/seat - Seat guest
bookingsGroup.MapPost("/{bookingId}/seat", async (
    Guid orgId, Guid siteId, Guid bookingId,
    [FromBody] SeatGuestRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IBookingGrain>(GrainKeys.Booking(orgId, siteId, bookingId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Booking not found"));

    await grain.SeatGuestAsync(new SeatGuestCommand(request.TableId, request.TableNumber, request.SeatedBy));
    return Results.Ok(new { message = "Guest seated" });
});

// ============================================================================
// Employees API
// ============================================================================

var employeesGroup = app.MapGroup("/api/orgs/{orgId}/employees").WithTags("Employees");

// POST /api/orgs/{orgId}/employees - Create employee
employeesGroup.MapPost("/", async (
    Guid orgId,
    [FromBody] CreateEmployeeRequest request,
    IGrainFactory grainFactory) =>
{
    var employeeId = Guid.NewGuid();
    var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
    var result = await grain.CreateAsync(new CreateEmployeeCommand(
        orgId, request.UserId, request.DefaultSiteId, request.EmployeeNumber, request.FirstName, request.LastName, request.Email, request.EmploymentType, request.HireDate));

    return Results.Created($"/api/orgs/{orgId}/employees/{employeeId}", Hal.Resource(new
    {
        id = result.Id,
        employeeNumber = result.EmployeeNumber,
        createdAt = result.CreatedAt
    }, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}" },
        ["clock-in"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}/clock-in" }
    }));
});

// GET /api/orgs/{orgId}/employees/{employeeId} - Get employee
employeesGroup.MapGet("/{employeeId}", async (Guid orgId, Guid employeeId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Employee not found"));

    var state = await grain.GetStateAsync();
    return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}" },
        ["clock-in"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}/clock-in" },
        ["clock-out"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}/clock-out" }
    }));
});

// PATCH /api/orgs/{orgId}/employees/{employeeId} - Update employee
employeesGroup.MapPatch("/{employeeId}", async (
    Guid orgId,
    Guid employeeId,
    [FromBody] UpdateEmployeeRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Employee not found"));

    await grain.UpdateAsync(new UpdateEmployeeCommand(
        request.FirstName, request.LastName, request.Email, request.HourlyRate, request.SalaryAmount, request.PayFrequency));
    var state = await grain.GetStateAsync();

    return Results.Ok(Hal.Resource(state, new Dictionary<string, object>
    {
        ["self"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}" }
    }));
});

// POST /api/orgs/{orgId}/employees/{employeeId}/clock-in - Clock in
employeesGroup.MapPost("/{employeeId}/clock-in", async (
    Guid orgId,
    Guid employeeId,
    [FromBody] ClockInRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Employee not found"));

    var result = await grain.ClockInAsync(new ClockInCommand(request.SiteId, request.ShiftId));
    return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
    {
        ["employee"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}" },
        ["clock-out"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}/clock-out" }
    }));
});

// POST /api/orgs/{orgId}/employees/{employeeId}/clock-out - Clock out
employeesGroup.MapPost("/{employeeId}/clock-out", async (
    Guid orgId,
    Guid employeeId,
    [FromBody] ClockOutRequest? request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Employee not found"));

    var result = await grain.ClockOutAsync(new ClockOutCommand(request?.Notes));
    return Results.Ok(Hal.Resource(result, new Dictionary<string, object>
    {
        ["employee"] = new { href = $"/api/orgs/{orgId}/employees/{employeeId}" }
    }));
});

// POST /api/orgs/{orgId}/employees/{employeeId}/roles - Assign role
employeesGroup.MapPost("/{employeeId}/roles", async (
    Guid orgId,
    Guid employeeId,
    [FromBody] AssignRoleRequest request,
    IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Employee not found"));

    await grain.AssignRoleAsync(new AssignRoleCommand(request.RoleId, request.RoleName, request.Department, request.IsPrimary, request.HourlyRateOverride));
    return Results.Ok(new { message = "Role assigned" });
});

// DELETE /api/orgs/{orgId}/employees/{employeeId}/roles/{roleId} - Remove role
employeesGroup.MapDelete("/{employeeId}/roles/{roleId}", async (Guid orgId, Guid employeeId, Guid roleId, IGrainFactory grainFactory) =>
{
    var grain = grainFactory.GetGrain<IEmployeeGrain>(GrainKeys.Employee(orgId, employeeId));
    if (!await grain.ExistsAsync())
        return Results.NotFound(Hal.Error("not_found", "Employee not found"));

    await grain.RemoveRoleAsync(roleId);
    return Results.NoContent();
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

// ============================================================================
// HAL+JSON Helper
// ============================================================================

public static class Hal
{
    public static object Resource(object data, Dictionary<string, object> links)
    {
        var result = new Dictionary<string, object> { ["_links"] = links };
        foreach (var prop in data.GetType().GetProperties())
        {
            var name = char.ToLowerInvariant(prop.Name[0]) + prop.Name[1..];
            result[name] = prop.GetValue(data)!;
        }
        return result;
    }

    public static object Collection(string selfHref, IEnumerable<object> items, int count)
    {
        return new Dictionary<string, object>
        {
            ["_links"] = new { self = new { href = selfHref } },
            ["_embedded"] = new { items },
            ["count"] = count
        };
    }

    public static object Error(string code, string message) =>
        new { error = code, error_description = message };
}

// ============================================================================
// Organization & Site Request DTOs
// ============================================================================

public record CreateOrgRequest(string Name, string Slug, DarkVelocity.Host.State.OrganizationSettings? Settings = null);
public record UpdateOrgRequest(string? Name = null, DarkVelocity.Host.State.OrganizationSettings? Settings = null);
public record SuspendOrgRequest(string Reason);

public record CreateSiteRequest(
    string Name,
    string Code,
    DarkVelocity.Host.State.Address Address,
    string Timezone = "America/New_York",
    string Currency = "USD");
public record UpdateSiteRequest(
    string? Name = null,
    DarkVelocity.Host.State.Address? Address = null,
    DarkVelocity.Host.State.OperatingHours? OperatingHours = null,
    DarkVelocity.Host.State.SiteSettings? Settings = null);

// ============================================================================
// Order Request DTOs
// ============================================================================

public record CreateOrderRequest(
    Guid CreatedBy,
    DarkVelocity.Host.State.OrderType Type,
    Guid? TableId = null,
    string? TableNumber = null,
    Guid? CustomerId = null,
    int GuestCount = 1);

public record AddLineRequest(
    Guid MenuItemId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    string? Notes = null,
    List<DarkVelocity.Host.State.OrderLineModifier>? Modifiers = null);

public record SendOrderRequest(Guid SentBy);
public record CloseOrderRequest(Guid ClosedBy);
public record VoidOrderRequest(Guid VoidedBy, string Reason);

public record ApplyDiscountRequest(
    string Name,
    DarkVelocity.Host.State.DiscountType Type,
    decimal Value,
    Guid AppliedBy,
    Guid? DiscountId = null,
    string? Reason = null,
    Guid? ApprovedBy = null);

// ============================================================================
// Payment Request DTOs
// ============================================================================

public record InitiatePaymentRequest(
    Guid OrderId,
    DarkVelocity.Host.State.PaymentMethod Method,
    decimal Amount,
    Guid CashierId,
    Guid? CustomerId = null,
    Guid? DrawerId = null);

public record CompleteCashRequest(decimal AmountTendered, decimal TipAmount = 0);

public record CompleteCardRequest(
    string GatewayReference,
    string AuthorizationCode,
    DarkVelocity.Host.State.CardInfo CardInfo,
    string GatewayName,
    decimal TipAmount = 0);

public record VoidPaymentRequest(Guid VoidedBy, string Reason);
public record RefundPaymentRequest(decimal Amount, string Reason, Guid IssuedBy);

// ============================================================================
// Menu Request DTOs
// ============================================================================

public record CreateMenuCategoryRequest(
    Guid LocationId,
    string Name,
    string? Description,
    int DisplayOrder,
    string? Color);

public record CreateMenuItemRequest(
    Guid LocationId,
    Guid CategoryId,
    string Name,
    decimal Price,
    Guid? AccountingGroupId = null,
    Guid? RecipeId = null,
    string? Description = null,
    string? ImageUrl = null,
    string? Sku = null,
    bool TrackInventory = false);

public record UpdateMenuItemRequest(
    Guid? CategoryId = null,
    Guid? AccountingGroupId = null,
    Guid? RecipeId = null,
    string? Name = null,
    string? Description = null,
    decimal? Price = null,
    string? ImageUrl = null,
    string? Sku = null,
    bool? IsActive = null,
    bool? TrackInventory = null);

// ============================================================================
// Customer Request DTOs
// ============================================================================

public record CreateCustomerRequest(
    string FirstName,
    string LastName,
    string? Email = null,
    string? Phone = null,
    DarkVelocity.Host.State.CustomerSource Source = DarkVelocity.Host.State.CustomerSource.Direct);

public record UpdateCustomerRequest(
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    string? Phone = null,
    DateOnly? DateOfBirth = null,
    DarkVelocity.Host.State.CustomerPreferences? Preferences = null);

public record EnrollLoyaltyRequest(Guid ProgramId, string MemberNumber, Guid InitialTierId, string TierName);
public record EarnPointsRequest(int Points, string Reason, Guid? OrderId = null, Guid? SiteId = null, decimal? SpendAmount = null);
public record RedeemPointsRequest(int Points, Guid OrderId, string Reason);

// ============================================================================
// Inventory Request DTOs
// ============================================================================

public record InitializeInventoryRequest(
    Guid IngredientId,
    string IngredientName,
    string Sku,
    string Unit,
    string Category,
    decimal ReorderPoint = 0,
    decimal ParLevel = 0);

public record ReceiveBatchRequest(
    string BatchNumber,
    decimal Quantity,
    decimal UnitCost,
    DateTime? ExpiryDate = null,
    Guid? SupplierId = null,
    Guid? DeliveryId = null,
    string? Location = null,
    string? Notes = null,
    Guid? ReceivedBy = null);

public record ConsumeStockRequest(decimal Quantity, string Reason, Guid? OrderId = null, Guid? PerformedBy = null);
public record AdjustInventoryRequest(decimal NewQuantity, string Reason, Guid AdjustedBy, Guid? ApprovedBy = null);

// ============================================================================
// Booking Request DTOs
// ============================================================================

public record RequestBookingRequest(
    DarkVelocity.Host.State.GuestInfo Guest,
    DateTime RequestedTime,
    int PartySize,
    TimeSpan? Duration = null,
    string? SpecialRequests = null,
    string? Occasion = null,
    DarkVelocity.Host.State.BookingSource Source = DarkVelocity.Host.State.BookingSource.Direct,
    string? ExternalRef = null,
    Guid? CustomerId = null);

public record ConfirmBookingRequest(DateTime? ConfirmedTime = null);
public record CancelBookingRequest(string Reason, Guid CancelledBy);
public record CheckInRequest(Guid CheckedInBy);
public record SeatGuestRequest(Guid TableId, string TableNumber, Guid SeatedBy);

// ============================================================================
// Employee Request DTOs
// ============================================================================

public record CreateEmployeeRequest(
    Guid UserId,
    Guid DefaultSiteId,
    string EmployeeNumber,
    string FirstName,
    string LastName,
    string Email,
    DarkVelocity.Host.State.EmploymentType EmploymentType = DarkVelocity.Host.State.EmploymentType.FullTime,
    DateOnly? HireDate = null);

public record UpdateEmployeeRequest(
    string? FirstName = null,
    string? LastName = null,
    string? Email = null,
    decimal? HourlyRate = null,
    decimal? SalaryAmount = null,
    string? PayFrequency = null);

public record ClockInRequest(Guid SiteId, Guid? ShiftId = null);
public record ClockOutRequest(string? Notes = null);
public record AssignRoleRequest(Guid RoleId, string RoleName, string Department, bool IsPrimary = false, decimal? HourlyRateOverride = null);

// Expose Program class for WebApplicationFactory integration testing
public partial class Program { }
