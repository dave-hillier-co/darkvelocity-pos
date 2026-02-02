using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.Contracts;

public record ConnectChannelRequest(
    DeliveryPlatformType PlatformType,
    IntegrationType IntegrationType,
    string Name,
    string? ApiCredentialsEncrypted = null,
    string? WebhookSecret = null,
    string? ExternalChannelId = null,
    string? Settings = null);

public record UpdateChannelRequest(
    string? Name = null,
    ChannelStatus? Status = null,
    string? ApiCredentialsEncrypted = null,
    string? WebhookSecret = null,
    string? Settings = null);

public record AddChannelLocationRequest(
    Guid LocationId,
    string ExternalStoreId,
    bool IsActive = true,
    string? MenuId = null,
    string? OperatingHoursOverride = null);

public record PauseChannelRequest(string? Reason = null);

public record RecordOrderRequest(decimal OrderTotal);

public record RecordErrorRequest(string ErrorMessage);

public record ConfigureStatusMappingRequest(
    DeliveryPlatformType PlatformType,
    List<StatusMappingEntryRequest> Mappings);

public record StatusMappingEntryRequest(
    string ExternalStatusCode,
    string? ExternalStatusName,
    InternalOrderStatus InternalStatus,
    bool TriggersPosAction = false,
    string? PosActionType = null);

public record AddStatusMappingRequest(
    string ExternalStatusCode,
    string? ExternalStatusName,
    InternalOrderStatus InternalStatus,
    bool TriggersPosAction = false,
    string? PosActionType = null);

public record RegisterChannelRequest(
    Guid ChannelId,
    DeliveryPlatformType PlatformType,
    IntegrationType IntegrationType,
    string Name);
