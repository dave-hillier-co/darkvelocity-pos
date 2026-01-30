using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Customers.Api.Dtos;

// LoyaltyProgram DTOs
public class LoyaltyProgramDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal PointsPerCurrencyUnit { get; set; }
    public decimal PointsValueInCurrency { get; set; }
    public int MinimumRedemption { get; set; }
    public int? PointsExpireAfterDays { get; set; }
    public int? WelcomeBonus { get; set; }
    public int? BirthdayBonus { get; set; }
    public int? ReferralBonus { get; set; }
    public string? TermsAndConditions { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<LoyaltyTierDto> Tiers { get; set; } = new();
}

public class LoyaltyProgramSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int MemberCount { get; set; }
}

public class CreateLoyaltyProgramRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "points";
    public decimal PointsPerCurrencyUnit { get; set; } = 1;
    public decimal PointsValueInCurrency { get; set; } = 0.01m;
    public int MinimumRedemption { get; set; } = 100;
    public int? PointsExpireAfterDays { get; set; }
    public int? WelcomeBonus { get; set; }
    public int? BirthdayBonus { get; set; }
    public int? ReferralBonus { get; set; }
    public string? TermsAndConditions { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}

public class UpdateLoyaltyProgramRequest
{
    public string? Name { get; set; }
    public string? Status { get; set; }
    public decimal? PointsPerCurrencyUnit { get; set; }
    public decimal? PointsValueInCurrency { get; set; }
    public int? MinimumRedemption { get; set; }
    public int? PointsExpireAfterDays { get; set; }
    public int? WelcomeBonus { get; set; }
    public int? BirthdayBonus { get; set; }
    public int? ReferralBonus { get; set; }
    public string? TermsAndConditions { get; set; }
    public DateOnly? EndDate { get; set; }
}

// LoyaltyTier DTOs
public class LoyaltyTierDto : HalResource
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinimumPoints { get; set; }
    public decimal PointsMultiplier { get; set; }
    public bool FreeDelivery { get; set; }
    public bool PriorityBooking { get; set; }
    public bool ExclusiveOffers { get; set; }
    public string? BirthdayReward { get; set; }
    public string? Color { get; set; }
    public string? IconUrl { get; set; }
    public int SortOrder { get; set; }
}

public class CreateLoyaltyTierRequest
{
    public string Name { get; set; } = string.Empty;
    public int MinimumPoints { get; set; }
    public decimal PointsMultiplier { get; set; } = 1.0m;
    public bool FreeDelivery { get; set; }
    public bool PriorityBooking { get; set; }
    public bool ExclusiveOffers { get; set; }
    public string? BirthdayReward { get; set; }
    public string? Color { get; set; }
    public string? IconUrl { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateLoyaltyTierRequest
{
    public string? Name { get; set; }
    public int? MinimumPoints { get; set; }
    public decimal? PointsMultiplier { get; set; }
    public bool? FreeDelivery { get; set; }
    public bool? PriorityBooking { get; set; }
    public bool? ExclusiveOffers { get; set; }
    public string? BirthdayReward { get; set; }
    public string? Color { get; set; }
    public string? IconUrl { get; set; }
    public int? SortOrder { get; set; }
}

// CustomerLoyalty DTOs
public class CustomerLoyaltyDto : HalResource
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public int CurrentPoints { get; set; }
    public int LifetimePoints { get; set; }
    public Guid? CurrentTierId { get; set; }
    public string? CurrentTierName { get; set; }
    public int TierQualifyingPoints { get; set; }
    public DateTime? TierExpiresAt { get; set; }
    public DateTime EnrolledAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

public class EnrollCustomerRequest
{
    public Guid ProgramId { get; set; }
}

public class EarnPointsRequest
{
    public int Points { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? LocationId { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class RedeemPointsRequest
{
    public int Points { get; set; }
    public Guid? RewardId { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class AdjustPointsRequest
{
    public int Points { get; set; }
    public string Reason { get; set; } = string.Empty;
}

// PointsTransaction DTOs
public class PointsTransactionDto : HalResource
{
    public Guid Id { get; set; }
    public Guid CustomerLoyaltyId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public int Points { get; set; }
    public int BalanceBefore { get; set; }
    public int BalanceAfter { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? LocationId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public DateTime ProcessedAt { get; set; }
}
