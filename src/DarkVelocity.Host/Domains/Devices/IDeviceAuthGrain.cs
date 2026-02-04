using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Device Authorization Flow Commands and Results (RFC 8628)
// ============================================================================

[GenerateSerializer]
public record DeviceCodeRequest(
    [property: Id(0)] string ClientId,
    [property: Id(1)] string Scope,
    [property: Id(2)] string? DeviceFingerprint = null,
    [property: Id(3)] string? IpAddress = null);

[GenerateSerializer]
public record DeviceCodeResponse(
    [property: Id(0)] string DeviceCode,
    [property: Id(1)] string UserCode,
    [property: Id(2)] string VerificationUri,
    [property: Id(3)] string VerificationUriComplete,
    [property: Id(4)] int ExpiresIn,
    [property: Id(5)] int Interval);

[GenerateSerializer]
public record DeviceTokenResponse(
    [property: Id(0)] string AccessToken,
    [property: Id(1)] string RefreshToken,
    [property: Id(2)] Guid DeviceId,
    [property: Id(3)] Guid OrganizationId,
    [property: Id(4)] Guid SiteId,
    [property: Id(5)] int ExpiresIn,
    [property: Id(6)] string TokenType = "Bearer");

[GenerateSerializer]
public record AuthorizeDeviceCommand(
    [property: Id(0)] Guid AuthorizedBy,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] string DeviceName,
    [property: Id(4)] DeviceType AppType);

/// <summary>
/// Grain for managing device authorization flow (OAuth 2.0 Device Authorization Grant).
/// Key: user_code (e.g., "ABCD1234")
/// Short-lived grain that expires after device authorization completes.
/// </summary>
public interface IDeviceAuthGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initiates the device authorization flow.
    /// </summary>
    Task<DeviceCodeResponse> InitiateAsync(DeviceCodeRequest request);

    /// <summary>
    /// Gets the current authorization status.
    /// </summary>
    Task<DeviceAuthStatus> GetStatusAsync();

    /// <summary>
    /// Gets the token if authorized, null otherwise.
    /// </summary>
    Task<DeviceTokenResponse?> GetTokenAsync(string deviceCode);

    /// <summary>
    /// Authorizes the device (called by authenticated user from browser).
    /// </summary>
    Task AuthorizeAsync(AuthorizeDeviceCommand command);

    /// <summary>
    /// Denies the authorization request.
    /// </summary>
    Task DenyAsync(string reason);

    /// <summary>
    /// Checks if the authorization has expired.
    /// </summary>
    Task<bool> IsExpiredAsync();
}

// ============================================================================
// Device Management Commands and Results
// ============================================================================

[GenerateSerializer]
public record RegisterDeviceCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string Name,
    [property: Id(3)] DeviceType Type,
    [property: Id(4)] string? Fingerprint,
    [property: Id(5)] Guid AuthorizedBy);

[GenerateSerializer]
public record DeviceSnapshot(
    [property: Id(0)] Guid Id,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] string Name,
    [property: Id(4)] DeviceType Type,
    [property: Id(5)] DeviceStatus Status,
    [property: Id(6)] DateTime AuthorizedAt,
    [property: Id(7)] DateTime? LastSeenAt,
    [property: Id(8)] Guid? CurrentUserId);

/// <summary>
/// Grain for managing authorized devices.
/// Key: "{orgId}:device:{deviceId}"
/// </summary>
public interface IDeviceGrain : IGrainWithStringKey
{
    Task<DeviceSnapshot> RegisterAsync(RegisterDeviceCommand command);
    Task<DeviceSnapshot> GetSnapshotAsync();
    Task SuspendAsync(string reason);
    Task RevokeAsync(string reason);
    Task ReactivateAsync();
    Task SetCurrentUserAsync(Guid? userId);
    Task RecordHeartbeatAsync(string? appVersion);
    Task<bool> IsAuthorizedAsync();
    Task<bool> ExistsAsync();
}

// ============================================================================
// Session Management Commands and Results
// ============================================================================

[GenerateSerializer]
public record CreateSessionCommand(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] Guid? SiteId,
    [property: Id(3)] Guid? DeviceId,
    [property: Id(4)] string AuthMethod,
    [property: Id(5)] string? IpAddress,
    [property: Id(6)] string? UserAgent);

[GenerateSerializer]
public record SessionTokens(
    [property: Id(0)] string AccessToken,
    [property: Id(1)] string RefreshToken,
    [property: Id(2)] DateTime AccessTokenExpires,
    [property: Id(3)] DateTime RefreshTokenExpires);

[GenerateSerializer]
public record RefreshResult(
    [property: Id(0)] bool Success,
    [property: Id(1)] SessionTokens? Tokens = null,
    [property: Id(2)] string? Error = null);

/// <summary>
/// Grain for managing user sessions and JWT tokens.
/// Key: "{orgId}:session:{sessionId}"
/// </summary>
public interface ISessionGrain : IGrainWithStringKey
{
    Task<SessionTokens> CreateAsync(CreateSessionCommand command);
    Task<RefreshResult> RefreshAsync(string refreshToken);
    Task RevokeAsync();
    Task<bool> IsValidAsync();
    Task<SessionState> GetStateAsync();
    Task RecordActivityAsync();
}

// ============================================================================
// User Lookup (for PIN login within organization)
// ============================================================================

[GenerateSerializer]
public record UserLookupResult(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] Guid OrganizationId);

/// <summary>
/// Summary of a user available for PIN login.
/// </summary>
[GenerateSerializer]
public record PinUserSummary(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] string? FirstName,
    [property: Id(3)] string? LastName,
    [property: Id(4)] string? AvatarUrl);

/// <summary>
/// Grain for looking up users within an organization (e.g., by PIN).
/// Key: "{orgId}:userlookup"
/// </summary>
public interface IUserLookupGrain : IGrainWithStringKey
{
    /// <summary>
    /// Registers a user's PIN for lookup.
    /// </summary>
    Task RegisterPinAsync(Guid userId, string pinHash);

    /// <summary>
    /// Unregisters a user's PIN.
    /// </summary>
    Task UnregisterPinAsync(Guid userId);

    /// <summary>
    /// Finds a user by their PIN hash within the organization.
    /// Optionally filters by site access.
    /// </summary>
    Task<UserLookupResult?> FindByPinHashAsync(string pinHash, Guid? siteId = null);

    /// <summary>
    /// Gets all users with PINs set who have access to the specified site.
    /// Used for OAuth-style PIN authentication user selection.
    /// </summary>
    Task<IReadOnlyList<PinUserSummary>> GetUsersForSiteAsync(Guid siteId);
}

// ============================================================================
// Refresh Token Lookup (for OAuth token refresh)
// ============================================================================

/// <summary>
/// Result of looking up a refresh token.
/// </summary>
[GenerateSerializer]
public record RefreshTokenLookupResult(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SessionId);

/// <summary>
/// Global grain for looking up refresh tokens to their sessions.
/// Maps SHA256(refresh_token) -> (orgId, sessionId).
/// Key: "global:refreshtokenlookup"
/// </summary>
public interface IRefreshTokenLookupGrain : IGrainWithStringKey
{
    /// <summary>
    /// Registers a refresh token hash to a session.
    /// </summary>
    Task RegisterAsync(string refreshTokenHash, Guid organizationId, Guid sessionId);

    /// <summary>
    /// Looks up a session by refresh token hash.
    /// </summary>
    Task<RefreshTokenLookupResult?> LookupAsync(string refreshTokenHash);

    /// <summary>
    /// Removes a refresh token mapping (called when token is rotated or session revoked).
    /// </summary>
    Task RemoveAsync(string refreshTokenHash);

    /// <summary>
    /// Updates the refresh token mapping during token rotation.
    /// Removes the old hash and registers the new one atomically.
    /// </summary>
    Task RotateAsync(string oldRefreshTokenHash, string newRefreshTokenHash, Guid organizationId, Guid sessionId);
}
