using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Accounting.Api.Entities;

/// <summary>
/// Represents calculated tax liability for a period.
/// </summary>
public class TaxLiability : BaseEntity
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// Optional location filter (null for tenant-wide)
    /// </summary>
    public Guid? LocationId { get; set; }

    /// <summary>
    /// Period identifier (e.g., "2026-Q1", "2026-01")
    /// </summary>
    public required string Period { get; set; }

    /// <summary>
    /// Tax code (e.g., "VAT-19", "VAT-7")
    /// </summary>
    public required string TaxCode { get; set; }

    /// <summary>
    /// Tax rate as decimal (e.g., 0.19 for 19%)
    /// </summary>
    public decimal TaxRate { get; set; }

    /// <summary>
    /// Total taxable amount for the period
    /// </summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>
    /// Total tax amount owed
    /// </summary>
    public decimal TaxAmount { get; set; }

    public TaxLiabilityStatus Status { get; set; } = TaxLiabilityStatus.Calculated;

    public DateTime? FiledAt { get; set; }

    public DateTime? PaidAt { get; set; }

    /// <summary>
    /// Reference number from tax authority filing
    /// </summary>
    public string? FilingReference { get; set; }

    /// <summary>
    /// Currency code (ISO 4217)
    /// </summary>
    public string Currency { get; set; } = "EUR";
}

public enum TaxLiabilityStatus
{
    Calculated,
    Filed,
    Paid
}
