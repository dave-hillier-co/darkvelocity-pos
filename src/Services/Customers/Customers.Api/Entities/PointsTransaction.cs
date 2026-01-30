using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Customers.Api.Entities;

public class PointsTransaction : BaseEntity
{
    public Guid CustomerLoyaltyId { get; set; }

    public string TransactionType { get; set; } = "earn"; // earn, redeem, expire, adjust, bonus, referral
    public int Points { get; set; } // positive for earn, negative for redeem
    public int BalanceBefore { get; set; }
    public int BalanceAfter { get; set; }

    public Guid? OrderId { get; set; }
    public Guid? LocationId { get; set; }
    public string Description { get; set; } = string.Empty;

    public DateTime? ExpiresAt { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public Guid? ProcessedByUserId { get; set; }

    public Dictionary<string, object>? Metadata { get; set; }

    // Navigation property
    public CustomerLoyalty? CustomerLoyalty { get; set; }
}
