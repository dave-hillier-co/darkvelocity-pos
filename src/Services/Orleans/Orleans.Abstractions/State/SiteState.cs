namespace DarkVelocity.Orleans.Abstractions.State;

public enum SiteStatus
{
    Open,
    Closed,
    TemporarilyClosed
}

public record Address
{
    public string Street { get; init; } = string.Empty;
    public string? Street2 { get; init; }
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string PostalCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
}

public record OperatingHours
{
    public IReadOnlyList<DaySchedule> Schedule { get; init; } = [];
}

public record DaySchedule
{
    public DayOfWeek Day { get; init; }
    public bool IsClosed { get; init; }
    public TimeOnly? OpenTime { get; init; }
    public TimeOnly? CloseTime { get; init; }
    public TimeOnly? BreakStart { get; init; }
    public TimeOnly? BreakEnd { get; init; }
}

public record TaxJurisdiction
{
    public string Country { get; init; } = string.Empty;
    public string? State { get; init; }
    public string? County { get; init; }
    public string? City { get; init; }
    public decimal DefaultTaxRate { get; init; }
}

public record BookingSettings
{
    public bool AcceptBookings { get; init; } = true;
    public int MaxAdvanceBookingDays { get; init; } = 30;
    public int MinAdvanceBookingHours { get; init; } = 2;
    public int DefaultDurationMinutes { get; init; } = 90;
    public int TurnTimeMinutes { get; init; } = 15;
    public int GracePeriodMinutes { get; init; } = 15;
    public bool RequireDeposit { get; init; }
    public int DepositPartySizeThreshold { get; init; } = 6;
    public decimal DepositAmountPerPerson { get; init; } = 25m;
}

public record SiteSettings
{
    public Guid? ActiveMenuId { get; init; }
    public Guid? DefaultPriceListId { get; init; }
    public int DefaultGuestCount { get; init; } = 1;
    public bool AutoPrintKitchenTickets { get; init; } = true;
    public bool AutoPrintReceipts { get; init; }
    public TimeSpan OrderTimeout { get; init; } = TimeSpan.FromHours(4);
    public BookingSettings BookingSettings { get; init; } = new();
}

[GenerateSerializer]
public sealed class SiteState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string Code { get; set; } = string.Empty;
    [Id(4)] public Address Address { get; set; } = new();
    [Id(5)] public string Timezone { get; set; } = "America/New_York";
    [Id(6)] public string Currency { get; set; } = "USD";
    [Id(7)] public string Locale { get; set; } = "en-US";
    [Id(8)] public TaxJurisdiction? TaxJurisdiction { get; set; }
    [Id(9)] public OperatingHours? OperatingHours { get; set; }
    [Id(10)] public SiteStatus Status { get; set; } = SiteStatus.Open;
    [Id(11)] public SiteSettings Settings { get; set; } = new();
    [Id(12)] public DateTime CreatedAt { get; set; }
    [Id(13)] public DateTime? UpdatedAt { get; set; }
    [Id(14)] public List<Guid> FloorIds { get; set; } = [];
    [Id(15)] public List<Guid> StationIds { get; set; } = [];
    [Id(16)] public int Version { get; set; }
}
