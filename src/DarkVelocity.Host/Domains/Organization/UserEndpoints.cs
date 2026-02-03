using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class UserEndpoints
{
    public static WebApplication MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/users").WithTags("Users");

        // Create a new user
        group.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateUserRequest request,
            IGrainFactory grainFactory) =>
        {
            var userId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            var result = await grain.CreateAsync(new CreateUserCommand(
                orgId, request.Email, request.DisplayName, request.Type, request.FirstName, request.LastName));

            // Register email in global lookup
            var emailLookup = grainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
            await emailLookup.RegisterEmailAsync(request.Email, orgId, userId);

            return Results.Created($"/api/orgs/{orgId}/users/{userId}", Hal.Resource(new
            {
                id = result.Id,
                email = result.Email,
                createdAt = result.CreatedAt
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/users/{userId}" },
                ["set-pin"] = new { href = $"/api/orgs/{orgId}/users/{userId}/pin" },
                ["external-ids"] = new { href = $"/api/orgs/{orgId}/users/{userId}/external-ids" }
            }));
        });

        // Get user by ID
        group.MapGet("/{userId}", async (Guid orgId, Guid userId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            var state = await grain.GetStateAsync();
            var response = MapToResponse(state);

            return Results.Ok(Hal.Resource(response, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/users/{userId}" },
                ["set-pin"] = new { href = $"/api/orgs/{orgId}/users/{userId}/pin" },
                ["external-ids"] = new { href = $"/api/orgs/{orgId}/users/{userId}/external-ids" },
                ["activate"] = new { href = $"/api/orgs/{orgId}/users/{userId}/activate" },
                ["deactivate"] = new { href = $"/api/orgs/{orgId}/users/{userId}/deactivate" }
            }));
        });

        // Update user
        group.MapPatch("/{userId}", async (
            Guid orgId,
            Guid userId,
            [FromBody] UpdateUserRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            await grain.UpdateAsync(new UpdateUserCommand(
                request.DisplayName, request.FirstName, request.LastName, request.Preferences));
            var state = await grain.GetStateAsync();

            return Results.Ok(Hal.Resource(MapToResponse(state), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/users/{userId}" }
            }));
        });

        // Deactivate user (soft delete)
        group.MapDelete("/{userId}", async (Guid orgId, Guid userId, IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            await grain.DeactivateAsync();

            // Unregister email from global lookup
            var state = await grain.GetStateAsync();
            var emailLookup = grainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
            await emailLookup.UnregisterEmailAsync(state.Email, orgId);

            return Results.NoContent();
        });

        // Grant site access
        group.MapPost("/{userId}/sites/{siteId}", async (
            Guid orgId,
            Guid userId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            await grain.GrantSiteAccessAsync(siteId);
            return Results.Ok(new { message = "Site access granted" });
        });

        // Revoke site access
        group.MapDelete("/{userId}/sites/{siteId}", async (
            Guid orgId,
            Guid userId,
            Guid siteId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            await grain.RevokeSiteAccessAsync(siteId);
            return Results.NoContent();
        });

        // Set PIN
        group.MapPost("/{userId}/pin", async (
            Guid orgId,
            Guid userId,
            [FromBody] SetPinRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            await grain.SetPinAsync(request.Pin);
            return Results.Ok(new { message = "PIN set successfully" });
        });

        // Link external identity
        group.MapPost("/{userId}/external-ids", async (
            Guid orgId,
            Guid userId,
            [FromBody] LinkExternalIdentityRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            await grain.LinkExternalIdentityAsync(request.Provider, request.ExternalId, request.Email);
            return Results.Ok(new { message = $"External identity {request.Provider} linked" });
        });

        // Unlink external identity
        group.MapDelete("/{userId}/external-ids/{provider}", async (
            Guid orgId,
            Guid userId,
            string provider,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            await grain.UnlinkExternalIdentityAsync(provider);
            return Results.NoContent();
        });

        // Get external identities
        group.MapGet("/{userId}/external-ids", async (
            Guid orgId,
            Guid userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            var externalIds = await grain.GetExternalIdsAsync();
            return Results.Ok(Hal.Resource(new { externalIds }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/users/{userId}/external-ids" },
                ["user"] = new { href = $"/api/orgs/{orgId}/users/{userId}" }
            }));
        });

        // Activate user
        group.MapPost("/{userId}/activate", async (
            Guid orgId,
            Guid userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            await grain.ActivateAsync();

            // Re-register email in global lookup
            var state = await grain.GetStateAsync();
            var emailLookup = grainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
            await emailLookup.RegisterEmailAsync(state.Email, orgId, userId);

            return Results.Ok(new { message = "User activated" });
        });

        // Deactivate user
        group.MapPost("/{userId}/deactivate", async (
            Guid orgId,
            Guid userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            await grain.DeactivateAsync();

            // Unregister email from global lookup
            var state = await grain.GetStateAsync();
            var emailLookup = grainFactory.GetGrain<IEmailLookupGrain>(GrainKeys.EmailLookup());
            await emailLookup.UnregisterEmailAsync(state.Email, orgId);

            return Results.Ok(new { message = "User deactivated" });
        });

        // Lock user
        group.MapPost("/{userId}/lock", async (
            Guid orgId,
            Guid userId,
            [FromBody] LockUserRequest? request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            await grain.LockAsync(request?.Reason ?? "Locked by administrator");
            return Results.Ok(new { message = "User locked" });
        });

        // Unlock user
        group.MapPost("/{userId}/unlock", async (
            Guid orgId,
            Guid userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            await grain.UnlockAsync();
            return Results.Ok(new { message = "User unlocked" });
        });

        // Add to group
        group.MapPost("/{userId}/groups/{groupId}", async (
            Guid orgId,
            Guid userId,
            Guid groupId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            await grain.AddToGroupAsync(groupId);
            return Results.Ok(new { message = "User added to group" });
        });

        // Remove from group
        group.MapDelete("/{userId}/groups/{groupId}", async (
            Guid orgId,
            Guid userId,
            Guid groupId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            await grain.RemoveFromGroupAsync(groupId);
            return Results.NoContent();
        });

        return app;
    }

    private static UserResponse MapToResponse(State.UserState state) => new(
        state.Id,
        state.OrganizationId,
        state.Email,
        state.DisplayName,
        state.FirstName,
        state.LastName,
        state.Status,
        state.Type,
        state.SiteAccess,
        state.UserGroupIds,
        state.ExternalIds,
        state.CreatedAt,
        state.UpdatedAt,
        state.LastLoginAt);
}

public record LockUserRequest(string? Reason = null);
