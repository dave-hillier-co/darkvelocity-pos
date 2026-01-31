namespace DarkVelocity.Host.Grains;

// ============================================================================
// Table Grain
// ============================================================================

public enum TableStatus
{
    Available,
    Occupied,
    Reserved,
    Closed
}

public record CreateTableCommand(
    Guid FloorPlanId,
    string TableNumber,
    string? Name,
    int MinCapacity,
    int MaxCapacity,
    string? Shape,
    int PositionX,
    int PositionY,
    int Width,
    int Height,
    int Rotation,
    bool IsCombinationAllowed,
    int AssignmentPriority,
    string? Notes);

public record UpdateTableCommand(
    string? TableNumber,
    string? Name,
    int? MinCapacity,
    int? MaxCapacity,
    string? Shape,
    int? PositionX,
    int? PositionY,
    int? Width,
    int? Height,
    int? Rotation,
    TableStatus? Status,
    bool? IsCombinationAllowed,
    bool? IsActive,
    int? AssignmentPriority,
    string? Notes);

public record TableSnapshot(
    Guid TableId,
    Guid LocationId,
    Guid FloorPlanId,
    string? FloorPlanName,
    string TableNumber,
    string? Name,
    int MinCapacity,
    int MaxCapacity,
    string? Shape,
    int PositionX,
    int PositionY,
    int Width,
    int Height,
    int Rotation,
    TableStatus Status,
    bool IsCombinationAllowed,
    bool IsActive,
    int AssignmentPriority,
    string? Notes,
    DateTime CreatedAt);

/// <summary>
/// Grain for table management.
/// Key: "{orgId}:{siteId}:table:{tableId}"
/// </summary>
public interface ITableGrain : IGrainWithStringKey
{
    Task<TableSnapshot> CreateAsync(CreateTableCommand command);
    Task<TableSnapshot> UpdateAsync(UpdateTableCommand command);
    Task<TableSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();
    Task DeleteAsync();

    // Status management
    Task<TableSnapshot> SetStatusAsync(TableStatus status);
    Task<TableStatus> GetStatusAsync();
    Task<bool> IsAvailableAsync();

    // Position update
    Task UpdatePositionAsync(int positionX, int positionY, int? rotation);
}

// ============================================================================
// Floor Plan Grain
// ============================================================================

public record CreateFloorPlanCommand(
    string Name,
    string? Description,
    int GridWidth,
    int GridHeight,
    string? BackgroundImageUrl,
    int SortOrder,
    int DefaultTurnTimeMinutes);

public record UpdateFloorPlanCommand(
    string? Name,
    string? Description,
    int? GridWidth,
    int? GridHeight,
    string? BackgroundImageUrl,
    int? SortOrder,
    bool? IsActive,
    int? DefaultTurnTimeMinutes);

public record FloorPlanSnapshot(
    Guid FloorPlanId,
    Guid LocationId,
    string Name,
    string? Description,
    int GridWidth,
    int GridHeight,
    string? BackgroundImageUrl,
    int SortOrder,
    bool IsActive,
    int DefaultTurnTimeMinutes,
    int TableCount,
    DateTime CreatedAt);

/// <summary>
/// Grain for floor plan management.
/// Key: "{orgId}:{siteId}:floorplan:{floorPlanId}"
/// </summary>
public interface IFloorPlanGrain : IGrainWithStringKey
{
    Task<FloorPlanSnapshot> CreateAsync(CreateFloorPlanCommand command);
    Task<FloorPlanSnapshot> UpdateAsync(UpdateFloorPlanCommand command);
    Task<FloorPlanSnapshot> GetSnapshotAsync();
    Task<bool> ExistsAsync();
    Task DeleteAsync();

    // Table tracking
    Task IncrementTableCountAsync();
    Task DecrementTableCountAsync();
    Task<int> GetTableCountAsync();
}

// ============================================================================
// Booking Settings Grain
// ============================================================================

public record UpdateBookingSettingsCommand(
    int? DefaultBookingDurationMinutes,
    int? MinAdvanceBookingMinutes,
    int? MaxAdvanceBookingDays,
    bool? AllowOnlineBookings,
    bool? RequireDeposit,
    decimal? DepositAmount,
    decimal? DepositPercentage,
    int? CancellationDeadlineMinutes,
    decimal? CancellationFeeAmount,
    decimal? CancellationFeePercentage,
    bool? AllowWaitlist,
    int? MaxWaitlistSize,
    TimeSpan? FirstServiceStart,
    TimeSpan? FirstServiceEnd,
    TimeSpan? SecondServiceStart,
    TimeSpan? SecondServiceEnd,
    int? TurnTimeMinutes,
    int? BufferTimeMinutes,
    string? ConfirmationMessageTemplate,
    string? ReminderMessageTemplate);

public record BookingSettingsSnapshot(
    Guid LocationId,
    int DefaultBookingDurationMinutes,
    int MinAdvanceBookingMinutes,
    int MaxAdvanceBookingDays,
    bool AllowOnlineBookings,
    bool RequireDeposit,
    decimal DepositAmount,
    decimal DepositPercentage,
    int CancellationDeadlineMinutes,
    decimal CancellationFeeAmount,
    decimal CancellationFeePercentage,
    bool AllowWaitlist,
    int MaxWaitlistSize,
    TimeSpan? FirstServiceStart,
    TimeSpan? FirstServiceEnd,
    TimeSpan? SecondServiceStart,
    TimeSpan? SecondServiceEnd,
    int TurnTimeMinutes,
    int BufferTimeMinutes,
    string? ConfirmationMessageTemplate,
    string? ReminderMessageTemplate);

/// <summary>
/// Grain for booking settings management.
/// Key: "{orgId}:{siteId}:bookingsettings"
/// </summary>
public interface IBookingSettingsGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid locationId);
    Task<BookingSettingsSnapshot> GetSettingsAsync();
    Task<BookingSettingsSnapshot> UpdateAsync(UpdateBookingSettingsCommand command);
    Task<bool> ExistsAsync();

    // Helper methods
    Task<bool> CanAcceptOnlineBookingAsync();
    Task<decimal> CalculateDepositAsync(decimal totalAmount);
    Task<decimal> CalculateCancellationFeeAsync(decimal totalAmount, DateTime bookingTime);
    Task<bool> IsWithinBookingWindowAsync(DateTime requestedTime);
}
