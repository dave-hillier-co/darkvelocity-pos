using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Customers.Api.Entities;

public class LoyaltyTier : BaseEntity
{
    public Guid ProgramId { get; set; }
    public string Name { get; set; } = string.Empty; // Bronze, Silver, Gold, Platinum
    public int MinimumPoints { get; set; } // threshold to reach tier
    public decimal PointsMultiplier { get; set; } = 1.0m; // e.g., 1.5x for Gold

    // Benefits
    public bool FreeDelivery { get; set; }
    public bool PriorityBooking { get; set; }
    public bool ExclusiveOffers { get; set; }
    public string? BirthdayReward { get; set; }
    public Dictionary<string, object>? AdditionalBenefits { get; set; }

    // Display
    public string? Color { get; set; } // for UI display
    public string? IconUrl { get; set; }
    public int SortOrder { get; set; }

    // Navigation property
    public LoyaltyProgram? Program { get; set; }
    public ICollection<CustomerLoyalty> Members { get; set; } = new List<CustomerLoyalty>();
}
