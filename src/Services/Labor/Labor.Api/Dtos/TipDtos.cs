using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Labor.Api.Dtos;

/// <summary>
/// Full tip pool details response.
/// </summary>
public class TipPoolDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public DateOnly Date { get; set; }
    public Guid? SalesPeriodId { get; set; }
    public decimal TotalTips { get; set; }
    public string DistributionMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? CalculatedAt { get; set; }
    public DateTime? DistributedAt { get; set; }
    public Guid? DistributedByUserId { get; set; }
    public string? Notes { get; set; }
    public int DistributionCount { get; set; }
    public List<TipDistributionDto> Distributions { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Summary tip pool for list views.
/// </summary>
public class TipPoolSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public DateOnly Date { get; set; }
    public decimal TotalTips { get; set; }
    public string DistributionMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int DistributionCount { get; set; }
}

/// <summary>
/// Tip distribution to an employee.
/// </summary>
public class TipDistributionDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TipPoolId { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public decimal HoursWorked { get; set; }
    public int? PointsEarned { get; set; }
    public decimal TipShare { get; set; }
    public decimal TipPercentage { get; set; }
    public decimal? DeclaredTips { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
}

/// <summary>
/// Tip pool rule configuration.
/// </summary>
public class TipPoolRuleDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LocationId { get; set; }
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public decimal PoolSharePercentage { get; set; }
    public decimal DistributionWeight { get; set; }
    public decimal? MinimumHoursToQualify { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Request to create a tip pool.
/// </summary>
public record CreateTipPoolRequest(
    DateOnly Date,
    decimal TotalTips,
    string DistributionMethod = "hours",
    Guid? SalesPeriodId = null,
    string? Notes = null);

/// <summary>
/// Request to update a tip pool.
/// </summary>
public record UpdateTipPoolRequest(
    decimal? TotalTips = null,
    string? DistributionMethod = null,
    string? Notes = null);

/// <summary>
/// Request to adjust a tip distribution.
/// </summary>
public record AdjustTipDistributionRequest(
    decimal? TipShare = null,
    decimal? DeclaredTips = null);

/// <summary>
/// Request to create a tip pool rule.
/// </summary>
public record CreateTipPoolRuleRequest(
    Guid RoleId,
    decimal PoolSharePercentage,
    decimal DistributionWeight = 1.0m,
    decimal? MinimumHoursToQualify = null);

/// <summary>
/// Request to update a tip pool rule.
/// </summary>
public record UpdateTipPoolRuleRequest(
    decimal? PoolSharePercentage = null,
    decimal? DistributionWeight = null,
    decimal? MinimumHoursToQualify = null,
    bool? IsActive = null);

/// <summary>
/// Employee tip history.
/// </summary>
public class EmployeeTipHistoryDto : HalResource
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public decimal TotalTips { get; set; }
    public decimal TotalHoursWorked { get; set; }
    public decimal AverageTipsPerHour { get; set; }
    public List<TipDistributionDto> Distributions { get; set; } = new();
}
