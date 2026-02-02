using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

public record RequestBookingRequest(
    GuestInfo Guest,
    DateTime RequestedTime,
    int PartySize,
    TimeSpan? Duration = null,
    string? SpecialRequests = null,
    string? Occasion = null,
    BookingSource Source = BookingSource.Direct,
    string? ExternalRef = null,
    Guid? CustomerId = null);

public record ConfirmBookingRequest(DateTime? ConfirmedTime = null);
public record CancelBookingRequest(string Reason, Guid CancelledBy);
public record CheckInRequest(Guid CheckedInBy);
public record SeatGuestRequest(Guid TableId, string TableNumber, Guid SeatedBy);
