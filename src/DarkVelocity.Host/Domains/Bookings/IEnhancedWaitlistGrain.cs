using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Enhanced Waitlist Types
// ============================================================================

[GenerateSerializer]
public record WaitlistEstimate
{
    [Id(0)] public int Position { get; init; }
    [Id(1)] public TimeSpan EstimatedWait { get; init; }
    [Id(2)] public TimeSpan MinWait { get; init; }
    [Id(3)] public TimeSpan MaxWait { get; init; }
    [Id(4)] public int PartiesAhead { get; init; }
    [Id(5)] public int CoversAhead { get; init; }
    [Id(6)] public TimeSpan AverageTurnTime { get; init; }
}

[GenerateSerializer]
public record WaitlistNotification
{
    [Id(0)] public Guid EntryId { get; init; }
    [Id(1)] public Guid NotificationId { get; init; }
    [Id(2)] public string Type { get; init; } = string.Empty; // "table_ready", "position_update", "removed"
    [Id(3)] public DateTime SentAt { get; init; }
    [Id(4)] public string Channel { get; init; } = string.Empty; // "sms", "push"
    [Id(5)] public bool Success { get; init; }
    [Id(6)] public string? ErrorMessage { get; init; }
}

[GenerateSerializer]
public record WaitlistPromotionResult
{
    [Id(0)] public Guid EntryId { get; init; }
    [Id(1)] public Guid? BookingId { get; init; }
    [Id(2)] public Guid? TableId { get; init; }
    [Id(3)] public string? TableNumber { get; init; }
    [Id(4)] public bool WasNotified { get; init; }
    [Id(5)] public DateTime PromotedAt { get; init; }
}

[GenerateSerializer]
public record WaitlistSettings
{
    [Id(0)] public bool AutoPromoteToBooking { get; init; } = true;
    [Id(1)] public bool SendTableReadyNotification { get; init; } = true;
    [Id(2)] public TimeSpan NotificationResponseTimeout { get; init; } = TimeSpan.FromMinutes(10);
    [Id(3)] public bool PrioritizeReturningCustomers { get; init; } = true;
    [Id(4)] public int ReturningCustomerBoostPositions { get; init; } = 2;
    [Id(5)] public TimeSpan MaxWaitTime { get; init; } = TimeSpan.FromHours(2);
    [Id(6)] public bool AutoExpireEntries { get; init; } = true;
    [Id(7)] public string DefaultNotificationChannel { get; init; } = "sms";
}

[GenerateSerializer]
public record UpdateWaitlistSettingsCommand(
    [property: Id(0)] bool? AutoPromoteToBooking = null,
    [property: Id(1)] bool? SendTableReadyNotification = null,
    [property: Id(2)] TimeSpan? NotificationResponseTimeout = null,
    [property: Id(3)] bool? PrioritizeReturningCustomers = null,
    [property: Id(4)] int? ReturningCustomerBoostPositions = null,
    [property: Id(5)] TimeSpan? MaxWaitTime = null,
    [property: Id(6)] bool? AutoExpireEntries = null,
    [property: Id(7)] string? DefaultNotificationChannel = null);

[GenerateSerializer]
public record AddToEnhancedWaitlistCommand(
    [property: Id(0)] GuestInfo Guest,
    [property: Id(1)] int PartySize,
    [property: Id(2)] string? TablePreferences = null,
    [property: Id(3)] NotificationMethod NotificationMethod = NotificationMethod.Sms,
    [property: Id(4)] Guid? CustomerId = null,
    [property: Id(5)] bool IsReturningCustomer = false,
    [property: Id(6)] int? CustomerVisitCount = null);

[GenerateSerializer]
public record EnhancedWaitlistEntryResult
{
    [Id(0)] public Guid EntryId { get; init; }
    [Id(1)] public int Position { get; init; }
    [Id(2)] public WaitlistEstimate Estimate { get; init; } = new();
    [Id(3)] public DateTime AddedAt { get; init; }
}

// ============================================================================
// Enhanced Waitlist Grain Interface
// ============================================================================

/// <summary>
/// Enhanced waitlist grain with estimated wait calculation, SMS notifications,
/// automatic promotion, and priority for returning customers.
/// Key: "{orgId}:{siteId}:waitlist:{date:yyyy-MM-dd}"
/// </summary>
public interface IEnhancedWaitlistGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid organizationId, Guid siteId, DateOnly date);
    Task<WaitlistState> GetStateAsync();

    // ============================================================================
    // Entry Management
    // ============================================================================

    /// <summary>
    /// Adds a party to the waitlist with enhanced features.
    /// </summary>
    Task<EnhancedWaitlistEntryResult> AddEntryAsync(AddToEnhancedWaitlistCommand command);

    /// <summary>
    /// Updates an entry's position with automatic re-estimation.
    /// </summary>
    Task UpdatePositionAsync(Guid entryId, int newPosition);

    /// <summary>
    /// Notifies an entry that their table is ready.
    /// </summary>
    Task<WaitlistNotification> NotifyTableReadyAsync(Guid entryId, Guid? tableId = null, string? tableNumber = null);

    /// <summary>
    /// Seats an entry at a table.
    /// </summary>
    Task<WaitlistPromotionResult> SeatEntryAsync(Guid entryId, Guid tableId, string tableNumber);

    /// <summary>
    /// Removes an entry from the waitlist.
    /// </summary>
    Task RemoveEntryAsync(Guid entryId, string reason);

    /// <summary>
    /// Promotes an entry to a booking (automatic or manual).
    /// </summary>
    Task<WaitlistPromotionResult> PromoteToBookingAsync(Guid entryId, DateTime bookingTime, Guid? tableId = null);

    // ============================================================================
    // Estimation
    // ============================================================================

    /// <summary>
    /// Gets detailed wait estimate for a party size.
    /// </summary>
    Task<WaitlistEstimate> GetWaitEstimateAsync(int partySize);

    /// <summary>
    /// Gets updated estimate for an existing entry.
    /// </summary>
    Task<WaitlistEstimate> GetEntryEstimateAsync(Guid entryId);

    /// <summary>
    /// Updates turn time data for more accurate estimates.
    /// </summary>
    Task UpdateTurnTimeDataAsync(int partySize, TimeSpan turnTime);

    // ============================================================================
    // Notifications
    // ============================================================================

    /// <summary>
    /// Sends a position update notification to an entry.
    /// </summary>
    Task<WaitlistNotification> SendPositionUpdateAsync(Guid entryId);

    /// <summary>
    /// Gets notification history for an entry.
    /// </summary>
    Task<IReadOnlyList<WaitlistNotification>> GetNotificationHistoryAsync(Guid entryId);

    // ============================================================================
    // Automatic Features
    // ============================================================================

    /// <summary>
    /// Finds the next entry suitable for a given table.
    /// </summary>
    Task<WaitlistEntry?> FindNextSuitableEntryAsync(int tableCapacity, IReadOnlyList<string>? tableTags = null);

    /// <summary>
    /// Expires entries that have exceeded the maximum wait time.
    /// </summary>
    Task<IReadOnlyList<Guid>> ExpireOldEntriesAsync();

    /// <summary>
    /// Recalculates and updates all estimates.
    /// </summary>
    Task RecalculateEstimatesAsync();

    // ============================================================================
    // Queries
    // ============================================================================

    Task<int> GetWaitingCountAsync();
    Task<IReadOnlyList<WaitlistEntry>> GetEntriesAsync();
    Task<IReadOnlyList<WaitlistEntry>> GetEntriesByStatusAsync(WaitlistStatus status);
    Task<WaitlistEntry?> GetEntryAsync(Guid entryId);

    // ============================================================================
    // Settings
    // ============================================================================

    Task<WaitlistSettings> GetSettingsAsync();
    Task UpdateSettingsAsync(UpdateWaitlistSettingsCommand command);

    Task<bool> ExistsAsync();
}
