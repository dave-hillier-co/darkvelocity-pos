using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

[GenerateSerializer]
public sealed class InventoryTransferState
{
    [Id(0)] public Guid TransferId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid SourceSiteId { get; set; }
    [Id(3)] public Guid DestinationSiteId { get; set; }
    [Id(4)] public string TransferNumber { get; set; } = string.Empty;
    [Id(5)] public TransferStatus Status { get; set; } = TransferStatus.Requested;

    [Id(6)] public List<InventoryTransferLineState> Lines { get; set; } = [];

    [Id(7)] public DateTime RequestedAt { get; set; }
    [Id(8)] public Guid RequestedBy { get; set; }
    [Id(9)] public DateTime? RequestedDeliveryDate { get; set; }

    [Id(10)] public DateTime? ApprovedAt { get; set; }
    [Id(11)] public Guid? ApprovedBy { get; set; }
    [Id(12)] public string? ApprovalNotes { get; set; }

    [Id(13)] public DateTime? RejectedAt { get; set; }
    [Id(14)] public Guid? RejectedBy { get; set; }
    [Id(15)] public string? RejectionReason { get; set; }

    [Id(16)] public DateTime? ShippedAt { get; set; }
    [Id(17)] public Guid? ShippedBy { get; set; }
    [Id(18)] public DateTime? EstimatedArrival { get; set; }
    [Id(19)] public string? TrackingNumber { get; set; }
    [Id(20)] public string? Carrier { get; set; }
    [Id(21)] public string? ShippingNotes { get; set; }

    [Id(22)] public DateTime? ReceivedAt { get; set; }
    [Id(23)] public Guid? ReceivedBy { get; set; }
    [Id(24)] public string? ReceiptNotes { get; set; }

    [Id(25)] public DateTime? CancelledAt { get; set; }
    [Id(26)] public Guid? CancelledBy { get; set; }
    [Id(27)] public string? CancellationReason { get; set; }
    [Id(28)] public bool StockReturnedToSource { get; set; }

    [Id(29)] public string? Notes { get; set; }

    /// <summary>
    /// Total value of items shipped.
    /// </summary>
    [Id(30)] public decimal TotalShippedValue { get; set; }

    /// <summary>
    /// Total value of items received.
    /// </summary>
    [Id(31)] public decimal TotalReceivedValue { get; set; }

    /// <summary>
    /// Variance between shipped and received values.
    /// </summary>
    [Id(32)] public decimal TotalVarianceValue { get; set; }
}

[GenerateSerializer]
public sealed class InventoryTransferLineState
{
    [Id(0)] public Guid LineId { get; set; }
    [Id(1)] public Guid IngredientId { get; set; }
    [Id(2)] public string IngredientName { get; set; } = string.Empty;
    [Id(3)] public string Unit { get; set; } = string.Empty;

    [Id(4)] public decimal RequestedQuantity { get; set; }
    [Id(5)] public decimal ShippedQuantity { get; set; }
    [Id(6)] public decimal ReceivedQuantity { get; set; }

    [Id(7)] public decimal UnitCost { get; set; }
    [Id(8)] public decimal ShippedValue { get; set; }
    [Id(9)] public decimal ReceivedValue { get; set; }

    [Id(10)] public string? Condition { get; set; }
    [Id(11)] public string? Notes { get; set; }
}
