using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record RequestBookingCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] GuestInfo Guest,
    [property: Id(3)] DateTime RequestedTime,
    [property: Id(4)] int PartySize,
    [property: Id(5)] TimeSpan? Duration = null,
    [property: Id(6)] string? SpecialRequests = null,
    [property: Id(7)] string? Occasion = null,
    [property: Id(8)] BookingSource Source = BookingSource.Direct,
    [property: Id(9)] string? ExternalRef = null,
    [property: Id(10)] Guid? CustomerId = null);

[GenerateSerializer]
public record ModifyBookingCommand(
    [property: Id(0)] DateTime? NewTime = null,
    [property: Id(1)] int? NewPartySize = null,
    [property: Id(2)] TimeSpan? NewDuration = null,
    [property: Id(3)] string? SpecialRequests = null);

[GenerateSerializer]
public record CancelBookingCommand(
    [property: Id(0)] string Reason,
    [property: Id(1)] Guid CancelledBy);

[GenerateSerializer]
public record AssignTableCommand(
    [property: Id(0)] Guid TableId,
    [property: Id(1)] string TableNumber,
    [property: Id(2)] int Capacity);

[GenerateSerializer]
public record RecordArrivalCommand([property: Id(0)] Guid CheckedInBy);

[GenerateSerializer]
public record SeatGuestCommand(
    [property: Id(0)] Guid TableId,
    [property: Id(1)] string TableNumber,
    [property: Id(2)] Guid SeatedBy);

[GenerateSerializer]
public record RecordDepartureCommand([property: Id(0)] Guid? OrderId = null);

[GenerateSerializer]
public record RequireDepositCommand(
    [property: Id(0)] decimal Amount,
    [property: Id(1)] DateTime RequiredBy);

[GenerateSerializer]
public record RecordDepositPaymentCommand(
    [property: Id(0)] PaymentMethod Method,
    [property: Id(1)] string PaymentReference);

[GenerateSerializer]
public record BookingRequestedResult([property: Id(0)] Guid Id, [property: Id(1)] string ConfirmationCode, [property: Id(2)] DateTime CreatedAt);
[GenerateSerializer]
public record BookingConfirmedResult([property: Id(0)] DateTime ConfirmedTime, [property: Id(1)] string ConfirmationCode);

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

[GenerateSerializer]
public record AddToWaitlistCommand(
    [property: Id(0)] GuestInfo Guest,
    [property: Id(1)] int PartySize,
    [property: Id(2)] TimeSpan QuotedWait,
    [property: Id(3)] string? TablePreferences = null,
    [property: Id(4)] NotificationMethod NotificationMethod = NotificationMethod.Sms);

[GenerateSerializer]
public record WaitlistEntryResult([property: Id(0)] Guid EntryId, [property: Id(1)] int Position, [property: Id(2)] TimeSpan QuotedWait);

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
