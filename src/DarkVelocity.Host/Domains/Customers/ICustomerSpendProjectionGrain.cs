using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Command to record customer spend from an order.
/// </summary>
[GenerateSerializer]
public record RecordSpendCommand(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] decimal NetSpend,
    [property: Id(3)] decimal GrossSpend,
    [property: Id(4)] decimal DiscountAmount,
    [property: Id(5)] decimal TaxAmount,
    [property: Id(6)] int ItemCount,
    [property: Id(7)] DateOnly TransactionDate);

/// <summary>
/// Command to reverse spend (for voids/refunds).
/// </summary>
[GenerateSerializer]
public record ReverseSpendCommand(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] decimal Amount,
    [property: Id(2)] string Reason);

/// <summary>
/// Command to redeem loyalty points.
/// </summary>
[GenerateSerializer]
public record RedeemSpendPointsCommand(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] int Points,
    [property: Id(2)] string RewardType);

/// <summary>
/// Result of recording spend.
/// </summary>
[GenerateSerializer]
public record RecordSpendResult(
    [property: Id(0)] int PointsEarned,
    [property: Id(1)] int TotalPoints,
    [property: Id(2)] string CurrentTier,
    [property: Id(3)] bool TierChanged,
    [property: Id(4)] string? NewTier);

/// <summary>
/// Result of redeeming points.
/// </summary>
[GenerateSerializer]
public record RedeemPointsResult(
    [property: Id(0)] decimal DiscountValue,
    [property: Id(1)] int RemainingPoints);

/// <summary>
/// Snapshot of customer loyalty status.
/// </summary>
[GenerateSerializer]
public record CustomerLoyaltySnapshot(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] decimal LifetimeSpend,
    [property: Id(2)] decimal YearToDateSpend,
    [property: Id(3)] int AvailablePoints,
    [property: Id(4)] string CurrentTier,
    [property: Id(5)] decimal TierMultiplier,
    [property: Id(6)] decimal SpendToNextTier,
    [property: Id(7)] string? NextTier,
    [property: Id(8)] int LifetimeTransactions,
    [property: Id(9)] DateTime? LastTransactionAt);

/// <summary>
/// Grain that maintains a projection of customer spend for loyalty calculations.
/// Loyalty is derived from accounting spend data, not tracked separately.
/// Key: "{orgId}:customerspend:{customerId}"
/// </summary>
public interface ICustomerSpendProjectionGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initializes the projection for a customer.
    /// </summary>
    Task InitializeAsync(Guid organizationId, Guid customerId);

    /// <summary>
    /// Records spend from an order - calculates and awards points.
    /// </summary>
    Task<RecordSpendResult> RecordSpendAsync(RecordSpendCommand command);

    /// <summary>
    /// Reverses spend for a void/refund.
    /// </summary>
    Task ReverseSpendAsync(ReverseSpendCommand command);

    /// <summary>
    /// Redeems points for a discount.
    /// </summary>
    Task<RedeemPointsResult> RedeemPointsAsync(RedeemSpendPointsCommand command);

    /// <summary>
    /// Gets the current loyalty snapshot.
    /// </summary>
    Task<CustomerLoyaltySnapshot> GetSnapshotAsync();

    /// <summary>
    /// Gets the full projection state.
    /// </summary>
    Task<CustomerSpendState> GetStateAsync();

    /// <summary>
    /// Gets available points.
    /// </summary>
    Task<int> GetAvailablePointsAsync();

    /// <summary>
    /// Checks if customer has enough points for redemption.
    /// </summary>
    Task<bool> HasSufficientPointsAsync(int points);

    /// <summary>
    /// Configures tier thresholds (typically done once per org).
    /// </summary>
    Task ConfigureTiersAsync(List<SpendTier> tiers);

    /// <summary>
    /// Checks if the projection exists.
    /// </summary>
    Task<bool> ExistsAsync();
}
