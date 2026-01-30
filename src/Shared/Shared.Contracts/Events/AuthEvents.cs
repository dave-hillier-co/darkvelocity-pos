namespace DarkVelocity.Shared.Contracts.Events;

/// <summary>
/// Published when a user successfully logs in.
/// </summary>
public sealed record UserLoggedIn(
    Guid UserId,
    Guid TenantId,
    Guid LocationId,
    string Username,
    string LoginMethod,
    string? IpAddress,
    string? UserAgent
) : IntegrationEvent
{
    public override string EventType => "auth.session.login";
}

/// <summary>
/// Published when a user logs out.
/// </summary>
public sealed record UserLoggedOut(
    Guid UserId,
    Guid TenantId,
    Guid LocationId,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "auth.session.logout";
}

/// <summary>
/// Published when a login attempt fails.
/// </summary>
public sealed record LoginFailed(
    Guid? UserId,
    Guid? LocationId,
    string LoginMethod,
    string FailureReason,
    string? IpAddress,
    string? AttemptedIdentifier
) : IntegrationEvent
{
    public override string EventType => "auth.session.login_failed";
}

/// <summary>
/// Published when a user's session token is refreshed.
/// </summary>
public sealed record TokenRefreshed(
    Guid UserId,
    Guid TenantId,
    Guid LocationId
) : IntegrationEvent
{
    public override string EventType => "auth.session.token_refreshed";
}

/// <summary>
/// Published when a user changes their PIN.
/// </summary>
public sealed record PinChanged(
    Guid UserId,
    Guid TenantId,
    Guid ChangedByUserId
) : IntegrationEvent
{
    public override string EventType => "auth.user.pin_changed";
}

/// <summary>
/// Published when a user's QR code token is regenerated.
/// </summary>
public sealed record QrTokenRegenerated(
    Guid UserId,
    Guid TenantId,
    Guid RegeneratedByUserId
) : IntegrationEvent
{
    public override string EventType => "auth.user.qr_token_regenerated";
}
