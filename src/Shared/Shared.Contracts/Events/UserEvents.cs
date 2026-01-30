namespace DarkVelocity.Shared.Contracts.Events;

/// <summary>
/// Published when a new POS user is created.
/// </summary>
public sealed record UserCreated(
    Guid UserId,
    Guid TenantId,
    Guid HomeLocationId,
    string Username,
    string FirstName,
    string LastName,
    string? Email,
    string UserGroupName
) : IntegrationEvent
{
    public override string EventType => "auth.user.created";
}

/// <summary>
/// Published when a user's details are updated.
/// </summary>
public sealed record UserUpdated(
    Guid UserId,
    Guid TenantId,
    List<string> ChangedFields,
    string? FirstName,
    string? LastName,
    string? Email,
    string? UserGroupName
) : IntegrationEvent
{
    public override string EventType => "auth.user.updated";
}

/// <summary>
/// Published when a user is deactivated (soft deleted).
/// </summary>
public sealed record UserDeactivated(
    Guid UserId,
    Guid TenantId,
    Guid HomeLocationId
) : IntegrationEvent
{
    public override string EventType => "auth.user.deactivated";
}

/// <summary>
/// Published when a user is reactivated.
/// </summary>
public sealed record UserReactivated(
    Guid UserId,
    Guid TenantId,
    Guid HomeLocationId
) : IntegrationEvent
{
    public override string EventType => "auth.user.reactivated";
}

/// <summary>
/// Published when a user's group (role/permissions) changes.
/// </summary>
public sealed record UserGroupChanged(
    Guid UserId,
    Guid TenantId,
    Guid OldUserGroupId,
    string OldUserGroupName,
    Guid NewUserGroupId,
    string NewUserGroupName
) : IntegrationEvent
{
    public override string EventType => "auth.user.group_changed";
}

/// <summary>
/// Published when a user gains access to a new location.
/// </summary>
public sealed record UserLocationAccessGranted(
    Guid UserId,
    Guid TenantId,
    Guid LocationId
) : IntegrationEvent
{
    public override string EventType => "auth.user.location_access_granted";
}

/// <summary>
/// Published when a user loses access to a location.
/// </summary>
public sealed record UserLocationAccessRevoked(
    Guid UserId,
    Guid TenantId,
    Guid LocationId
) : IntegrationEvent
{
    public override string EventType => "auth.user.location_access_revoked";
}
