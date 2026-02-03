namespace DarkVelocity.Host.Contracts;

// ============================================================================
// Menu Batch Operations
// ============================================================================

public record BatchUpsertMenuItemsRequest(
    string IdempotencyKey,
    List<Grains.BatchUpsertMenuItemRequest> Items);

public record BatchRetrieveRequest(List<Guid> Ids);

public record BatchDeleteRequest(List<Guid> Ids);

// ============================================================================
// Inventory Batch Operations
// ============================================================================

public record BatchInventoryChangeRequest(
    string IdempotencyKey,
    List<Grains.BatchInventoryChange> Changes);

public record BatchInventoryCountsRequest(
    List<Grains.BatchInventoryCountRequest> Items);

public record PhysicalCountItem(Guid InventoryItemId, decimal CountedQuantity, Guid CountedBy);

public record BatchPhysicalCountRequest(
    string IdempotencyKey,
    List<PhysicalCountItem> Counts);

public record BatchTransferRequest(
    string IdempotencyKey,
    List<Grains.BatchInventoryTransfer> Transfers);

// ============================================================================
// Order Batch Operations
// ============================================================================

public record CloneOrderApiRequest(
    Guid SourceOrderId,
    Guid CreatedBy,
    State.OrderType? NewType = null,
    Guid? NewTableId = null,
    string? NewTableNumber = null,
    bool IncludeDiscounts = false,
    bool IncludeServiceCharges = false);

public record ServiceChargeInput(string Name, decimal Rate, bool IsTaxable);

public record CalculateOrderRequest(
    List<Grains.AddLineCommand> Lines,
    List<Grains.ApplyDiscountCommand>? Discounts = null,
    List<ServiceChargeInput>? ServiceCharges = null,
    decimal TaxRate = 0);

// ============================================================================
// Customer Batch Operations
// ============================================================================

public record BatchCreateCustomersRequest(
    string IdempotencyKey,
    List<Grains.CreateCustomerCommand> Customers);

public record CreateCustomerGroupRequest(string Name, string? Description = null);

public record AddCustomersToGroupRequest(List<Guid> CustomerIds);
