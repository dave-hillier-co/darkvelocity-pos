using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Labor.Api.Dtos;

/// <summary>
/// Full role details response.
/// </summary>
public class RoleDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public decimal? DefaultHourlyRate { get; set; }
    public string Color { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public List<string> RequiredCertifications { get; set; } = new();
    public bool IsActive { get; set; }
    public int EmployeeCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Summary role for list views.
/// </summary>
public class RoleSummaryDto : HalResource
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal? DefaultHourlyRate { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Request to create a new role.
/// </summary>
public record CreateRoleRequest(
    string Name,
    string Department = "foh",
    decimal? DefaultHourlyRate = null,
    string Color = "#3B82F6",
    int SortOrder = 0,
    List<string>? RequiredCertifications = null);

/// <summary>
/// Request to update a role.
/// </summary>
public record UpdateRoleRequest(
    string? Name = null,
    string? Department = null,
    decimal? DefaultHourlyRate = null,
    string? Color = null,
    int? SortOrder = null,
    List<string>? RequiredCertifications = null,
    bool? IsActive = null);
