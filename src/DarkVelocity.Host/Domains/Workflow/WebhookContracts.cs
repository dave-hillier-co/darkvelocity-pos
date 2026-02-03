namespace DarkVelocity.Host.Contracts;

// ============================================================================
// Webhook Request DTOs
// ============================================================================

public record CreateWebhookRequest(
    string Name,
    string Url,
    List<string> EventTypes,
    string? Secret = null,
    Dictionary<string, string>? Headers = null);

public record UpdateWebhookRequest(
    string? Name = null,
    string? Url = null,
    string? Secret = null,
    List<string>? EventTypes = null,
    Dictionary<string, string>? Headers = null);
