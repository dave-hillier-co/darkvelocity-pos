using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Booking.Api.Entities;

/// <summary>
/// Defines available booking time slots for a location
/// </summary>
public class TimeSlot : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }

    /// <summary>
    /// Day of week (0 = Sunday, 6 = Saturday)
    /// </summary>
    public int DayOfWeek { get; set; }

    /// <summary>
    /// Start time of this slot
    /// </summary>
    public TimeOnly StartTime { get; set; }

    /// <summary>
    /// End time of this slot
    /// </summary>
    public TimeOnly EndTime { get; set; }

    /// <summary>
    /// Interval between booking slots in minutes
    /// </summary>
    public int IntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Maximum number of bookings allowed in this slot
    /// </summary>
    public int? MaxBookings { get; set; }

    /// <summary>
    /// Maximum total covers allowed in this slot
    /// </summary>
    public int? MaxCovers { get; set; }

    /// <summary>
    /// Expected turn time in minutes for bookings in this slot
    /// </summary>
    public int TurnTimeMinutes { get; set; } = 90;

    /// <summary>
    /// Name/label for this time slot (e.g., "Lunch", "Dinner", "Brunch")
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Whether this time slot is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional floor plan restriction (null = all floor plans)
    /// </summary>
    public Guid? FloorPlanId { get; set; }

    /// <summary>
    /// Priority for slot selection when multiple overlap
    /// </summary>
    public int Priority { get; set; } = 0;
}

/// <summary>
/// Represents a specific date override for availability (e.g., holidays, special events)
/// </summary>
public class DateOverride : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }

    /// <summary>
    /// The specific date this override applies to
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Type of override
    /// </summary>
    public string OverrideType { get; set; } = "closed"; // closed, special_hours, increased_capacity, reduced_capacity

    /// <summary>
    /// Name/reason for this override (e.g., "Christmas Day", "Private Event")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the override
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Override start time (if special_hours)
    /// </summary>
    public TimeOnly? StartTime { get; set; }

    /// <summary>
    /// Override end time (if special_hours)
    /// </summary>
    public TimeOnly? EndTime { get; set; }

    /// <summary>
    /// Override max bookings (if capacity adjustment)
    /// </summary>
    public int? MaxBookings { get; set; }

    /// <summary>
    /// Override max covers (if capacity adjustment)
    /// </summary>
    public int? MaxCovers { get; set; }

    /// <summary>
    /// Whether online booking is disabled for this date
    /// </summary>
    public bool DisableOnlineBooking { get; set; }

    /// <summary>
    /// Notes about this override
    /// </summary>
    public string? Notes { get; set; }
}
