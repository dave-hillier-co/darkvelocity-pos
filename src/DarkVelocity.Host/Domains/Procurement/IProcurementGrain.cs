namespace DarkVelocity.Host.Grains;

// ============================================================================
// Supplier Grain
// ============================================================================

[GenerateSerializer]
public record CreateSupplierCommand(
    [property: Id(0)] string Code,
    [property: Id(1)] string Name,
    [property: Id(2)] string ContactName,
    [property: Id(3)] string ContactEmail,
    [property: Id(4)] string ContactPhone,
    [property: Id(5)] string Address,
    [property: Id(6)] int PaymentTermsDays,
    [property: Id(7)] int LeadTimeDays,
    [property: Id(8)] string? Notes);

[GenerateSerializer]
public record UpdateSupplierCommand(
    [property: Id(0)] string? Name,
    [property: Id(1)] string? ContactName,
    [property: Id(2)] string? ContactEmail,
    [property: Id(3)] string? ContactPhone,
    [property: Id(4)] string? Address,
    [property: Id(5)] int? PaymentTermsDays,
    [property: Id(6)] int? LeadTimeDays,
    [property: Id(7)] string? Notes,
    [property: Id(8)] bool? IsActive);

[GenerateSerializer]
public record SupplierIngredient(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] string IngredientName,
    [property: Id(2)] string Sku,
    [property: Id(3)] string SupplierSku,
    [property: Id(4)] decimal UnitPrice,
    [property: Id(5)] string Unit,
    [property: Id(6)] int MinOrderQuantity,
    [property: Id(7)] int LeadTimeDays);

[GenerateSerializer]
public record SupplierSnapshot(
    [property: Id(0)] Guid SupplierId,
    [property: Id(1)] string Code,
    [property: Id(2)] string Name,
    [property: Id(3)] string ContactName,
    [property: Id(4)] string ContactEmail,
    [property: Id(5)] string ContactPhone,
    [property: Id(6)] string Address,
    [property: Id(7)] int PaymentTermsDays,
    [property: Id(8)] int LeadTimeDays,
    [property: Id(9)] string? Notes,
    [property: Id(10)] bool IsActive,
    [property: Id(11)] IReadOnlyList<SupplierIngredient> Ingredients,
    [property: Id(12)] decimal TotalPurchasesYtd,
    [property: Id(13)] int OnTimeDeliveryPercent);

/// <summary>
/// Grain for supplier management.
/// Key: "{orgId}:supplier:{supplierId}"
/// </summary>
public interface ISupplierGrain : IGrainWithStringKey
{
    Task<SupplierSnapshot> CreateAsync(CreateSupplierCommand command);
    Task<SupplierSnapshot> UpdateAsync(UpdateSupplierCommand command);
    Task AddIngredientAsync(SupplierIngredient ingredient);
    Task RemoveIngredientAsync(Guid ingredientId);
    Task UpdateIngredientPriceAsync(Guid ingredientId, decimal newPrice);
    Task<SupplierSnapshot> GetSnapshotAsync();
    Task<decimal> GetIngredientPriceAsync(Guid ingredientId);
    Task RecordPurchaseAsync(decimal amount, bool onTime);
    Task<int> GetVersionAsync();
}

// ============================================================================
// Purchase Order Grain
// ============================================================================

public enum PurchaseOrderStatus
{
    Draft,
    Submitted,
    PartiallyReceived,
    Received,
    Cancelled
}

[GenerateSerializer]
public record CreatePurchaseOrderCommand(
    [property: Id(0)] Guid SupplierId,
    [property: Id(1)] Guid LocationId,
    [property: Id(2)] Guid? CreatedByUserId,
    [property: Id(3)] DateTime ExpectedDeliveryDate,
    [property: Id(4)] string? Notes);

[GenerateSerializer]
public record AddPurchaseOrderLineCommand(
    [property: Id(0)] Guid LineId,
    [property: Id(1)] Guid IngredientId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] decimal QuantityOrdered,
    [property: Id(4)] decimal UnitPrice,
    [property: Id(5)] string? Notes);

[GenerateSerializer]
public record UpdatePurchaseOrderLineCommand(
    [property: Id(0)] Guid LineId,
    [property: Id(1)] decimal? QuantityOrdered,
    [property: Id(2)] decimal? UnitPrice,
    [property: Id(3)] string? Notes);

[GenerateSerializer]
public record SubmitPurchaseOrderCommand(
    [property: Id(0)] Guid SubmittedByUserId);

[GenerateSerializer]
public record ReceiveLineCommand(
    [property: Id(0)] Guid LineId,
    [property: Id(1)] decimal QuantityReceived);

[GenerateSerializer]
public record CancelPurchaseOrderCommand(
    [property: Id(0)] string Reason,
    [property: Id(1)] Guid CancelledByUserId);

[GenerateSerializer]
public record PurchaseOrderLineSnapshot(
    [property: Id(0)] Guid LineId,
    [property: Id(1)] Guid IngredientId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] decimal QuantityOrdered,
    [property: Id(4)] decimal QuantityReceived,
    [property: Id(5)] decimal UnitPrice,
    [property: Id(6)] decimal LineTotal,
    [property: Id(7)] string? Notes);

[GenerateSerializer]
public record PurchaseOrderSnapshot(
    [property: Id(0)] Guid PurchaseOrderId,
    [property: Id(1)] string OrderNumber,
    [property: Id(2)] Guid SupplierId,
    [property: Id(3)] string SupplierName,
    [property: Id(4)] Guid LocationId,
    [property: Id(5)] Guid? CreatedByUserId,
    [property: Id(6)] PurchaseOrderStatus Status,
    [property: Id(7)] DateTime ExpectedDeliveryDate,
    [property: Id(8)] DateTime? SubmittedAt,
    [property: Id(9)] DateTime? ReceivedAt,
    [property: Id(10)] DateTime? CancelledAt,
    [property: Id(11)] string? CancellationReason,
    [property: Id(12)] decimal OrderTotal,
    [property: Id(13)] IReadOnlyList<PurchaseOrderLineSnapshot> Lines,
    [property: Id(14)] string? Notes);

/// <summary>
/// Grain for purchase order management.
/// Key: "{orgId}:purchaseorder:{purchaseOrderId}"
/// </summary>
public interface IPurchaseOrderGrain : IGrainWithStringKey
{
    Task<PurchaseOrderSnapshot> CreateAsync(CreatePurchaseOrderCommand command);
    Task AddLineAsync(AddPurchaseOrderLineCommand command);
    Task UpdateLineAsync(UpdatePurchaseOrderLineCommand command);
    Task RemoveLineAsync(Guid lineId);
    Task<PurchaseOrderSnapshot> SubmitAsync(SubmitPurchaseOrderCommand command);
    Task ReceiveLineAsync(ReceiveLineCommand command);
    Task<PurchaseOrderSnapshot> CancelAsync(CancelPurchaseOrderCommand command);
    Task<PurchaseOrderSnapshot> GetSnapshotAsync();
    Task<decimal> GetTotalAsync();
    Task<bool> IsFullyReceivedAsync();
}

// ============================================================================
// Delivery Grain
// ============================================================================

public enum DeliveryStatus
{
    Pending,
    Accepted,
    Rejected
}

[GenerateSerializer]
public record CreateDeliveryCommand(
    [property: Id(0)] Guid SupplierId,
    [property: Id(1)] Guid? PurchaseOrderId,
    [property: Id(2)] Guid LocationId,
    [property: Id(3)] Guid? ReceivedByUserId,
    [property: Id(4)] string? SupplierInvoiceNumber,
    [property: Id(5)] string? Notes);

[GenerateSerializer]
public record AddDeliveryLineCommand(
    [property: Id(0)] Guid LineId,
    [property: Id(1)] Guid IngredientId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] Guid? PurchaseOrderLineId,
    [property: Id(4)] decimal QuantityReceived,
    [property: Id(5)] decimal UnitCost,
    [property: Id(6)] string? BatchNumber,
    [property: Id(7)] DateTime? ExpiryDate,
    [property: Id(8)] string? Notes);

[GenerateSerializer]
public record RecordDiscrepancyCommand(
    [property: Id(0)] Guid DiscrepancyId,
    [property: Id(1)] Guid LineId,
    [property: Id(2)] DiscrepancyType Type,
    [property: Id(3)] decimal ExpectedQuantity,
    [property: Id(4)] decimal ActualQuantity,
    [property: Id(5)] string? Notes);

public enum DiscrepancyType
{
    ShortDelivery,
    OverDelivery,
    DamagedGoods,
    WrongItem,
    QualityIssue,
    IncorrectPrice
}

[GenerateSerializer]
public record AcceptDeliveryCommand(
    [property: Id(0)] Guid AcceptedByUserId);

[GenerateSerializer]
public record RejectDeliveryCommand(
    [property: Id(0)] string Reason,
    [property: Id(1)] Guid RejectedByUserId);

[GenerateSerializer]
public record DeliveryLineSnapshot(
    [property: Id(0)] Guid LineId,
    [property: Id(1)] Guid IngredientId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] Guid? PurchaseOrderLineId,
    [property: Id(4)] decimal QuantityReceived,
    [property: Id(5)] decimal UnitCost,
    [property: Id(6)] decimal LineTotal,
    [property: Id(7)] string? BatchNumber,
    [property: Id(8)] DateTime? ExpiryDate,
    [property: Id(9)] string? Notes);

[GenerateSerializer]
public record DeliveryDiscrepancySnapshot(
    [property: Id(0)] Guid DiscrepancyId,
    [property: Id(1)] Guid LineId,
    [property: Id(2)] DiscrepancyType Type,
    [property: Id(3)] decimal ExpectedQuantity,
    [property: Id(4)] decimal ActualQuantity,
    [property: Id(5)] string? Notes);

[GenerateSerializer]
public record DeliverySnapshot(
    [property: Id(0)] Guid DeliveryId,
    [property: Id(1)] string DeliveryNumber,
    [property: Id(2)] Guid SupplierId,
    [property: Id(3)] string SupplierName,
    [property: Id(4)] Guid? PurchaseOrderId,
    [property: Id(5)] Guid LocationId,
    [property: Id(6)] Guid? ReceivedByUserId,
    [property: Id(7)] DeliveryStatus Status,
    [property: Id(8)] DateTime ReceivedAt,
    [property: Id(9)] DateTime? AcceptedAt,
    [property: Id(10)] DateTime? RejectedAt,
    [property: Id(11)] string? RejectionReason,
    [property: Id(12)] decimal TotalValue,
    [property: Id(13)] bool HasDiscrepancies,
    [property: Id(14)] string? SupplierInvoiceNumber,
    [property: Id(15)] IReadOnlyList<DeliveryLineSnapshot> Lines,
    [property: Id(16)] IReadOnlyList<DeliveryDiscrepancySnapshot> Discrepancies,
    [property: Id(17)] string? Notes);

/// <summary>
/// Grain for delivery management.
/// Key: "{orgId}:delivery:{deliveryId}"
/// </summary>
public interface IDeliveryGrain : IGrainWithStringKey
{
    Task<DeliverySnapshot> CreateAsync(CreateDeliveryCommand command);
    Task AddLineAsync(AddDeliveryLineCommand command);
    Task RecordDiscrepancyAsync(RecordDiscrepancyCommand command);
    Task<DeliverySnapshot> AcceptAsync(AcceptDeliveryCommand command);
    Task<DeliverySnapshot> RejectAsync(RejectDeliveryCommand command);
    Task<DeliverySnapshot> GetSnapshotAsync();
    Task<bool> HasDiscrepanciesAsync();
}
