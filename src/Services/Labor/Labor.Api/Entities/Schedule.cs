using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Represents a weekly schedule for a location.
/// </summary>
public class Schedule : BaseEntity, ILocationScoped
{
    /// <summary>
    /// The tenant this schedule belongs to.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// The location this schedule is for.
    /// </summary>
    public Guid LocationId { get; set; }

    /// <summary>
    /// The start date of the week (always Monday).
    /// </summary>
    public DateOnly WeekStartDate { get; set; }

    /// <summary>
    /// Status: draft, published, locked.
    /// </summary>
    public string Status { get; set; } = "draft";

    /// <summary>
    /// When the schedule was published to employees.
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// User who published the schedule.
    /// </summary>
    public Guid? PublishedByUserId { get; set; }

    /// <summary>
    /// Total scheduled hours for the week.
    /// </summary>
    public decimal TotalScheduledHours { get; set; }

    /// <summary>
    /// Total labor cost for the week.
    /// </summary>
    public decimal TotalLaborCost { get; set; }

    /// <summary>
    /// Notes about this schedule.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Additional metadata as JSON.
    /// </summary>
    public string? MetadataJson { get; set; }

    // Navigation properties
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
}
