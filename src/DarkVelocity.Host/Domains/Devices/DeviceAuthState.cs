namespace DarkVelocity.Host.State;

public enum DeviceAuthStatus
{
    Pending,
    Authorized,
    Expired,
    Denied
}

public enum DeviceType
{
    Pos,
    Kds,
    Backoffice
}

public enum DeviceStatus
{
    Pending,
    Authorized,
    Suspended,
    Revoked
}

/// <summary>
/// State for device authorization flow (short-lived, expires after authorization).
/// </summary>
[GenerateSerializer]
public sealed class DeviceAuthState
{
    [Id(0)] public string UserCode { get; set; } = string.Empty;
    [Id(1)] public string DeviceCode { get; set; } = string.Empty;
    [Id(2)] public string ClientId { get; set; } = string.Empty;
    [Id(3)] public string Scope { get; set; } = string.Empty;
    [Id(4)] public string? DeviceFingerprint { get; set; }
    [Id(5)] public string? IpAddress { get; set; }
    [Id(6)] public DeviceAuthStatus Status { get; set; } = DeviceAuthStatus.Pending;
    [Id(7)] public DateTime CreatedAt { get; set; }
    [Id(8)] public DateTime ExpiresAt { get; set; }
    [Id(9)] public Guid? AuthorizedBy { get; set; }
    [Id(10)] public Guid? OrganizationId { get; set; }
    [Id(11)] public Guid? SiteId { get; set; }
    [Id(12)] public Guid? DeviceId { get; set; }
    [Id(13)] public string? DeviceName { get; set; }
    [Id(14)] public DeviceType? AppType { get; set; }
    [Id(15)] public string? DenialReason { get; set; }
    [Id(16)] public int PollCount { get; set; }
    [Id(17)] public bool Initialized { get; set; }
}

/// <summary>
/// State for an authorized device.
/// </summary>
[GenerateSerializer]
public sealed class DeviceState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public DeviceType Type { get; set; }
    [Id(5)] public DeviceStatus Status { get; set; } = DeviceStatus.Pending;
    [Id(6)] public string? Fingerprint { get; set; }
    [Id(7)] public Guid AuthorizedBy { get; set; }
    [Id(8)] public DateTime AuthorizedAt { get; set; }
    [Id(9)] public DateTime? LastSeenAt { get; set; }
    [Id(10)] public string? LastAppVersion { get; set; }
    [Id(11)] public Guid? CurrentUserId { get; set; }
    [Id(12)] public string? SuspensionReason { get; set; }
    [Id(13)] public string? RevocationReason { get; set; }
    [Id(14)] public int Version { get; set; }
}

/// <summary>
/// State for a user session (JWT tokens).
/// </summary>
[GenerateSerializer]
public sealed class SessionState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid UserId { get; set; }
    [Id(2)] public Guid OrganizationId { get; set; }
    [Id(3)] public Guid? SiteId { get; set; }
    [Id(4)] public Guid? DeviceId { get; set; }
    [Id(5)] public string AuthMethod { get; set; } = string.Empty;
    [Id(6)] public string? IpAddress { get; set; }
    [Id(7)] public string? UserAgent { get; set; }
    [Id(8)] public string RefreshTokenHash { get; set; } = string.Empty;
    [Id(9)] public DateTime CreatedAt { get; set; }
    [Id(10)] public DateTime ExpiresAt { get; set; }
    [Id(11)] public DateTime? LastActivityAt { get; set; }
    [Id(12)] public bool IsRevoked { get; set; }
    [Id(13)] public int RefreshCount { get; set; }
}
