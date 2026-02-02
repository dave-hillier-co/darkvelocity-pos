namespace DarkVelocity.Host.Events.JournaledEvents;

/// <summary>
/// Base interface for all PurchaseDocument journaled events used in event sourcing.
/// </summary>
public interface IPurchaseDocumentJournaledEvent
{
    Guid DocumentId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentCreatedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SiteId { get; init; }
    [Id(3)] public string DocumentType { get; init; } = ""; // Invoice, Receipt, PurchaseOrder
    [Id(4)] public string DocumentNumber { get; init; } = "";
    [Id(5)] public Guid VendorId { get; init; }
    [Id(6)] public string VendorName { get; init; } = "";
    [Id(7)] public DateOnly DocumentDate { get; init; }
    [Id(8)] public string? Source { get; init; }
    [Id(9)] public Guid CreatedBy { get; init; }
    [Id(10)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineAddedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public string Description { get; init; } = "";
    [Id(3)] public decimal Quantity { get; init; }
    [Id(4)] public string Unit { get; init; } = "";
    [Id(5)] public decimal UnitPrice { get; init; }
    [Id(6)] public decimal LineTotal { get; init; }
    [Id(7)] public Guid? IngredientId { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineUpdatedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public decimal? Quantity { get; init; }
    [Id(3)] public decimal? UnitPrice { get; init; }
    [Id(4)] public decimal? LineTotal { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineRemovedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid LineId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentTotalsUpdatedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public decimal Subtotal { get; init; }
    [Id(2)] public decimal TaxAmount { get; init; }
    [Id(3)] public decimal ShippingAmount { get; init; }
    [Id(4)] public decimal TotalAmount { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentSubmittedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid SubmittedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentApprovedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid ApprovedBy { get; init; }
    [Id(2)] public string? Notes { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentRejectedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid RejectedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentReceivedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid ReceivedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentPartiallyReceivedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public List<LineReceiptInfo> LinesReceived { get; init; } = [];
    [Id(2)] public Guid ReceivedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record LineReceiptInfo
{
    [Id(0)] public Guid LineId { get; init; }
    [Id(1)] public decimal QuantityReceived { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentPaidJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public decimal AmountPaid { get; init; }
    [Id(2)] public string PaymentMethod { get; init; } = "";
    [Id(3)] public string? PaymentReference { get; init; }
    [Id(4)] public Guid PaidBy { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentCancelledJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid CancelledBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLinkedToDeliveryJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid DeliveryId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentProcessingRequestedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentExtractionAppliedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public string? VendorName { get; init; }
    [Id(2)] public DateOnly? DocumentDate { get; init; }
    [Id(3)] public decimal? Total { get; init; }
    [Id(4)] public int LineCount { get; init; }
    [Id(5)] public decimal Confidence { get; init; }
    [Id(6)] public string? ProcessorVersion { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentExtractionFailedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineMappedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public int LineIndex { get; init; }
    [Id(2)] public Guid IngredientId { get; init; }
    [Id(3)] public string? IngredientSku { get; init; }
    [Id(4)] public string? IngredientName { get; init; }
    [Id(5)] public string MappingSource { get; init; } = "";
    [Id(6)] public decimal Confidence { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineUnmappedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public int LineIndex { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentLineModifiedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public int LineIndex { get; init; }
    [Id(2)] public string? Description { get; init; }
    [Id(3)] public decimal? Quantity { get; init; }
    [Id(4)] public string? Unit { get; init; }
    [Id(5)] public decimal? UnitPrice { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PurchaseDocumentConfirmedJournaledEvent : IPurchaseDocumentJournaledEvent
{
    [Id(0)] public Guid DocumentId { get; init; }
    [Id(1)] public Guid ConfirmedBy { get; init; }
    [Id(2)] public Guid? VendorId { get; init; }
    [Id(3)] public string? VendorName { get; init; }
    [Id(4)] public DateOnly? DocumentDate { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}
