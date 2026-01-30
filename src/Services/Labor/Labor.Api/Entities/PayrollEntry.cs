using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Represents an individual employee's payroll entry for a period.
/// </summary>
public class PayrollEntry : BaseEntity
{
    /// <summary>
    /// Reference to the payroll period.
    /// </summary>
    public Guid PayrollPeriodId { get; set; }

    /// <summary>
    /// Reference to the employee.
    /// </summary>
    public Guid EmployeeId { get; set; }

    /// <summary>
    /// Total regular hours worked.
    /// </summary>
    public decimal RegularHours { get; set; }

    /// <summary>
    /// Total overtime hours worked.
    /// </summary>
    public decimal OvertimeHours { get; set; }

    /// <summary>
    /// Calculated regular pay.
    /// </summary>
    public decimal RegularPay { get; set; }

    /// <summary>
    /// Calculated overtime pay.
    /// </summary>
    public decimal OvertimePay { get; set; }

    /// <summary>
    /// Total tip income for the period.
    /// </summary>
    public decimal TipIncome { get; set; }

    /// <summary>
    /// Total gross pay (regular + overtime + tips + adjustments).
    /// </summary>
    public decimal GrossPay { get; set; }

    /// <summary>
    /// Manual adjustments (positive or negative).
    /// </summary>
    public decimal Adjustments { get; set; }

    /// <summary>
    /// Notes explaining adjustments.
    /// </summary>
    public string? AdjustmentNotes { get; set; }

    /// <summary>
    /// Status: pending, approved, disputed.
    /// </summary>
    public string Status { get; set; } = "pending";

    // Navigation properties
    public PayrollPeriod? PayrollPeriod { get; set; }
    public Employee? Employee { get; set; }
}
