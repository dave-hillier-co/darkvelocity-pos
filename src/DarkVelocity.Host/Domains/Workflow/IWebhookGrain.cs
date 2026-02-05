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
