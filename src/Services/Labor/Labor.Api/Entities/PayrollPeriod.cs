using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Represents a payroll period for processing employee wages.
/// </summary>
public class PayrollPeriod : BaseEntity
{
    /// <summary>
    /// The tenant this payroll period belongs to.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Start date of the pay period.
    /// </summary>
    public DateOnly PeriodStart { get; set; }

    /// <summary>
    /// End date of the pay period.
    /// </summary>
    public DateOnly PeriodEnd { get; set; }

    /// <summary>
    /// Status: open, processing, approved, exported, paid.
    /// </summary>
    public string Status { get; set; } = "open";

    /// <summary>
    /// Total regular hours across all employees.
    /// </summary>
    public decimal TotalRegularHours { get; set; }

    /// <summary>
    /// Total overtime hours across all employees.
    /// </summary>
    public decimal TotalOvertimeHours { get; set; }

    /// <summary>
    /// Total gross pay for the period.
    /// </summary>
    public decimal TotalGrossPay { get; set; }

    /// <summary>
    /// Total tips for the period.
    /// </summary>
    public decimal TotalTips { get; set; }

    /// <summary>
    /// When the payroll was processed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// User who approved the payroll.
    /// </summary>
    public Guid? ApprovedByUserId { get; set; }

    /// <summary>
    /// When the payroll was exported.
    /// </summary>
    public DateTime? ExportedAt { get; set; }

    /// <summary>
    /// Export format used: adp, gusto, paychex, datev, sage, generic.
    /// </summary>
    public string? ExportFormat { get; set; }

    // Navigation properties
    public ICollection<PayrollEntry> Entries { get; set; } = new List<PayrollEntry>();
}
