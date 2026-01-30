using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Accounting.Api.Entities;

/// <summary>
/// Represents a cost center for departmental expense tracking.
/// </summary>
public class CostCenter : BaseEntity
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// Unique code for the cost center (e.g., "FOH", "BOH", "BAR")
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Display name for the cost center
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Optional location assignment (null for tenant-wide cost centers)
    /// </summary>
    public Guid? LocationId { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Description of the cost center's purpose
    /// </summary>
    public string? Description { get; set; }
}
