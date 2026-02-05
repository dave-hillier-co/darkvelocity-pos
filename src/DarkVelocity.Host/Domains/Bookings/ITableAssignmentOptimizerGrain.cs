using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Table Assignment Optimizer Types
// ============================================================================

[GenerateSerializer]
public record TableAssignmentRequest(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] int PartySize,
    [property: Id(2)] DateTime BookingTime,
    [property: Id(3)] TimeSpan Duration,
    [property: Id(4)] string? SeatingPreference = null,
    [property: Id(5)] bool IsVip = false,
    [property: Id(6)] Guid? PreferredServerId = null,
    [property: Id(7)] Guid? PreferredTableId = null);

[GenerateSerializer]
public record TableAssignmentResult
{
    [Id(0)] public bool Success { get; init; }
    [Id(1)] public IReadOnlyList<TableRecommendation> Recommendations { get; init; } = [];
    [Id(2)] public string? Message { get; init; }
}

[GenerateSerializer]
public record TableRecommendation
{
    [Id(0)] public Guid TableId { get; init; }
    [Id(1)] public string TableNumber { get; init; } = string.Empty;
    [Id(2)] public int Capacity { get; init; }
    [Id(3)] public int Score { get; init; }
    [Id(4)] public IReadOnlyList<string> Reasons { get; init; } = [];
    [Id(5)] public bool RequiresCombination { get; init; }
    [Id(6)] public IReadOnlyList<Guid>? CombinedTableIds { get; init; }
    [Id(7)] public Guid? ServerId { get; init; }
    [Id(8)] public string? ServerName { get; init; }
}

[GenerateSerializer]
public record ServerSection
{
    [Id(0)] public Guid ServerId { get; init; }
    [Id(1)] public string ServerName { get; init; } = string.Empty;
    [Id(2)] public IReadOnlyList<Guid> TableIds { get; init; } = [];
    [Id(3)] public int CurrentCoverCount { get; init; }
    [Id(4)] public int MaxCovers { get; init; }
}

[GenerateSerializer]
public record UpdateServerSectionCommand(
    [property: Id(0)] Guid ServerId,
    [property: Id(1)] string ServerName,
    [property: Id(2)] IReadOnlyList<Guid> TableIds,
    [property: Id(3)] int MaxCovers = 30);

// ============================================================================
// Table Assignment Optimizer Grain Interface
// ============================================================================

/// <summary>
/// Grain for optimizing table assignments for bookings.
/// Considers: size match, server sections, VIP preferences, workload balance.
/// Key: "{orgId}:{siteId}:tableoptimizer"
/// </summary>
public interface ITableAssignmentOptimizerGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid organizationId, Guid siteId);

    /// <summary>
    /// Gets optimal table recommendations for a booking.
    /// </summary>
    Task<TableAssignmentResult> GetRecommendationsAsync(TableAssignmentRequest request);

    /// <summary>
    /// Auto-assigns a table for a booking based on optimization.
    /// </summary>
    Task<TableRecommendation?> AutoAssignAsync(TableAssignmentRequest request);

    /// <summary>
    /// Registers a table with its properties for optimization.
    /// </summary>
    Task RegisterTableAsync(Guid tableId, string tableNumber, int minCapacity, int maxCapacity, bool isCombinable, IReadOnlyList<string>? tags = null);

    /// <summary>
    /// Removes a table from optimization.
    /// </summary>
    Task UnregisterTableAsync(Guid tableId);

    /// <summary>
    /// Updates server sections for workload balancing.
    /// </summary>
    Task UpdateServerSectionAsync(UpdateServerSectionCommand command);

    /// <summary>
    /// Removes a server section.
    /// </summary>
    Task RemoveServerSectionAsync(Guid serverId);

    /// <summary>
    /// Gets all server sections.
    /// </summary>
    Task<IReadOnlyList<ServerSection>> GetServerSectionsAsync();

    /// <summary>
    /// Records that a table is being used (for workload tracking).
    /// </summary>
    Task RecordTableUsageAsync(Guid tableId, Guid serverId, int covers);

    /// <summary>
    /// Clears table usage (when guests depart).
    /// </summary>
    Task ClearTableUsageAsync(Guid tableId);

    /// <summary>
    /// Gets current server workload summary.
    /// </summary>
    Task<IReadOnlyList<ServerWorkload>> GetServerWorkloadsAsync();

    Task<bool> ExistsAsync();
}

[GenerateSerializer]
public record ServerWorkload
{
    [Id(0)] public Guid ServerId { get; init; }
    [Id(1)] public string ServerName { get; init; } = string.Empty;
    [Id(2)] public int CurrentCovers { get; init; }
    [Id(3)] public int MaxCovers { get; init; }
    [Id(4)] public int TableCount { get; init; }
    [Id(5)] public int OccupiedTableCount { get; init; }
    [Id(6)] public decimal LoadPercentage { get; init; }
}
