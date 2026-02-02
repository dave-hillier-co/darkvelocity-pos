using Orleans;

namespace DarkVelocity.Host.Services;

/// <summary>
/// Service for ingesting emails containing invoices and receipts.
/// Implementations can use different email providers (Azure Logic Apps, AWS SES, SendGrid, etc.)
/// </summary>
public interface IEmailIngestionService
{
    /// <summary>
    /// Parse an incoming email from a webhook payload.
    /// </summary>
    Task<ParsedEmail> ParseEmailAsync(
        Stream emailContent,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parse an incoming email from raw MIME format.
    /// </summary>
    Task<ParsedEmail> ParseMimeEmailAsync(
        string mimeContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extract the site ID from an email address (e.g., invoices-{siteId}@domain.com).
    /// </summary>
    SiteEmailInfo? ParseInboxAddress(string emailAddress);
}

/// <summary>
/// A parsed email with extracted metadata and attachments.
/// </summary>
[GenerateSerializer]
public record ParsedEmail
{
    /// <summary>Unique message ID from email headers</summary>
    [Id(0)] public required string MessageId { get; init; }

    /// <summary>Sender email address</summary>
    [Id(1)] public required string From { get; init; }

    /// <summary>Sender display name if available</summary>
    [Id(2)] public string? FromName { get; init; }

    /// <summary>Recipient email address (the inbox)</summary>
    [Id(3)] public required string To { get; init; }

    /// <summary>Email subject line</summary>
    [Id(4)] public required string Subject { get; init; }

    /// <summary>Plain text body</summary>
    [Id(5)] public string? TextBody { get; init; }

    /// <summary>HTML body</summary>
    [Id(6)] public string? HtmlBody { get; init; }

    /// <summary>When the email was sent</summary>
    [Id(7)] public DateTime SentAt { get; init; }

    /// <summary>When the email was received by our system</summary>
    [Id(8)] public DateTime ReceivedAt { get; init; }

    /// <summary>Extracted attachments</summary>
    [Id(9)] public required IReadOnlyList<EmailAttachment> Attachments { get; init; }

    /// <summary>Raw headers for debugging</summary>
    [Id(10)] public Dictionary<string, string>? Headers { get; init; }
}

/// <summary>
/// An attachment extracted from an email.
/// </summary>
[GenerateSerializer]
public record EmailAttachment
{
    /// <summary>Original filename</summary>
    [Id(0)] public required string Filename { get; init; }

    /// <summary>MIME content type</summary>
    [Id(1)] public required string ContentType { get; init; }

    /// <summary>Size in bytes</summary>
    [Id(2)] public required long SizeBytes { get; init; }

    /// <summary>The attachment content</summary>
    [Id(3)] public required byte[] Content { get; init; }

    /// <summary>Content ID for inline attachments</summary>
    [Id(4)] public string? ContentId { get; init; }

    /// <summary>Whether this appears to be a document (PDF, image)</summary>
    public bool IsDocument => IsDocumentContentType(ContentType);

    private static bool IsDocumentContentType(string contentType)
    {
        var lower = contentType.ToLowerInvariant();
        return lower.StartsWith("application/pdf") ||
               lower.StartsWith("image/") ||
               lower.Contains("spreadsheet") ||
               lower.Contains("excel");
    }
}

/// <summary>
/// Information extracted from a site-specific inbox address.
/// </summary>
[GenerateSerializer]
public record SiteEmailInfo(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string InboxType); // "invoices", "receipts", "expenses"

/// <summary>
/// Result of processing an incoming email.
/// </summary>
[GenerateSerializer]
public record EmailProcessingResult
{
    [Id(0)] public required bool Success { get; init; }
    [Id(1)] public required string MessageId { get; init; }
    [Id(2)] public IReadOnlyList<Guid>? CreatedDocumentIds { get; init; }
    [Id(3)] public string? Error { get; init; }
    [Id(4)] public int AttachmentsProcessed { get; init; }
    [Id(5)] public int AttachmentsSkipped { get; init; }
}
