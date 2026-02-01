namespace DarkVelocity.Host.State;

public enum TableStatus
{
    Available,
    Reserved,
    Occupied,
    Dirty,
    Blocked,
    OutOfService
}

public enum TableShape
{
    Square,
    Rectangle,
    Round,
    Oval,
    Bar
}

[GenerateSerializer]
public record TablePosition
{
    [Id(0)] public int X { get; init; }
    [Id(1)] public int Y { get; init; }
    [Id(2)] public int Width { get; init; }
    [Id(3)] public int Height { get; init; }
    [Id(4)] public int Rotation { get; init; }
}

[GenerateSerializer]
public record TableOccupancy
{
    [Id(0)] public Guid? BookingId { get; init; }
    [Id(1)] public Guid? OrderId { get; init; }
    [Id(2)] public string? GuestName { get; init; }
    [Id(3)] public int GuestCount { get; init; }
    [Id(4)] public DateTime SeatedAt { get; init; }
    [Id(5)] public Guid? ServerId { get; init; }
}

[GenerateSerializer]
public sealed class TableState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public Guid? FloorPlanId { get; set; }

    [Id(4)] public string Number { get; set; } = string.Empty;
    [Id(5)] public string? Name { get; set; }
    [Id(6)] public int MinCapacity { get; set; } = 1;
    [Id(7)] public int MaxCapacity { get; set; } = 4;
    [Id(8)] public TableShape Shape { get; set; } = TableShape.Square;
    [Id(9)] public TablePosition? Position { get; set; }

    [Id(10)] public TableStatus Status { get; set; } = TableStatus.Available;
    [Id(11)] public TableOccupancy? CurrentOccupancy { get; set; }

    [Id(12)] public List<string> Tags { get; set; } = [];
    [Id(13)] public bool IsCombinable { get; set; } = true;
    [Id(14)] public List<Guid> CombinedWith { get; set; } = [];
    [Id(15)] public int SortOrder { get; set; }

    [Id(16)] public DateTime CreatedAt { get; set; }
    [Id(17)] public DateTime? UpdatedAt { get; set; }
    [Id(18)] public int Version { get; set; }
}

// Floor Plan State
[GenerateSerializer]
public record FloorPlanSection
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public string Name { get; init; } = string.Empty;
    [Id(2)] public string? Color { get; init; }
    [Id(3)] public int SortOrder { get; init; }
}

[GenerateSerializer]
public sealed class FloorPlanState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }

    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public bool IsDefault { get; set; }
    [Id(5)] public bool IsActive { get; set; } = true;

    [Id(6)] public int Width { get; set; } = 800;
    [Id(7)] public int Height { get; set; } = 600;
    [Id(8)] public string? BackgroundImageUrl { get; set; }

    [Id(9)] public List<Guid> TableIds { get; set; } = [];
    [Id(10)] public List<FloorPlanSection> Sections { get; set; } = [];

    [Id(11)] public DateTime CreatedAt { get; set; }
    [Id(12)] public DateTime? UpdatedAt { get; set; }
    [Id(13)] public int Version { get; set; }
}

// Booking Availability
[GenerateSerializer]
public record AvailabilitySlot
{
    [Id(0)] public TimeOnly Time { get; init; }
    [Id(1)] public bool IsAvailable { get; init; }
    [Id(2)] public int AvailableCapacity { get; init; }
    [Id(3)] public List<Guid> AvailableTableIds { get; init; } = [];
}

[GenerateSerializer]
public sealed class BookingSettingsState
{
    [Id(0)] public Guid SiteId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }

    // Operating hours
    [Id(2)] public TimeOnly DefaultOpenTime { get; set; } = new(11, 0);
    [Id(3)] public TimeOnly DefaultCloseTime { get; set; } = new(22, 0);
    [Id(4)] public TimeSpan DefaultDuration { get; set; } = TimeSpan.FromMinutes(90);
    [Id(5)] public TimeSpan SlotInterval { get; set; } = TimeSpan.FromMinutes(15);
    [Id(6)] public TimeSpan TurnoverBuffer { get; set; } = TimeSpan.FromMinutes(15);

    // Capacity
    [Id(7)] public int MaxPartySizeOnline { get; set; } = 8;
    [Id(8)] public int MaxBookingsPerSlot { get; set; } = 10;
    [Id(9)] public int AdvanceBookingDays { get; set; } = 30;

    // Policies
    [Id(10)] public bool RequireDeposit { get; set; }
    [Id(11)] public decimal DepositAmount { get; set; }
    [Id(12)] public int DepositPartySizeThreshold { get; set; } = 6;
    [Id(13)] public TimeSpan CancellationDeadline { get; set; } = TimeSpan.FromHours(24);

    [Id(14)] public List<DateOnly> BlockedDates { get; set; } = [];
    [Id(15)] public int Version { get; set; }
}
