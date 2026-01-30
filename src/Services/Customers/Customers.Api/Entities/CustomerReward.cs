using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Customers.Api.Entities;

public class CustomerReward : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Guid RewardId { get; set; }

    public string Code { get; set; } = string.Empty; // unique redemption code
    public string Status { get; set; } = "available"; // available, redeemed, expired, cancelled

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RedeemedAt { get; set; }
    public Guid? RedeemedOrderId { get; set; }
    public Guid? RedeemedLocationId { get; set; }

    // Navigation properties
    public Customer? Customer { get; set; }
    public Reward? Reward { get; set; }
}
