namespace DarkVelocity.Host.State;

/// <summary>
/// State for OAuth authorization flow (CSRF protection).
/// </summary>
[GenerateSerializer]
public sealed class OAuthFlowState
{
    [Id(0)] public bool Initialized { get; set; }
    [Id(1)] public string Provider { get; set; } = string.Empty;
    [Id(2)] public string ReturnUrl { get; set; } = string.Empty;
    [Id(3)] public string? CodeChallenge { get; set; }
    [Id(4)] public string? CodeChallengeMethod { get; set; }
    [Id(5)] public string? ClientId { get; set; }
    [Id(6)] public string? Nonce { get; set; }
    [Id(7)] public string? Scope { get; set; }
    [Id(8)] public DateTime CreatedAt { get; set; }
    [Id(9)] public DateTime ExpiresAt { get; set; }
    [Id(10)] public bool Consumed { get; set; }
}

/// <summary>
/// State for external OAuth identity mapping.
/// </summary>
[GenerateSerializer]
public sealed class ExternalIdentityState
{
    [Id(0)] public bool Linked { get; set; }
    [Id(1)] public Guid UserId { get; set; }
    [Id(2)] public Guid OrganizationId { get; set; }
    [Id(3)] public string Provider { get; set; } = string.Empty;
    [Id(4)] public string ExternalId { get; set; } = string.Empty;
    [Id(5)] public string? Email { get; set; }
    [Id(6)] public string? Name { get; set; }
    [Id(7)] public string? PictureUrl { get; set; }
    [Id(8)] public DateTime LinkedAt { get; set; }
    [Id(9)] public DateTime? LastUsedAt { get; set; }
}

/// <summary>
/// State for OAuth authorization code (code flow).
/// </summary>
[GenerateSerializer]
public sealed class AuthorizationCodeState
{
    [Id(0)] public bool Initialized { get; set; }
    [Id(1)] public Guid UserId { get; set; }
    [Id(2)] public Guid OrganizationId { get; set; }
    [Id(3)] public string ClientId { get; set; } = string.Empty;
    [Id(4)] public string RedirectUri { get; set; } = string.Empty;
    [Id(5)] public string? Scope { get; set; }
    [Id(6)] public string? CodeChallenge { get; set; }
    [Id(7)] public string? CodeChallengeMethod { get; set; }
    [Id(8)] public string? Nonce { get; set; }
    [Id(9)] public string? DisplayName { get; set; }
    [Id(10)] public List<string> Roles { get; set; } = [];
    [Id(11)] public DateTime CreatedAt { get; set; }
    [Id(12)] public DateTime ExpiresAt { get; set; }
    [Id(13)] public bool Exchanged { get; set; }
}
