using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Transfer status.
/// </summary>
public enum TransferStatus
{
    /// <summary>Transfer has been requested but not yet approved.</summary>
    Requested,
    /// <summary>Transfer has been approved and is ready to ship.</summary>
    Approved,
    /// <summary>Transfer request was rejected.</summary>
    Rejected,
    /// <summary>Transfer items have been shipped.</summary>
    Shipped,
    /// <summary>Transfer items have been received at destination.</summary>
    Received,
    /// <summary>Transfer was cancelled.</summary>
    Cancelled
}

[GenerateSerializer]
public record RequestTransferCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SourceSiteId,
    [property: Id(2)] Guid DestinationSiteId,
    [property: Id(3)] string TransferNumber,
    [property: Id(4)] Guid RequestedBy,
    [property: Id(5)] List<TransferLineRequest> Lines,
    [property: Id(6)] DateTime? RequestedDeliveryDate = null,
    [property: Id(7)] string? Notes = null);

[GenerateSerializer]
public record TransferLineRequest(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] string IngredientName,
    [property: Id(2)] decimal Quantity,
    [property: Id(3)] string Unit);

[GenerateSerializer]
public record ApproveTransferCommand(
    [property: Id(0)] Guid ApprovedBy,
    [property: Id(1)] string? Notes = null);

[GenerateSerializer]
public record RejectTransferCommand(
    [property: Id(0)] Guid RejectedBy,
    [property: Id(1)] string Reason);

[GenerateSerializer]
public record ShipTransferCommand(
    [property: Id(0)] Guid ShippedBy,
    [property: Id(1)] DateTime? EstimatedArrival = null,
    [property: Id(2)] string? TrackingNumber = null,
    [property: Id(3)] string? Carrier = null,
    [property: Id(4)] string? Notes = null);

[GenerateSerializer]
public record ReceiveTransferItemCommand(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] decimal ReceivedQuantity,
    [property: Id(2)] Guid ReceivedBy,
    [property: Id(3)] string? Condition = null,
    [property: Id(4)] string? Notes = null);

[GenerateSerializer]
public record FinalizeTransferReceiptCommand(
    [property: Id(0)] Guid ReceivedBy,
    [property: Id(1)] string? Notes = null);

[GenerateSerializer]
public record CancelTransferCommand(
    [property: Id(0)] Guid CancelledBy,
    [property: Id(1)] string Reason,
    [property: Id(2)] bool ReturnStockToSource = true);

[GenerateSerializer]
public record TransferLineState
{
    [Id(0)] public Guid LineId { get; init; }
    [Id(1)] public Guid IngredientId { get; init; }
    [Id(2)] public string IngredientName { get; init; } = string.Empty;
    [Id(3)] public decimal RequestedQuantity { get; init; }
    [Id(4)] public decimal ShippedQuantity { get; init; }
    [Id(5)] public decimal ReceivedQuantity { get; init; }
    [Id(6)] public decimal UnitCost { get; init; }
    [Id(7)] public string Unit { get; init; } = string.Empty;
    [Id(8)] public string? Condition { get; init; }
    [Id(9)] public string? Notes { get; init; }
}

[GenerateSerializer]
public record TransferSummary
{
    [Id(0)] public Guid TransferId { get; init; }
    [Id(1)] public string TransferNumber { get; init; } = string.Empty;
    [Id(2)] public TransferStatus Status { get; init; }
    [Id(3)] public Guid SourceSiteId { get; init; }
    [Id(4)] public Guid DestinationSiteId { get; init; }
    [Id(5)] public DateTime RequestedAt { get; init; }
    [Id(6)] public DateTime? ShippedAt { get; init; }
    [Id(7)] public DateTime? ReceivedAt { get; init; }
    [Id(8)] public int TotalLines { get; init; }
    [Id(9)] public decimal TotalValue { get; init; }
    [Id(10)] public decimal TotalVariance { get; init; }
}

/// <summary>
/// Grain for managing inventory transfer lifecycle between sites.
/// Handles the full workflow: Request -> Approve -> Ship -> Receive.
/// </summary>
public interface IInventoryTransferGrain : IGrainWithStringKey
{
    /// <summary>
    /// Requests a transfer from source site to destination site.
    /// </summary>
    Task RequestAsync(RequestTransferCommand command);

    /// <summary>
    /// Gets the current transfer state.
    /// </summary>
    Task<InventoryTransferState> GetStateAsync();

    /// <summary>
    /// Gets a summary of the transfer.
    /// </summary>
    Task<TransferSummary> GetSummaryAsync();

    /// <summary>
    /// Approves the transfer request (typically by source site manager).
    /// </summary>
    Task ApproveAsync(ApproveTransferCommand command);

    /// <summary>
    /// Rejects the transfer request.
    /// </summary>
    Task RejectAsync(RejectTransferCommand command);

    /// <summary>
    /// Marks items as shipped (deducts from source inventory).
    /// </summary>
    Task ShipAsync(ShipTransferCommand command);

    /// <summary>
    /// Records receipt of a specific item.
    /// </summary>
    Task ReceiveItemAsync(ReceiveTransferItemCommand command);

    /// <summary>
    /// Finalizes the transfer receipt (credits destination inventory).
    /// </summary>
    Task FinalizeReceiptAsync(FinalizeTransferReceiptCommand command);

    /// <summary>
    /// Cancels the transfer.
    /// </summary>
    Task CancelAsync(CancelTransferCommand command);

    /// <summary>
    /// Gets transfer lines.
    /// </summary>
    Task<IReadOnlyList<TransferLineState>> GetLinesAsync();

    /// <summary>
    /// Gets variance between shipped and received quantities.
    /// </summary>
    Task<IReadOnlyList<TransferLineVariance>> GetVariancesAsync();

    /// <summary>
    /// Checks if the transfer exists.
    /// </summary>
    Task<bool> ExistsAsync();
}

[GenerateSerializer]
public record TransferLineVariance(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] string IngredientName,
    [property: Id(2)] decimal ShippedQuantity,
    [property: Id(3)] decimal ReceivedQuantity,
    [property: Id(4)] decimal Variance,
    [property: Id(5)] decimal VariancePercentage,
    [property: Id(6)] decimal VarianceValue);
