using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Booking.Api.Entities;

/// <summary>
/// Defines deposit requirements for bookings at a location
/// </summary>
public class DepositPolicy : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }

    /// <summary>
    /// Name of this policy (e.g., "Standard", "Large Party", "Special Events")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of when this policy applies
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Minimum party size for this policy to apply
    /// </summary>
    public int MinPartySize { get; set; } = 1;

    /// <summary>
    /// Maximum party size for this policy (null = no limit)
    /// </summary>
    public int? MaxPartySize { get; set; }

    /// <summary>
    /// How the deposit amount is calculated
    /// </summary>
    public string DepositType { get; set; } = "per_person"; // per_person, flat_rate, percentage

    /// <summary>
    /// Amount per person (if DepositType = per_person)
    /// </summary>
    public decimal? AmountPerPerson { get; set; }

    /// <summary>
    /// Flat deposit amount (if DepositType = flat_rate)
    /// </summary>
    public decimal? FlatAmount { get; set; }

    /// <summary>
    /// Percentage of minimum spend (if DepositType = percentage)
    /// </summary>
    public decimal? PercentageRate { get; set; }

    /// <summary>
    /// Minimum total deposit amount regardless of calculation
    /// </summary>
    public decimal? MinimumAmount { get; set; }

    /// <summary>
    /// Maximum total deposit amount regardless of calculation
    /// </summary>
    public decimal? MaximumAmount { get; set; }

    /// <summary>
    /// Currency code (ISO 4217)
    /// </summary>
    public string CurrencyCode { get; set; } = "GBP";

    /// <summary>
    /// Hours before booking that cancellation allows full refund
    /// </summary>
    public int RefundableUntilHours { get; set; } = 24;

    /// <summary>
    /// Percentage refunded if cancelled within the refundable window
    /// </summary>
    public decimal RefundPercentage { get; set; } = 100;

    /// <summary>
    /// Whether deposit is forfeited on no-show
    /// </summary>
    public bool ForfeitsOnNoShow { get; set; } = true;

    /// <summary>
    /// Whether this policy applies to specific days (null = all days)
    /// Comma-separated: "Monday,Tuesday,Saturday"
    /// </summary>
    public string? ApplicableDays { get; set; }

    /// <summary>
    /// Whether this policy applies to specific time ranges
    /// </summary>
    public TimeOnly? ApplicableFromTime { get; set; }
    public TimeOnly? ApplicableToTime { get; set; }

    /// <summary>
    /// Whether this policy is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Priority for policy selection (higher = checked first)
    /// </summary>
    public int Priority { get; set; } = 0;
}
