using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Customers.Api.Entities;

public class Reward : BaseEntity
{
    public Guid TenantId { get; set; }
    public Guid ProgramId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "discount"; // discount, free_item, voucher, experience

    public int PointsCost { get; set; }
    public decimal? Value { get; set; } // monetary value if applicable
    public Guid? MenuItemId { get; set; } // for free item rewards
    public decimal? DiscountPercentage { get; set; }

    public int? MaxRedemptionsPerCustomer { get; set; }
    public int? TotalAvailable { get; set; }
    public int TotalRedeemed { get; set; }

    public DateTime ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public LoyaltyProgram? Program { get; set; }
    public ICollection<CustomerReward> CustomerRewards { get; set; } = new List<CustomerReward>();
}
