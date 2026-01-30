namespace DarkVelocity.Orleans.Abstractions.State;

public enum TicketStatus
{
    New,
    InProgress,
    Ready,
    Served,
    Voided
}

public enum TicketItemStatus
{
    Pending,
    Preparing,
    Ready,
    Served,
    Voided
}

public enum TicketPriority
{
    Normal,
    Rush,
    VIP,
    AllDay
}

public record TicketItem
{
    public Guid Id { get; init; }
    public Guid OrderLineId { get; init; }
    public Guid MenuItemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public TicketItemStatus Status { get; init; }
    public List<string> Modifiers { get; init; } = [];
    public string? SpecialInstructions { get; init; }
    public Guid? StationId { get; init; }
    public string? StationName { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public Guid? PreparedBy { get; init; }
    public int? CourseNumber { get; init; }
}

[GenerateSerializer]
public sealed class KitchenTicketState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public Guid OrderId { get; set; }
    [Id(4)] public string OrderNumber { get; set; } = string.Empty;
    [Id(5)] public string TicketNumber { get; set; } = string.Empty;
    [Id(6)] public TicketStatus Status { get; set; } = TicketStatus.New;
    [Id(7)] public TicketPriority Priority { get; set; } = TicketPriority.Normal;

    [Id(8)] public OrderType OrderType { get; set; }
    [Id(9)] public string? TableNumber { get; set; }
    [Id(10)] public int? GuestCount { get; set; }
    [Id(11)] public string? ServerName { get; set; }

    [Id(12)] public List<TicketItem> Items { get; set; } = [];
    [Id(13)] public string? Notes { get; set; }
    [Id(14)] public int CourseNumber { get; set; } = 1;
    [Id(15)] public bool IsFireAll { get; set; }

    [Id(16)] public Guid? PrimaryStationId { get; set; }
    [Id(17)] public List<Guid> AssignedStationIds { get; set; } = [];

    [Id(18)] public DateTime CreatedAt { get; set; }
    [Id(19)] public DateTime? ReceivedAt { get; set; }
    [Id(20)] public DateTime? StartedAt { get; set; }
    [Id(21)] public DateTime? CompletedAt { get; set; }
    [Id(22)] public DateTime? BumpedAt { get; set; }
    [Id(23)] public Guid? BumpedBy { get; set; }

    [Id(24)] public TimeSpan? PrepTime { get; set; }
    [Id(25)] public TimeSpan? WaitTime { get; set; }

    [Id(26)] public int Version { get; set; }
}

public enum StationStatus
{
    Open,
    Closed,
    Paused
}

public enum StationType
{
    Grill,
    Fryer,
    Saute,
    Salad,
    Dessert,
    Prep,
    Expo,
    Bar,
    Other
}

[GenerateSerializer]
public sealed class KitchenStationState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public StationType Type { get; set; }
    [Id(5)] public StationStatus Status { get; set; } = StationStatus.Open;
    [Id(6)] public int DisplayOrder { get; set; }

    [Id(7)] public List<Guid> AssignedMenuItemCategories { get; set; } = [];
    [Id(8)] public List<Guid> AssignedMenuItemIds { get; set; } = [];
    [Id(9)] public Guid? PrinterId { get; set; }
    [Id(10)] public Guid? DisplayId { get; set; }

    [Id(11)] public List<Guid> CurrentTicketIds { get; set; } = [];
    [Id(12)] public int ActiveItemCount { get; set; }
    [Id(13)] public TimeSpan AveragePrepTime { get; set; }

    [Id(14)] public DateTime? OpenedAt { get; set; }
    [Id(15)] public Guid? OpenedBy { get; set; }
    [Id(16)] public DateTime? ClosedAt { get; set; }

    [Id(17)] public int Version { get; set; }
}
