namespace DarkVelocity.Host.Authorization;

/// <summary>
/// Service for managing authorization relationships in SpiceDB.
/// Handles session creation/revocation and role assignments.
/// </summary>
public interface IAuthorizationService
{
    /// <summary>
    /// Creates an active session for a user at a site with the specified auth scope.
    /// Called at login time.
    /// </summary>
    /// <param name="userId">The user's ID</param>
    /// <param name="orgId">The organization ID</param>
    /// <param name="siteId">The site ID</param>
    /// <param name="authMethod">The authentication method ("pin", "oauth", "password")</param>
    Task CreateSessionAsync(Guid userId, Guid orgId, Guid siteId, string authMethod, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes the active session for a user at a site.
    /// Called at logout time.
    /// </summary>
    Task RevokeSessionAsync(Guid userId, Guid orgId, Guid siteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a role to a user at a site.
    /// </summary>
    Task AssignSiteRoleAsync(Guid userId, Guid orgId, Guid siteId, SiteRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a role from a user at a site.
    /// </summary>
    Task RemoveSiteRoleAsync(Guid userId, Guid orgId, Guid siteId, SiteRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a role to a user at the organization level.
    /// </summary>
    Task AssignOrgRoleAsync(Guid userId, Guid orgId, OrgRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a role from a user at the organization level.
    /// </summary>
    Task RemoveOrgRoleAsync(Guid userId, Guid orgId, OrgRole role, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets up organization-to-site relationship.
    /// Called when creating a site.
    /// </summary>
    Task SetupSiteAsync(Guid orgId, Guid siteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets up resource-to-site relationships.
    /// Called when creating site-scoped resources.
    /// </summary>
    Task SetupSiteResourceAsync(string resourceType, string resourceId, Guid orgId, Guid siteId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets up resource-to-organization relationships.
    /// Called when creating org-scoped resources.
    /// </summary>
    Task SetupOrgResourceAsync(string resourceType, string resourceId, Guid orgId, CancellationToken cancellationToken = default);
}

public enum SiteRole
{
    Staff,
    Supervisor,
    Manager
}

public enum OrgRole
{
    Member,
    Admin,
    Owner
}

public sealed class AuthorizationService : IAuthorizationService
{
    private readonly ISpiceDbClient _spiceDb;
    private readonly ILogger<AuthorizationService> _logger;

    public AuthorizationService(ISpiceDbClient spiceDb, ILogger<AuthorizationService> logger)
    {
        _spiceDb = spiceDb;
        _logger = logger;
    }

    public async Task CreateSessionAsync(Guid userId, Guid orgId, Guid siteId, string authMethod, CancellationToken cancellationToken = default)
    {
        // Determine scope based on auth method
        var scope = authMethod switch
        {
            "pin" => "pos",
            "oauth" => "full",
            "password" => "full",
            _ => "pos" // Default to restricted scope for unknown methods
        };

        var siteResourceId = $"{orgId}:{siteId}";

        _logger.LogInformation(
            "Creating session for user {UserId} at site {SiteId} with scope {Scope} (auth method: {AuthMethod})",
            userId, siteId, scope, authMethod);

        await _spiceDb.WriteRelationshipAsync(
            resourceType: ResourceTypes.Site,
            resourceId: siteResourceId,
            relation: "active_session",
            subjectType: "user",
            subjectId: userId.ToString(),
            caveatName: "session_scope",
            caveatContext: new Dictionary<string, object> { ["scope"] = scope },
            cancellationToken: cancellationToken);
    }

    public async Task RevokeSessionAsync(Guid userId, Guid orgId, Guid siteId, CancellationToken cancellationToken = default)
    {
        var siteResourceId = $"{orgId}:{siteId}";

        _logger.LogInformation("Revoking session for user {UserId} at site {SiteId}", userId, siteId);

        await _spiceDb.DeleteRelationshipAsync(
            resourceType: ResourceTypes.Site,
            resourceId: siteResourceId,
            relation: "active_session",
            subjectType: "user",
            subjectId: userId.ToString(),
            cancellationToken: cancellationToken);
    }

    public async Task AssignSiteRoleAsync(Guid userId, Guid orgId, Guid siteId, SiteRole role, CancellationToken cancellationToken = default)
    {
        var siteResourceId = $"{orgId}:{siteId}";
        var relation = role.ToString().ToLowerInvariant();

        _logger.LogInformation("Assigning {Role} role to user {UserId} at site {SiteId}", role, userId, siteId);

        await _spiceDb.WriteRelationshipAsync(
            resourceType: ResourceTypes.Site,
            resourceId: siteResourceId,
            relation: relation,
            subjectType: "user",
            subjectId: userId.ToString(),
            cancellationToken: cancellationToken);
    }

    public async Task RemoveSiteRoleAsync(Guid userId, Guid orgId, Guid siteId, SiteRole role, CancellationToken cancellationToken = default)
    {
        var siteResourceId = $"{orgId}:{siteId}";
        var relation = role.ToString().ToLowerInvariant();

        _logger.LogInformation("Removing {Role} role from user {UserId} at site {SiteId}", role, userId, siteId);

        await _spiceDb.DeleteRelationshipAsync(
            resourceType: ResourceTypes.Site,
            resourceId: siteResourceId,
            relation: relation,
            subjectType: "user",
            subjectId: userId.ToString(),
            cancellationToken: cancellationToken);
    }

    public async Task AssignOrgRoleAsync(Guid userId, Guid orgId, OrgRole role, CancellationToken cancellationToken = default)
    {
        var relation = role.ToString().ToLowerInvariant();

        _logger.LogInformation("Assigning {Role} role to user {UserId} at org {OrgId}", role, userId, orgId);

        await _spiceDb.WriteRelationshipAsync(
            resourceType: ResourceTypes.Organization,
            resourceId: orgId.ToString(),
            relation: relation,
            subjectType: "user",
            subjectId: userId.ToString(),
            cancellationToken: cancellationToken);
    }

    public async Task RemoveOrgRoleAsync(Guid userId, Guid orgId, OrgRole role, CancellationToken cancellationToken = default)
    {
        var relation = role.ToString().ToLowerInvariant();

        _logger.LogInformation("Removing {Role} role from user {UserId} at org {OrgId}", role, userId, orgId);

        await _spiceDb.DeleteRelationshipAsync(
            resourceType: ResourceTypes.Organization,
            resourceId: orgId.ToString(),
            relation: relation,
            subjectType: "user",
            subjectId: userId.ToString(),
            cancellationToken: cancellationToken);
    }

    public async Task SetupSiteAsync(Guid orgId, Guid siteId, CancellationToken cancellationToken = default)
    {
        var siteResourceId = $"{orgId}:{siteId}";

        _logger.LogInformation("Setting up site {SiteId} under org {OrgId}", siteId, orgId);

        await _spiceDb.WriteRelationshipAsync(
            resourceType: ResourceTypes.Site,
            resourceId: siteResourceId,
            relation: "organization",
            subjectType: ResourceTypes.Organization,
            subjectId: orgId.ToString(),
            cancellationToken: cancellationToken);
    }

    public async Task SetupSiteResourceAsync(string resourceType, string resourceId, Guid orgId, Guid siteId, CancellationToken cancellationToken = default)
    {
        var siteResourceId = $"{orgId}:{siteId}";

        _logger.LogDebug("Setting up {ResourceType}:{ResourceId} under site {SiteId}", resourceType, resourceId, siteId);

        await _spiceDb.WriteRelationshipAsync(
            resourceType: resourceType,
            resourceId: resourceId,
            relation: "site",
            subjectType: ResourceTypes.Site,
            subjectId: siteResourceId,
            cancellationToken: cancellationToken);
    }

    public async Task SetupOrgResourceAsync(string resourceType, string resourceId, Guid orgId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Setting up {ResourceType}:{ResourceId} under org {OrgId}", resourceType, resourceId, orgId);

        await _spiceDb.WriteRelationshipAsync(
            resourceType: resourceType,
            resourceId: resourceId,
            relation: "organization",
            subjectType: ResourceTypes.Organization,
            subjectId: orgId.ToString(),
            cancellationToken: cancellationToken);
    }
}
