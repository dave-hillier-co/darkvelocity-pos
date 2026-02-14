using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

// ============================================================================
// Supplier State
// ============================================================================

[GenerateSerializer]
public sealed class SupplierState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid SupplierId { get; set; }
    [Id(2)] public string Code { get; set; } = string.Empty;
    [Id(3)] public string Name { get; set; } = string.Empty;
    [Id(4)] public string ContactName { get; set; } = string.Empty;
    [Id(5)] public string ContactEmail { get; set; } = string.Empty;
    [Id(6)] public string ContactPhone { get; set; } = string.Empty;
    [Id(7)] public string Address { get; set; } = string.Empty;
    [Id(8)] public int PaymentTermsDays { get; set; } = 30;
    [Id(9)] public int LeadTimeDays { get; set; } = 3;
    [Id(10)] public string? Notes { get; set; }
    [Id(11)] public bool IsActive { get; set; } = true;
    [Id(12)] public List<SupplierCatalogItemState> Catalog { get; set; } = [];
    [Id(13)] public decimal TotalPurchasesYtd { get; set; }
    [Id(14)] public int TotalDeliveries { get; set; }
    [Id(15)] public int OnTimeDeliveries { get; set; }
    [Id(16)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class SupplierCatalogItemState
{
    [Id(0)] public Guid SkuId { get; set; }
    [Id(1)] public string SkuCode { get; set; } = string.Empty;
    [Id(2)] public string ProductName { get; set; } = string.Empty;
    [Id(3)] public string SupplierProductCode { get; set; } = string.Empty;
    [Id(4)] public decimal UnitPrice { get; set; }
    [Id(5)] public string Unit { get; set; } = string.Empty;
    [Id(6)] public int MinOrderQuantity { get; set; }
    [Id(7)] public int LeadTimeDays { get; set; }
}

// ============================================================================
// Purchase Order State
// ============================================================================

[GenerateSerializer]
public sealed class PurchaseOrderState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid PurchaseOrderId { get; set; }
    [Id(2)] public string OrderNumber { get; set; } = string.Empty;
    [Id(3)] public Guid SupplierId { get; set; }
    [Id(4)] public string SupplierName { get; set; } = string.Empty;
    [Id(5)] public Guid LocationId { get; set; }
    [Id(6)] public Guid? CreatedByUserId { get; set; }
    [Id(7)] public PurchaseOrderStatus Status { get; set; }
    [Id(8)] public DateTime ExpectedDeliveryDate { get; set; }
    [Id(9)] public DateTime? SubmittedAt { get; set; }
    [Id(10)] public DateTime? ReceivedAt { get; set; }
    [Id(11)] public DateTime? CancelledAt { get; set; }
    [Id(12)] public string? CancellationReason { get; set; }
    [Id(13)] public decimal OrderTotal { get; set; }
    [Id(14)] public List<PurchaseOrderLineState> Lines { get; set; } = [];
    [Id(15)] public string? Notes { get; set; }
    [Id(16)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class PurchaseOrderLineState
{
    [Id(0)] public Guid LineId { get; set; }
    [Id(1)] public Guid SkuId { get; set; }
    [Id(2)] public string SkuCode { get; set; } = string.Empty;
    [Id(3)] public string ProductName { get; set; } = string.Empty;
    [Id(4)] public decimal QuantityOrdered { get; set; }
    [Id(5)] public decimal QuantityReceived { get; set; }
    [Id(6)] public decimal UnitPrice { get; set; }
    [Id(7)] public decimal LineTotal { get; set; }
    [Id(8)] public string? Notes { get; set; }
}

// ============================================================================
// Delivery State
// ============================================================================

[GenerateSerializer]
public sealed class DeliveryState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public Guid DeliveryId { get; set; }
    [Id(2)] public string DeliveryNumber { get; set; } = string.Empty;
    [Id(3)] public Guid SupplierId { get; set; }
    [Id(4)] public string SupplierName { get; set; } = string.Empty;
    [Id(5)] public Guid? PurchaseOrderId { get; set; }
    [Id(6)] public Guid LocationId { get; set; }
    [Id(7)] public Guid? ReceivedByUserId { get; set; }
    [Id(8)] public DeliveryStatus Status { get; set; }
    [Id(9)] public DateTime ReceivedAt { get; set; }
    [Id(10)] public DateTime? AcceptedAt { get; set; }
    [Id(11)] public DateTime? RejectedAt { get; set; }
    [Id(12)] public string? RejectionReason { get; set; }
    [Id(13)] public decimal TotalValue { get; set; }
    [Id(14)] public bool HasDiscrepancies { get; set; }
    [Id(15)] public string? SupplierInvoiceNumber { get; set; }
    [Id(16)] public List<DeliveryLineState> Lines { get; set; } = [];
    [Id(17)] public List<DeliveryDiscrepancyState> Discrepancies { get; set; } = [];
    [Id(18)] public string? Notes { get; set; }
    [Id(19)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class DeliveryLineState
{
    [Id(0)] public Guid LineId { get; set; }
    [Id(1)] public Guid SkuId { get; set; }
    [Id(2)] public string SkuCode { get; set; } = string.Empty;
    [Id(3)] public string ProductName { get; set; } = string.Empty;
    [Id(4)] public Guid? PurchaseOrderLineId { get; set; }
    [Id(5)] public decimal QuantityReceived { get; set; }
    [Id(6)] public decimal UnitCost { get; set; }
    [Id(7)] public decimal LineTotal { get; set; }
    [Id(8)] public string? BatchNumber { get; set; }
    [Id(9)] public DateTime? ExpiryDate { get; set; }
    [Id(10)] public string? Notes { get; set; }
}

[GenerateSerializer]
public sealed class DeliveryDiscrepancyState
{
    [Id(0)] public Guid DiscrepancyId { get; set; }
    [Id(1)] public Guid LineId { get; set; }
    [Id(2)] public DiscrepancyType Type { get; set; }
    [Id(3)] public decimal ExpectedQuantity { get; set; }
    [Id(4)] public decimal ActualQuantity { get; set; }
    [Id(5)] public string? Notes { get; set; }
}
