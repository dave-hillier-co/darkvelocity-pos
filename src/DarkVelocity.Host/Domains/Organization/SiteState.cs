namespace DarkVelocity.Host.State;

public enum SiteStatus
{
    Open,
    Closed,
    TemporarilyClosed
}

[GenerateSerializer]
public record Address
{
    [Id(0)] public string Street { get; init; } = string.Empty;
    [Id(1)] public string? Street2 { get; init; }
    [Id(2)] public string City { get; init; } = string.Empty;
    [Id(3)] public string State { get; init; } = string.Empty;
    [Id(4)] public string PostalCode { get; init; } = string.Empty;
    [Id(5)] public string Country { get; init; } = string.Empty;
    [Id(6)] public double? Latitude { get; init; }
    [Id(7)] public double? Longitude { get; init; }
}

[GenerateSerializer]
public record OperatingHours
{
    [Id(0)] public IReadOnlyList<DaySchedule> Schedule { get; init; } = [];
}

[GenerateSerializer]
public record DaySchedule
{
    [Id(0)] public DayOfWeek Day { get; init; }
    [Id(1)] public bool IsClosed { get; init; }
    [Id(2)] public TimeOnly? OpenTime { get; init; }
    [Id(3)] public TimeOnly? CloseTime { get; init; }
    [Id(4)] public TimeOnly? BreakStart { get; init; }
    [Id(5)] public TimeOnly? BreakEnd { get; init; }
}

[GenerateSerializer]
public record TaxJurisdiction
{
    [Id(0)] public string Country { get; init; } = string.Empty;
    [Id(1)] public string? State { get; init; }
    [Id(2)] public string? County { get; init; }
    [Id(3)] public string? City { get; init; }
    [Id(4)] public decimal DefaultTaxRate { get; init; }
}

[GenerateSerializer]
public record BookingSettings
{
    [Id(0)] public bool AcceptBookings { get; init; } = true;
    [Id(1)] public int MaxAdvanceBookingDays { get; init; } = 30;
    [Id(2)] public int MinAdvanceBookingHours { get; init; } = 2;
    [Id(3)] public int DefaultDurationMinutes { get; init; } = 90;
    [Id(4)] public int TurnTimeMinutes { get; init; } = 15;
    [Id(5)] public int GracePeriodMinutes { get; init; } = 15;
    [Id(6)] public bool RequireDeposit { get; init; }
    [Id(7)] public int DepositPartySizeThreshold { get; init; } = 6;
    [Id(8)] public decimal DepositAmountPerPerson { get; init; } = 25m;
}

[GenerateSerializer]
public record SiteSettings
{
    [Id(0)] public Guid? ActiveMenuId { get; init; }
    [Id(1)] public Guid? DefaultPriceListId { get; init; }
    [Id(2)] public int DefaultGuestCount { get; init; } = 1;
    [Id(3)] public bool AutoPrintKitchenTickets { get; init; } = true;
    [Id(4)] public bool AutoPrintReceipts { get; init; }
    [Id(5)] public TimeSpan OrderTimeout { get; init; } = TimeSpan.FromHours(4);
    [Id(6)] public BookingSettings BookingSettings { get; init; } = new();
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
