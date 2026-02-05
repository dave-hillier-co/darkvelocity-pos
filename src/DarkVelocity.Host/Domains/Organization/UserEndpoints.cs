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

            return Results.Ok(Hal.Resource(response, BuildUserLinks(orgId, userId, state)));
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
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(new { siteId, granted = true }, BuildUserLinks(orgId, userId, state)));
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
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(new { pinSet = true }, BuildUserLinks(orgId, userId, state)));
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
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(new { provider = request.Provider, linked = true }, BuildUserLinks(orgId, userId, state)));
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

            return Results.Ok(Hal.Resource(new { status = "Active" }, BuildUserLinks(orgId, userId, state)));
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

            return Results.Ok(Hal.Resource(new { status = "Inactive" }, BuildUserLinks(orgId, userId, state)));
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
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(new { status = "Locked" }, BuildUserLinks(orgId, userId, state)));
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
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(new { status = "Active" }, BuildUserLinks(orgId, userId, state)));
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
            var state = await grain.GetStateAsync();
            return Results.Ok(Hal.Resource(new { groupId, added = true }, BuildUserLinks(orgId, userId, state)));
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

    private static Dictionary<string, object> BuildUserLinks(Guid orgId, Guid userId, State.UserState state)
    {
        var basePath = $"/api/orgs/{orgId}/users/{userId}";
        var links = new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["organization"] = new { href = $"/api/orgs/{orgId}" },
            ["set-pin"] = new { href = $"{basePath}/pin" },
            ["external-ids"] = new { href = $"{basePath}/external-ids" },
            ["groups"] = new { href = $"{basePath}/groups" }
        };

        switch (state.Status)
        {
            case State.UserStatus.Active:
                links["deactivate"] = new { href = $"{basePath}/deactivate" };
                links["lock"] = new { href = $"{basePath}/lock" };
                break;
            case State.UserStatus.Inactive:
                links["activate"] = new { href = $"{basePath}/activate" };
                break;
            case State.UserStatus.Locked:
                links["unlock"] = new { href = $"{basePath}/unlock" };
                break;
        }

        // Add site access links
        if (state.SiteAccess.Count > 0)
        {
            var siteLinks = state.SiteAccess.Select(s => new { href = $"/api/orgs/{orgId}/sites/{s}" }).ToArray();
            links["sites"] = siteLinks;
        }

        return links;
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
