using DarkVelocity.Shared.Contracts.Hal;

namespace DarkVelocity.Booking.Api.Dtos;

public class WaitlistEntryDto : HalResource
{
    public Guid Id { get; set; }
    public Guid LocationId { get; set; }
    public string GuestName { get; set; } = string.Empty;
    public string? GuestPhone { get; set; }
    public string? GuestEmail { get; set; }
    public int PartySize { get; set; }
    public DateOnly RequestedDate { get; set; }
    public TimeOnly? PreferredTime { get; set; }
    public TimeOnly? LatestAcceptableTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public int QueuePosition { get; set; }
    public int? EstimatedWaitMinutes { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? NotifiedAt { get; set; }
    public DateTime? OfferExpiresAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? SeatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? OfferedTableId { get; set; }
    public string? OfferedTableNumber { get; set; }
    public Guid? ConvertedToBookingId { get; set; }
    public int NotificationCount { get; set; }
    public string? Notes { get; set; }
    public string Source { get; set; } = string.Empty;
    public Guid? PreferredFloorPlanId { get; set; }
    public string? SeatingPreference { get; set; }
}

public class WaitlistSummaryDto
{
    public int TotalWaiting { get; set; }
    public int? AverageWaitMinutes { get; set; }
    public int? LongestWaitMinutes { get; set; }
    public List<WaitlistByPartySizeDto> ByPartySize { get; set; } = new();
}

public class WaitlistByPartySizeDto
{
    public int PartySize { get; set; }
    public int Count { get; set; }
    public int? AverageWaitMinutes { get; set; }
}

// Request DTOs

public record CreateWaitlistEntryRequest(
    string GuestName,
    int PartySize,
    DateOnly RequestedDate,
    TimeOnly? PreferredTime = null,
    TimeOnly? LatestAcceptableTime = null,
    string? GuestPhone = null,
    string? GuestEmail = null,
    string? Notes = null,
    string Source = "walk_in",
    Guid? PreferredFloorPlanId = null,
    string? SeatingPreference = null);

public record UpdateWaitlistEntryRequest(
    string? GuestName = null,
    string? GuestPhone = null,
    string? GuestEmail = null,
    int? PartySize = null,
    TimeOnly? PreferredTime = null,
    TimeOnly? LatestAcceptableTime = null,
    string? Notes = null,
    Guid? PreferredFloorPlanId = null,
    string? SeatingPreference = null);

public record OfferTableRequest(
    Guid TableId,
    int? OfferExpiryMinutes = null);

public record ConvertToBookingRequest(
    Guid? TableId = null,
    int? DurationMinutes = null);

public record CancelWaitlistEntryRequest(
    string? Reason = null);
