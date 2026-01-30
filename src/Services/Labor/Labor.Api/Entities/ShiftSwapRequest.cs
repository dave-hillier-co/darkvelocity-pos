using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Labor.Api.Entities;

/// <summary>
/// Represents a request to swap, drop, or pick up a shift.
/// </summary>
public class ShiftSwapRequest : BaseEntity
{
    /// <summary>
    /// The tenant this request belongs to.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// The employee requesting the swap/drop.
    /// </summary>
    public Guid RequestingEmployeeId { get; set; }

    /// <summary>
    /// The shift being offered/dropped.
    /// </summary>
    public Guid RequestingShiftId { get; set; }

    /// <summary>
    /// The employee being asked to swap (null for drop/pickup requests).
    /// </summary>
    public Guid? TargetEmployeeId { get; set; }

    /// <summary>
    /// The shift being offered in exchange (null for drop requests).
    /// </summary>
    public Guid? TargetShiftId { get; set; }

    /// <summary>
    /// Type of request: swap, drop, pickup.
    /// </summary>
    public string Type { get; set; } = "swap";

    /// <summary>
    /// Status: pending, approved, rejected, cancelled.
    /// </summary>
    public string Status { get; set; } = "pending";

    /// <summary>
    /// When the request was made.
    /// </summary>
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the target employee or manager responded.
    /// </summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// Whether manager approval is required after employee accepts.
    /// </summary>
    public bool ManagerApprovalRequired { get; set; } = true;

    /// <summary>
    /// Manager who approved the swap.
    /// </summary>
    public Guid? ManagerApprovedByUserId { get; set; }

    /// <summary>
    /// Reason for the swap request.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Additional notes.
    /// </summary>
    public string? Notes { get; set; }

    // Navigation properties
    public Employee? RequestingEmployee { get; set; }
    public Employee? TargetEmployee { get; set; }
    public Shift? RequestingShift { get; set; }
    public Shift? TargetShift { get; set; }
}
