using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

// ============================================================================
// Channel State
// ============================================================================

[GenerateSerializer]
public sealed class ChannelState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid ChannelId { get; set; }
    [Id(2)] public DeliveryPlatformType PlatformType { get; set; }
    [Id(3)] public IntegrationType IntegrationType { get; set; }
    [Id(4)] public string Name { get; set; } = string.Empty;
    [Id(5)] public ChannelStatus Status { get; set; }
    [Id(6)] public string? ApiCredentialsEncrypted { get; set; }
    [Id(7)] public string? WebhookSecret { get; set; }
    [Id(8)] public string? ExternalChannelId { get; set; }
    [Id(9)] public string? Settings { get; set; }
    [Id(10)] public DateTime? ConnectedAt { get; set; }
    [Id(11)] public DateTime? LastSyncAt { get; set; }
    [Id(12)] public DateTime? LastOrderAt { get; set; }
    [Id(13)] public DateTime? LastHeartbeatAt { get; set; }
    [Id(14)] public List<ChannelLocationState> Locations { get; set; } = [];
    [Id(15)] public int TotalOrdersToday { get; set; }
    [Id(16)] public decimal TotalRevenueToday { get; set; }
    [Id(17)] public DateTime TodayDate { get; set; }
    [Id(18)] public string? LastErrorMessage { get; set; }
    [Id(19)] public string? PauseReason { get; set; }
    [Id(20)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class ChannelLocationState
{
    [Id(0)] public Guid LocationId { get; set; }
    [Id(1)] public string ExternalStoreId { get; set; } = string.Empty;
    [Id(2)] public bool IsActive { get; set; }
    [Id(3)] public string? MenuId { get; set; }
    [Id(4)] public string? OperatingHoursOverride { get; set; }
}

// ============================================================================
// Status Mapping State
// ============================================================================

[GenerateSerializer]
public sealed class StatusMappingState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid MappingId { get; set; }
    [Id(2)] public DeliveryPlatformType PlatformType { get; set; }
    [Id(3)] public List<StatusMappingEntryState> Mappings { get; set; } = [];
    [Id(4)] public DateTime ConfiguredAt { get; set; }
    [Id(5)] public DateTime? LastUsedAt { get; set; }
    [Id(6)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class StatusMappingEntryState
{
    [Id(0)] public string ExternalStatusCode { get; set; } = string.Empty;
    [Id(1)] public string? ExternalStatusName { get; set; }
    [Id(2)] public InternalOrderStatus InternalStatus { get; set; }
    [Id(3)] public bool TriggersPosAction { get; set; }
    [Id(4)] public string? PosActionType { get; set; }
}

// ============================================================================
// Channel Registry State
// ============================================================================

[GenerateSerializer]
public sealed class ChannelRegistryState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public List<RegisteredChannelState> Channels { get; set; } = [];
    [Id(2)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class RegisteredChannelState
{
    [Id(0)] public Guid ChannelId { get; set; }
    [Id(1)] public DeliveryPlatformType PlatformType { get; set; }
    [Id(2)] public IntegrationType IntegrationType { get; set; }
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public ChannelStatus Status { get; set; }
    [Id(5)] public List<Guid> LocationIds { get; set; } = [];
    [Id(6)] public DateTime? LastOrderAt { get; set; }
}
