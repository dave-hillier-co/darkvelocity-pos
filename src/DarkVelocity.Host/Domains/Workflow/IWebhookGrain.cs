using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// Webhook Subscription Commands
[GenerateSerializer]
public record CreateWebhookCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] string Name,
    [property: Id(2)] string Url,
    [property: Id(3)] List<string> EventTypes,
    [property: Id(4)] string? Secret = null,
    [property: Id(5)] Dictionary<string, string>? Headers = null);

[GenerateSerializer]
public record UpdateWebhookCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] string? Url = null,
    [property: Id(2)] string? Secret = null,
    [property: Id(3)] List<string>? EventTypes = null,
    [property: Id(4)] Dictionary<string, string>? Headers = null);

[GenerateSerializer]
public record WebhookCreatedResult([property: Id(0)] Guid Id, [property: Id(1)] string Name, [property: Id(2)] DateTime CreatedAt);

[GenerateSerializer]
public record DeliveryResult([property: Id(0)] Guid DeliveryId, [property: Id(1)] bool Success, [property: Id(2)] int StatusCode);

public interface IWebhookSubscriptionGrain : IGrainWithStringKey
{
    Task<WebhookCreatedResult> CreateAsync(CreateWebhookCommand command);
    Task<WebhookSubscriptionState> GetStateAsync();
    Task UpdateAsync(UpdateWebhookCommand command);
    Task DeleteAsync();

    // Event subscription
    Task SubscribeToEventAsync(string eventType);
    Task UnsubscribeFromEventAsync(string eventType);
    Task<bool> IsSubscribedToEventAsync(string eventType);

    // Status management
    Task PauseAsync();
    Task ResumeAsync();
    Task<WebhookStatus> GetStatusAsync();

    // Delivery
    Task<DeliveryResult> DeliverAsync(string eventType, string payload);
    Task RecordDeliveryAsync(WebhookDelivery delivery);
    Task<IReadOnlyList<WebhookDelivery>> GetRecentDeliveriesAsync();

    // Queries
    Task<bool> ExistsAsync();
}

// Booking Calendar Grain (for listing bookings by date)
[GenerateSerializer]
public record AddBookingToCalendarCommand(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] string ConfirmationCode,
    [property: Id(2)] TimeOnly Time,
    [property: Id(3)] int PartySize,
    [property: Id(4)] string GuestName,
    [property: Id(5)] BookingStatus Status);

[GenerateSerializer]
public record UpdateBookingInCalendarCommand(
    [property: Id(0)] Guid BookingId,
    [property: Id(1)] BookingStatus? Status = null,
    [property: Id(2)] TimeOnly? Time = null,
    [property: Id(3)] int? PartySize = null,
    [property: Id(4)] Guid? TableId = null,
    [property: Id(5)] string? TableNumber = null);

public interface IBookingCalendarGrain : IGrainWithStringKey
{
    Task InitializeAsync(Guid organizationId, Guid siteId, DateOnly date);
    Task<BookingCalendarState> GetStateAsync();

    // Booking management
    Task AddBookingAsync(AddBookingToCalendarCommand command);
    Task UpdateBookingAsync(UpdateBookingInCalendarCommand command);
    Task RemoveBookingAsync(Guid bookingId);

    // Queries
    Task<IReadOnlyList<BookingReference>> GetBookingsAsync(BookingStatus? status = null);
    Task<IReadOnlyList<BookingReference>> GetBookingsByTimeRangeAsync(TimeOnly start, TimeOnly end);
    Task<int> GetCoverCountAsync();
    Task<int> GetBookingCountAsync(BookingStatus? status = null);

    Task<bool> ExistsAsync();
}

// Customer Visit History Commands
[GenerateSerializer]
public record RecordCustomerVisitCommand(
    [property: Id(0)] Guid SiteId,
    [property: Id(1)] string? SiteName,
    [property: Id(2)] Guid? OrderId,
    [property: Id(3)] Guid? BookingId,
    [property: Id(4)] decimal SpendAmount,
    [property: Id(5)] int PartySize = 1,
    [property: Id(6)] int PointsEarned = 0,
    [property: Id(7)] string? Notes = null);

// Extended customer grain methods for visit history
public interface ICustomerVisitHistoryGrain : IGrainWithStringKey
{
    Task RecordVisitAsync(RecordCustomerVisitCommand command);
    Task<IReadOnlyList<CustomerVisit>> GetVisitHistoryAsync(int limit = 50);
    Task<IReadOnlyList<CustomerVisit>> GetVisitsBySiteAsync(Guid siteId, int limit = 20);
    Task<CustomerVisit?> GetLastVisitAsync();
}
