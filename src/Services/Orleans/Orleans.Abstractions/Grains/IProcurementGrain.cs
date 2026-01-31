namespace DarkVelocity.Orleans.Abstractions.Grains;

// ============================================================================
// Supplier Grain
// ============================================================================

public record CreateSupplierCommand(
    string Code,
    string Name,
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    string Address,
    int PaymentTermsDays,
    int LeadTimeDays,
    string? Notes);

public record UpdateSupplierCommand(
    string? Name,
    string? ContactName,
    string? ContactEmail,
    string? ContactPhone,
    string? Address,
    int? PaymentTermsDays,
    int? LeadTimeDays,
    string? Notes,
    bool? IsActive);

public record SupplierIngredient(
    Guid IngredientId,
    string IngredientName,
    string Sku,
    string SupplierSku,
    decimal UnitPrice,
    string Unit,
    int MinOrderQuantity,
    int LeadTimeDays);

public record SupplierSnapshot(
    Guid SupplierId,
    string Code,
    string Name,
    string ContactName,
    string ContactEmail,
    string ContactPhone,
    string Address,
    int PaymentTermsDays,
    int LeadTimeDays,
    string? Notes,
    bool IsActive,
    IReadOnlyList<SupplierIngredient> Ingredients,
    decimal TotalPurchasesYtd,
    int OnTimeDeliveryPercent);

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

public record CreatePurchaseOrderCommand(
    Guid SupplierId,
    Guid LocationId,
    Guid? CreatedByUserId,
    DateTime ExpectedDeliveryDate,
    string? Notes);

public record AddPurchaseOrderLineCommand(
    Guid LineId,
    Guid IngredientId,
    string IngredientName,
    decimal QuantityOrdered,
    decimal UnitPrice,
    string? Notes);

public record UpdatePurchaseOrderLineCommand(
    Guid LineId,
    decimal? QuantityOrdered,
    decimal? UnitPrice,
    string? Notes);

public record SubmitPurchaseOrderCommand(
    Guid SubmittedByUserId);

public record ReceiveLineCommand(
    Guid LineId,
    decimal QuantityReceived);

public record CancelPurchaseOrderCommand(
    string Reason,
    Guid CancelledByUserId);

public record PurchaseOrderLineSnapshot(
    Guid LineId,
    Guid IngredientId,
    string IngredientName,
    decimal QuantityOrdered,
    decimal QuantityReceived,
    decimal UnitPrice,
    decimal LineTotal,
    string? Notes);

public record PurchaseOrderSnapshot(
    Guid PurchaseOrderId,
    string OrderNumber,
    Guid SupplierId,
    string SupplierName,
    Guid LocationId,
    Guid? CreatedByUserId,
    PurchaseOrderStatus Status,
    DateTime ExpectedDeliveryDate,
    DateTime? SubmittedAt,
    DateTime? ReceivedAt,
    DateTime? CancelledAt,
    string? CancellationReason,
    decimal OrderTotal,
    IReadOnlyList<PurchaseOrderLineSnapshot> Lines,
    string? Notes);

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

public record CreateDeliveryCommand(
    Guid SupplierId,
    Guid? PurchaseOrderId,
    Guid LocationId,
    Guid? ReceivedByUserId,
    string? SupplierInvoiceNumber,
    string? Notes);

public record AddDeliveryLineCommand(
    Guid LineId,
    Guid IngredientId,
    string IngredientName,
    Guid? PurchaseOrderLineId,
    decimal QuantityReceived,
    decimal UnitCost,
    string? BatchNumber,
    DateTime? ExpiryDate,
    string? Notes);

public record RecordDiscrepancyCommand(
    Guid DiscrepancyId,
    Guid LineId,
    DiscrepancyType Type,
    decimal ExpectedQuantity,
    decimal ActualQuantity,
    string? Notes);

public enum DiscrepancyType
{
    ShortDelivery,
    OverDelivery,
    DamagedGoods,
    WrongItem,
    QualityIssue,
    IncorrectPrice
}

public record AcceptDeliveryCommand(
    Guid AcceptedByUserId);

public record RejectDeliveryCommand(
    string Reason,
    Guid RejectedByUserId);

public record DeliveryLineSnapshot(
    Guid LineId,
    Guid IngredientId,
    string IngredientName,
    Guid? PurchaseOrderLineId,
    decimal QuantityReceived,
    decimal UnitCost,
    decimal LineTotal,
    string? BatchNumber,
    DateTime? ExpiryDate,
    string? Notes);

public record DeliveryDiscrepancySnapshot(
    Guid DiscrepancyId,
    Guid LineId,
    DiscrepancyType Type,
    decimal ExpectedQuantity,
    decimal ActualQuantity,
    string? Notes);

public record DeliverySnapshot(
    Guid DeliveryId,
    string DeliveryNumber,
    Guid SupplierId,
    string SupplierName,
    Guid? PurchaseOrderId,
    Guid LocationId,
    Guid? ReceivedByUserId,
    DeliveryStatus Status,
    DateTime ReceivedAt,
    DateTime? AcceptedAt,
    DateTime? RejectedAt,
    string? RejectionReason,
    decimal TotalValue,
    bool HasDiscrepancies,
    string? SupplierInvoiceNumber,
    IReadOnlyList<DeliveryLineSnapshot> Lines,
    IReadOnlyList<DeliveryDiscrepancySnapshot> Discrepancies,
    string? Notes);

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
