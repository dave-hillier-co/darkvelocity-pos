using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.GiftCards.Api.Dtos;

// ============================================
// Gift Card DTOs
// ============================================

public class GiftCardDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public Guid ProgramId { get; set; }
    public string? ProgramName { get; set; }
    public Guid? DesignId { get; set; }
    public string? DesignName { get; set; }

    // Card details (number is masked for security)
    public string CardNumber { get; set; } = string.Empty;
    public string MaskedCardNumber { get; set; } = string.Empty;
    public bool HasPin { get; set; }
    public string CardType { get; set; } = string.Empty;

    // Balance
    public decimal InitialBalance { get; set; }
    public decimal CurrentBalance { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;

    // Status
    public string Status { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public bool IsExpired { get; set; }
    public bool IsDepleted { get; set; }

    // Timestamps
    public DateTime IssuedAt { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? SuspendedAt { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTime CreatedAt { get; set; }

    // Recipient/sender info
    public string? RecipientName { get; set; }
    public string? RecipientEmail { get; set; }
    public string? GiftMessage { get; set; }
    public string? PurchaserName { get; set; }
    public string? PurchaserEmail { get; set; }

    // User tracking
    public Guid? IssuedByUserId { get; set; }
    public Guid? ActivatedByUserId { get; set; }

    // Additional
    public string? Notes { get; set; }
    public string? ExternalReference { get; set; }

    // Transaction history
    public List<GiftCardTransactionDto> RecentTransactions { get; set; } = new();
    public int TotalTransactionCount { get; set; }
}

public class GiftCardSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public string MaskedCardNumber { get; set; } = string.Empty;
    public string CardType { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public bool IsExpired { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public string? RecipientName { get; set; }
    public string? ProgramName { get; set; }
}

public class GiftCardBalanceDto : HalResource
{
    public Guid Id { get; set; }
    public string MaskedCardNumber { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ExpiryDate { get; set; }
    public bool IsExpired { get; set; }
    public bool CanRedeem { get; set; }
    public bool CanReload { get; set; }
}

// ============================================
// Gift Card Transaction DTOs
// ============================================

public class GiftCardTransactionDto : HalResource
{
    public Guid Id { get; set; }
    public Guid GiftCardId { get; set; }
    public Guid LocationId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid? UserId { get; set; }
    public string? Reason { get; set; }
    public DateTime ProcessedAt { get; set; }
    public string? ExternalReference { get; set; }
    public string? TransactionReference { get; set; }
    public string? Notes { get; set; }
}

// ============================================
// Gift Card Program DTOs
// ============================================

public class GiftCardProgramDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CardNumberPrefix { get; set; } = string.Empty;
    public int? DefaultExpiryMonths { get; set; }
    public decimal MinimumLoadAmount { get; set; }
    public decimal MaximumLoadAmount { get; set; }
    public decimal MaximumBalance { get; set; }
    public bool AllowReload { get; set; }
    public bool AllowPartialRedemption { get; set; }
    public bool RequirePin { get; set; }
    public bool IsActive { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Statistics
    public int TotalCardsIssued { get; set; }
    public int ActiveCardsCount { get; set; }
    public decimal TotalOutstandingBalance { get; set; }

    // Designs
    public List<GiftCardDesignDto> Designs { get; set; } = new();
}

public class GiftCardProgramSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public int ActiveCardsCount { get; set; }
    public decimal TotalOutstandingBalance { get; set; }
}

// ============================================
// Gift Card Design DTOs
// ============================================

public class GiftCardDesignDto : HalResource
{
    public Guid Id { get; set; }
    public Guid ProgramId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================
// Report DTOs
// ============================================

public class GiftCardLiabilityReportDto : HalResource
{
    public DateTime AsOfDate { get; set; }
    public decimal TotalOutstandingBalance { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public int TotalActiveCards { get; set; }
    public int TotalSuspendedCards { get; set; }
    public int TotalExpiredCards { get; set; }
    public int TotalDepletedCards { get; set; }

    // Breakdown by program
    public List<ProgramLiabilityDto> ByProgram { get; set; } = new();

    // Breakdown by age
    public List<AgeBucketLiabilityDto> ByAge { get; set; } = new();
}

public class ProgramLiabilityDto
{
    public Guid ProgramId { get; set; }
    public string ProgramName { get; set; } = string.Empty;
    public decimal OutstandingBalance { get; set; }
    public int ActiveCardsCount { get; set; }
}

public class AgeBucketLiabilityDto
{
    public string Bucket { get; set; } = string.Empty; // "0-30 days", "30-60 days", "60-90 days", "90+ days"
    public decimal OutstandingBalance { get; set; }
    public int CardsCount { get; set; }
}

public class GiftCardActivityReportDto : HalResource
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;

    // Summary
    public int CardsIssued { get; set; }
    public int CardsActivated { get; set; }
    public decimal TotalActivationAmount { get; set; }
    public int RedemptionCount { get; set; }
    public decimal TotalRedemptionAmount { get; set; }
    public int ReloadCount { get; set; }
    public decimal TotalReloadAmount { get; set; }
    public int CardsExpired { get; set; }
    public decimal ExpiredAmount { get; set; }

    // Daily breakdown
    public List<DailyActivityDto> DailyActivity { get; set; } = new();
}

public class DailyActivityDto
{
    public DateOnly Date { get; set; }
    public int CardsIssued { get; set; }
    public decimal ActivationAmount { get; set; }
    public int RedemptionCount { get; set; }
    public decimal RedemptionAmount { get; set; }
    public int ReloadCount { get; set; }
    public decimal ReloadAmount { get; set; }
}

public class ExpiringCardsReportDto : HalResource
{
    public DateTime AsOfDate { get; set; }
    public int ExpiringInNext30Days { get; set; }
    public decimal ExpiringIn30DaysAmount { get; set; }
    public int ExpiringInNext60Days { get; set; }
    public decimal ExpiringIn60DaysAmount { get; set; }
    public int ExpiringInNext90Days { get; set; }
    public decimal ExpiringIn90DaysAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;

    public List<GiftCardSummaryDto> ExpiringCards { get; set; } = new();
}

// ============================================
// Request DTOs
// ============================================

public record CreateGiftCardRequest(
    Guid ProgramId,
    decimal InitialBalance,
    string CardType = "physical",
    Guid? DesignId = null,
    string? RecipientName = null,
    string? RecipientEmail = null,
    string? GiftMessage = null,
    string? PurchaserName = null,
    string? PurchaserEmail = null,
    string? Notes = null,
    Guid? IssuedByUserId = null);

public record ActivateGiftCardRequest(
    string? Pin = null,
    Guid? ActivatedByUserId = null);

public record RedeemGiftCardRequest(
    decimal Amount,
    string? Pin = null,
    Guid? OrderId = null,
    Guid? PaymentId = null,
    Guid? UserId = null,
    string? Notes = null);

public record ReloadGiftCardRequest(
    decimal Amount,
    Guid? UserId = null,
    string? Notes = null);

public record RefundToGiftCardRequest(
    decimal Amount,
    Guid? OrderId = null,
    Guid? UserId = null,
    string? Reason = null);

public record AdjustBalanceRequest(
    decimal Amount,
    string Reason,
    Guid? UserId = null);

public record SuspendGiftCardRequest(
    string Reason,
    Guid? SuspendedByUserId = null);

public record ResumeGiftCardRequest(
    Guid? ResumedByUserId = null);

public record LookupGiftCardRequest(
    string CardNumber,
    string? Pin = null);

// ============================================
// Program Request DTOs
// ============================================

public record CreateGiftCardProgramRequest(
    string Name,
    string? Description = null,
    string CardNumberPrefix = "GC",
    int? DefaultExpiryMonths = null,
    decimal MinimumLoadAmount = 5.00m,
    decimal MaximumLoadAmount = 500.00m,
    decimal MaximumBalance = 1000.00m,
    bool AllowReload = true,
    bool AllowPartialRedemption = true,
    bool RequirePin = false,
    string CurrencyCode = "EUR");

public record UpdateGiftCardProgramRequest(
    string? Name = null,
    string? Description = null,
    int? DefaultExpiryMonths = null,
    decimal? MinimumLoadAmount = null,
    decimal? MaximumLoadAmount = null,
    decimal? MaximumBalance = null,
    bool? AllowReload = null,
    bool? AllowPartialRedemption = null,
    bool? RequirePin = null,
    bool? IsActive = null);

// ============================================
// Design Request DTOs
// ============================================

public record CreateGiftCardDesignRequest(
    string Name,
    string? Description = null,
    string? ImageUrl = null,
    string? ThumbnailUrl = null,
    bool IsDefault = false,
    int SortOrder = 0);

public record UpdateGiftCardDesignRequest(
    string? Name = null,
    string? Description = null,
    string? ImageUrl = null,
    string? ThumbnailUrl = null,
    bool? IsDefault = null,
    bool? IsActive = null,
    int? SortOrder = null);
