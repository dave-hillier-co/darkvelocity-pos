using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Defines tip pooling rules for a specific role at a location.
/// </summary>
public class TipPoolRule : BaseEntity, ILocationScoped
{
    /// <summary>
    /// The tenant this rule belongs to.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// The location this rule applies to.
    /// </summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// The role this rule applies to.
    /// </summary>
    public Guid RoleId { get; set; }

    /// <summary>
    /// Percentage of role's tips that go to the pool.
    /// </summary>
    public decimal PoolSharePercentage { get; set; }

    /// <summary>
    /// Weight used for weighted distribution (higher = larger share).
    /// </summary>
    public decimal DistributionWeight { get; set; } = 1.0m;

    /// <summary>
    /// Minimum hours required to qualify for tip distribution.
    /// </summary>
    public decimal? MinimumHoursToQualify { get; set; }

    /// <summary>
    /// Whether this rule is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Role? Role { get; set; }
}
