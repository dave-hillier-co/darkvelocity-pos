namespace DarkVelocity.Host.State;

/// <summary>
/// Loyalty tier configuration with spend thresholds.
/// </summary>
[GenerateSerializer]
public sealed class SpendTier
{
    [Id(0)] public string Name { get; set; } = "Bronze";
    [Id(1)] public decimal MinSpend { get; set; }
    [Id(2)] public decimal MaxSpend { get; set; } = decimal.MaxValue;
    [Id(3)] public decimal PointsMultiplier { get; set; } = 1.0m;
    [Id(4)] public decimal PointsPerDollar { get; set; } = 1.0m;
}

/// <summary>
/// Record of a spend transaction for the projection.
/// </summary>
[GenerateSerializer]
public sealed class SpendTransaction
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrderId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public decimal NetSpend { get; set; }
    [Id(4)] public decimal GrossSpend { get; set; }
    [Id(5)] public decimal DiscountAmount { get; set; }
    [Id(6)] public int PointsEarned { get; set; }
    [Id(7)] public DateOnly TransactionDate { get; set; }
    [Id(8)] public DateTime RecordedAt { get; set; }
}

/// <summary>
/// Record of points redemption.
/// </summary>
[GenerateSerializer]
public sealed class PointsRedemption
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrderId { get; set; }
    [Id(2)] public int PointsRedeemed { get; set; }
    [Id(3)] public decimal DiscountValue { get; set; }
    [Id(4)] public string RewardType { get; set; } = string.Empty;
    [Id(5)] public DateTime RedeemedAt { get; set; }
}

/// <summary>
/// State for customer spend projection - the source of truth for loyalty.
/// Key: "{orgId}:customerspend:{customerId}"
/// </summary>
[GenerateSerializer]
public sealed class CustomerSpendState
{
    [Id(0)] public Guid CustomerId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }

    // Cumulative spend tracking
    [Id(2)] public decimal LifetimeSpend { get; set; }
    [Id(3)] public decimal YearToDateSpend { get; set; }
    [Id(4)] public decimal MonthToDateSpend { get; set; }
    [Id(5)] public int LifetimeTransactions { get; set; }

    // Points tracking (derived from spend)
    [Id(6)] public int TotalPointsEarned { get; set; }
    [Id(7)] public int TotalPointsRedeemed { get; set; }
    [Id(8)] public int AvailablePoints { get; set; }

    // Current tier (calculated from lifetime spend)
    [Id(9)] public string CurrentTier { get; set; } = "Bronze";
    [Id(10)] public decimal CurrentTierMultiplier { get; set; } = 1.0m;
    [Id(11)] public decimal SpendToNextTier { get; set; }
    [Id(12)] public string? NextTier { get; set; }

    // Transaction history (last N transactions for audit)
    [Id(13)] public List<SpendTransaction> RecentTransactions { get; set; } = [];
    [Id(14)] public List<PointsRedemption> RecentRedemptions { get; set; } = [];

    // Timestamps
    [Id(15)] public DateTime? FirstTransactionAt { get; set; }
    [Id(16)] public DateTime? LastTransactionAt { get; set; }
    [Id(17)] public DateTime CreatedAt { get; set; }
    [Id(18)] public DateTime? UpdatedAt { get; set; }
    [Id(19)] public int Version { get; set; }

    // Period tracking for YTD/MTD reset
    [Id(20)] public int CurrentYear { get; set; }
    [Id(21)] public int CurrentMonth { get; set; }
}
