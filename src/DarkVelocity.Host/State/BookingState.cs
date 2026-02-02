namespace DarkVelocity.Host.State;

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

[GenerateSerializer]
public record GuestInfo
{
    [Id(0)] public string Name { get; init; } = string.Empty;
    [Id(1)] public string? Phone { get; init; }
    [Id(2)] public string? Email { get; init; }
    [Id(3)] public string? Notes { get; init; }
    [Id(4)] public VipStatus VipStatus { get; init; }
    [Id(5)] public int VisitCount { get; init; }
}

[GenerateSerializer]
public record DepositInfo
{
    [Id(0)] public decimal Amount { get; init; }
    [Id(1)] public DepositStatus Status { get; init; }
    [Id(2)] public DateTime RequiredAt { get; init; }
    [Id(3)] public DateTime? PaidAt { get; init; }
    [Id(4)] public PaymentMethod? PaymentMethod { get; init; }
    [Id(5)] public string? PaymentReference { get; init; }
    [Id(6)] public DateTime? ForfeitedAt { get; init; }
    [Id(7)] public DateTime? RefundedAt { get; init; }
    [Id(8)] public string? RefundReason { get; init; }
}

[GenerateSerializer]
public record TableAssignment
{
    [Id(0)] public Guid TableId { get; init; }
    [Id(1)] public string TableNumber { get; init; } = string.Empty;
    [Id(2)] public int Capacity { get; init; }
    [Id(3)] public DateTime? AssignedAt { get; init; }
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

[GenerateSerializer]
public record WaitlistEntry
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public int Position { get; init; }
    [Id(2)] public GuestInfo Guest { get; init; } = new();
    [Id(3)] public int PartySize { get; init; }
    [Id(4)] public DateTime CheckedInAt { get; init; }
    [Id(5)] public TimeSpan QuotedWait { get; init; }
    [Id(6)] public WaitlistStatus Status { get; init; }
    [Id(7)] public string? TablePreferences { get; init; }
    [Id(8)] public NotificationMethod NotificationMethod { get; init; }
    [Id(9)] public DateTime? NotifiedAt { get; init; }
    [Id(10)] public DateTime? SeatedAt { get; init; }
    [Id(11)] public DateTime? LeftAt { get; init; }
    [Id(12)] public Guid? ConvertedToBookingId { get; init; }
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
