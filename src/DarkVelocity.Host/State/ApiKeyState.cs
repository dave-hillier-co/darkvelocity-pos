namespace DarkVelocity.Host.State;

/// <summary>
/// Status of an API key.
/// </summary>
public enum ApiKeyStatus
{
    /// <summary>
    /// Key is active and can be used for authentication.
    /// </summary>
    Active,

    /// <summary>
    /// Key has been revoked and can no longer be used.
    /// </summary>
    Revoked,

    /// <summary>
    /// Key has expired based on its expiration date.
    /// </summary>
    Expired
}

/// <summary>
/// Type of API key determining base access level.
/// </summary>
public enum ApiKeyType
{
    /// <summary>
    /// Secret key with full API access (server-side use only).
    /// </summary>
    Secret,

    /// <summary>
    /// Publishable key with limited access (safe for client-side use).
    /// </summary>
    Publishable
}

/// <summary>
/// Represents a permission scope that can be granted to an API key.
/// Scopes allow fine-grained control over what operations the key can perform.
/// </summary>
[GenerateSerializer]
public record ApiKeyScope
{
    /// <summary>
    /// The resource type this scope applies to (e.g., "orders", "customers", "menu").
    /// </summary>
    [Id(0)] public required string Resource { get; init; }

    /// <summary>
    /// The actions permitted on the resource (e.g., "read", "write", "delete").
    /// </summary>
    [Id(1)] public required List<string> Actions { get; init; }
}

/// <summary>
/// State for a user-issued API key with custom claims support.
/// Users can issue themselves API keys with a subset of their functionality.
/// Named UserApiKeyState to distinguish from PaymentGatewayState.ApiKeyState.
/// </summary>
[GenerateSerializer]
public sealed class UserApiKeyState
{
    /// <summary>
    /// Unique identifier for the API key.
    /// </summary>
    [Id(0)] public Guid Id { get; set; }

    /// <summary>
    /// The organization this key belongs to.
    /// </summary>
    [Id(1)] public Guid OrganizationId { get; set; }

    /// <summary>
    /// The user who created/owns this API key.
    /// </summary>
    [Id(2)] public Guid UserId { get; set; }

    /// <summary>
    /// Human-readable name for the API key (e.g., "Production Integration", "Testing").
    /// </summary>
    [Id(3)] public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what this key is used for.
    /// </summary>
    [Id(4)] public string? Description { get; set; }

    /// <summary>
    /// SHA-256 hash of the API key. The actual key is only shown once at creation.
    /// </summary>
    [Id(5)] public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// Prefix of the key for display purposes (e.g., "sk_live_abc...").
    /// </summary>
    [Id(6)] public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Type of API key (Secret or Publishable).
    /// </summary>
    [Id(7)] public ApiKeyType Type { get; set; } = ApiKeyType.Secret;

    /// <summary>
    /// Whether this is a test mode key (accesses test data only).
    /// </summary>
    [Id(8)] public bool IsTestMode { get; set; }

    /// <summary>
    /// Current status of the key.
    /// </summary>
    [Id(9)] public ApiKeyStatus Status { get; set; } = ApiKeyStatus.Active;

    /// <summary>
    /// Permission scopes granted to this key.
    /// If empty, the key inherits all permissions from the user (subject to key type).
    /// </summary>
    [Id(10)] public List<ApiKeyScope> Scopes { get; set; } = [];

    /// <summary>
    /// Custom claims to include in the authentication context.
    /// These can be used for fine-grained authorization checks.
    /// </summary>
    [Id(11)] public Dictionary<string, string> CustomClaims { get; set; } = [];

    /// <summary>
    /// Roles explicitly granted to this key.
    /// If empty, inherits a subset of the user's roles based on key type.
    /// </summary>
    [Id(12)] public List<string> Roles { get; set; } = [];

    /// <summary>
    /// Specific site IDs this key is restricted to.
    /// If empty, the key can access all sites the user has access to.
    /// </summary>
    [Id(13)] public List<Guid> AllowedSiteIds { get; set; } = [];

    /// <summary>
    /// IP addresses or CIDR ranges allowed to use this key.
    /// If empty, no IP restrictions are applied.
    /// </summary>
    [Id(14)] public List<string> AllowedIpRanges { get; set; } = [];

    /// <summary>
    /// Rate limit per minute for this key. 0 means no limit (uses default).
    /// </summary>
    [Id(15)] public int RateLimitPerMinute { get; set; }

    /// <summary>
    /// When the key was created.
    /// </summary>
    [Id(16)] public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the key was last updated.
    /// </summary>
    [Id(17)] public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// When the key expires. Null means no expiration.
    /// </summary>
    [Id(18)] public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// When the key was last used for authentication.
    /// </summary>
    [Id(19)] public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// IP address from the last usage.
    /// </summary>
    [Id(20)] public string? LastUsedFromIp { get; set; }

    /// <summary>
    /// Total number of times this key has been used.
    /// </summary>
    [Id(21)] public long UsageCount { get; set; }

    /// <summary>
    /// Version for optimistic concurrency.
    /// </summary>
    [Id(22)] public int Version { get; set; }

    /// <summary>
    /// Why the key was revoked (if revoked).
    /// </summary>
    [Id(23)] public string? RevocationReason { get; set; }

    /// <summary>
    /// When the key was revoked.
    /// </summary>
    [Id(24)] public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Who revoked the key.
    /// </summary>
    [Id(25)] public Guid? RevokedBy { get; set; }
}

/// <summary>
/// State for the user API key registry - tracks all API keys for a user.
/// </summary>
[GenerateSerializer]
public sealed class UserApiKeyRegistryState
{
    /// <summary>
    /// The user this registry belongs to.
    /// </summary>
    [Id(0)] public Guid UserId { get; set; }

    /// <summary>
    /// The organization ID.
    /// </summary>
    [Id(1)] public Guid OrganizationId { get; set; }

    /// <summary>
    /// All API key IDs owned by this user.
    /// </summary>
    [Id(2)] public List<Guid> ApiKeyIds { get; set; } = [];

    /// <summary>
    /// Index of key hashes to key IDs for fast lookup during authentication.
    /// </summary>
    [Id(3)] public Dictionary<string, Guid> KeyHashIndex { get; set; } = [];

    /// <summary>
    /// When the registry was created.
    /// </summary>
    [Id(4)] public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the registry was last updated.
    /// </summary>
    [Id(5)] public DateTime? UpdatedAt { get; set; }
}
