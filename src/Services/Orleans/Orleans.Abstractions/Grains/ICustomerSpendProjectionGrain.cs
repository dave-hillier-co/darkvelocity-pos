using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Grains;

/// <summary>
/// Command to record customer spend from an order.
/// </summary>
public record RecordSpendCommand(
    Guid OrderId,
    Guid SiteId,
    decimal NetSpend,
    decimal GrossSpend,
    decimal DiscountAmount,
    int ItemCount,
    DateOnly TransactionDate);

/// <summary>
/// Command to reverse spend (for voids/refunds).
/// </summary>
public record ReverseSpendCommand(
    Guid OrderId,
    decimal Amount,
    string Reason);

/// <summary>
/// Command to redeem loyalty points.
/// </summary>
public record RedeemPointsCommand(
    Guid OrderId,
    int Points,
    string RewardType);

/// <summary>
/// Result of recording spend.
/// </summary>
public record RecordSpendResult(
    int PointsEarned,
    int TotalPoints,
    string CurrentTier,
    bool TierChanged,
    string? NewTier);

/// <summary>
/// Result of redeeming points.
/// </summary>
public record RedeemPointsResult(
    decimal DiscountValue,
    int RemainingPoints);

/// <summary>
/// Snapshot of customer loyalty status.
/// </summary>
public record CustomerLoyaltySnapshot(
    Guid CustomerId,
    decimal LifetimeSpend,
    decimal YearToDateSpend,
    int AvailablePoints,
    string CurrentTier,
    decimal TierMultiplier,
    decimal SpendToNextTier,
    string? NextTier,
    int LifetimeTransactions,
    DateTime? LastTransactionAt);

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
    Task<RedeemPointsResult> RedeemPointsAsync(RedeemPointsCommand command);

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
    Task ConfigureTiersAsync(List<LoyaltyTier> tiers);

    /// <summary>
    /// Checks if the projection exists.
    /// </summary>
    Task<bool> ExistsAsync();
}
