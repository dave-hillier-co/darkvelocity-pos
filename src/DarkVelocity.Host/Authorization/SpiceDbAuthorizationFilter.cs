using System.Security.Claims;

namespace DarkVelocity.Host.Authorization;

/// <summary>
/// Endpoint filter that performs SpiceDB permission checks based on RequirePermissionAttribute.
/// </summary>
public sealed class SpiceDbAuthorizationFilter : IEndpointFilter
{
    private readonly ISpiceDbClient _spiceDb;
    private readonly ILogger<SpiceDbAuthorizationFilter> _logger;

    public SpiceDbAuthorizationFilter(ISpiceDbClient spiceDb, ILogger<SpiceDbAuthorizationFilter> logger)
    {
        _spiceDb = spiceDb;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        var requirement = endpoint?.Metadata.GetMetadata<RequirePermissionAttribute>();

        // No permission requirement, proceed
        if (requirement is null)
            return await next(context);

        var user = context.HttpContext.User;

        // Check if user is authenticated
        if (user.Identity?.IsAuthenticated != true)
        {
            _logger.LogWarning("Unauthenticated request to protected endpoint");
            return Results.Unauthorized();
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("No user ID claim found in token");
            return Results.Unauthorized();
        }

        // Build resource ID from route values
        string resourceId;
        try
        {
            resourceId = requirement.BuildResourceId(context.HttpContext.Request.RouteValues);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to build resource ID for permission check");
            return Results.BadRequest(new { error = "invalid_request", error_description = ex.Message });
        }

        // Perform permission check
        var allowed = await _spiceDb.CheckPermissionAsync(
            resourceType: requirement.ResourceType,
            resourceId: resourceId,
            permission: requirement.Permission,
            subjectType: "user",
            subjectId: userId,
            cancellationToken: context.HttpContext.RequestAborted);

        if (!allowed)
        {
            _logger.LogInformation(
                "Permission denied: user {UserId} lacks {Permission} on {ResourceType}:{ResourceId}",
                userId, requirement.Permission, requirement.ResourceType, resourceId);

            return Results.Json(
                new { error = "forbidden", error_description = "You do not have permission to perform this action" },
                statusCode: StatusCodes.Status403Forbidden);
        }

        _logger.LogDebug(
            "Permission granted: user {UserId} has {Permission} on {ResourceType}:{ResourceId}",
            userId, requirement.Permission, requirement.ResourceType, resourceId);

        return await next(context);
    }
}

/// <summary>
/// Extension methods for adding SpiceDB authorization to endpoint groups.
/// </summary>
public static class SpiceDbAuthorizationExtensions
{
    /// <summary>
    /// Adds SpiceDB authorization filter to the route group.
    /// </summary>
    public static RouteGroupBuilder RequireSpiceDbAuthorization(this RouteGroupBuilder group)
    {
        return group.AddEndpointFilter<SpiceDbAuthorizationFilter>();
    }

    /// <summary>
    /// Adds SpiceDB authorization filter to a single endpoint.
    /// </summary>
    public static RouteHandlerBuilder RequireSpiceDbAuthorization(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter<SpiceDbAuthorizationFilter>();
    }
}
