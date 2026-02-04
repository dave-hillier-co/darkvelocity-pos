using DarkVelocity.Host.Auth;
using DarkVelocity.Host.Authorization;
using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using Orleans.Streams;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace DarkVelocity.Host.Endpoints;

/// <summary>
/// Pending OAuth state for multi-org login.
/// </summary>
public sealed record PendingOAuthState(
    string Email,
    string? Name,
    string Provider,
    string ExternalId,
    List<EmailUserMapping> Organizations,
    DateTime ExpiresAt);

/// <summary>
/// Pending PIN auth state for OAuth-style PIN login.
/// </summary>
public sealed record PendingPinAuthState(
    Guid OrganizationId,
    Guid SiteId,
    string ClientId,
    string RedirectUri,
    string? CodeChallenge,
    string? CodeChallengeMethod,
    string? Scope,
    string? Nonce,
    string? ClientState,
    DateTime ExpiresAt);

/// <summary>
/// Response for PIN auth user selection.
/// </summary>
public record PinAuthUsersResponse(
    string PendingToken,
    Guid OrganizationId,
    Guid SiteId,
    List<PinUserOption> Users);

/// <summary>
/// User option for PIN selection.
/// </summary>
public record PinUserOption(
    Guid UserId,
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? AvatarUrl);

/// <summary>
/// Request to authenticate with PIN.
/// </summary>
public record PinAuthenticateRequest(
    string PendingToken,
    Guid UserId,
    string Pin);

public static class OAuthEndpoints
{
    private const string PendingOAuthCachePrefix = "pending_oauth_";
    private const string PendingPinAuthCachePrefix = "pending_pin_auth_";
    private static readonly TimeSpan PendingOAuthExpiry = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PendingPinAuthExpiry = TimeSpan.FromMinutes(10);

    public static WebApplication MapOAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/oauth").WithTags("OAuth");

        // ========================================================================
        // OAuth 2.0 Authorization Endpoint
        // ========================================================================

        group.MapGet("/authorize", async (
            HttpContext context,
            IGrainFactory grainFactory,
            string response_type,
            string client_id,
            string redirect_uri,
            string? scope = null,
            string? state = null,
            string? nonce = null,
            string? code_challenge = null,
            string? code_challenge_method = null,
            string? provider = null) =>
        {
            // Validate response type
            if (response_type != "code" && response_type != "token")
            {
                return Results.BadRequest(new OAuthError("unsupported_response_type",
                    "Supported response types: code, token"));
            }

            // Validate client (in production, lookup from database)
            if (!IsValidClient(client_id, redirect_uri))
            {
                return Results.BadRequest(new OAuthError("invalid_client",
                    "Unknown client_id or invalid redirect_uri"));
            }

            // Generate and store state for CSRF protection
            var internalState = GrainKeys.GenerateOAuthState();
            var stateGrain = grainFactory.GetGrain<IOAuthStateGrain>(GrainKeys.OAuthState(internalState));
            await stateGrain.InitializeAsync(new OAuthStateRequest(
                provider ?? "google",
                redirect_uri,
                code_challenge,
                code_challenge_method,
                client_id,
                nonce,
                scope));

            // Combine internal state with client state for callback
            var combinedState = string.IsNullOrEmpty(state)
                ? internalState
                : $"{internalState}|{state}";

            var selectedProvider = (provider?.ToLowerInvariant() ?? "google") switch
            {
                "google" => "Google",
                "microsoft" => "Microsoft",
                _ => "Google"
            };

            var properties = new AuthenticationProperties
            {
                RedirectUri = $"/api/oauth/callback?state={Uri.EscapeDataString(combinedState)}&response_type={response_type}",
                Items = { ["LoginProvider"] = selectedProvider }
            };

            return Results.Challenge(properties, [selectedProvider]);
        }).WithName("OAuthAuthorize")
          .WithDescription("OAuth 2.0 Authorization Endpoint - initiates authorization flow");

        // ========================================================================
        // Legacy Login Endpoint (simplified authorization)
        // ========================================================================

        group.MapGet("/login/{provider}", async (
            HttpContext context,
            IGrainFactory grainFactory,
            string provider,
            string? returnUrl = null,
            string? code_challenge = null,
            string? code_challenge_method = null) =>
        {
            var redirectUri = returnUrl ?? "/";

            // Generate state for CSRF protection
            var state = GrainKeys.GenerateOAuthState();
            var stateGrain = grainFactory.GetGrain<IOAuthStateGrain>(GrainKeys.OAuthState(state));
            await stateGrain.InitializeAsync(new OAuthStateRequest(
                provider,
                redirectUri,
                code_challenge,
                code_challenge_method));

            var properties = new AuthenticationProperties
            {
                RedirectUri = $"/api/oauth/callback?state={Uri.EscapeDataString(state)}&response_type=token"
            };

            return provider.ToLowerInvariant() switch
            {
                "google" => Results.Challenge(properties, ["Google"]),
                "microsoft" => Results.Challenge(properties, ["Microsoft"]),
                _ => Results.BadRequest(new OAuthError("invalid_provider",
                    "Supported providers: google, microsoft"))
            };
        }).WithName("OAuthLogin")
          .WithDescription("Simplified OAuth login - redirects to provider");

        // ========================================================================
        // OAuth Callback Handler - Uses Real User Management
        // ========================================================================

        group.MapGet("/callback", async (
            HttpContext context,
            IGrainFactory grainFactory,
            JwtTokenService tokenService,
            IMemoryCache cache,
            string? state = null,
            string? response_type = null,
            string? error = null,
            string? error_description = null) =>
        {
            // Handle OAuth provider errors
            if (!string.IsNullOrEmpty(error))
            {
                var errorRedirect = "/?error=" + Uri.EscapeDataString(error);
                if (!string.IsNullOrEmpty(error_description))
                    errorRedirect += "&error_description=" + Uri.EscapeDataString(error_description);
                return Results.Redirect(errorRedirect);
            }

            // Validate state parameter
            if (string.IsNullOrEmpty(state))
            {
                return Results.Redirect("/?error=missing_state");
            }

            // Parse combined state (internal|client)
            var stateParts = state.Split('|', 2);
            var internalState = stateParts[0];
            var clientState = stateParts.Length > 1 ? stateParts[1] : null;

            // Validate and consume state
            var stateGrain = grainFactory.GetGrain<IOAuthStateGrain>(GrainKeys.OAuthState(internalState));
            var validation = await stateGrain.ValidateAndConsumeAsync();

            if (!validation.IsValid)
            {
                return Results.Redirect($"/?error={validation.Error}");
            }

            // Authenticate with cookie
            var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded || result.Principal == null)
            {
                return Results.Redirect($"{validation.ReturnUrl}?error=auth_failed");
            }

            // Extract claims from OAuth provider
            var claims = result.Principal.Claims.ToList();
            var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var externalId = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var pictureUrl = claims.FirstOrDefault(c => c.Type == "picture" || c.Type == "urn:google:picture")?.Value;

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(externalId))
            {
                return Results.Redirect($"{validation.ReturnUrl}?error=missing_claims");
            }

            // Sign out of cookie auth
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var provider = validation.Provider ?? "google";
            var redirectTarget = validation.ReturnUrl ?? "http://localhost:5174";

            // Look up user by email across all organizations
            var emailLookup = grainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
            var emailMappings = await emailLookup.FindByEmailAsync(email);

            if (emailMappings.Count == 0)
            {
                // No user found with this email - not invited
                return Results.Redirect($"{redirectTarget}?error=not_invited&email={Uri.EscapeDataString(email)}");
            }

            if (emailMappings.Count == 1)
            {
                // Single organization - complete login directly
                var mapping = emailMappings[0];
                return await CompleteOAuthLoginAsync(
                    grainFactory, tokenService, cache,
                    mapping.OrganizationId, mapping.UserId,
                    provider, externalId, email, name,
                    response_type, validation, clientState, redirectTarget);
            }

            // Multiple organizations - store pending state and redirect to org selector
            var pendingToken = Guid.NewGuid().ToString("N");
            var pendingState = new PendingOAuthState(
                email, name, provider, externalId, emailMappings.ToList(),
                DateTime.UtcNow.Add(PendingOAuthExpiry));

            cache.Set(
                PendingOAuthCachePrefix + pendingToken,
                pendingState,
                PendingOAuthExpiry);

            return Results.Redirect($"{redirectTarget}/select-org?pending_token={pendingToken}");
        }).WithName("OAuthCallback")
          .WithDescription("OAuth callback handler - completes authorization flow");

        // ========================================================================
        // Pending OAuth State Endpoint (for multi-org flow)
        // ========================================================================

        group.MapGet("/pending", (
            [FromQuery] string pendingToken,
            IGrainFactory grainFactory,
            IMemoryCache cache) =>
        {
            if (string.IsNullOrEmpty(pendingToken))
            {
                return Results.BadRequest(Hal.Error("missing_token", "Pending token is required"));
            }

            if (!cache.TryGetValue<PendingOAuthState>(PendingOAuthCachePrefix + pendingToken, out var pendingState) ||
                pendingState == null)
            {
                return Results.NotFound(Hal.Error("expired_or_invalid", "Pending OAuth session not found or expired"));
            }

            if (pendingState.ExpiresAt < DateTime.UtcNow)
            {
                cache.Remove(PendingOAuthCachePrefix + pendingToken);
                return Results.NotFound(Hal.Error("expired", "Pending OAuth session expired"));
            }

            var orgOptions = pendingState.Organizations
                .Select(m => new OrganizationOption(m.OrganizationId, m.OrganizationId.ToString()))
                .ToList();

            return Results.Ok(new PendingOAuthResponse(
                pendingToken,
                pendingState.Email,
                pendingState.Name,
                orgOptions));
        }).WithName("OAuthPending")
          .WithDescription("Get pending OAuth state for organization selection");

        // ========================================================================
        // Select Organization Endpoint (for multi-org flow)
        // ========================================================================

        group.MapPost("/select-org", async (
            [FromBody] SelectOrganizationRequest request,
            IGrainFactory grainFactory,
            JwtTokenService tokenService,
            IMemoryCache cache) =>
        {
            if (string.IsNullOrEmpty(request.PendingToken))
            {
                return Results.BadRequest(Hal.Error("missing_token", "Pending token is required"));
            }

            if (!cache.TryGetValue<PendingOAuthState>(PendingOAuthCachePrefix + request.PendingToken, out var pendingState) ||
                pendingState == null)
            {
                return Results.NotFound(Hal.Error("expired_or_invalid", "Pending OAuth session not found or expired"));
            }

            var selectedMapping = pendingState.Organizations
                .FirstOrDefault(m => m.OrganizationId == request.OrganizationId);

            if (selectedMapping == null)
            {
                return Results.BadRequest(Hal.Error("invalid_org", "Selected organization is not valid for this login"));
            }

            cache.Remove(PendingOAuthCachePrefix + request.PendingToken);

            return await CompleteOAuthLoginAsync(
                grainFactory, tokenService, cache,
                selectedMapping.OrganizationId, selectedMapping.UserId,
                pendingState.Provider, pendingState.ExternalId,
                pendingState.Email, pendingState.Name,
                "token", null, null, null);
        }).WithName("OAuthSelectOrg")
          .WithDescription("Complete OAuth login after organization selection");

        // ========================================================================
        // Token Endpoint (RFC 6749)
        // ========================================================================

        group.MapPost("/token", async (
            HttpContext context,
            IGrainFactory grainFactory,
            JwtTokenService tokenService) =>
        {
            var form = await context.Request.ReadFormAsync();
            var grantType = form["grant_type"].FirstOrDefault();
            var clientId = form["client_id"].FirstOrDefault();
            var clientSecret = form["client_secret"].FirstOrDefault();

            // Check Authorization header for client credentials
            if (string.IsNullOrEmpty(clientId))
            {
                var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                if (authHeader?.StartsWith("Basic ") == true)
                {
                    var encoded = authHeader["Basic ".Length..];
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    var parts = decoded.Split(':', 2);
                    clientId = parts[0];
                    clientSecret = parts.Length > 1 ? parts[1] : null;
                }
            }

            if (string.IsNullOrEmpty(clientId))
            {
                return Results.Json(new OAuthError("invalid_client", "client_id is required"),
                    statusCode: 401);
            }

            return grantType switch
            {
                "authorization_code" => await HandleAuthorizationCodeGrant(
                    form, clientId, grainFactory, tokenService),
                "refresh_token" => await HandleRefreshTokenGrant(
                    form, clientId, grainFactory, tokenService),
                "client_credentials" => HandleClientCredentialsGrant(
                    clientId, clientSecret, tokenService),
                _ => Results.Json(new OAuthError("unsupported_grant_type",
                    "Supported grant types: authorization_code, refresh_token, client_credentials"),
                    statusCode: 400)
            };
        }).WithName("OAuthToken")
          .WithDescription("OAuth 2.0 Token Endpoint - exchanges codes/refresh tokens for access tokens");

        // ========================================================================
        // Token Revocation Endpoint (RFC 7009)
        // ========================================================================

        group.MapPost("/revoke", async (
            HttpContext context,
            IGrainFactory grainFactory,
            JwtTokenService tokenService) =>
        {
            var form = await context.Request.ReadFormAsync();
            var token = form["token"].FirstOrDefault();

            if (string.IsNullOrEmpty(token))
            {
                return Results.Json(new OAuthError("invalid_request", "token is required"),
                    statusCode: 400);
            }

            var principal = tokenService.ValidateToken(token);
            if (principal != null)
            {
                var sessionId = principal.FindFirst("session_id")?.Value;
                var orgId = principal.FindFirst("org_id")?.Value;

                if (!string.IsNullOrEmpty(sessionId) && !string.IsNullOrEmpty(orgId))
                {
                    var sessionGrain = grainFactory.GetGrain<ISessionGrain>(
                        GrainKeys.Session(Guid.Parse(orgId), Guid.Parse(sessionId)));
                    await sessionGrain.RevokeAsync();
                }
            }

            return Results.Ok();
        }).WithName("OAuthRevoke")
          .WithDescription("OAuth 2.0 Token Revocation Endpoint");

        // ========================================================================
        // Token Introspection Endpoint (RFC 7662)
        // ========================================================================

        group.MapPost("/introspect", async (
            HttpContext context,
            JwtTokenService tokenService) =>
        {
            var form = await context.Request.ReadFormAsync();
            var token = form["token"].FirstOrDefault();

            if (string.IsNullOrEmpty(token))
            {
                return Results.Json(new { active = false });
            }

            var principal = tokenService.ValidateToken(token);
            if (principal == null)
            {
                return Results.Json(new { active = false });
            }

            var claims = principal.Claims.ToList();
            return Results.Json(new
            {
                active = true,
                sub = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == JwtRegisteredClaimNames.Sub)?.Value,
                client_id = claims.FirstOrDefault(c => c.Type == "client_id")?.Value,
                username = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == JwtRegisteredClaimNames.Name)?.Value,
                scope = claims.FirstOrDefault(c => c.Type == "scope")?.Value,
                exp = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Exp)?.Value,
                iat = claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Iat)?.Value,
                org_id = claims.FirstOrDefault(c => c.Type == "org_id")?.Value,
                site_id = claims.FirstOrDefault(c => c.Type == "site_id")?.Value,
                token_type = "Bearer"
            });
        }).WithName("OAuthIntrospect")
          .WithDescription("OAuth 2.0 Token Introspection Endpoint (RFC 7662)");

        // ========================================================================
        // UserInfo Endpoint (OpenID Connect)
        // ========================================================================

        group.MapGet("/userinfo", async (HttpContext context, IGrainFactory grainFactory) =>
        {
            if (context.User.Identity?.IsAuthenticated != true)
                return Results.Unauthorized();

            var claims = context.User.Claims.ToList();
            var userIdClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == JwtRegisteredClaimNames.Sub)?.Value;
            var orgIdClaim = claims.FirstOrDefault(c => c.Type == "org_id")?.Value;

            if (Guid.TryParse(userIdClaim, out var userId) && Guid.TryParse(orgIdClaim, out var orgId))
            {
                var userGrain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
                if (await userGrain.ExistsAsync())
                {
                    var state = await userGrain.GetStateAsync();
                    var roles = await userGrain.GetRolesAsync();

                    return Results.Ok(new
                    {
                        sub = state.Id.ToString(),
                        name = state.DisplayName,
                        email = state.Email,
                        given_name = state.FirstName,
                        family_name = state.LastName,
                        preferred_username = state.DisplayName,
                        org_id = state.OrganizationId.ToString(),
                        site_id = claims.FirstOrDefault(c => c.Type == "site_id")?.Value,
                        roles = roles.ToArray(),
                        user_type = state.Type.ToString(),
                        status = state.Status.ToString()
                    });
                }
            }

            // Fallback to claims if grain lookup fails
            return Results.Ok(new
            {
                sub = userIdClaim,
                name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name || c.Type == JwtRegisteredClaimNames.Name)?.Value,
                org_id = orgIdClaim,
                site_id = claims.FirstOrDefault(c => c.Type == "site_id")?.Value,
                roles = claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray()
            });
        }).RequireAuthorization()
          .WithName("OAuthUserInfo")
          .WithDescription("OpenID Connect UserInfo Endpoint");

        // ========================================================================
        // OpenID Connect Discovery Document
        // ========================================================================

        app.MapGet("/.well-known/openid-configuration", (HttpContext context, JwtSettings jwtSettings) =>
        {
            var baseUrl = $"{context.Request.Scheme}://{context.Request.Host}";
            return Results.Json(new
            {
                issuer = jwtSettings.Issuer,
                authorization_endpoint = $"{baseUrl}/api/oauth/authorize",
                token_endpoint = $"{baseUrl}/api/oauth/token",
                userinfo_endpoint = $"{baseUrl}/api/oauth/userinfo",
                revocation_endpoint = $"{baseUrl}/api/oauth/revoke",
                introspection_endpoint = $"{baseUrl}/api/oauth/introspect",
                jwks_uri = $"{baseUrl}/.well-known/jwks.json",
                response_types_supported = new[] { "code", "token", "id_token", "code token", "code id_token" },
                grant_types_supported = new[] { "authorization_code", "refresh_token", "client_credentials" },
                subject_types_supported = new[] { "public" },
                id_token_signing_alg_values_supported = new[] { "HS256", "RS256" },
                token_endpoint_auth_methods_supported = new[] { "client_secret_basic", "client_secret_post" },
                code_challenge_methods_supported = new[] { "plain", "S256" },
                scopes_supported = new[] { "openid", "profile", "email", "offline_access" },
                claims_supported = new[] { "sub", "name", "email", "org_id", "site_id", "roles" }
            });
        }).WithName("OpenIDConfiguration")
          .WithDescription("OpenID Connect Discovery Document")
          .WithTags("OAuth");

        // ========================================================================
        // JSON Web Key Set (JWKS) Endpoint
        // ========================================================================

        app.MapGet("/.well-known/jwks.json", (JwtSettings jwtSettings) =>
        {
            var keyId = ComputeKeyId(jwtSettings.SecretKey);
            return Results.Json(new
            {
                keys = new[]
                {
                    new
                    {
                        kty = "oct",
                        use = "sig",
                        kid = keyId,
                        alg = "HS256"
                    }
                }
            });
        }).WithName("JWKS")
          .WithDescription("JSON Web Key Set for token verification")
          .WithTags("OAuth");

        // ========================================================================
        // PIN Authentication - OAuth-Style Flow
        // ========================================================================

        // Initiate PIN auth - returns list of users for selection
        group.MapGet("/pin/authorize", async (
            HttpContext context,
            IGrainFactory grainFactory,
            IMemoryCache cache,
            string response_type,
            string client_id,
            string redirect_uri,
            Guid organization_id,
            Guid site_id,
            string? scope = null,
            string? state = null,
            string? nonce = null,
            string? code_challenge = null,
            string? code_challenge_method = null) =>
        {
            // Validate response type
            if (response_type != "code")
            {
                return Results.BadRequest(new OAuthError("unsupported_response_type",
                    "PIN auth only supports response_type=code"));
            }

            // Validate client
            if (!IsValidClient(client_id, redirect_uri))
            {
                return Results.BadRequest(new OAuthError("invalid_client",
                    "Unknown client_id or invalid redirect_uri"));
            }

            // Get users available for PIN login at this site
            var userLookup = grainFactory.GetGrain<IUserLookupGrain>(GrainKeys.UserLookup(organization_id));
            var users = await userLookup.GetUsersForSiteAsync(site_id);

            if (users.Count == 0)
            {
                return Results.BadRequest(new OAuthError("no_users",
                    "No users with PINs are available for this site"));
            }

            // Store pending state
            var pendingToken = Guid.NewGuid().ToString("N");
            var pendingState = new PendingPinAuthState(
                organization_id,
                site_id,
                client_id,
                redirect_uri,
                code_challenge,
                code_challenge_method,
                scope,
                nonce,
                state,
                DateTime.UtcNow.Add(PendingPinAuthExpiry));

            cache.Set(
                PendingPinAuthCachePrefix + pendingToken,
                pendingState,
                PendingPinAuthExpiry);

            // Return users for selection
            var userOptions = users.Select(u => new PinUserOption(
                u.UserId,
                u.DisplayName,
                u.FirstName,
                u.LastName,
                u.AvatarUrl)).ToList();

            return Results.Ok(new PinAuthUsersResponse(
                pendingToken,
                organization_id,
                site_id,
                userOptions));
        }).WithName("OAuthPinAuthorize")
          .WithDescription("OAuth-style PIN authorization - returns users for PIN selection");

        // Get pending PIN auth state (for resuming flow)
        group.MapGet("/pin/pending", (
            [FromQuery] string pending_token,
            IGrainFactory grainFactory,
            IMemoryCache cache) =>
        {
            if (string.IsNullOrEmpty(pending_token))
            {
                return Results.BadRequest(Hal.Error("missing_token", "Pending token is required"));
            }

            if (!cache.TryGetValue<PendingPinAuthState>(PendingPinAuthCachePrefix + pending_token, out var pendingState) ||
                pendingState == null)
            {
                return Results.NotFound(Hal.Error("expired_or_invalid", "Pending PIN auth session not found or expired"));
            }

            if (pendingState.ExpiresAt < DateTime.UtcNow)
            {
                cache.Remove(PendingPinAuthCachePrefix + pending_token);
                return Results.NotFound(Hal.Error("expired", "Pending PIN auth session expired"));
            }

            return Results.Ok(new
            {
                pending_token,
                organization_id = pendingState.OrganizationId,
                site_id = pendingState.SiteId,
                client_id = pendingState.ClientId,
                redirect_uri = pendingState.RedirectUri
            });
        }).WithName("OAuthPinPending")
          .WithDescription("Get pending PIN auth state");

        // Authenticate with PIN - returns authorization code
        group.MapPost("/pin/authenticate", async (
            [FromBody] PinAuthenticateRequest request,
            IGrainFactory grainFactory,
            JwtTokenService tokenService,
            IMemoryCache cache,
            IAuthorizationService authService) =>
        {
            if (string.IsNullOrEmpty(request.PendingToken))
            {
                return Results.BadRequest(new OAuthError("missing_token", "Pending token is required"));
            }

            if (string.IsNullOrEmpty(request.Pin))
            {
                return Results.BadRequest(new OAuthError("missing_pin", "PIN is required"));
            }

            // Retrieve and validate pending state
            if (!cache.TryGetValue<PendingPinAuthState>(PendingPinAuthCachePrefix + request.PendingToken, out var pendingState) ||
                pendingState == null)
            {
                return Results.BadRequest(new OAuthError("invalid_token", "Pending PIN auth session not found or expired"));
            }

            if (pendingState.ExpiresAt < DateTime.UtcNow)
            {
                cache.Remove(PendingPinAuthCachePrefix + request.PendingToken);
                return Results.BadRequest(new OAuthError("expired", "Pending PIN auth session expired"));
            }

            // Verify the user has a PIN and verify it
            var userGrain = grainFactory.GetGrain<IUserGrain>(
                GrainKeys.User(pendingState.OrganizationId, request.UserId));

            if (!await userGrain.ExistsAsync())
            {
                return Results.BadRequest(new OAuthError("invalid_user", "User not found"));
            }

            var userState = await userGrain.GetStateAsync();

            // Check user status
            if (userState.Status == UserStatus.Inactive)
            {
                return Results.BadRequest(new OAuthError("user_inactive", "User account is inactive"));
            }

            if (userState.Status == UserStatus.Locked)
            {
                return Results.BadRequest(new OAuthError("user_locked", "User account is locked"));
            }

            // Verify site access
            if (!userState.SiteAccess.Contains(pendingState.SiteId))
            {
                return Results.BadRequest(new OAuthError("no_site_access", "User does not have access to this site"));
            }

            // Verify PIN
            var authResult = await userGrain.VerifyPinAsync(request.Pin);
            if (!authResult.Success)
            {
                return Results.BadRequest(new OAuthError("invalid_pin", authResult.Error ?? "Invalid PIN"));
            }

            // Consume the pending state
            cache.Remove(PendingPinAuthCachePrefix + request.PendingToken);

            // Record login
            await userGrain.RecordLoginAsync();

            // Create SpiceDB session with PIN scope (restricted to POS operations)
            await authService.CreateSessionAsync(
                request.UserId,
                pendingState.OrganizationId,
                pendingState.SiteId,
                "pin");

            // Get user roles
            var roles = await userGrain.GetRolesAsync();

            // Generate authorization code
            var code = GrainKeys.GenerateAuthorizationCode();
            var codeGrain = grainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));
            await codeGrain.IssueAsync(new AuthorizationCodeRequest(
                request.UserId,
                pendingState.OrganizationId,
                pendingState.ClientId,
                pendingState.RedirectUri,
                pendingState.Scope,
                pendingState.CodeChallenge,
                pendingState.CodeChallengeMethod,
                pendingState.Nonce,
                userState.DisplayName,
                roles));

            // Build redirect URL with code
            var redirectUrl = AddQueryParam(pendingState.RedirectUri, "code", code);
            if (!string.IsNullOrEmpty(pendingState.ClientState))
            {
                redirectUrl = AddQueryParam(redirectUrl, "state", pendingState.ClientState);
            }

            return Results.Ok(new
            {
                code,
                redirect_uri = redirectUrl,
                state = pendingState.ClientState
            });
        }).WithName("OAuthPinAuthenticate")
          .WithDescription("Authenticate with PIN and receive authorization code");

        return app;
    }

    // ========================================================================
    // Complete OAuth Login - Real User Management
    // ========================================================================

    private static async Task<IResult> CompleteOAuthLoginAsync(
        IGrainFactory grainFactory,
        JwtTokenService tokenService,
        IMemoryCache cache,
        Guid orgId,
        Guid userId,
        string provider,
        string externalId,
        string email,
        string? name,
        string? responseType,
        OAuthStateValidation? validation,
        string? clientState,
        string? redirectTarget)
    {
        // Get user grain
        var userGrain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
        if (!await userGrain.ExistsAsync())
        {
            var target = redirectTarget ?? validation?.ReturnUrl ?? "http://localhost:5174";
            return Results.Redirect($"{target}?error=user_not_found");
        }

        var state = await userGrain.GetStateAsync();

        // Check user status
        if (state.Status == UserStatus.Inactive)
        {
            var target = redirectTarget ?? validation?.ReturnUrl ?? "http://localhost:5174";
            return Results.Redirect($"{target}?error=user_inactive");
        }

        if (state.Status == UserStatus.Locked)
        {
            var target = redirectTarget ?? validation?.ReturnUrl ?? "http://localhost:5174";
            return Results.Redirect($"{target}?error=user_locked");
        }

        // Link external identity if not already linked
        var externalIds = await userGrain.GetExternalIdsAsync();
        if (!externalIds.ContainsKey(provider.ToLowerInvariant()))
        {
            await userGrain.LinkExternalIdentityAsync(provider, externalId, email);
        }

        // Record login (event publishing handled by grain)
        await userGrain.RecordLoginAsync(provider, email);

        // Get roles from user state
        var roles = await userGrain.GetRolesAsync();
        var displayName = name ?? state.DisplayName;
        var finalRedirectTarget = redirectTarget ?? validation?.ReturnUrl ?? "http://localhost:5174";

        // Handle authorization code flow
        if (responseType == "code" && validation != null)
        {
            var code = GrainKeys.GenerateAuthorizationCode();
            var codeGrain = grainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));
            await codeGrain.IssueAsync(new AuthorizationCodeRequest(
                userId,
                orgId,
                validation.ClientId ?? "default",
                finalRedirectTarget,
                validation.Scope,
                validation.CodeChallenge,
                validation.CodeChallengeMethod,
                validation.Nonce,
                displayName,
                roles));

            var codeRedirect = AddQueryParam(finalRedirectTarget, "code", code);
            if (!string.IsNullOrEmpty(clientState))
                codeRedirect = AddQueryParam(codeRedirect, "state", clientState);

            return Results.Redirect(codeRedirect);
        }

        // Implicit flow - issue tokens directly
        var (accessToken, expires) = tokenService.GenerateAccessToken(
            userId,
            displayName,
            orgId,
            roles: roles);
        var refreshToken = tokenService.GenerateRefreshToken();

        // Check if we should return JSON (API call) or redirect (browser flow)
        if (string.IsNullOrEmpty(redirectTarget) && validation == null)
        {
            return Results.Ok(new
            {
                access_token = accessToken,
                refresh_token = refreshToken,
                expires_in = (int)(expires - DateTime.UtcNow).TotalSeconds,
                token_type = "Bearer",
                user_id = userId,
                org_id = orgId,
                display_name = displayName,
                roles
            });
        }

        var fragment = $"access_token={accessToken}" +
            $"&token_type=Bearer" +
            $"&expires_in={(int)(expires - DateTime.UtcNow).TotalSeconds}" +
            $"&refresh_token={refreshToken}" +
            $"&user_id={userId}" +
            $"&org_id={orgId}" +
            $"&display_name={Uri.EscapeDataString(displayName)}";

        if (!string.IsNullOrEmpty(clientState))
            fragment += $"&state={Uri.EscapeDataString(clientState)}";

        var separator = finalRedirectTarget.Contains('#') ? "&" : "#";
        return Results.Redirect($"{finalRedirectTarget}{separator}{fragment}");
    }

    // ========================================================================
    // Helper Methods
    // ========================================================================

    private static async Task<IResult> HandleAuthorizationCodeGrant(
        IFormCollection form,
        string clientId,
        IGrainFactory grainFactory,
        JwtTokenService tokenService)
    {
        var code = form["code"].FirstOrDefault();
        var codeVerifier = form["code_verifier"].FirstOrDefault();

        if (string.IsNullOrEmpty(code))
        {
            return Results.Json(new OAuthError("invalid_request", "code is required"),
                statusCode: 400);
        }

        var codeGrain = grainFactory.GetGrain<IAuthorizationCodeGrain>(GrainKeys.AuthorizationCode(code));
        var exchange = await codeGrain.ExchangeAsync(clientId, codeVerifier);

        if (exchange == null)
        {
            return Results.Json(new OAuthError("invalid_grant",
                "Invalid, expired, or already used authorization code"),
                statusCode: 400);
        }

        // Create a session for proper refresh token management
        var sessionId = Guid.NewGuid();
        var sessionGrain = grainFactory.GetGrain<ISessionGrain>(
            GrainKeys.Session(exchange.OrganizationId, sessionId));
        var sessionTokens = await sessionGrain.CreateAsync(new CreateSessionCommand(
            exchange.UserId,
            exchange.OrganizationId,
            null, // siteId not available from authorization code
            null, // deviceId not available from authorization code
            "oauth",
            null,
            null));

        // Register the refresh token in the global lookup for OAuth token refresh
        var refreshTokenHash = HashToken(sessionTokens.RefreshToken);
        var refreshTokenLookup = grainFactory.GetGrain<IRefreshTokenLookupGrain>(GrainKeys.RefreshTokenLookup());
        await refreshTokenLookup.RegisterAsync(refreshTokenHash, exchange.OrganizationId, sessionId);

        // Generate access token with session_id claim for proper token management
        var (accessToken, expires) = tokenService.GenerateAccessToken(
            exchange.UserId,
            exchange.DisplayName ?? "User",
            exchange.OrganizationId,
            roles: exchange.Roles,
            sessionId: sessionId);

        return Results.Json(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = (int)(expires - DateTime.UtcNow).TotalSeconds,
            refresh_token = sessionTokens.RefreshToken,
            scope = exchange.Scope
        });
    }

    private static async Task<IResult> HandleRefreshTokenGrant(
        IFormCollection form,
        string clientId,
        IGrainFactory grainFactory,
        JwtTokenService tokenService)
    {
        var refreshToken = form["refresh_token"].FirstOrDefault();

        if (string.IsNullOrEmpty(refreshToken))
        {
            return Results.Json(new OAuthError("invalid_request", "refresh_token is required"),
                statusCode: 400);
        }

        // Look up the session associated with this refresh token
        var refreshTokenHash = HashToken(refreshToken);
        var refreshTokenLookup = grainFactory.GetGrain<IRefreshTokenLookupGrain>(GrainKeys.RefreshTokenLookup());
        var lookupResult = await refreshTokenLookup.LookupAsync(refreshTokenHash);

        if (lookupResult == null)
        {
            return Results.Json(new OAuthError("invalid_grant", "Refresh token not found or expired"),
                statusCode: 400);
        }

        // Get the session and refresh the tokens
        var sessionGrain = grainFactory.GetGrain<ISessionGrain>(
            GrainKeys.Session(lookupResult.OrganizationId, lookupResult.SessionId));
        var refreshResult = await sessionGrain.RefreshAsync(refreshToken);

        if (!refreshResult.Success)
        {
            // Remove the stale lookup entry if refresh failed
            await refreshTokenLookup.RemoveAsync(refreshTokenHash);
            return Results.Json(new OAuthError("invalid_grant", refreshResult.Error ?? "Refresh token invalid or expired"),
                statusCode: 400);
        }

        // Update the refresh token lookup with the new token (rotation)
        var newRefreshTokenHash = HashToken(refreshResult.Tokens!.RefreshToken);
        await refreshTokenLookup.RotateAsync(
            refreshTokenHash,
            newRefreshTokenHash,
            lookupResult.OrganizationId,
            lookupResult.SessionId);

        // Get user info from session for the access token
        var sessionState = await sessionGrain.GetStateAsync();
        var userGrain = grainFactory.GetGrain<IUserGrain>(
            GrainKeys.User(sessionState.OrganizationId, sessionState.UserId));
        var userState = await userGrain.GetStateAsync();
        var roles = await userGrain.GetRolesAsync();

        // Generate new access token with session claim
        var (accessToken, expires) = tokenService.GenerateAccessToken(
            sessionState.UserId,
            userState.DisplayName,
            sessionState.OrganizationId,
            sessionState.SiteId,
            sessionState.DeviceId,
            roles,
            lookupResult.SessionId);

        return Results.Json(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = (int)(expires - DateTime.UtcNow).TotalSeconds,
            refresh_token = refreshResult.Tokens.RefreshToken
        });
    }

    private static IResult HandleClientCredentialsGrant(
        string clientId,
        string? clientSecret,
        JwtTokenService tokenService)
    {
        if (!IsValidClientCredentials(clientId, clientSecret))
        {
            return Results.Json(new OAuthError("invalid_client", "Invalid client credentials"),
                statusCode: 401);
        }

        var (accessToken, expires) = tokenService.GenerateAccessToken(
            Guid.Empty,
            clientId,
            Guid.Empty,
            roles: ["api_client"]);

        return Results.Json(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = (int)(expires - DateTime.UtcNow).TotalSeconds
        });
    }

    private static bool IsValidClient(string clientId, string redirectUri)
    {
        var validClients = new Dictionary<string, string[]>
        {
            ["default"] = ["http://localhost:5173", "http://localhost:5174", "http://localhost:5175"],
            ["pos"] = ["http://localhost:5173", "https://pos.darkvelocity.app"],
            ["backoffice"] = ["http://localhost:5174", "https://admin.darkvelocity.app"],
        };

        if (!validClients.TryGetValue(clientId, out var allowedUris))
            return true;

        return allowedUris.Any(u => redirectUri.StartsWith(u, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsValidClientCredentials(string clientId, string? clientSecret)
    {
        if (clientId.StartsWith("test_") || clientId == "default")
            return true;

        return !string.IsNullOrEmpty(clientSecret);
    }

    private static string AddQueryParam(string url, string key, string value)
    {
        var separator = url.Contains('?') ? "&" : "?";
        return $"{url}{separator}{key}={Uri.EscapeDataString(value)}";
    }

    private static string ComputeKeyId(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hash[..8])
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}

/// <summary>
/// OAuth 2.0 error response.
/// </summary>
public record OAuthError(string error, string? error_description = null, string? error_uri = null);
