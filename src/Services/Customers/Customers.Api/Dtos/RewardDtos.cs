using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Customers.Api.Dtos;

// Reward DTOs
public class RewardDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProgramId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public int PointsCost { get; set; }
    public decimal? Value { get; set; }
    public Guid? MenuItemId { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public int? MaxRedemptionsPerCustomer { get; set; }
    public int? TotalAvailable { get; set; }
    public int TotalRedeemed { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RewardSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int PointsCost { get; set; }
    public decimal? Value { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; }
}

public class CreateRewardRequest
{
    public Guid ProgramId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = "discount";
    public int PointsCost { get; set; }
    public decimal? Value { get; set; }
    public Guid? MenuItemId { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public int? MaxRedemptionsPerCustomer { get; set; }
    public int? TotalAvailable { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? ImageUrl { get; set; }
}

public class UpdateRewardRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public int? PointsCost { get; set; }
    public decimal? Value { get; set; }
    public Guid? MenuItemId { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public int? MaxRedemptionsPerCustomer { get; set; }
    public int? TotalAvailable { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? ImageUrl { get; set; }
    public bool? IsActive { get; set; }
}

// CustomerReward DTOs
public class CustomerRewardDto : HalResource
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid RewardId { get; set; }
    public string RewardName { get; set; } = string.Empty;
    public string RewardType { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RedeemedAt { get; set; }
    public Guid? RedeemedOrderId { get; set; }
    public Guid? RedeemedLocationId { get; set; }
}

public class RedeemCustomerRewardRequest
{
    public Guid? OrderId { get; set; }
    public Guid? LocationId { get; set; }
}

// Referral DTOs
public class ReferralDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ReferrerCustomerId { get; set; }
    public string? ReferrerName { get; set; }
    public Guid? ReferredCustomerId { get; set; }
    public string? ReferredName { get; set; }
    public string ReferralCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ReferrerBonus { get; set; }
    public int RefereeBonus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class ReferralCodeDto : HalResource
{
    public string Code { get; set; } = string.Empty;
    public int ReferrerBonus { get; set; }
    public int RefereeBonus { get; set; }
}

public class ValidateReferralRequest
{
    public string Code { get; set; } = string.Empty;
}

public class ValidateReferralResponse : HalResource
{
    public bool IsValid { get; set; }
    public string? ReferrerName { get; set; }
    public int RefereeBonus { get; set; }
    public string? Message { get; set; }
}
