using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Manages ordered collections of line items.
/// Used as a composition grain by OrderGrain, PurchaseDocumentGrain, etc.
/// Key format: org:{orgId}:lines:{ownerType}:{ownerId}
/// </summary>
public interface ILineItemsGrain : IGrainWithStringKey
{
    /// <summary>
    /// Adds a new line item to the collection.
    /// </summary>
    Task<LineItemResult> AddAsync(
        string itemType,
        decimal quantity,
        decimal unitPrice,
        Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Updates an existing line item.
    /// </summary>
    Task UpdateAsync(
        Guid lineId,
        decimal? quantity = null,
        decimal? unitPrice = null,
        Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Voids a line item (marks as voided but retains for audit).
    /// </summary>
    Task VoidAsync(Guid lineId, Guid voidedBy, string reason);

    /// <summary>
    /// Removes a line item completely.
    /// </summary>
    Task RemoveAsync(Guid lineId);

    /// <summary>
    /// Gets all line items, optionally including voided items.
    /// </summary>
    Task<IReadOnlyList<LineItem>> GetLinesAsync(bool includeVoided = false);

    /// <summary>
    /// Gets totals for the line items (excludes voided items).
    /// </summary>
    Task<LineItemTotals> GetTotalsAsync();

    /// <summary>
    /// Gets the current state of the line items collection.
    /// </summary>
    Task<LineItemsState> GetStateAsync();

    /// <summary>
    /// Checks if this grain has been initialized with any lines.
    /// </summary>
    Task<bool> HasLinesAsync();
}

/// <summary>
/// Result of adding a line item.
/// </summary>
[GenerateSerializer]
public record LineItemResult(
    [property: Id(0)] Guid LineId,
    [property: Id(1)] int Index,
    [property: Id(2)] decimal ExtendedPrice,
    [property: Id(3)] LineItemTotals Totals);

/// <summary>
/// Totals for a line items collection.
/// </summary>
[GenerateSerializer]
public record LineItemTotals
{
    [Id(0)] public int LineCount { get; init; }
    [Id(1)] public int VoidedCount { get; init; }
    [Id(2)] public decimal TotalQuantity { get; init; }
    [Id(3)] public decimal Subtotal { get; init; }
}
