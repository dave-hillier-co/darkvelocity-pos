using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Booking.Api.Entities;

/// <summary>
/// Represents an individual table within a floor plan
/// </summary>
public class Table : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public Guid FloorPlanId { get; set; }

    /// <summary>
    /// Table number displayed to staff (e.g., "1", "A1", "Patio-3")
    /// </summary>
    public string TableNumber { get; set; } = string.Empty;

    /// <summary>
    /// Optional friendly name (e.g., "Window Table", "Chef's Table")
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Minimum seating capacity
    /// </summary>
    public int MinCapacity { get; set; } = 1;

    /// <summary>
    /// Maximum seating capacity
    /// </summary>
    public int MaxCapacity { get; set; } = 4;

    /// <summary>
    /// Table shape for UI rendering
    /// </summary>
    public string Shape { get; set; } = "rectangle"; // rectangle, round, square, oval

    /// <summary>
    /// X position on the floor plan grid
    /// </summary>
    public int PositionX { get; set; }

    /// <summary>
    /// Y position on the floor plan grid
    /// </summary>
    public int PositionY { get; set; }

    /// <summary>
    /// Width of the table on the grid (in grid units)
    /// </summary>
    public int Width { get; set; } = 2;

    /// <summary>
    /// Height of the table on the grid (in grid units)
    /// </summary>
    public int Height { get; set; } = 1;

    /// <summary>
    /// Rotation angle in degrees (0, 90, 180, 270)
    /// </summary>
    public int Rotation { get; set; }

    /// <summary>
    /// Current operational status
    /// </summary>
    public string Status { get; set; } = "available"; // available, occupied, reserved, closed

    /// <summary>
    /// Whether this table can be combined with adjacent tables
    /// </summary>
    public bool IsCombinationAllowed { get; set; } = true;

    /// <summary>
    /// Whether this table is currently active and available
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Priority for auto-assignment (lower = assigned first)
    /// </summary>
    public int AssignmentPriority { get; set; } = 100;

    /// <summary>
    /// Special notes about this table (e.g., "Near kitchen", "Wheelchair accessible")
    /// </summary>
    public string? Notes { get; set; }

    // Navigation properties
    public FloorPlan? FloorPlan { get; set; }
    public ICollection<TableCombinationTable> TableCombinations { get; set; } = new List<TableCombinationTable>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
