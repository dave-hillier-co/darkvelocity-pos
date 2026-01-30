using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Accounting.Api.Entities;

/// <summary>
/// Represents a snapshot of gift card liability at a point in time.
/// </summary>
public class GiftCardLiability : BaseEntity
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// Date this liability snapshot was taken
    /// </summary>
    public DateOnly AsOfDate { get; set; }

    /// <summary>
    /// Total number of outstanding gift cards with positive balance
    /// </summary>
    public int TotalOutstandingCards { get; set; }

    /// <summary>
    /// Total liability amount
    /// </summary>
    public decimal TotalLiability { get; set; }

    /// <summary>
    /// Currency code (ISO 4217)
    /// </summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>
    /// Breakdown by program (stored as JSON)
    /// </summary>
    public string? BreakdownByProgramJson { get; set; }

    /// <summary>
    /// Breakdown by age bucket (stored as JSON)
    /// e.g., {"0-30": 5000, "30-60": 3000, "60-90": 1500, "90+": 500}
    /// </summary>
    public string? BreakdownByAgeJson { get; set; }

    /// <summary>
    /// Timestamp when this liability was calculated
    /// </summary>
    public DateTime CalculatedAt { get; set; }
}
