using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Fiscalisation.Api.Entities;

/// <summary>
/// Represents a tax rate configuration for fiscal reporting.
/// Maps tax rates to fiscal codes required by KassenSichV (e.g., "A" = 19%, "B" = 7%).
/// </summary>
public class TaxRate : BaseEntity
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// ISO 3166-1 alpha-2 country code
    /// </summary>
    public string CountryCode { get; set; } = "DE";

    /// <summary>
    /// Tax rate as decimal (e.g., 0.19 for 19%)
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Fiscal code used in DSFinV-K exports.
    /// For Germany: A=19%, B=7%, C=10.7%, D=5.5%, E=0%
    /// </summary>
    public string FiscalCode { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Date from which this rate is effective
    /// </summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>
    /// Date until which this rate is effective (null = indefinite)
    /// </summary>
    public DateTime? EffectiveTo { get; set; }

    public bool IsActive { get; set; } = true;
}
