using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Commands
// ============================================================================

/// <summary>
/// Command to receive a new purchase document.
/// </summary>
[GenerateSerializer]
public record ReceivePurchaseDocumentCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid DocumentId,
    [property: Id(3)] PurchaseDocumentType DocumentType,
    [property: Id(4)] DocumentSource Source,
    [property: Id(5)] string StorageUrl,
    [property: Id(6)] string OriginalFilename,
    [property: Id(7)] string ContentType,
    [property: Id(8)] long FileSizeBytes,
    [property: Id(9)] string? EmailFrom = null,
    [property: Id(10)] string? EmailSubject = null,
    [property: Id(11)] bool? IsPaid = null);

/// <summary>
/// Command to apply extraction results from OCR processing.
/// </summary>
[GenerateSerializer]
public record ApplyExtractionResultCommand(
    [property: Id(0)] ExtractedDocumentData Data,
    [property: Id(1)] decimal Confidence,
    [property: Id(2)] string ProcessorVersion);

/// <summary>
/// Command to mark extraction as failed.
/// </summary>
[GenerateSerializer]
public record MarkExtractionFailedCommand(
    [property: Id(0)] string FailureReason,
    [property: Id(1)] string? ProcessorError = null);

/// <summary>
/// Command to map a line item to an internal SKU.
/// </summary>
[GenerateSerializer]
public record MapLineCommand(
    [property: Id(0)] int LineIndex,
    [property: Id(1)] Guid IngredientId,
    [property: Id(2)] string IngredientSku,
    [property: Id(3)] string IngredientName,
    [property: Id(4)] MappingSource Source,
    [property: Id(5)] decimal Confidence = 1.0m);

/// <summary>
/// Command to unmap a line item (clear SKU mapping).
/// </summary>
[GenerateSerializer]
public record UnmapLineCommand(
    [property: Id(0)] int LineIndex);

/// <summary>
/// Command to update extracted line item data.
/// </summary>
[GenerateSerializer]
public record UpdatePurchaseLineCommand(
    [property: Id(0)] int LineIndex,
    [property: Id(1)] string? Description = null,
    [property: Id(2)] decimal? Quantity = null,
    [property: Id(3)] string? Unit = null,
    [property: Id(4)] decimal? UnitPrice = null);

/// <summary>
/// Command to confirm the document data.
/// </summary>
[GenerateSerializer]
public record ConfirmPurchaseDocumentCommand(
    [property: Id(0)] Guid ConfirmedBy,
    [property: Id(1)] Guid? VendorId = null,
    [property: Id(2)] string? VendorName = null,
    [property: Id(3)] DateOnly? DocumentDate = null,
    [property: Id(4)] string? Currency = null);

/// <summary>
/// Command to reject/discard a document.
/// </summary>
[GenerateSerializer]
public record RejectPurchaseDocumentCommand(
    [property: Id(0)] Guid RejectedBy,
    [property: Id(1)] string Reason);

// ============================================================================
// Results
// ============================================================================

/// <summary>
/// Snapshot of purchase document state for API responses.
/// </summary>
[GenerateSerializer]
public record PurchaseDocumentSnapshot(
    [property: Id(0)] Guid DocumentId,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] PurchaseDocumentType DocumentType,
    [property: Id(4)] PurchaseDocumentStatus Status,
    [property: Id(5)] DocumentSource Source,
    [property: Id(6)] string StorageUrl,
    [property: Id(7)] string OriginalFilename,
    [property: Id(8)] string? VendorName,
    [property: Id(9)] DateOnly? DocumentDate,
    [property: Id(10)] string? InvoiceNumber,
    [property: Id(11)] IReadOnlyList<PurchaseDocumentLineSnapshot> Lines,
    [property: Id(12)] decimal? Total,
    [property: Id(13)] string Currency,
    [property: Id(14)] bool IsPaid,
    [property: Id(15)] decimal ExtractionConfidence,
    [property: Id(16)] string? ProcessingError,
    [property: Id(17)] DateTime CreatedAt,
    [property: Id(18)] DateTime? ConfirmedAt,
    [property: Id(19)] int Version);

/// <summary>
/// Snapshot of a line item.
/// </summary>
[GenerateSerializer]
public record PurchaseDocumentLineSnapshot(
    [property: Id(0)] int LineIndex,
    [property: Id(1)] string Description,
    [property: Id(2)] decimal? Quantity,
    [property: Id(3)] string? Unit,
    [property: Id(4)] decimal? UnitPrice,
    [property: Id(5)] decimal? TotalPrice,
    [property: Id(6)] Guid? MappedIngredientId,
    [property: Id(7)] string? MappedIngredientSku,
    [property: Id(8)] string? MappedIngredientName,
    [property: Id(9)] MappingSource? MappingSource,
    [property: Id(10)] decimal MappingConfidence,
    [property: Id(11)] IReadOnlyList<SuggestedMapping>? Suggestions);

// ============================================================================
// Grain Interface
// ============================================================================

/// <summary>
/// Grain representing a purchase document (invoice or receipt).
/// </summary>
public interface IPurchaseDocumentGrain : IGrainWithStringKey
{
    /// <summary>
    /// Records a new document received from an external source.
    /// Follows the "Received" pattern for external data.
    /// </summary>
    Task<PurchaseDocumentSnapshot> ReceiveAsync(ReceivePurchaseDocumentCommand command);

    /// <summary>
    /// Triggers OCR/extraction processing.
    /// </summary>
    Task RequestProcessingAsync();

    /// <summary>
    /// Called when extraction completes successfully.
    /// </summary>
    Task ApplyExtractionResultAsync(ApplyExtractionResultCommand command);

    /// <summary>
    /// Called when extraction fails.
    /// </summary>
    Task MarkExtractionFailedAsync(MarkExtractionFailedCommand command);

    /// <summary>
    /// Map a line item to an internal SKU.
    /// </summary>
    Task MapLineAsync(MapLineCommand command);

    /// <summary>
    /// Clear SKU mapping for a line item.
    /// </summary>
    Task UnmapLineAsync(UnmapLineCommand command);

    /// <summary>
    /// Update extracted line item data (corrections).
    /// </summary>
    Task UpdateLineAsync(UpdatePurchaseLineCommand command);

    /// <summary>
    /// Set suggestions for an unmapped line item.
    /// </summary>
    Task SetLineSuggestionsAsync(int lineIndex, IReadOnlyList<SuggestedMapping> suggestions);

    /// <summary>
    /// Confirm the document data is correct and ready for downstream processing.
    /// </summary>
    Task<PurchaseDocumentSnapshot> ConfirmAsync(ConfirmPurchaseDocumentCommand command);

    /// <summary>
    /// Reject/discard the document.
    /// </summary>
    Task RejectAsync(RejectPurchaseDocumentCommand command);

    /// <summary>
    /// Get the current state snapshot.
    /// </summary>
    Task<PurchaseDocumentSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Get the full state (for internal use).
    /// </summary>
    Task<PurchaseDocumentState> GetStateAsync();

    /// <summary>
    /// Check if the document exists.
    /// </summary>
    Task<bool> ExistsAsync();
}
