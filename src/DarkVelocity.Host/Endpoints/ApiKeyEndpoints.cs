using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DarkVelocity.Host.Endpoints;

public static class ApiKeyEndpoints
{
    public static WebApplication MapApiKeyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/api-keys")
            .WithTags("API Keys")
            .RequireAuthorization();

        // ========================================================================
        // List API Keys
        // ========================================================================

        group.MapGet("/", async (
            Guid orgId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            var userId = GetUserId(user);
            if (userId == null)
                return Results.Unauthorized();

            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var registryGrain = grainFactory.GetGrain<IApiKeyRegistryGrain>(
                GrainKeys.ApiKeyRegistry(orgId, userId.Value));

            if (!await registryGrain.ExistsAsync())
            {
                return Results.Ok(new { items = Array.Empty<ApiKeyListItem>(), total = 0 });
            }

            var keyIds = await registryGrain.GetKeyIdsAsync();
            var items = new List<ApiKeyListItem>();

            foreach (var keyId in keyIds)
            {
                var keyGrain = grainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));
                if (await keyGrain.ExistsAsync())
                {
                    var summary = await keyGrain.GetSummaryAsync();
                    items.Add(new ApiKeyListItem(
                        summary.Id,
                        summary.Name,
                        summary.Description,
                        summary.KeyPrefix,
                        summary.Type,
                        summary.IsTestMode,
                        summary.Status,
                        summary.Scopes,
                        summary.CreatedAt,
                        summary.ExpiresAt,
                        summary.LastUsedAt));
                }
            }

            return Results.Ok(new { items, total = items.Count });
        }).WithName("ListApiKeys")
          .WithDescription("List all API keys for the current user");

        // ========================================================================
        // Create API Key
        // ========================================================================

        group.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateApiKeyRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            var userId = GetUserId(user);
            if (userId == null)
                return Results.Unauthorized();

            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            // Validate scopes against user's permissions
            var userRoles = GetUserRoles(user);
            if (request.Roles != null)
            {
                foreach (var role in request.Roles)
                {
                    if (!userRoles.Contains(role))
                    {
                        return Results.BadRequest(new
                        {
                            error = "invalid_role",
                            error_description = $"Cannot grant role '{role}' - you don't have this permission"
                        });
                    }
                }
            }

            // Validate site access
            var userSites = GetUserSites(user);
            if (request.AllowedSiteIds != null && userSites.Count > 0)
            {
                foreach (var siteId in request.AllowedSiteIds)
                {
                    if (!userSites.Contains(siteId))
                    {
                        return Results.BadRequest(new
                        {
                            error = "invalid_site",
                            error_description = $"Cannot grant access to site '{siteId}' - you don't have access"
                        });
                    }
                }
            }

            // Initialize registry if needed
            var registryGrain = grainFactory.GetGrain<IApiKeyRegistryGrain>(
                GrainKeys.ApiKeyRegistry(orgId, userId.Value));
            if (!await registryGrain.ExistsAsync())
            {
                await registryGrain.InitializeAsync(orgId, userId.Value);
            }

            // Create the API key
            var keyId = Guid.NewGuid();
            var keyGrain = grainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

            var scopes = request.Scopes?.Select(s => new ApiKeyScope
            {
                Resource = s.Resource,
                Actions = s.Actions
            }).ToList();

            var result = await keyGrain.CreateAsync(new CreateApiKeyCommand(
                orgId,
                userId.Value,
                request.Name,
                request.Description,
                request.Type,
                request.IsTestMode,
                scopes,
                request.CustomClaims,
                request.Roles,
                request.AllowedSiteIds,
                request.AllowedIpRanges,
                request.RateLimitPerMinute,
                request.ExpiresAt));

            return Results.Created(
                $"/api/orgs/{orgId}/api-keys/{result.Id}",
                new CreateApiKeyResponse(
                    result.Id,
                    result.ApiKey,
                    result.KeyPrefix,
                    result.CreatedAt));
        }).WithName("CreateApiKey")
          .WithDescription("Create a new API key with optional custom claims and scopes");

        // ========================================================================
        // Get API Key Details
        // ========================================================================

        group.MapGet("/{keyId}", async (
            Guid orgId,
            Guid keyId,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            var userId = GetUserId(user);
            if (userId == null)
                return Results.Unauthorized();

            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var keyGrain = grainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

            if (!await keyGrain.ExistsAsync())
                return Results.NotFound(new { error = "not_found", error_description = "API key not found" });

            var state = await keyGrain.GetStateAsync();

            // Verify ownership
            if (state.UserId != userId.Value)
                return Results.Forbid();

            var scopeResponses = state.Scopes.Select(s => new ApiKeyScopeResponse(s.Resource, s.Actions)).ToList();

            return Results.Ok(new ApiKeyResponse(
                state.Id,
                state.Name,
                state.Description,
                state.KeyPrefix,
                state.Type,
                state.IsTestMode,
                state.Status,
                scopeResponses,
                state.CustomClaims,
                state.Roles,
                state.AllowedSiteIds,
                state.AllowedIpRanges,
                state.RateLimitPerMinute,
                state.CreatedAt,
                state.UpdatedAt,
                state.ExpiresAt,
                state.LastUsedAt,
                state.UsageCount));
        }).WithName("GetApiKey")
          .WithDescription("Get details of a specific API key");

        // ========================================================================
        // Update API Key
        // ========================================================================

        group.MapPatch("/{keyId}", async (
            Guid orgId,
            Guid keyId,
            [FromBody] UpdateApiKeyRequest request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            var userId = GetUserId(user);
            if (userId == null)
                return Results.Unauthorized();

            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var keyGrain = grainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

            if (!await keyGrain.ExistsAsync())
                return Results.NotFound(new { error = "not_found", error_description = "API key not found" });

            var state = await keyGrain.GetStateAsync();

            // Verify ownership
            if (state.UserId != userId.Value)
                return Results.Forbid();

            // Validate roles if being updated
            if (request.Roles != null)
            {
                var userRoles = GetUserRoles(user);
                foreach (var role in request.Roles)
                {
                    if (!userRoles.Contains(role))
                    {
                        return Results.BadRequest(new
                        {
                            error = "invalid_role",
                            error_description = $"Cannot grant role '{role}' - you don't have this permission"
                        });
                    }
                }
            }

            // Validate site access if being updated
            if (request.AllowedSiteIds != null)
            {
                var userSites = GetUserSites(user);
                if (userSites.Count > 0)
                {
                    foreach (var siteId in request.AllowedSiteIds)
                    {
                        if (!userSites.Contains(siteId))
                        {
                            return Results.BadRequest(new
                            {
                                error = "invalid_site",
                                error_description = $"Cannot grant access to site '{siteId}' - you don't have access"
                            });
                        }
                    }
                }
            }

            var scopes = request.Scopes?.Select(s => new ApiKeyScope
            {
                Resource = s.Resource,
                Actions = s.Actions
            }).ToList();

            await keyGrain.UpdateAsync(new UpdateApiKeyCommand(
                request.Name,
                request.Description,
                scopes,
                request.CustomClaims,
                request.Roles,
                request.AllowedSiteIds,
                request.AllowedIpRanges,
                request.RateLimitPerMinute,
                request.ExpiresAt));

            return Results.NoContent();
        }).WithName("UpdateApiKey")
          .WithDescription("Update an API key's settings (cannot change key type or mode)");

        // ========================================================================
        // Revoke API Key
        // ========================================================================

        group.MapDelete("/{keyId}", async (
            Guid orgId,
            Guid keyId,
            [FromBody] RevokeApiKeyRequest? request,
            ClaimsPrincipal user,
            IGrainFactory grainFactory) =>
        {
            var userId = GetUserId(user);
            if (userId == null)
                return Results.Unauthorized();

            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            var keyGrain = grainFactory.GetGrain<IApiKeyGrain>(GrainKeys.ApiKey(orgId, keyId));

            if (!await keyGrain.ExistsAsync())
                return Results.NotFound(new { error = "not_found", error_description = "API key not found" });

            var state = await keyGrain.GetStateAsync();

            // Verify ownership (or admin can revoke)
            var isAdmin = user.IsInRole("admin") || user.IsInRole("owner");
            if (state.UserId != userId.Value && !isAdmin)
                return Results.Forbid();

            await keyGrain.RevokeAsync(userId.Value, request?.Reason);

            return Results.NoContent();
        }).WithName("RevokeApiKey")
          .WithDescription("Revoke an API key, making it permanently unusable");

        // ========================================================================
        // Get Available Scopes
        // ========================================================================

        group.MapGet("/scopes", (Guid orgId, ClaimsPrincipal user) =>
        {
            if (!ValidateOrgAccess(user, orgId))
                return Results.Forbid();

            // Return available scopes that can be granted to API keys
            var scopes = new List<AvailableScope>
            {
                new("orders", "Orders", "Access to order management", ["read", "write", "delete"]),
                new("customers", "Customers", "Access to customer data", ["read", "write", "delete"]),
                new("menu", "Menu", "Access to menu management", ["read", "write"]),
                new("inventory", "Inventory", "Access to inventory management", ["read", "write"]),
                new("payments", "Payments", "Access to payment processing", ["read", "write", "refund"]),
                new("bookings", "Bookings", "Access to reservation management", ["read", "write", "delete"]),
                new("employees", "Employees", "Access to employee data", ["read", "write"]),
                new("reports", "Reports", "Access to reporting data", ["read"]),
                new("sites", "Sites", "Access to site configuration", ["read", "write"]),
                new("webhooks", "Webhooks", "Access to webhook management", ["read", "write", "delete"])
            };

            return Results.Ok(new AvailableScopesResponse(scopes));
        }).WithName("GetAvailableScopes")
          .WithDescription("Get list of available scopes that can be granted to API keys");

        return app;
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var subClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(subClaim) || !Guid.TryParse(subClaim, out var userId))
            return null;

        return userId;
    }

    private static bool ValidateOrgAccess(ClaimsPrincipal user, Guid orgId)
    {
        var orgClaim = user.FindFirst("org_id")?.Value;
        if (string.IsNullOrEmpty(orgClaim) || !Guid.TryParse(orgClaim, out var userOrgId))
            return false;

        return userOrgId == orgId;
    }

    private static HashSet<string> GetUserRoles(ClaimsPrincipal user)
    {
        return user.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet();
    }

    private static HashSet<Guid> GetUserSites(ClaimsPrincipal user)
    {
        var sites = new HashSet<Guid>();
        var sitesClaim = user.FindFirst("allowed_sites")?.Value;

        if (!string.IsNullOrEmpty(sitesClaim))
        {
            foreach (var siteId in sitesClaim.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Guid.TryParse(siteId.Trim(), out var id))
                    sites.Add(id);
            }
        }

        return sites;
    }
}
