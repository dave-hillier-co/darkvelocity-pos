using DarkVelocity.Host.Events;

namespace DarkVelocity.Host.Contracts;

/// <summary>
/// Request to map a line item to an internal SKU.
/// </summary>
public record MapLineRequest(
    Guid IngredientId,
    string IngredientSku,
    string IngredientName,
    MappingSource? Source = null,
    decimal? Confidence = null);

/// <summary>
/// Request to update a line item.
/// </summary>
public record UpdateLineRequest(
    string? Description = null,
    decimal? Quantity = null,
    string? Unit = null,
    decimal? UnitPrice = null);

/// <summary>
/// Request to confirm a document.
/// </summary>
public record ConfirmDocumentRequest(
    Guid ConfirmedBy,
    Guid? VendorId = null,
    string? VendorName = null,
    DateOnly? DocumentDate = null,
    string? Currency = null);

/// <summary>
/// Request to reject/delete a document.
/// </summary>
public record RejectDocumentRequest(
    Guid RejectedBy,
    string Reason);
