using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

// Device code flow
public record DeviceCodeApiRequest(string ClientId, string? Scope, string? DeviceFingerprint);
public record DeviceTokenApiRequest(string UserCode, string DeviceCode);
public record AuthorizeDeviceApiRequest(
    string UserCode,
    Guid AuthorizedBy,
    Guid OrganizationId,
    Guid SiteId,
    string DeviceName,
    DeviceType AppType);
public record DenyDeviceApiRequest(string UserCode, string? Reason);

// PIN authentication
public record PinLoginApiRequest(string Pin, Guid OrganizationId, Guid SiteId, Guid DeviceId);
public record PinLoginResponse(string AccessToken, string RefreshToken, int ExpiresIn, Guid UserId, string DisplayName);
public record LogoutApiRequest(Guid OrganizationId, Guid DeviceId, Guid SessionId);
public record RefreshTokenApiRequest(Guid OrganizationId, Guid SessionId, string RefreshToken);
public record RefreshTokenResponse(string AccessToken, string RefreshToken, int ExpiresIn);

// Device management
public record DeviceHeartbeatRequest(string? AppVersion);
public record SuspendDeviceRequest(string Reason);
public record RevokeDeviceRequest(string Reason);

// Station selection (KDS)
public record SelectStationRequest(Guid DeviceId, Guid StationId, string StationName);
