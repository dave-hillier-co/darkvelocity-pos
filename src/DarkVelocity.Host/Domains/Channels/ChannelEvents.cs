using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.Events;

// ============================================================================
// Channel Domain Event Interfaces
// ============================================================================

/// <summary>
/// Base interface for Channel domain events (for JournaledGrain event sourcing).
/// </summary>
public interface IChannelEvent
{
    Guid ChannelId { get; }
    DateTimeOffset OccurredAt { get; }
}

// ============================================================================
// Channel Domain Events (for JournaledGrain)
// ============================================================================

/// <summary>
/// Domain event: A channel was connected.
/// </summary>
[GenerateSerializer]
public sealed record ChannelConnected(
    [property: Id(0)] Guid ChannelId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] string Name,
    [property: Id(3)] DeliveryPlatformType PlatformType,
    [property: Id(4)] IntegrationType IntegrationType,
    [property: Id(5)] string? ExternalChannelId,
    [property: Id(6)] string? ApiCredentialsEncrypted,
    [property: Id(7)] string? WebhookSecret,
    [property: Id(8)] string? Settings,
    [property: Id(9)] DateTimeOffset OccurredAt
) : IChannelEvent;

/// <summary>
/// Domain event: A channel's configuration was updated.
/// </summary>
[GenerateSerializer]
public sealed record ChannelUpdated(
    [property: Id(0)] Guid ChannelId,
    [property: Id(1)] string? Name,
    [property: Id(2)] string? ApiCredentialsEncrypted,
    [property: Id(3)] string? WebhookSecret,
    [property: Id(4)] string? Settings,
    [property: Id(5)] DateTimeOffset OccurredAt
) : IChannelEvent;

/// <summary>
/// Domain event: A channel was disconnected.
/// </summary>
[GenerateSerializer]
public sealed record ChannelDisconnected(
    [property: Id(0)] Guid ChannelId,
    [property: Id(1)] DateTimeOffset OccurredAt
) : IChannelEvent;

/// <summary>
/// Domain event: A channel was paused.
/// </summary>
[GenerateSerializer]
public sealed record ChannelPaused(
    [property: Id(0)] Guid ChannelId,
    [property: Id(1)] string? Reason,
    [property: Id(2)] DateTimeOffset OccurredAt
) : IChannelEvent;

/// <summary>
/// Domain event: A channel was resumed.
/// </summary>
[GenerateSerializer]
public sealed record ChannelResumed(
    [property: Id(0)] Guid ChannelId,
    [property: Id(1)] DateTimeOffset OccurredAt
) : IChannelEvent;

/// <summary>
/// Domain event: A location mapping was added to a channel.
/// </summary>
[GenerateSerializer]
public sealed record ChannelLocationMappingAdded(
    [property: Id(0)] Guid ChannelId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] string ExternalStoreId,
    [property: Id(3)] bool IsActive,
    [property: Id(4)] string? MenuId,
    [property: Id(5)] string? OperatingHoursOverride,
    [property: Id(6)] DateTimeOffset OccurredAt
) : IChannelEvent;

/// <summary>
/// Domain event: A location mapping was removed from a channel.
/// </summary>
[GenerateSerializer]
public sealed record ChannelLocationMappingRemoved(
    [property: Id(0)] Guid ChannelId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] DateTimeOffset OccurredAt
) : IChannelEvent;

/// <summary>
/// Domain event: An order was recorded on a channel.
/// </summary>
[GenerateSerializer]
public sealed record ChannelOrderRecorded(
    [property: Id(0)] Guid ChannelId,
    [property: Id(1)] decimal Amount,
    [property: Id(2)] DateTimeOffset OccurredAt
) : IChannelEvent;

/// <summary>
/// Domain event: A sync was recorded on a channel.
/// </summary>
[GenerateSerializer]
public sealed record ChannelSyncRecorded(
    [property: Id(0)] Guid ChannelId,
    [property: Id(1)] DateTimeOffset OccurredAt
) : IChannelEvent;

/// <summary>
/// Domain event: A heartbeat was recorded on a channel.
/// </summary>
[GenerateSerializer]
public sealed record ChannelHeartbeatRecorded(
    [property: Id(0)] Guid ChannelId,
    [property: Id(1)] DateTimeOffset OccurredAt
) : IChannelEvent;

/// <summary>
/// Domain event: An error was recorded on a channel.
/// </summary>
[GenerateSerializer]
public sealed record ChannelErrorRecorded(
    [property: Id(0)] Guid ChannelId,
    [property: Id(1)] string ErrorMessage,
    [property: Id(2)] DateTimeOffset OccurredAt
) : IChannelEvent;

/// <summary>
/// Domain event: Daily counters were reset on a channel.
/// </summary>
[GenerateSerializer]
public sealed record ChannelDailyCountersReset(
    [property: Id(0)] Guid ChannelId,
    [property: Id(1)] DateOnly NewDate,
    [property: Id(2)] DateTimeOffset OccurredAt
) : IChannelEvent;

// ============================================================================
// Status Mapping Domain Event Interfaces
// ============================================================================

/// <summary>
/// Base interface for StatusMapping domain events (for JournaledGrain event sourcing).
/// </summary>
public interface IStatusMappingEvent
{
    Guid MappingId { get; }
    DateTimeOffset OccurredAt { get; }
}

// ============================================================================
// Status Mapping Domain Events (for JournaledGrain)
// ============================================================================

/// <summary>
/// Domain event: A status mapping was configured.
/// </summary>
[GenerateSerializer]
public sealed record StatusMappingConfigured(
    [property: Id(0)] Guid MappingId,
    [property: Id(1)] Guid OrgId,
    [property: Id(2)] DeliveryPlatformType PlatformType,
    [property: Id(3)] List<StatusMappingEntryData> Mappings,
    [property: Id(4)] DateTimeOffset OccurredAt
) : IStatusMappingEvent;

/// <summary>
/// Data transfer object for status mapping entries in events.
/// </summary>
[GenerateSerializer]
public sealed record StatusMappingEntryData(
    [property: Id(0)] string ExternalStatusCode,
    [property: Id(1)] string? ExternalStatusName,
    [property: Id(2)] InternalOrderStatus InternalStatus,
    [property: Id(3)] bool TriggersPosAction,
    [property: Id(4)] string? PosActionType
);

/// <summary>
/// Domain event: A status mapping entry was added.
/// </summary>
[GenerateSerializer]
public sealed record StatusMappingEntryAdded(
    [property: Id(0)] Guid MappingId,
    [property: Id(1)] string ExternalStatusCode,
    [property: Id(2)] string? ExternalStatusName,
    [property: Id(3)] InternalOrderStatus InternalStatus,
    [property: Id(4)] bool TriggersPosAction,
    [property: Id(5)] string? PosActionType,
    [property: Id(6)] DateTimeOffset OccurredAt
) : IStatusMappingEvent;

/// <summary>
/// Domain event: A status mapping entry was removed.
/// </summary>
[GenerateSerializer]
public sealed record StatusMappingEntryRemoved(
    [property: Id(0)] Guid MappingId,
    [property: Id(1)] string ExternalStatusCode,
    [property: Id(2)] DateTimeOffset OccurredAt
) : IStatusMappingEvent;

/// <summary>
/// Domain event: A status mapping usage was recorded.
/// </summary>
[GenerateSerializer]
public sealed record StatusMappingUsageRecorded(
    [property: Id(0)] Guid MappingId,
    [property: Id(1)] string ExternalStatusCode,
    [property: Id(2)] DateTimeOffset OccurredAt
) : IStatusMappingEvent;
