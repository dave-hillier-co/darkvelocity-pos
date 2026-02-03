using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Command to create a new API key.
/// </summary>
[GenerateSerializer]
public record CreateApiKeyCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] string Name,
    [property: Id(3)] string? Description,
    [property: Id(4)] ApiKeyType Type,
    [property: Id(5)] bool IsTestMode,
    [property: Id(6)] List<ApiKeyScope>? Scopes,
    [property: Id(7)] Dictionary<string, string>? CustomClaims,
    [property: Id(8)] List<string>? Roles,
    [property: Id(9)] List<Guid>? AllowedSiteIds,
    [property: Id(10)] List<string>? AllowedIpRanges,
    [property: Id(11)] int? RateLimitPerMinute,
    [property: Id(12)] DateTime? ExpiresAt);

/// <summary>
/// Result of creating an API key.
/// </summary>
[GenerateSerializer]
public record ApiKeyCreatedResult(
    [property: Id(0)] Guid Id,
    [property: Id(1)] string ApiKey,
    [property: Id(2)] string KeyPrefix,
    [property: Id(3)] DateTime CreatedAt);

/// <summary>
/// Command to update an API key.
/// </summary>
[GenerateSerializer]
public record UpdateApiKeyCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] string? Description,
    [property: Id(2)] List<ApiKeyScope>? Scopes,
    [property: Id(3)] Dictionary<string, string>? CustomClaims,
    [property: Id(4)] List<string>? Roles,
    [property: Id(5)] List<Guid>? AllowedSiteIds,
    [property: Id(6)] List<string>? AllowedIpRanges,
    [property: Id(7)] int? RateLimitPerMinute,
    [property: Id(8)] DateTime? ExpiresAt);

/// <summary>
/// Result of validating an API key.
/// </summary>
[GenerateSerializer]
public record ApiKeyValidationResult(
    [property: Id(0)] bool IsValid,
    [property: Id(1)] string? Error,
    [property: Id(2)] Guid? KeyId,
    [property: Id(3)] Guid? UserId,
    [property: Id(4)] Guid? OrganizationId,
    [property: Id(5)] ApiKeyType? Type,
    [property: Id(6)] bool IsTestMode,
    [property: Id(7)] List<ApiKeyScope>? Scopes,
    [property: Id(8)] Dictionary<string, string>? CustomClaims,
    [property: Id(9)] List<string>? Roles,
    [property: Id(10)] List<Guid>? AllowedSiteIds,
    [property: Id(11)] int RateLimitPerMinute);

/// <summary>
/// Summary information about an API key (for listing).
/// </summary>
[GenerateSerializer]
public record ApiKeySummary(
    [property: Id(0)] Guid Id,
    [property: Id(1)] string Name,
    [property: Id(2)] string? Description,
    [property: Id(3)] string KeyPrefix,
    [property: Id(4)] ApiKeyType Type,
    [property: Id(5)] bool IsTestMode,
    [property: Id(6)] ApiKeyStatus Status,
    [property: Id(7)] List<string> Scopes,
    [property: Id(8)] DateTime CreatedAt,
    [property: Id(9)] DateTime? ExpiresAt,
    [property: Id(10)] DateTime? LastUsedAt);

/// <summary>
/// Grain for managing an individual API key.
/// Key pattern: {orgId}:apikey:{keyId}
/// </summary>
public interface IApiKeyGrain : IGrainWithStringKey
{
    /// <summary>
    /// Creates a new API key with the specified settings.
    /// </summary>
    Task<ApiKeyCreatedResult> CreateAsync(CreateApiKeyCommand command);

    /// <summary>
    /// Gets the current state of the API key.
    /// </summary>
    Task<UserApiKeyState> GetStateAsync();

    /// <summary>
    /// Updates the API key settings. Cannot change key type or mode.
    /// </summary>
    Task UpdateAsync(UpdateApiKeyCommand command);

    /// <summary>
    /// Revokes the API key, making it permanently unusable.
    /// </summary>
    Task RevokeAsync(Guid revokedBy, string? reason);

    /// <summary>
    /// Validates the provided key against the stored hash.
    /// Returns validation result with key metadata if valid.
    /// </summary>
    Task<ApiKeyValidationResult> ValidateAsync(string apiKey, string? ipAddress);

    /// <summary>
    /// Records a usage of this API key.
    /// </summary>
    Task RecordUsageAsync(string? ipAddress);

    /// <summary>
    /// Checks if this API key exists.
    /// </summary>
    Task<bool> ExistsAsync();

    /// <summary>
    /// Gets a summary of the API key for listing purposes.
    /// </summary>
    Task<ApiKeySummary> GetSummaryAsync();
}

/// <summary>
/// Grain for managing the collection of API keys for a user.
/// Key pattern: {orgId}:apikeyregistry:{userId}
/// </summary>
public interface IApiKeyRegistryGrain : IGrainWithStringKey
{
    /// <summary>
    /// Registers a new API key for the user.
    /// </summary>
    Task RegisterKeyAsync(Guid keyId, string keyHash);

    /// <summary>
    /// Removes a key from the registry (when revoked/deleted).
    /// </summary>
    Task UnregisterKeyAsync(Guid keyId, string keyHash);

    /// <summary>
    /// Gets all API key IDs for the user.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetKeyIdsAsync();

    /// <summary>
    /// Looks up a key ID by its hash (for authentication).
    /// </summary>
    Task<Guid?> FindKeyIdByHashAsync(string keyHash);

    /// <summary>
    /// Checks if the registry exists.
    /// </summary>
    Task<bool> ExistsAsync();

    /// <summary>
    /// Initializes the registry for a user.
    /// </summary>
    Task InitializeAsync(Guid organizationId, Guid userId);
}

/// <summary>
/// Global lookup grain for finding API keys by their hash.
/// Key pattern: global:apikeylookup
/// </summary>
public interface IApiKeyLookupGrain : IGrainWithStringKey
{
    /// <summary>
    /// Registers a key hash to its organization and key ID for fast lookup.
    /// </summary>
    Task RegisterAsync(string keyHash, Guid organizationId, Guid keyId);

    /// <summary>
    /// Unregisters a key hash.
    /// </summary>
    Task UnregisterAsync(string keyHash);

    /// <summary>
    /// Looks up a key by its hash. Returns org ID and key ID if found.
    /// </summary>
    Task<(Guid OrganizationId, Guid KeyId)?> LookupAsync(string keyHash);
}
