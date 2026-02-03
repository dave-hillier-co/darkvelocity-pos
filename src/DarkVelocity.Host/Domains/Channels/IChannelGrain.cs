namespace DarkVelocity.Host.Grains;

// ============================================================================
// Channel Grain - Base abstraction for delivery/order channels
// ============================================================================

/// <summary>
/// Represents the operational status of a channel.
/// </summary>
public enum ChannelStatus
{
    Active,
    Paused,
    Disconnected,
    Maintenance,
    Error
}

/// <summary>
/// Configuration for connecting a channel.
/// </summary>
[GenerateSerializer]
public record ConnectChannelCommand(
    [property: Id(0)] DeliveryPlatformType PlatformType,
    [property: Id(1)] IntegrationType IntegrationType,
    [property: Id(2)] string Name,
    [property: Id(3)] string? ApiCredentialsEncrypted,
    [property: Id(4)] string? WebhookSecret,
    [property: Id(5)] string? ExternalChannelId,
    [property: Id(6)] string? Settings);

/// <summary>
/// Updates to channel configuration.
/// </summary>
[GenerateSerializer]
public record UpdateChannelCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] ChannelStatus? Status,
    [property: Id(2)] string? ApiCredentialsEncrypted,
    [property: Id(3)] string? WebhookSecret,
    [property: Id(4)] string? Settings);

/// <summary>
/// Maps a location to an external channel store.
/// </summary>
[GenerateSerializer]
public record ChannelLocationMapping(
    [property: Id(0)] Guid LocationId,
    [property: Id(1)] string ExternalStoreId,
    [property: Id(2)] bool IsActive,
    [property: Id(3)] string? MenuId,
    [property: Id(4)] string? OperatingHoursOverride);

/// <summary>
/// Snapshot of channel state.
/// </summary>
[GenerateSerializer]
public record ChannelSnapshot(
    [property: Id(0)] Guid ChannelId,
    [property: Id(1)] DeliveryPlatformType PlatformType,
    [property: Id(2)] IntegrationType IntegrationType,
    [property: Id(3)] string Name,
    [property: Id(4)] ChannelStatus Status,
    [property: Id(5)] string? ExternalChannelId,
    [property: Id(6)] DateTime? ConnectedAt,
    [property: Id(7)] DateTime? LastSyncAt,
    [property: Id(8)] DateTime? LastOrderAt,
    [property: Id(9)] DateTime? LastHeartbeatAt,
    [property: Id(10)] IReadOnlyList<ChannelLocationMapping> Locations,
    [property: Id(11)] int TotalOrdersToday,
    [property: Id(12)] decimal TotalRevenueToday,
    [property: Id(13)] string? LastErrorMessage);

/// <summary>
/// Base grain interface for all channel integrations.
/// Provides common functionality for managing delivery/order channels.
/// Key: "{orgId}:channel:{channelId}"
/// </summary>
public interface IChannelGrain : IGrainWithStringKey
{
    /// <summary>
    /// Connect and configure the channel.
    /// </summary>
    Task<ChannelSnapshot> ConnectAsync(ConnectChannelCommand command);

    /// <summary>
    /// Update channel configuration.
    /// </summary>
    Task<ChannelSnapshot> UpdateAsync(UpdateChannelCommand command);

    /// <summary>
    /// Disconnect the channel (soft delete).
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Pause order acceptance temporarily.
    /// </summary>
    Task PauseAsync(string? reason = null);

    /// <summary>
    /// Resume order acceptance.
    /// </summary>
    Task ResumeAsync();

    /// <summary>
    /// Add or update a location mapping for this channel.
    /// </summary>
    Task AddLocationMappingAsync(ChannelLocationMapping mapping);

    /// <summary>
    /// Remove a location from this channel.
    /// </summary>
    Task RemoveLocationMappingAsync(Guid locationId);

    /// <summary>
    /// Get the current channel snapshot.
    /// </summary>
    Task<ChannelSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Record an order received from this channel.
    /// </summary>
    Task RecordOrderAsync(decimal orderTotal);

    /// <summary>
    /// Record a successful sync with the external platform.
    /// </summary>
    Task RecordSyncAsync();

    /// <summary>
    /// Record a heartbeat from the channel (for health monitoring).
    /// </summary>
    Task RecordHeartbeatAsync();

    /// <summary>
    /// Record an error that occurred on this channel.
    /// </summary>
    Task RecordErrorAsync(string errorMessage);

    /// <summary>
    /// Check if the channel is currently accepting orders.
    /// </summary>
    Task<bool> IsAcceptingOrdersAsync();
}

// ============================================================================
// Status Mapping - Translates between internal and external status codes
// ============================================================================

/// <summary>
/// Internal order status that all channels map to.
/// </summary>
public enum InternalOrderStatus
{
    Received,
    Accepted,
    Rejected,
    Preparing,
    Ready,
    PickedUp,
    Delivered,
    Cancelled,
    Failed
}

/// <summary>
/// A mapping between an external platform status code and internal status.
/// </summary>
[GenerateSerializer]
public record StatusMappingEntry(
    [property: Id(0)] string ExternalStatusCode,
    [property: Id(1)] string? ExternalStatusName,
    [property: Id(2)] InternalOrderStatus InternalStatus,
    [property: Id(3)] bool TriggersPosAction,
    [property: Id(4)] string? PosActionType);

/// <summary>
/// Configuration for status mappings.
/// </summary>
[GenerateSerializer]
public record ConfigureStatusMappingCommand(
    [property: Id(0)] DeliveryPlatformType PlatformType,
    [property: Id(1)] IReadOnlyList<StatusMappingEntry> Mappings);

/// <summary>
/// Snapshot of status mapping configuration.
/// </summary>
[GenerateSerializer]
public record StatusMappingSnapshot(
    [property: Id(0)] Guid MappingId,
    [property: Id(1)] DeliveryPlatformType PlatformType,
    [property: Id(2)] IReadOnlyList<StatusMappingEntry> Mappings,
    [property: Id(3)] DateTime ConfiguredAt,
    [property: Id(4)] DateTime? LastUsedAt);

/// <summary>
/// Grain for managing status code mappings between platforms and internal status.
/// Key: "{orgId}:statusmapping:{platformType}"
/// </summary>
public interface IStatusMappingGrain : IGrainWithStringKey
{
    /// <summary>
    /// Configure status mappings for a platform.
    /// </summary>
    Task<StatusMappingSnapshot> ConfigureAsync(ConfigureStatusMappingCommand command);

    /// <summary>
    /// Add or update a single mapping.
    /// </summary>
    Task AddMappingAsync(StatusMappingEntry mapping);

    /// <summary>
    /// Remove a mapping by external status code.
    /// </summary>
    Task RemoveMappingAsync(string externalStatusCode);

    /// <summary>
    /// Get the internal status for an external status code.
    /// </summary>
    Task<InternalOrderStatus?> GetInternalStatusAsync(string externalStatusCode);

    /// <summary>
    /// Get the external status code for an internal status.
    /// Returns the first match if multiple external codes map to the same internal status.
    /// </summary>
    Task<string?> GetExternalStatusAsync(InternalOrderStatus internalStatus);

    /// <summary>
    /// Get all mappings.
    /// </summary>
    Task<StatusMappingSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Record that a mapping was used (for tracking).
    /// </summary>
    Task RecordUsageAsync(string externalStatusCode);
}

// ============================================================================
// Channel Registry - Tracks all channels for an organization
// ============================================================================

/// <summary>
/// Summary of a registered channel.
/// </summary>
[GenerateSerializer]
public record ChannelSummary(
    [property: Id(0)] Guid ChannelId,
    [property: Id(1)] DeliveryPlatformType PlatformType,
    [property: Id(2)] IntegrationType IntegrationType,
    [property: Id(3)] string Name,
    [property: Id(4)] ChannelStatus Status,
    [property: Id(5)] int LocationCount,
    [property: Id(6)] DateTime? LastOrderAt);

/// <summary>
/// Grain for tracking all channels registered for an organization.
/// Key: "{orgId}:channelregistry"
/// </summary>
public interface IChannelRegistryGrain : IGrainWithStringKey
{
    /// <summary>
    /// Register a new channel.
    /// </summary>
    Task RegisterChannelAsync(Guid channelId, DeliveryPlatformType platformType, IntegrationType integrationType, string name);

    /// <summary>
    /// Unregister a channel.
    /// </summary>
    Task UnregisterChannelAsync(Guid channelId);

    /// <summary>
    /// Update channel status in the registry.
    /// </summary>
    Task UpdateChannelStatusAsync(Guid channelId, ChannelStatus status);

    /// <summary>
    /// Get all registered channels.
    /// </summary>
    Task<IReadOnlyList<ChannelSummary>> GetAllChannelsAsync();

    /// <summary>
    /// Get channels by integration type.
    /// </summary>
    Task<IReadOnlyList<ChannelSummary>> GetChannelsByTypeAsync(IntegrationType integrationType);

    /// <summary>
    /// Get channels by platform type.
    /// </summary>
    Task<IReadOnlyList<ChannelSummary>> GetChannelsByPlatformAsync(DeliveryPlatformType platformType);

    /// <summary>
    /// Get active channels for a specific location.
    /// </summary>
    Task<IReadOnlyList<ChannelSummary>> GetChannelsForLocationAsync(Guid locationId);
}
