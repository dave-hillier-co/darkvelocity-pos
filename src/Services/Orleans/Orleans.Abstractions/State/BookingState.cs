namespace DarkVelocity.Orleans.Abstractions.State;

public enum BookingStatus
{
    Requested,
    PendingDeposit,
    Confirmed,
    Arrived,
    Seated,
    Completed,
    NoShow,
    Cancelled
}

public enum BookingSource
{
    Direct,
    Website,
    Phone,
    WalkIn,
    OpenTable,
    Resy,
    GoogleMaps,
    Facebook,
    Other
}

public enum VipStatus
{
    None,
    Regular,
    Silver,
    Gold,
    Platinum
}

public enum DepositStatus
{
    Required,
    Paid,
    Waived,
    Forfeited,
    Refunded
}

public record GuestInfo
{
    public string Name { get; init; } = string.Empty;
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Notes { get; init; }
    public VipStatus VipStatus { get; init; }
    public int VisitCount { get; init; }
}

public record DepositInfo
{
    public decimal Amount { get; init; }
    public DepositStatus Status { get; init; }
    public DateTime RequiredAt { get; init; }
    public DateTime? PaidAt { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public string? PaymentReference { get; init; }
    public DateTime? ForfeitedAt { get; init; }
    public DateTime? RefundedAt { get; init; }
    public string? RefundReason { get; init; }
}

public record TableAssignment
{
    public Guid TableId { get; init; }
    public string TableNumber { get; init; } = string.Empty;
    public int Capacity { get; init; }
    public DateTime? AssignedAt { get; init; }
}

[GenerateSerializer]
public sealed class BookingState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SiteId { get; set; }
    [Id(3)] public string ConfirmationCode { get; set; } = string.Empty;
    [Id(4)] public BookingStatus Status { get; set; } = BookingStatus.Requested;

    [Id(5)] public DateTime RequestedTime { get; set; }
    [Id(6)] public DateTime? ConfirmedTime { get; set; }
    [Id(7)] public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(90);
    [Id(8)] public int PartySize { get; set; }

    [Id(9)] public GuestInfo Guest { get; set; } = new();
    [Id(10)] public Guid? CustomerId { get; set; }
    [Id(11)] public List<TableAssignment> TableAssignments { get; set; } = [];

    [Id(12)] public string? SpecialRequests { get; set; }
    [Id(13)] public List<string> Tags { get; set; } = [];
    [Id(14)] public string? Occasion { get; set; }

    [Id(15)] public DepositInfo? Deposit { get; set; }
    [Id(16)] public BookingSource Source { get; set; }
    [Id(17)] public string? ExternalRef { get; set; }
    [Id(18)] public string? Notes { get; set; }

    // Timestamps
    [Id(19)] public DateTime CreatedAt { get; set; }
    [Id(20)] public DateTime? ConfirmedAt { get; set; }
    [Id(21)] public DateTime? ArrivedAt { get; set; }
    [Id(22)] public DateTime? SeatedAt { get; set; }
    [Id(23)] public DateTime? DepartedAt { get; set; }
    [Id(24)] public DateTime? CancelledAt { get; set; }
    [Id(25)] public string? CancellationReason { get; set; }
    [Id(26)] public Guid? CancelledBy { get; set; }

    [Id(27)] public Guid? LinkedOrderId { get; set; }
    [Id(28)] public Guid? SeatedBy { get; set; }
    [Id(29)] public Guid? CheckedInBy { get; set; }

    [Id(30)] public int Version { get; set; }
}

// Waitlist
public enum WaitlistStatus
{
    Waiting,
    Notified,
    Seated,
    Left,
    Expired
}

public enum NotificationMethod
{
    Sms,
    PagerBuzzer,
    Display
}

public record WaitlistEntry
{
    public Guid Id { get; init; }
    public int Position { get; init; }
    public GuestInfo Guest { get; init; } = new();
    public int PartySize { get; init; }
    public DateTime CheckedInAt { get; init; }
    public TimeSpan QuotedWait { get; init; }
    public WaitlistStatus Status { get; init; }
    public string? TablePreferences { get; init; }
    public NotificationMethod NotificationMethod { get; init; }
    public DateTime? NotifiedAt { get; init; }
    public DateTime? SeatedAt { get; init; }
    public DateTime? LeftAt { get; init; }
    public Guid? ConvertedToBookingId { get; init; }
}

[GenerateSerializer]
public sealed class WaitlistState
{
    [Id(0)] public Guid SiteId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public DateOnly Date { get; set; }
    [Id(3)] public List<WaitlistEntry> Entries { get; set; } = [];
    [Id(4)] public int CurrentPosition { get; set; }
    [Id(5)] public TimeSpan AverageWait { get; set; }
    [Id(6)] public int Version { get; set; }
}
