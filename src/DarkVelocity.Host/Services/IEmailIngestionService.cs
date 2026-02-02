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
public record ParsedEmail
{
    /// <summary>Unique message ID from email headers</summary>
    public required string MessageId { get; init; }

    /// <summary>Sender email address</summary>
    public required string From { get; init; }

    /// <summary>Sender display name if available</summary>
    public string? FromName { get; init; }

    /// <summary>Recipient email address (the inbox)</summary>
    public required string To { get; init; }

    /// <summary>Email subject line</summary>
    public required string Subject { get; init; }

    /// <summary>Plain text body</summary>
    public string? TextBody { get; init; }

    /// <summary>HTML body</summary>
    public string? HtmlBody { get; init; }

    /// <summary>When the email was sent</summary>
    public DateTime SentAt { get; init; }

    /// <summary>When the email was received by our system</summary>
    public DateTime ReceivedAt { get; init; }

    /// <summary>Extracted attachments</summary>
    public required IReadOnlyList<EmailAttachment> Attachments { get; init; }

    /// <summary>Raw headers for debugging</summary>
    public Dictionary<string, string>? Headers { get; init; }
}

/// <summary>
/// An attachment extracted from an email.
/// </summary>
public record EmailAttachment
{
    /// <summary>Original filename</summary>
    public required string Filename { get; init; }

    /// <summary>MIME content type</summary>
    public required string ContentType { get; init; }

    /// <summary>Size in bytes</summary>
    public required long SizeBytes { get; init; }

    /// <summary>The attachment content</summary>
    public required byte[] Content { get; init; }

    /// <summary>Content ID for inline attachments</summary>
    public string? ContentId { get; init; }

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
public record SiteEmailInfo(
    Guid OrganizationId,
    Guid SiteId,
    string InboxType); // "invoices", "receipts", "expenses"

/// <summary>
/// Result of processing an incoming email.
/// </summary>
public record EmailProcessingResult
{
    public required bool Success { get; init; }
    public required string MessageId { get; init; }
    public IReadOnlyList<Guid>? CreatedDocumentIds { get; init; }
    public string? Error { get; init; }
    public int AttachmentsProcessed { get; init; }
    public int AttachmentsSkipped { get; init; }
}
