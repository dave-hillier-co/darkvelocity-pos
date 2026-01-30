using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Represents a daily tip pool for distribution among employees.
/// </summary>
public class TipPool : BaseEntity, ILocationScoped
{
    /// <summary>
    /// The tenant this tip pool belongs to.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// The location this tip pool is for.
    /// </summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// The date of the tip pool.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Reference to the sales period (if applicable).
    /// </summary>
    public Guid? SalesPeriodId { get; set; }

    /// <summary>
    /// Total tips collected for the pool.
    /// </summary>
    public decimal TotalTips { get; set; }

    /// <summary>
    /// Distribution method: equal, hours, points, custom.
    /// </summary>
    public string DistributionMethod { get; set; } = "hours";

    /// <summary>
    /// Status: pending, calculated, distributed, locked.
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// When the distribution was calculated.
    /// </summary>
    public DateTime? CalculatedAt { get; set; }

    /// <summary>
    /// When the tips were distributed to employees.
    /// </summary>
    public DateTime? DistributedAt { get; set; }

    /// <summary>
    /// User who finalized the distribution.
    /// </summary>
    public Guid? DistributedByUserId { get; set; }

    /// <summary>
    /// Notes about this tip pool.
    /// </summary>
    public string? Notes { get; set; }

    // Navigation properties
    public ICollection<TipDistribution> Distributions { get; set; } = new List<TipDistribution>();
}
