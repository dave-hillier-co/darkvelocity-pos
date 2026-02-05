using DarkVelocity.Host.Contracts;
using DarkVelocity.Host.Grains;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class UserGroupEndpoints
{
    public static WebApplication MapUserGroupEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/groups").WithTags("User Groups");

        // Create a new user group
        group.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateUserGroupRequest request,
            IGrainFactory grainFactory) =>
        {
            var groupId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(orgId, groupId));
            var result = await grain.CreateAsync(new CreateUserGroupCommand(
                orgId, request.Name, request.Description));

            return Results.Created($"/api/orgs/{orgId}/groups/{groupId}", Hal.Resource(new
            {
                id = result.Id,
                name = request.Name,
                createdAt = result.CreatedAt
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/groups/{groupId}" },
                ["members"] = new { href = $"/api/orgs/{orgId}/groups/{groupId}/members" }
            }));
        });

        // Get user group by ID
        group.MapGet("/{groupId}", async (
            Guid orgId,
            Guid groupId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(orgId, groupId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User group not found"));

            var state = await grain.GetStateAsync();
            var response = MapToResponse(state);

            return Results.Ok(Hal.Resource(response, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/groups/{groupId}" },
                ["members"] = new { href = $"/api/orgs/{orgId}/groups/{groupId}/members" },
                ["add-member"] = new { href = $"/api/orgs/{orgId}/groups/{groupId}/members/{{userId}}", templated = true }
            }));
        });

        // Update user group
        group.MapPatch("/{groupId}", async (
            Guid orgId,
            Guid groupId,
            [FromBody] UpdateUserGroupRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(orgId, groupId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User group not found"));

            await grain.UpdateAsync(request.Name, request.Description);
            var state = await grain.GetStateAsync();

            return Results.Ok(Hal.Resource(MapToResponse(state), new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/groups/{groupId}" }
            }));
        });

        // Get members of a group
        group.MapGet("/{groupId}/members", async (
            Guid orgId,
            Guid groupId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(orgId, groupId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User group not found"));

            var memberIds = await grain.GetMembersAsync();

            // Fetch user details for all members
            var members = new List<object>();
            foreach (var memberId in memberIds)
            {
                var userGrain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, memberId));
                if (await userGrain.ExistsAsync())
                {
                    var userState = await userGrain.GetStateAsync();
                    members.Add(new
                    {
                        id = userState.Id,
                        email = userState.Email,
                        displayName = userState.DisplayName,
                        status = userState.Status.ToString(),
                        type = userState.Type.ToString()
                    });
                }
            }

            return Results.Ok(Hal.Resource(new
            {
                groupId,
                memberCount = members.Count,
                _embedded = new { members }
            }, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/groups/{groupId}/members" },
                ["group"] = new { href = $"/api/orgs/{orgId}/groups/{groupId}" }
            }));
        });

        // Add member to group
        group.MapPost("/{groupId}/members/{userId}", async (
            Guid orgId,
            Guid groupId,
            Guid userId,
            IGrainFactory grainFactory) =>
        {
            var groupGrain = grainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(orgId, groupId));
            if (!await groupGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User group not found"));

            var userGrain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await userGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            // Add user to group (bidirectional)
            await groupGrain.AddMemberAsync(userId);
            await userGrain.AddToGroupAsync(groupId);

            return Results.Ok(new { message = "Member added to group" });
        });

        // Remove member from group
        group.MapDelete("/{groupId}/members/{userId}", async (
            Guid orgId,
            Guid groupId,
            Guid userId,
            IGrainFactory grainFactory) =>
        {
            var groupGrain = grainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(orgId, groupId));
            if (!await groupGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User group not found"));

            var userGrain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
            if (!await userGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User not found"));

            // Remove user from group (bidirectional)
            await groupGrain.RemoveMemberAsync(userId);
            await userGrain.RemoveFromGroupAsync(groupId);

            return Results.NoContent();
        });

        // Bulk add members to group
        group.MapPost("/{groupId}/members", async (
            Guid orgId,
            Guid groupId,
            [FromBody] BulkAddMembersRequest request,
            IGrainFactory grainFactory) =>
        {
            var groupGrain = grainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(orgId, groupId));
            if (!await groupGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User group not found"));

            var added = new List<Guid>();
            var notFound = new List<Guid>();

            foreach (var userId in request.UserIds)
            {
                var userGrain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
                if (await userGrain.ExistsAsync())
                {
                    await groupGrain.AddMemberAsync(userId);
                    await userGrain.AddToGroupAsync(groupId);
                    added.Add(userId);
                }
                else
                {
                    notFound.Add(userId);
                }
            }

            return Results.Ok(new
            {
                message = $"Added {added.Count} members to group",
                added,
                notFound
            });
        });

        // Bulk remove members from group
        group.MapDelete("/{groupId}/members", async (
            Guid orgId,
            Guid groupId,
            [FromBody] BulkRemoveMembersRequest request,
            IGrainFactory grainFactory) =>
        {
            var groupGrain = grainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(orgId, groupId));
            if (!await groupGrain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User group not found"));

            var removed = new List<Guid>();

            foreach (var userId in request.UserIds)
            {
                var userGrain = grainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));
                if (await userGrain.ExistsAsync())
                {
                    await groupGrain.RemoveMemberAsync(userId);
                    await userGrain.RemoveFromGroupAsync(groupId);
                    removed.Add(userId);
                }
            }

            return Results.Ok(new
            {
                message = $"Removed {removed.Count} members from group",
                removed
            });
        });

        // Check if user is member of group
        group.MapGet("/{groupId}/members/{userId}", async (
            Guid orgId,
            Guid groupId,
            Guid userId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(orgId, groupId));
            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "User group not found"));

            var isMember = await grain.HasMemberAsync(userId);
            return Results.Ok(new { isMember });
        });

        return app;
    }

    private static UserGroupResponse MapToResponse(State.UserGroupState state) => new(
        state.Id,
        state.OrganizationId,
        state.Name,
        state.Description,
        state.MemberIds,
        state.IsSystemGroup,
        state.CreatedAt,
        state.UpdatedAt);
}

public record BulkAddMembersRequest(List<Guid> UserIds);
public record BulkRemoveMembersRequest(List<Guid> UserIds);
