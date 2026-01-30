using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Customers.Api.Entities;

public class LoyaltyProgram : BaseEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "points"; // points, stamps, tiered, cashback
    public string Status { get; set; } = "active"; // active, paused, ended

    // Points configuration
    public decimal PointsPerCurrencyUnit { get; set; } = 1; // e.g., 1 point per EUR
    public decimal PointsValueInCurrency { get; set; } = 0.01m; // e.g., 100 points = 1 EUR
    public int MinimumRedemption { get; set; } = 100; // minimum points to redeem
    public int? PointsExpireAfterDays { get; set; }

    // Bonuses
    public int? WelcomeBonus { get; set; }
    public int? BirthdayBonus { get; set; }
    public int? ReferralBonus { get; set; }

    public string? TermsAndConditions { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public Dictionary<string, object>? Settings { get; set; }

    // Navigation properties
    public ICollection<LoyaltyTier> Tiers { get; set; } = new List<LoyaltyTier>();
    public ICollection<CustomerLoyalty> Members { get; set; } = new List<CustomerLoyalty>();
    public ICollection<Reward> Rewards { get; set; } = new List<Reward>();
}
