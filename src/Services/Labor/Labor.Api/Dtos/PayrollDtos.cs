using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Labor.Api.Dtos;

/// <summary>
/// Full payroll period details response.
/// </summary>
public class PayrollPeriodDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalRegularHours { get; set; }
    public decimal TotalOvertimeHours { get; set; }
    public decimal TotalGrossPay { get; set; }
    public decimal TotalTips { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ExportedAt { get; set; }
    public string? ExportFormat { get; set; }
    public int EntryCount { get; set; }
    public List<PayrollEntryDto> Entries { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Summary payroll period for list views.
/// </summary>
public class PayrollPeriodSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalRegularHours { get; set; }
    public decimal TotalOvertimeHours { get; set; }
    public decimal TotalGrossPay { get; set; }
    public int EntryCount { get; set; }
}

/// <summary>
/// Payroll entry for an employee.
/// </summary>
public class PayrollEntryDto : HalResource
{
    public Guid Id { get; set; }
    public Guid PayrollPeriodId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public decimal RegularHours { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal RegularPay { get; set; }
    public decimal OvertimePay { get; set; }
    public decimal TipIncome { get; set; }
    public decimal GrossPay { get; set; }
    public decimal Adjustments { get; set; }
    public string? AdjustmentNotes { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Request to create a payroll period.
/// </summary>
public record CreatePayrollPeriodRequest(
    DateOnly PeriodStart,
    DateOnly PeriodEnd);

/// <summary>
/// Request to adjust a payroll entry.
/// </summary>
public record AdjustPayrollEntryRequest(
    decimal Adjustments,
    string? AdjustmentNotes = null);

/// <summary>
/// Request to export payroll.
/// </summary>
public record ExportPayrollRequest(
    string Format);

/// <summary>
/// Employee payroll history.
/// </summary>
public class EmployeePayrollHistoryDto : HalResource
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeNumber { get; set; } = string.Empty;
    public List<PayrollEntryDto> Entries { get; set; } = new();
    public decimal TotalRegularHours { get; set; }
    public decimal TotalOvertimeHours { get; set; }
    public decimal TotalGrossPay { get; set; }
    public decimal TotalTips { get; set; }
}
