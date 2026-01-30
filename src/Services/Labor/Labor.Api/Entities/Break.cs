using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Represents a break taken during a time entry.
/// </summary>
public class Break : BaseEntity
{
    /// <summary>
    /// Reference to the time entry this break belongs to.
    /// </summary>
    public Guid TimeEntryId { get; set; }

    /// <summary>
    /// When the break started.
    /// </summary>
    public DateTime StartAt { get; set; }

    /// <summary>
    /// When the break ended (null if still on break).
    /// </summary>
    public DateTime? EndAt { get; set; }

    /// <summary>
    /// Type of break: paid, unpaid, meal.
    /// </summary>
    public string Type { get; set; } = "unpaid";

    /// <summary>
    /// Duration in minutes (calculated or manual).
    /// </summary>
    public int DurationMinutes { get; set; }

    /// <summary>
    /// Whether this break was auto-deducted.
    /// </summary>
    public bool AutoDeducted { get; set; }

    // Navigation properties
    public TimeEntry? TimeEntry { get; set; }
}
