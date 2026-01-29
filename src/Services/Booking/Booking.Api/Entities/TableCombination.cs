using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Booking.Api.Entities;

/// <summary>
/// Represents a combination of multiple tables that can be joined together for larger parties
/// </summary>
public class TableCombination : BaseEntity, ILocationScoped
{
    public Guid LocationId { get; set; }
    public Guid FloorPlanId { get; set; }

    /// <summary>
    /// Display name for this combination (e.g., "Tables 1-3", "Large Party Setup")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Combined seating capacity when tables are joined
    /// </summary>
    public int CombinedCapacity { get; set; }

    /// <summary>
    /// Minimum party size for this combination to be suggested
    /// </summary>
    public int MinPartySize { get; set; }

    /// <summary>
    /// Whether this combination is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Notes about setup or requirements for this combination
    /// </summary>
    public string? Notes { get; set; }

    // Navigation properties
    public FloorPlan? FloorPlan { get; set; }
    public ICollection<TableCombinationTable> Tables { get; set; } = new List<TableCombinationTable>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}

/// <summary>
/// Join table for many-to-many relationship between TableCombination and Table
/// </summary>
public class TableCombinationTable : BaseEntity
{
    public Guid TableCombinationId { get; set; }
    public Guid TableId { get; set; }

    /// <summary>
    /// Position of this table in the combination (for display ordering)
    /// </summary>
    public int Position { get; set; }

    // Navigation properties
    public TableCombination? TableCombination { get; set; }
    public Table? Table { get; set; }
}
