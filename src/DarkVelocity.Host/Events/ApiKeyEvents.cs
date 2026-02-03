using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events;

/// <summary>
/// Published when a new API key is created.
/// </summary>
public sealed record ApiKeyCreated(
    Guid KeyId,
    Guid UserId,
    Guid OrganizationId,
    string Name,
    ApiKeyType Type,
    bool IsTestMode,
    string KeyPrefix
) : IntegrationEvent
{
    public override string EventType => "auth.apikey.created";
}

/// <summary>
/// Published when an API key is updated.
/// </summary>
public sealed record ApiKeyUpdated(
    Guid KeyId,
    Guid UserId,
    Guid OrganizationId,
    string Name,
    List<string>? UpdatedFields
) : IntegrationEvent
{
    public override string EventType => "auth.apikey.updated";
}

/// <summary>
/// Published when an API key is revoked.
/// </summary>
public sealed record ApiKeyRevoked(
    Guid KeyId,
    Guid UserId,
    Guid OrganizationId,
    Guid RevokedBy,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "auth.apikey.revoked";
}

/// <summary>
/// Published when an API key is used for authentication.
/// </summary>
public sealed record ApiKeyUsed(
    Guid KeyId,
    Guid UserId,
    Guid OrganizationId,
    string? IpAddress,
    string? UserAgent,
    string? Endpoint
) : IntegrationEvent
{
    public override string EventType => "auth.apikey.used";
}

/// <summary>
/// Published when an API key authentication fails.
/// </summary>
public sealed record ApiKeyAuthFailed(
    string? KeyPrefix,
    Guid? OrganizationId,
    string Reason,
    string? IpAddress
) : IntegrationEvent
{
    public override string EventType => "auth.apikey.auth_failed";
}
