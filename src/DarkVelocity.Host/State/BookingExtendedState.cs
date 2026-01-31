using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

[GenerateSerializer]
public sealed class TableState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public Guid TableId { get; set; }
    [Id(3)] public Guid FloorPlanId { get; set; }
    [Id(4)] public string? FloorPlanName { get; set; }
    [Id(5)] public string TableNumber { get; set; } = string.Empty;
    [Id(6)] public string? Name { get; set; }
    [Id(7)] public int MinCapacity { get; set; }
    [Id(8)] public int MaxCapacity { get; set; }
    [Id(9)] public string? Shape { get; set; }
    [Id(10)] public int PositionX { get; set; }
    [Id(11)] public int PositionY { get; set; }
    [Id(12)] public int Width { get; set; }
    [Id(13)] public int Height { get; set; }
    [Id(14)] public int Rotation { get; set; }
    [Id(15)] public TableStatus Status { get; set; } = TableStatus.Available;
    [Id(16)] public bool IsCombinationAllowed { get; set; }
    [Id(17)] public bool IsActive { get; set; } = true;
    [Id(18)] public int AssignmentPriority { get; set; }
    [Id(19)] public string? Notes { get; set; }
    [Id(20)] public DateTime CreatedAt { get; set; }
    [Id(21)] public DateTime? UpdatedAt { get; set; }
    [Id(22)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class FloorPlanState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public Guid FloorPlanId { get; set; }
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public string? Description { get; set; }
    [Id(5)] public int GridWidth { get; set; }
    [Id(6)] public int GridHeight { get; set; }
    [Id(7)] public string? BackgroundImageUrl { get; set; }
    [Id(8)] public int SortOrder { get; set; }
    [Id(9)] public bool IsActive { get; set; } = true;
    [Id(10)] public int DefaultTurnTimeMinutes { get; set; }
    [Id(11)] public int TableCount { get; set; }
    [Id(12)] public DateTime CreatedAt { get; set; }
    [Id(13)] public DateTime? UpdatedAt { get; set; }
    [Id(14)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class BookingSettingsState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid LocationId { get; set; }
    [Id(2)] public int DefaultBookingDurationMinutes { get; set; } = 90;
    [Id(3)] public int MinAdvanceBookingMinutes { get; set; } = 30;
    [Id(4)] public int MaxAdvanceBookingDays { get; set; } = 60;
    [Id(5)] public bool AllowOnlineBookings { get; set; } = true;
    [Id(6)] public bool RequireDeposit { get; set; }
    [Id(7)] public decimal DepositAmount { get; set; }
    [Id(8)] public decimal DepositPercentage { get; set; }
    [Id(9)] public int CancellationDeadlineMinutes { get; set; } = 120;
    [Id(10)] public decimal CancellationFeeAmount { get; set; }
    [Id(11)] public decimal CancellationFeePercentage { get; set; }
    [Id(12)] public bool AllowWaitlist { get; set; } = true;
    [Id(13)] public int MaxWaitlistSize { get; set; } = 20;
    [Id(14)] public TimeSpan? FirstServiceStart { get; set; }
    [Id(15)] public TimeSpan? FirstServiceEnd { get; set; }
    [Id(16)] public TimeSpan? SecondServiceStart { get; set; }
    [Id(17)] public TimeSpan? SecondServiceEnd { get; set; }
    [Id(18)] public int TurnTimeMinutes { get; set; } = 15;
    [Id(19)] public int BufferTimeMinutes { get; set; } = 5;
    [Id(20)] public string? ConfirmationMessageTemplate { get; set; }
    [Id(21)] public string? ReminderMessageTemplate { get; set; }
    [Id(22)] public DateTime CreatedAt { get; set; }
    [Id(23)] public DateTime? UpdatedAt { get; set; }
    [Id(24)] public int Version { get; set; }
}
