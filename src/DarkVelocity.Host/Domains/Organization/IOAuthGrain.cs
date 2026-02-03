using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// OAuth authorization state for CSRF protection and flow tracking.
/// Key: state token (random string)
/// </summary>
public interface IOAuthStateGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initializes the OAuth state with flow parameters.
    /// </summary>
    Task InitializeAsync(OAuthStateRequest request);

    /// <summary>
    /// Validates and consumes the state (one-time use).
    /// </summary>
    Task<OAuthStateValidation> ValidateAndConsumeAsync();

    /// <summary>
    /// Gets the current state without consuming it.
    /// </summary>
    Task<OAuthFlowState> GetStateAsync();
}

/// <summary>
/// Maps external OAuth identities to internal users.
/// Key: provider:externalId (e.g., "google:123456789")
/// </summary>
public interface IExternalIdentityGrain : IGrainWithStringKey
{
    /// <summary>
    /// Links this external identity to a user.
    /// </summary>
    Task LinkAsync(LinkExternalIdentityCommand command);

    /// <summary>
    /// Gets the linked user ID, or null if not linked.
    /// </summary>
    Task<ExternalIdentityInfo?> GetLinkedUserAsync();

    /// <summary>
    /// Updates the external identity info (email, name changes from provider).
    /// </summary>
    Task UpdateInfoAsync(string? email, string? name, string? pictureUrl);

    /// <summary>
    /// Unlinks this external identity from the user.
    /// </summary>
    Task UnlinkAsync();
}

/// <summary>
/// Manages OAuth authorization codes for the authorization code flow.
/// Key: authorization code
/// </summary>
public interface IAuthorizationCodeGrain : IGrainWithStringKey
{
    /// <summary>
    /// Issues an authorization code for the authenticated user.
    /// </summary>
    Task IssueAsync(AuthorizationCodeRequest request);

    /// <summary>
    /// Exchanges the code for tokens (one-time use).
    /// </summary>
    Task<AuthorizationCodeExchange?> ExchangeAsync(string clientId, string? codeVerifier);

    /// <summary>
    /// Checks if the code is still valid.
    /// </summary>
    Task<bool> IsValidAsync();
}

// ============================================================================
// Request/Response Records
// ============================================================================

[GenerateSerializer]
public record OAuthStateRequest(
    [property: Id(0)] string Provider,
    [property: Id(1)] string ReturnUrl,
    [property: Id(2)] string? CodeChallenge = null,
    [property: Id(3)] string? CodeChallengeMethod = null,
    [property: Id(4)] string? ClientId = null,
    [property: Id(5)] string? Nonce = null,
    [property: Id(6)] string? Scope = null);

[GenerateSerializer]
public record OAuthStateValidation(
    [property: Id(0)] bool IsValid,
    [property: Id(1)] string? Provider = null,
    [property: Id(2)] string? ReturnUrl = null,
    [property: Id(3)] string? CodeChallenge = null,
    [property: Id(4)] string? CodeChallengeMethod = null,
    [property: Id(5)] string? ClientId = null,
    [property: Id(6)] string? Nonce = null,
    [property: Id(7)] string? Scope = null,
    [property: Id(8)] string? Error = null);

[GenerateSerializer]
public record LinkExternalIdentityCommand(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] string Provider,
    [property: Id(3)] string ExternalId,
    [property: Id(4)] string? Email = null,
    [property: Id(5)] string? Name = null,
    [property: Id(6)] string? PictureUrl = null);

[GenerateSerializer]
public record ExternalIdentityInfo(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] string Provider,
    [property: Id(3)] string ExternalId,
    [property: Id(4)] string? Email,
    [property: Id(5)] string? Name,
    [property: Id(6)] string? PictureUrl,
    [property: Id(7)] DateTime LinkedAt,
    [property: Id(8)] DateTime? LastUsedAt);

[GenerateSerializer]
public record AuthorizationCodeRequest(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] string ClientId,
    [property: Id(3)] string RedirectUri,
    [property: Id(4)] string? Scope = null,
    [property: Id(5)] string? CodeChallenge = null,
    [property: Id(6)] string? CodeChallengeMethod = null,
    [property: Id(7)] string? Nonce = null,
    [property: Id(8)] string? DisplayName = null,
    [property: Id(9)] IReadOnlyList<string>? Roles = null);

[GenerateSerializer]
public record AuthorizationCodeExchange(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] string? DisplayName,
    [property: Id(3)] IReadOnlyList<string>? Roles,
    [property: Id(4)] string? Scope,
    [property: Id(5)] string? Nonce);
