namespace DarkVelocity.Host.Contracts;

public record InitializeInventoryRequest(
    Guid IngredientId,
    string IngredientName,
    string Sku,
    string Unit,
    string Category,
    decimal ReorderPoint = 0,
    decimal ParLevel = 0);

public record ReceiveBatchRequest(
    string BatchNumber,
    decimal Quantity,
    decimal UnitCost,
    DateTime? ExpiryDate = null,
    Guid? SupplierId = null,
    Guid? DeliveryId = null,
    string? Location = null,
    string? Notes = null,
    Guid? ReceivedBy = null);

public record ConsumeStockRequest(decimal Quantity, string Reason, Guid? OrderId = null, Guid? PerformedBy = null);
public record AdjustInventoryRequest(decimal NewQuantity, string Reason, Guid AdjustedBy, Guid? ApprovedBy = null);
