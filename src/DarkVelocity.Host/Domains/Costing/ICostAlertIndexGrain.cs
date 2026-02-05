namespace DarkVelocity.Host.Grains;

// ============================================================================
// Cost Alert Index - Summary and Filter Types
// ============================================================================

/// <summary>
/// Alert status for filtering.
/// </summary>
public enum CostAlertStatus
{
    Active,
    Acknowledged,
    All
}

/// <summary>
/// Summary of a cost alert for indexing.
/// </summary>
[GenerateSerializer]
public record CostAlertSummary(
    [property: Id(0)] Guid AlertId,
    [property: Id(1)] CostAlertType AlertType,
    [property: Id(2)] string? RecipeName,
    [property: Id(3)] string? IngredientName,
    [property: Id(4)] string? MenuItemName,
    [property: Id(5)] decimal ChangePercent,
    [property: Id(6)] bool IsAcknowledged,
    [property: Id(7)] CostAlertAction ActionTaken,
    [property: Id(8)] DateTime CreatedAt,
    [property: Id(9)] DateTime? AcknowledgedAt);

/// <summary>
/// Query parameters for filtering alerts.
/// </summary>
[GenerateSerializer]
public record CostAlertQuery(
    [property: Id(0)] CostAlertStatus? Status,
    [property: Id(1)] CostAlertType? AlertType,
    [property: Id(2)] DateTime? FromDate,
    [property: Id(3)] DateTime? ToDate,
    [property: Id(4)] int Skip = 0,
    [property: Id(5)] int Take = 50);

/// <summary>
/// Result of querying cost alerts.
/// </summary>
[GenerateSerializer]
public record CostAlertQueryResult(
    [property: Id(0)] IReadOnlyList<CostAlertSummary> Alerts,
    [property: Id(1)] int TotalCount,
    [property: Id(2)] int ActiveCount,
    [property: Id(3)] int AcknowledgedCount);

// ============================================================================
// Cost Alert Index Events
// ============================================================================

[GenerateSerializer]
public record AlertIndexedEvent(
    [property: Id(0)] Guid AlertId,
    [property: Id(1)] CostAlertSummary Summary,
    [property: Id(2)] DateTime IndexedAt);

[GenerateSerializer]
public record AlertRemovedEvent(
    [property: Id(0)] Guid AlertId,
    [property: Id(1)] DateTime RemovedAt);

[GenerateSerializer]
public record AlertStatusUpdatedEvent(
    [property: Id(0)] Guid AlertId,
    [property: Id(1)] bool IsAcknowledged,
    [property: Id(2)] CostAlertAction ActionTaken,
    [property: Id(3)] DateTime? AcknowledgedAt,
    [property: Id(4)] DateTime UpdatedAt);

// ============================================================================
// Cost Alert Index Grain Interface
// ============================================================================

/// <summary>
/// Index grain for querying cost alerts within an organization.
/// Maintains a list of all alert IDs with summaries for efficient querying.
/// Key: "org:{orgId}:index:costalerts"
/// </summary>
public interface ICostAlertIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Registers a new cost alert in the index.
    /// </summary>
    Task RegisterAsync(Guid alertId, CostAlertSummary summary);

    /// <summary>
    /// Updates an existing alert's status in the index.
    /// </summary>
    Task UpdateStatusAsync(Guid alertId, bool isAcknowledged, CostAlertAction actionTaken, DateTime? acknowledgedAt);

    /// <summary>
    /// Removes an alert from the index.
    /// </summary>
    Task RemoveAsync(Guid alertId);

    /// <summary>
    /// Queries alerts with optional filtering.
    /// </summary>
    Task<CostAlertQueryResult> QueryAsync(CostAlertQuery query);

    /// <summary>
    /// Gets all alert IDs in the index.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAllAlertIdsAsync();

    /// <summary>
    /// Gets the count of active (unacknowledged) alerts.
    /// </summary>
    Task<int> GetActiveCountAsync();

    /// <summary>
    /// Gets a summary of an alert by ID.
    /// </summary>
    Task<CostAlertSummary?> GetByIdAsync(Guid alertId);

    /// <summary>
    /// Clears all alerts from the index.
    /// </summary>
    Task ClearAsync();
}
