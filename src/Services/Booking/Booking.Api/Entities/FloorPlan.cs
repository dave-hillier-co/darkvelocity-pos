using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Booking.Api.Entities;

/// <summary>
/// Represents a floor plan/room/section within a location (e.g., main dining, patio, private room)
/// </summary>
public class FloorPlan : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>
    /// Grid width for table positioning (in units)
    /// </summary>
    public int GridWidth { get; set; } = 20;

    /// <summary>
    /// Grid height for table positioning (in units)
    /// </summary>
    public int GridHeight { get; set; } = 15;

    /// <summary>
    /// Optional background image URL for the floor plan
    /// </summary>
    public string? BackgroundImageUrl { get; set; }

    /// <summary>
    /// Display order when multiple floor plans exist
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this floor plan is active and available for bookings
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Default turn time in minutes for this floor plan (can be overridden per booking)
    /// </summary>
    public int DefaultTurnTimeMinutes { get; set; } = 90;

    public ICollection<Table> Tables { get; set; } = new List<Table>();
}
