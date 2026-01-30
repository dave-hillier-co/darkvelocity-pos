using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Accounting.Api.Dtos;

// Response DTOs

public class CostCenterDto : HalResource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Request DTOs

public record CreateCostCenterRequest(
    string Code,
    string Name,
    Guid? LocationId = null,
    string? Description = null);

public record UpdateCostCenterRequest(
    string? Name = null,
    Guid? LocationId = null,
    bool? IsActive = null,
    string? Description = null);
