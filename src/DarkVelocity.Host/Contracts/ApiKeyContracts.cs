using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

/// <summary>
/// Request to create a new API key.
/// </summary>
public record CreateApiKeyRequest(
    /// <summary>
    /// Human-readable name for the API key.
    /// </summary>
    string Name,

    /// <summary>
    /// Optional description of what this key is used for.
    /// </summary>
    string? Description = null,

    /// <summary>
    /// Type of API key. Secret keys have full access, publishable keys have limited access.
    /// </summary>
    ApiKeyType Type = ApiKeyType.Secret,

    /// <summary>
    /// Whether this is a test mode key (accesses test data only).
    /// </summary>
    bool IsTestMode = false,

    /// <summary>
    /// Permission scopes to grant to this key. If null/empty, inherits from user.
    /// </summary>
    List<ApiKeyScopeRequest>? Scopes = null,

    /// <summary>
    /// Custom claims to include in authentication context.
    /// </summary>
    Dictionary<string, string>? CustomClaims = null,

    /// <summary>
    /// Roles to grant to this key. If null/empty, inherits subset based on key type.
    /// </summary>
    List<string>? Roles = null,

    /// <summary>
    /// Restrict key to specific site IDs. If null/empty, can access all user's sites.
    /// </summary>
    List<Guid>? AllowedSiteIds = null,

    /// <summary>
    /// Restrict key to specific IP addresses or CIDR ranges.
    /// </summary>
    List<string>? AllowedIpRanges = null,

    /// <summary>
    /// Rate limit per minute. 0 or null means use default.
    /// </summary>
    int? RateLimitPerMinute = null,

    /// <summary>
    /// When the key expires. Null means no expiration.
    /// </summary>
    DateTime? ExpiresAt = null);

/// <summary>
/// Scope definition in request format.
/// </summary>
public record ApiKeyScopeRequest(
    /// <summary>
    /// Resource type (e.g., "orders", "customers", "menu").
    /// </summary>
    string Resource,

    /// <summary>
    /// Actions allowed on the resource (e.g., "read", "write", "delete").
    /// </summary>
    List<string> Actions);

/// <summary>
/// Request to update an API key.
/// </summary>
public record UpdateApiKeyRequest(
    string? Name = null,
    string? Description = null,
    List<ApiKeyScopeRequest>? Scopes = null,
    Dictionary<string, string>? CustomClaims = null,
    List<string>? Roles = null,
    List<Guid>? AllowedSiteIds = null,
    List<string>? AllowedIpRanges = null,
    int? RateLimitPerMinute = null,
    DateTime? ExpiresAt = null);

/// <summary>
/// Request to revoke an API key.
/// </summary>
public record RevokeApiKeyRequest(
    /// <summary>
    /// Optional reason for revocation.
    /// </summary>
    string? Reason = null);

/// <summary>
/// Response when an API key is created. Contains the actual key which is only shown once.
/// </summary>
public record CreateApiKeyResponse(
    /// <summary>
    /// The API key ID.
    /// </summary>
    Guid Id,

    /// <summary>
    /// The full API key. Store this securely - it will not be shown again.
    /// </summary>
    string ApiKey,

    /// <summary>
    /// The key prefix for display purposes.
    /// </summary>
    string KeyPrefix,

    /// <summary>
    /// When the key was created.
    /// </summary>
    DateTime CreatedAt);

/// <summary>
/// Summary of an API key for listing purposes.
/// </summary>
public record ApiKeyResponse(
    Guid Id,
    string Name,
    string? Description,
    string KeyPrefix,
    ApiKeyType Type,
    bool IsTestMode,
    ApiKeyStatus Status,
    List<ApiKeyScopeResponse> Scopes,
    Dictionary<string, string> CustomClaims,
    List<string> Roles,
    List<Guid> AllowedSiteIds,
    List<string> AllowedIpRanges,
    int RateLimitPerMinute,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    long UsageCount);

/// <summary>
/// Scope in response format.
/// </summary>
public record ApiKeyScopeResponse(
    string Resource,
    List<string> Actions);

/// <summary>
/// Brief summary of an API key for list views.
/// </summary>
public record ApiKeyListItem(
    Guid Id,
    string Name,
    string? Description,
    string KeyPrefix,
    ApiKeyType Type,
    bool IsTestMode,
    ApiKeyStatus Status,
    List<string> ScopeResources,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt);

/// <summary>
/// Available scopes that can be granted to API keys.
/// </summary>
public record AvailableScopesResponse(
    List<AvailableScope> Scopes);

/// <summary>
/// Definition of an available scope.
/// </summary>
public record AvailableScope(
    string Resource,
    string DisplayName,
    string Description,
    List<string> AvailableActions);
