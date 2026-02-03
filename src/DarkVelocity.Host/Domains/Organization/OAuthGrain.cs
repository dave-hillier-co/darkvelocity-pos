using DarkVelocity.Host.State;
using Orleans.Runtime;
using System.Security.Cryptography;
using System.Text;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Manages OAuth state for CSRF protection.
/// Key: state parameter (random string)
/// Lifetime: 10 minutes
/// </summary>
public class OAuthStateGrain : Grain, IOAuthStateGrain
{
    private readonly IPersistentState<OAuthFlowState> _state;
    private const int StateExpirationMinutes = 10;

    public OAuthStateGrain(
        [PersistentState("oauthstate", "OrleansStorage")]
        IPersistentState<OAuthFlowState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(OAuthStateRequest request)
    {
        if (_state.State.Initialized)
            throw new InvalidOperationException("OAuth state already initialized");

        _state.State = new OAuthFlowState
        {
            Initialized = true,
            Provider = request.Provider,
            ReturnUrl = request.ReturnUrl,
            CodeChallenge = request.CodeChallenge,
            CodeChallengeMethod = request.CodeChallengeMethod,
            ClientId = request.ClientId,
            Nonce = request.Nonce,
            Scope = request.Scope,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(StateExpirationMinutes),
            Consumed = false
        };

        await _state.WriteStateAsync();

        // Schedule cleanup
        RegisterTimer(
            CleanupAsync,
            null,
            TimeSpan.FromMinutes(StateExpirationMinutes + 1),
            TimeSpan.FromMilliseconds(-1));
    }

    public async Task<OAuthStateValidation> ValidateAndConsumeAsync()
    {
        if (!_state.State.Initialized)
            return new OAuthStateValidation(false, Error: "invalid_state");

        if (_state.State.Consumed)
            return new OAuthStateValidation(false, Error: "state_already_used");

        if (DateTime.UtcNow > _state.State.ExpiresAt)
            return new OAuthStateValidation(false, Error: "state_expired");

        // Mark as consumed (one-time use)
        _state.State.Consumed = true;
        await _state.WriteStateAsync();

        return new OAuthStateValidation(
            true,
            _state.State.Provider,
            _state.State.ReturnUrl,
            _state.State.CodeChallenge,
            _state.State.CodeChallengeMethod,
            _state.State.ClientId,
            _state.State.Nonce,
            _state.State.Scope);
    }

    public Task<OAuthFlowState> GetStateAsync() => Task.FromResult(_state.State);

    private async Task CleanupAsync(object? _)
    {
        await _state.ClearStateAsync();
        DeactivateOnIdle();
    }
}

/// <summary>
/// Maps external OAuth identities to internal users.
/// Key: provider:externalId (e.g., "google:123456789")
/// </summary>
public class ExternalIdentityGrain : Grain, IExternalIdentityGrain
{
    private readonly IPersistentState<ExternalIdentityState> _state;

    public ExternalIdentityGrain(
        [PersistentState("externalidentity", "OrleansStorage")]
        IPersistentState<ExternalIdentityState> state)
    {
        _state = state;
    }

    public async Task LinkAsync(LinkExternalIdentityCommand command)
    {
        if (_state.State.Linked)
            throw new InvalidOperationException("External identity already linked to a user");

        _state.State = new ExternalIdentityState
        {
            Linked = true,
            UserId = command.UserId,
            OrganizationId = command.OrganizationId,
            Provider = command.Provider,
            ExternalId = command.ExternalId,
            Email = command.Email,
            Name = command.Name,
            PictureUrl = command.PictureUrl,
            LinkedAt = DateTime.UtcNow,
            LastUsedAt = DateTime.UtcNow
        };

        await _state.WriteStateAsync();
    }

    public async Task<ExternalIdentityInfo?> GetLinkedUserAsync()
    {
        if (!_state.State.Linked)
            return null;

        // Update last used timestamp
        _state.State.LastUsedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return new ExternalIdentityInfo(
            _state.State.UserId,
            _state.State.OrganizationId,
            _state.State.Provider,
            _state.State.ExternalId,
            _state.State.Email,
            _state.State.Name,
            _state.State.PictureUrl,
            _state.State.LinkedAt,
            _state.State.LastUsedAt);
    }

    public async Task UpdateInfoAsync(string? email, string? name, string? pictureUrl)
    {
        if (!_state.State.Linked)
            return;

        if (email != null) _state.State.Email = email;
        if (name != null) _state.State.Name = name;
        if (pictureUrl != null) _state.State.PictureUrl = pictureUrl;

        await _state.WriteStateAsync();
    }

    public async Task UnlinkAsync()
    {
        if (!_state.State.Linked)
            return;

        await _state.ClearStateAsync();
    }
}

/// <summary>
/// Manages OAuth authorization codes.
/// Key: authorization code
/// Lifetime: 10 minutes (per OAuth 2.0 spec recommendation)
/// </summary>
public class AuthorizationCodeGrain : Grain, IAuthorizationCodeGrain
{
    private readonly IPersistentState<AuthorizationCodeState> _state;
    private const int CodeExpirationMinutes = 10;

    public AuthorizationCodeGrain(
        [PersistentState("authcode", "OrleansStorage")]
        IPersistentState<AuthorizationCodeState> state)
    {
        _state = state;
    }

    public async Task IssueAsync(AuthorizationCodeRequest request)
    {
        if (_state.State.Initialized)
            throw new InvalidOperationException("Authorization code already issued");

        _state.State = new AuthorizationCodeState
        {
            Initialized = true,
            UserId = request.UserId,
            OrganizationId = request.OrganizationId,
            ClientId = request.ClientId,
            RedirectUri = request.RedirectUri,
            Scope = request.Scope,
            CodeChallenge = request.CodeChallenge,
            CodeChallengeMethod = request.CodeChallengeMethod,
            Nonce = request.Nonce,
            DisplayName = request.DisplayName,
            Roles = request.Roles?.ToList() ?? [],
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(CodeExpirationMinutes),
            Exchanged = false
        };

        await _state.WriteStateAsync();

        // Schedule cleanup
        RegisterTimer(
            CleanupAsync,
            null,
            TimeSpan.FromMinutes(CodeExpirationMinutes + 1),
            TimeSpan.FromMilliseconds(-1));
    }

    public async Task<AuthorizationCodeExchange?> ExchangeAsync(string clientId, string? codeVerifier)
    {
        if (!_state.State.Initialized)
            return null;

        if (_state.State.Exchanged)
            return null; // Code already used

        if (DateTime.UtcNow > _state.State.ExpiresAt)
            return null; // Code expired

        if (_state.State.ClientId != clientId)
            return null; // Client mismatch

        // Validate PKCE if code challenge was provided
        if (!string.IsNullOrEmpty(_state.State.CodeChallenge))
        {
            if (string.IsNullOrEmpty(codeVerifier))
                return null; // PKCE required but verifier not provided

            var expectedChallenge = _state.State.CodeChallengeMethod?.ToUpperInvariant() switch
            {
                "S256" => ComputeS256Challenge(codeVerifier),
                "PLAIN" => codeVerifier,
                _ => ComputeS256Challenge(codeVerifier) // Default to S256
            };

            if (expectedChallenge != _state.State.CodeChallenge)
                return null; // PKCE validation failed
        }

        // Mark as exchanged (one-time use per OAuth 2.0 spec)
        _state.State.Exchanged = true;
        await _state.WriteStateAsync();

        return new AuthorizationCodeExchange(
            _state.State.UserId,
            _state.State.OrganizationId,
            _state.State.DisplayName,
            _state.State.Roles,
            _state.State.Scope,
            _state.State.Nonce);
    }

    public Task<bool> IsValidAsync()
    {
        return Task.FromResult(
            _state.State.Initialized &&
            !_state.State.Exchanged &&
            DateTime.UtcNow <= _state.State.ExpiresAt);
    }

    private async Task CleanupAsync(object? _)
    {
        await _state.ClearStateAsync();
        DeactivateOnIdle();
    }

    private static string ComputeS256Challenge(string verifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
