using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Grains;

public record RequestBookingCommand(
    Guid OrganizationId,
    Guid SiteId,
    GuestInfo Guest,
    DateTime RequestedTime,
    int PartySize,
    TimeSpan? Duration = null,
    string? SpecialRequests = null,
    string? Occasion = null,
    BookingSource Source = BookingSource.Direct,
    string? ExternalRef = null,
    Guid? CustomerId = null);

public record ModifyBookingCommand(
    DateTime? NewTime = null,
    int? NewPartySize = null,
    TimeSpan? NewDuration = null,
    string? SpecialRequests = null);

public record CancelBookingCommand(
    string Reason,
    Guid CancelledBy);

public record AssignTableCommand(
    Guid TableId,
    string TableNumber,
    int Capacity);

public record RecordArrivalCommand(Guid CheckedInBy);

public record SeatGuestCommand(
    Guid TableId,
    string TableNumber,
    Guid SeatedBy);

public record RecordDepartureCommand(Guid? OrderId = null);

public record RequireDepositCommand(
    decimal Amount,
    DateTime RequiredBy);

public record RecordDepositPaymentCommand(
    PaymentMethod Method,
    string PaymentReference);

public record BookingRequestedResult(Guid Id, string ConfirmationCode, DateTime CreatedAt);
public record BookingConfirmedResult(DateTime ConfirmedTime, string ConfirmationCode);

public interface IBookingGrain : IGrainWithStringKey
{
    Task<BookingRequestedResult> RequestAsync(RequestBookingCommand command);
    Task<BookingState> GetStateAsync();

    // Booking lifecycle
    Task<BookingConfirmedResult> ConfirmAsync(DateTime? confirmedTime = null);
    Task ModifyAsync(ModifyBookingCommand command);
    Task CancelAsync(CancelBookingCommand command);

    // Table management
    Task AssignTableAsync(AssignTableCommand command);
    Task ClearTableAssignmentAsync();

    // Guest arrival & seating
    Task<DateTime> RecordArrivalAsync(RecordArrivalCommand command);
    Task SeatGuestAsync(SeatGuestCommand command);
    Task RecordDepartureAsync(RecordDepartureCommand command);
    Task MarkNoShowAsync(Guid? markedBy = null);

    // Special requests
    Task AddSpecialRequestAsync(string request);
    Task AddNoteAsync(string note, Guid addedBy);
    Task AddTagAsync(string tag);

    // Deposit management
    Task RequireDepositAsync(RequireDepositCommand command);
    Task RecordDepositPaymentAsync(RecordDepositPaymentCommand command);
    Task WaiveDepositAsync(Guid waivedBy);
    Task ForfeitDepositAsync();
    Task RefundDepositAsync(string reason, Guid refundedBy);

    // Order linking
    Task LinkToOrderAsync(Guid orderId);

    // Queries
    Task<bool> ExistsAsync();
    Task<BookingStatus> GetStatusAsync();
    Task<bool> RequiresDepositAsync();
}

public record AddToWaitlistCommand(
    GuestInfo Guest,
    int PartySize,
    TimeSpan QuotedWait,
    string? TablePreferences = null,
    NotificationMethod NotificationMethod = NotificationMethod.Sms);

public record WaitlistEntryResult(Guid EntryId, int Position, TimeSpan QuotedWait);

public interface IWaitlistGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid organizationId, Guid siteId, DateOnly date);
    Task<WaitlistState> GetStateAsync();

    Task<WaitlistEntryResult> AddEntryAsync(AddToWaitlistCommand command);
    Task UpdatePositionAsync(Guid entryId, int newPosition);
    Task NotifyEntryAsync(Guid entryId);
    Task SeatEntryAsync(Guid entryId, Guid tableId);
    Task RemoveEntryAsync(Guid entryId, string reason);
    Task<Guid?> ConvertToBookingAsync(Guid entryId, DateTime bookingTime);

    Task<int> GetWaitingCountAsync();
    Task<TimeSpan> GetEstimatedWaitAsync(int partySize);
    Task<IReadOnlyList<WaitlistEntry>> GetEntriesAsync();
    Task<bool> ExistsAsync();
}
