namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all BookingSettings events used in event sourcing.
/// </summary>
public interface IBookingSettingsEvent
{
    Guid SiteId { get; }
    DateTimeOffset OccurredAt { get; }
}

[GenerateSerializer]
public sealed record BookingSettingsInitialized : IBookingSettingsEvent
{
    [Id(0)] public Guid SiteId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public DateTimeOffset OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingSettingsUpdated : IBookingSettingsEvent
{
    [Id(0)] public Guid SiteId { get; init; }
    [Id(1)] public TimeOnly? DefaultOpenTime { get; init; }
    [Id(2)] public TimeOnly? DefaultCloseTime { get; init; }
    [Id(3)] public TimeSpan? DefaultDuration { get; init; }
    [Id(4)] public TimeSpan? SlotInterval { get; init; }
    [Id(5)] public int? MaxPartySizeOnline { get; init; }
    [Id(6)] public int? MaxBookingsPerSlot { get; init; }
    [Id(7)] public int? AdvanceBookingDays { get; init; }
    [Id(8)] public bool? RequireDeposit { get; init; }
    [Id(9)] public decimal? DepositAmount { get; init; }
    [Id(10)] public DateTimeOffset OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingDateBlocked : IBookingSettingsEvent
{
    [Id(0)] public Guid SiteId { get; init; }
    [Id(1)] public DateOnly Date { get; init; }
    [Id(2)] public DateTimeOffset OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingDateUnblocked : IBookingSettingsEvent
{
    [Id(0)] public Guid SiteId { get; init; }
    [Id(1)] public DateOnly Date { get; init; }
    [Id(2)] public DateTimeOffset OccurredAt { get; init; }
}
