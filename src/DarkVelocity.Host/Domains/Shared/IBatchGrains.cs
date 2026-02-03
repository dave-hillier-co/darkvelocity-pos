namespace DarkVelocity.Host.Grains;

// ============================================================================
// Menu Batch Grain - Catalog batch operations (like Square's batch-upsert)
// ============================================================================

[GenerateSerializer]
public record BatchUpsertMenuItemRequest(
    [property: Id(0)] Guid? ItemId,
    [property: Id(1)] CreateMenuItemCommand? Create,
    [property: Id(2)] UpdateMenuItemCommand? Update);

[GenerateSerializer]
public record BatchUpsertMenuItemResult(
    [property: Id(0)] Guid ItemId,
    [property: Id(1)] bool Created,
    [property: Id(2)] MenuItemSnapshot? Snapshot,
    [property: Id(3)] string? Error);

[GenerateSerializer]
public record BatchRetrieveMenuItemsResult(
    [property: Id(0)] IReadOnlyList<MenuItemSnapshot> Items,
    [property: Id(1)] IReadOnlyList<BatchRetrieveError> Errors);

[GenerateSerializer]
public record BatchDeleteMenuItemsResult(
    [property: Id(0)] IReadOnlyList<Guid> DeletedIds,
    [property: Id(1)] IReadOnlyList<BatchDeleteError> Errors);

[GenerateSerializer]
public record BatchRetrieveError(
    [property: Id(0)] Guid ItemId,
    [property: Id(1)] string ErrorCode,
    [property: Id(2)] string Message);

[GenerateSerializer]
public record BatchDeleteError(
    [property: Id(0)] Guid ItemId,
    [property: Id(1)] string ErrorCode,
    [property: Id(2)] string Message);

[GenerateSerializer]
public record MenuSearchFilter(
    [property: Id(0)] string? TextQuery = null,
    [property: Id(1)] Guid? CategoryId = null,
    [property: Id(2)] bool? IsActive = null,
    [property: Id(3)] decimal? MinPrice = null,
    [property: Id(4)] decimal? MaxPrice = null,
    [property: Id(5)] IReadOnlyList<string>? Skus = null,
    [property: Id(6)] int Limit = 100,
    [property: Id(7)] string? Cursor = null);

[GenerateSerializer]
public record MenuSearchResult(
    [property: Id(0)] IReadOnlyList<MenuItemSnapshot> Items,
    [property: Id(1)] string? NextCursor,
    [property: Id(2)] int TotalCount);

/// <summary>
/// Grain for batch menu/catalog operations.
/// Key: "{orgId}:menubatch"
/// Provides Square-like batch operations for catalog items.
/// </summary>
public interface IMenuBatchGrain : IGrainWithStringKey
{
    /// <summary>
    /// Batch upsert menu items. Creates new items or updates existing ones.
    /// Similar to Square's POST /v2/catalog/batch-upsert
    /// </summary>
    Task<IReadOnlyList<BatchUpsertMenuItemResult>> BatchUpsertAsync(
        string idempotencyKey,
        IReadOnlyList<BatchUpsertMenuItemRequest> requests);

    /// <summary>
    /// Batch retrieve menu items by their IDs.
    /// Similar to Square's POST /v2/catalog/batch-retrieve
    /// </summary>
    Task<BatchRetrieveMenuItemsResult> BatchRetrieveAsync(IReadOnlyList<Guid> itemIds);

    /// <summary>
    /// Batch delete (deactivate) menu items.
    /// Similar to Square's POST /v2/catalog/batch-delete
    /// </summary>
    Task<BatchDeleteMenuItemsResult> BatchDeleteAsync(IReadOnlyList<Guid> itemIds);

    /// <summary>
    /// Search menu items with filters.
    /// Similar to Square's POST /v2/catalog/search
    /// </summary>
    Task<MenuSearchResult> SearchAsync(MenuSearchFilter filter);

    /// <summary>
    /// List all menu items with pagination.
    /// Similar to Square's GET /v2/catalog/list
    /// </summary>
    Task<MenuSearchResult> ListAsync(int limit = 100, string? cursor = null, bool includeInactive = false);

    /// <summary>
    /// Batch upsert categories.
    /// </summary>
    Task<IReadOnlyList<MenuCategorySnapshot>> BatchUpsertCategoriesAsync(
        string idempotencyKey,
        IReadOnlyList<CreateMenuCategoryCommand> categories);
}

// ============================================================================
// Inventory Batch Grain - Inventory batch operations
// ============================================================================

[GenerateSerializer]
public record BatchInventoryChange(
    [property: Id(0)] Guid InventoryItemId,
    [property: Id(1)] BatchInventoryChangeType ChangeType,
    [property: Id(2)] decimal Quantity,
    [property: Id(3)] decimal? UnitCost = null,
    [property: Id(4)] string? BatchNumber = null,
    [property: Id(5)] DateTime? ExpiryDate = null,
    [property: Id(6)] string? Reason = null,
    [property: Id(7)] Guid? PerformedBy = null);

public enum BatchInventoryChangeType
{
    Adjustment,
    Receive,
    Consume,
    Waste,
    Transfer
}

[GenerateSerializer]
public record BatchInventoryChangeResult(
    [property: Id(0)] Guid InventoryItemId,
    [property: Id(1)] bool Success,
    [property: Id(2)] decimal? NewQuantity,
    [property: Id(3)] string? Error);

[GenerateSerializer]
public record BatchInventoryCountRequest(
    [property: Id(0)] Guid InventoryItemId,
    [property: Id(1)] Guid? SiteId = null);

[GenerateSerializer]
public record BatchInventoryCount(
    [property: Id(0)] Guid InventoryItemId,
    [property: Id(1)] string IngredientName,
    [property: Id(2)] decimal QuantityOnHand,
    [property: Id(3)] decimal WeightedAverageCost,
    [property: Id(4)] State.StockLevel StockLevel,
    [property: Id(5)] DateTime? EarliestExpiry);

[GenerateSerializer]
public record BatchInventoryTransfer(
    [property: Id(0)] Guid InventoryItemId,
    [property: Id(1)] Guid SourceSiteId,
    [property: Id(2)] Guid DestinationSiteId,
    [property: Id(3)] decimal Quantity,
    [property: Id(4)] Guid TransferredBy);

[GenerateSerializer]
public record BatchInventoryTransferResult(
    [property: Id(0)] Guid TransferId,
    [property: Id(1)] IReadOnlyList<BatchInventoryTransferItemResult> Items);

[GenerateSerializer]
public record BatchInventoryTransferItemResult(
    [property: Id(0)] Guid InventoryItemId,
    [property: Id(1)] bool Success,
    [property: Id(2)] decimal QuantityTransferred,
    [property: Id(3)] string? Error);

[GenerateSerializer]
public record InventorySearchFilter(
    [property: Id(0)] Guid? SiteId = null,
    [property: Id(1)] string? Category = null,
    [property: Id(2)] State.StockLevel? StockLevel = null,
    [property: Id(3)] bool? HasExpiringSoon = null,
    [property: Id(4)] int ExpiringSoonDays = 7,
    [property: Id(5)] string? TextQuery = null,
    [property: Id(6)] int Limit = 100,
    [property: Id(7)] string? Cursor = null);

[GenerateSerializer]
public record InventorySearchResult(
    [property: Id(0)] IReadOnlyList<BatchInventoryCount> Items,
    [property: Id(1)] string? NextCursor,
    [property: Id(2)] int TotalCount);

/// <summary>
/// Grain for batch inventory operations.
/// Key: "{orgId}:inventorybatch"
/// Provides Square-like batch operations for inventory.
/// </summary>
public interface IInventoryBatchGrain : IGrainWithStringKey
{
    /// <summary>
    /// Batch change inventory levels (adjustments, receives, consumes, waste).
    /// Similar to Square's POST /v2/inventory/batch-change
    /// </summary>
    Task<IReadOnlyList<BatchInventoryChangeResult>> BatchChangeAsync(
        string idempotencyKey,
        IReadOnlyList<BatchInventoryChange> changes);

    /// <summary>
    /// Batch retrieve inventory counts.
    /// Similar to Square's POST /v2/inventory/batch-retrieve-counts
    /// </summary>
    Task<IReadOnlyList<BatchInventoryCount>> BatchRetrieveCountsAsync(
        IReadOnlyList<BatchInventoryCountRequest> requests);

    /// <summary>
    /// Record physical counts for multiple items.
    /// Similar to Square's POST /v2/inventory/physical-count
    /// </summary>
    Task<IReadOnlyList<BatchInventoryChangeResult>> BatchPhysicalCountAsync(
        string idempotencyKey,
        IReadOnlyList<(Guid InventoryItemId, decimal CountedQuantity, Guid CountedBy)> counts);

    /// <summary>
    /// Transfer inventory between sites for multiple items.
    /// Similar to Square's POST /v2/inventory/transfer
    /// </summary>
    Task<BatchInventoryTransferResult> BatchTransferAsync(
        string idempotencyKey,
        IReadOnlyList<BatchInventoryTransfer> transfers);

    /// <summary>
    /// Search inventory items with filters.
    /// </summary>
    Task<InventorySearchResult> SearchAsync(InventorySearchFilter filter);

    /// <summary>
    /// Get all items below reorder point for a site.
    /// </summary>
    Task<IReadOnlyList<BatchInventoryCount>> GetBelowReorderPointAsync(Guid siteId);

    /// <summary>
    /// Get all items expiring within specified days.
    /// </summary>
    Task<IReadOnlyList<BatchInventoryCount>> GetExpiringSoonAsync(Guid siteId, int days = 7);
}

// ============================================================================
// Order Batch Grain - Order batch operations
// ============================================================================

[GenerateSerializer]
public record OrderSearchFilter(
    [property: Id(0)] Guid? SiteId = null,
    [property: Id(1)] State.OrderStatus? Status = null,
    [property: Id(2)] State.OrderType? Type = null,
    [property: Id(3)] Guid? CustomerId = null,
    [property: Id(4)] Guid? ServerId = null,
    [property: Id(5)] DateTime? CreatedAfter = null,
    [property: Id(6)] DateTime? CreatedBefore = null,
    [property: Id(7)] DateTime? ClosedAfter = null,
    [property: Id(8)] DateTime? ClosedBefore = null,
    [property: Id(9)] decimal? MinTotal = null,
    [property: Id(10)] decimal? MaxTotal = null,
    [property: Id(11)] string? OrderNumber = null,
    [property: Id(12)] int Limit = 50,
    [property: Id(13)] string? Cursor = null,
    [property: Id(14)] OrderSortField SortField = OrderSortField.CreatedAt,
    [property: Id(15)] bool SortDescending = true);

public enum OrderSortField
{
    CreatedAt,
    ClosedAt,
    GrandTotal,
    OrderNumber
}

[GenerateSerializer]
public record OrderSummary(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] string OrderNumber,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] State.OrderStatus Status,
    [property: Id(4)] State.OrderType Type,
    [property: Id(5)] Guid? CustomerId,
    [property: Id(6)] string? CustomerName,
    [property: Id(7)] Guid? ServerId,
    [property: Id(8)] string? ServerName,
    [property: Id(9)] string? TableNumber,
    [property: Id(10)] int ItemCount,
    [property: Id(11)] decimal Subtotal,
    [property: Id(12)] decimal GrandTotal,
    [property: Id(13)] decimal PaidAmount,
    [property: Id(14)] decimal BalanceDue,
    [property: Id(15)] DateTime CreatedAt,
    [property: Id(16)] DateTime? ClosedAt);

[GenerateSerializer]
public record OrderSearchResult(
    [property: Id(0)] IReadOnlyList<OrderSummary> Orders,
    [property: Id(1)] string? NextCursor,
    [property: Id(2)] int TotalCount);

[GenerateSerializer]
public record BatchRetrieveOrdersResult(
    [property: Id(0)] IReadOnlyList<State.OrderState> Orders,
    [property: Id(1)] IReadOnlyList<BatchRetrieveError> Errors);

[GenerateSerializer]
public record CloneOrderRequest(
    [property: Id(0)] Guid SourceOrderId,
    [property: Id(1)] Guid CreatedBy,
    [property: Id(2)] State.OrderType? NewType = null,
    [property: Id(3)] Guid? NewTableId = null,
    [property: Id(4)] string? NewTableNumber = null,
    [property: Id(5)] bool IncludeDiscounts = false,
    [property: Id(6)] bool IncludeServiceCharges = false);

// CloneOrderResult is defined in IOrderGrain.cs

/// <summary>
/// Grain for batch order operations.
/// Key: "{orgId}:orderbatch"
/// Provides Square-like batch operations for orders.
/// </summary>
public interface IOrderBatchGrain : IGrainWithStringKey
{
    /// <summary>
    /// Search orders with filters.
    /// Similar to Square's POST /v2/orders/search
    /// </summary>
    Task<OrderSearchResult> SearchAsync(OrderSearchFilter filter);

    /// <summary>
    /// Batch retrieve orders by their IDs.
    /// Similar to Square's POST /v2/orders/batch-retrieve
    /// </summary>
    Task<BatchRetrieveOrdersResult> BatchRetrieveAsync(IReadOnlyList<Guid> orderIds);

    /// <summary>
    /// Clone an order.
    /// Similar to Square's POST /v2/orders/clone
    /// </summary>
    Task<CloneOrderResult> CloneAsync(CloneOrderRequest request);

    /// <summary>
    /// Get open orders for a site.
    /// </summary>
    Task<IReadOnlyList<OrderSummary>> GetOpenOrdersAsync(Guid siteId);

    /// <summary>
    /// Get orders for a specific table.
    /// </summary>
    Task<IReadOnlyList<OrderSummary>> GetOrdersByTableAsync(Guid siteId, Guid tableId);

    /// <summary>
    /// Get orders for a specific customer.
    /// </summary>
    Task<IReadOnlyList<OrderSummary>> GetOrdersByCustomerAsync(Guid customerId, int limit = 50);

    /// <summary>
    /// Get orders for a specific server/employee.
    /// </summary>
    Task<IReadOnlyList<OrderSummary>> GetOrdersByServerAsync(Guid siteId, Guid serverId, DateTime date);

    /// <summary>
    /// Calculate order totals without persisting (preview).
    /// Similar to Square's POST /v2/orders/calculate
    /// </summary>
    Task<OrderTotals> CalculateAsync(
        IReadOnlyList<AddLineCommand> lines,
        IReadOnlyList<ApplyDiscountCommand>? discounts = null,
        IReadOnlyList<(string Name, decimal Rate, bool IsTaxable)>? serviceCharges = null,
        decimal taxRate = 0);
}

// ============================================================================
// Customer Batch Grain - Customer batch operations
// ============================================================================

[GenerateSerializer]
public record CustomerSearchFilter(
    [property: Id(0)] string? TextQuery = null,
    [property: Id(1)] string? Email = null,
    [property: Id(2)] string? Phone = null,
    [property: Id(3)] bool? IsLoyaltyMember = null,
    [property: Id(4)] Guid? LoyaltyTierId = null,
    [property: Id(5)] IReadOnlyList<string>? Tags = null,
    [property: Id(6)] DateTime? CreatedAfter = null,
    [property: Id(7)] DateTime? CreatedBefore = null,
    [property: Id(8)] DateTime? LastVisitAfter = null,
    [property: Id(9)] DateTime? LastVisitBefore = null,
    [property: Id(10)] decimal? MinLifetimeSpend = null,
    [property: Id(11)] int? MinVisitCount = null,
    [property: Id(12)] int Limit = 50,
    [property: Id(13)] string? Cursor = null,
    [property: Id(14)] CustomerSortField SortField = CustomerSortField.CreatedAt,
    [property: Id(15)] bool SortDescending = true);

public enum CustomerSortField
{
    CreatedAt,
    LastVisit,
    LifetimeSpend,
    Name,
    PointsBalance
}

[GenerateSerializer]
public record CustomerSummary(
    [property: Id(0)] Guid CustomerId,
    [property: Id(1)] string DisplayName,
    [property: Id(2)] string? Email,
    [property: Id(3)] string? Phone,
    [property: Id(4)] bool IsLoyaltyMember,
    [property: Id(5)] string? LoyaltyTierName,
    [property: Id(6)] int PointsBalance,
    [property: Id(7)] decimal LifetimeSpend,
    [property: Id(8)] int VisitCount,
    [property: Id(9)] DateTime? LastVisit,
    [property: Id(10)] IReadOnlyList<string> Tags,
    [property: Id(11)] DateTime CreatedAt);

[GenerateSerializer]
public record CustomerSearchResult(
    [property: Id(0)] IReadOnlyList<CustomerSummary> Customers,
    [property: Id(1)] string? NextCursor,
    [property: Id(2)] int TotalCount);

[GenerateSerializer]
public record BatchRetrieveCustomersResult(
    [property: Id(0)] IReadOnlyList<State.CustomerState> Customers,
    [property: Id(1)] IReadOnlyList<BatchRetrieveError> Errors);

[GenerateSerializer]
public record CustomerGroupDefinition(
    [property: Id(0)] Guid GroupId,
    [property: Id(1)] string Name,
    [property: Id(2)] string? Description,
    [property: Id(3)] CustomerSearchFilter? AutoMembershipFilter);

[GenerateSerializer]
public record BatchAddToGroupResult(
    [property: Id(0)] int CustomersAdded,
    [property: Id(1)] IReadOnlyList<BatchRetrieveError> Errors);

/// <summary>
/// Grain for batch customer operations.
/// Key: "{orgId}:customerbatch"
/// Provides Square-like batch operations for customers.
/// </summary>
public interface ICustomerBatchGrain : IGrainWithStringKey
{
    /// <summary>
    /// Search customers with filters.
    /// Similar to Square's POST /v2/customers/search
    /// </summary>
    Task<CustomerSearchResult> SearchAsync(CustomerSearchFilter filter);

    /// <summary>
    /// Batch retrieve customers by their IDs.
    /// </summary>
    Task<BatchRetrieveCustomersResult> BatchRetrieveAsync(IReadOnlyList<Guid> customerIds);

    /// <summary>
    /// Batch create customers.
    /// </summary>
    Task<IReadOnlyList<CustomerCreatedResult>> BatchCreateAsync(
        string idempotencyKey,
        IReadOnlyList<CreateCustomerCommand> customers);

    /// <summary>
    /// Create a customer group.
    /// Similar to Square's POST /v2/customers/groups
    /// </summary>
    Task<CustomerGroupDefinition> CreateGroupAsync(string name, string? description = null);

    /// <summary>
    /// Get all customer groups.
    /// </summary>
    Task<IReadOnlyList<CustomerGroupDefinition>> GetGroupsAsync();

    /// <summary>
    /// Add customers to a group.
    /// </summary>
    Task<BatchAddToGroupResult> AddToGroupAsync(Guid groupId, IReadOnlyList<Guid> customerIds);

    /// <summary>
    /// Remove customers from a group.
    /// </summary>
    Task<BatchAddToGroupResult> RemoveFromGroupAsync(Guid groupId, IReadOnlyList<Guid> customerIds);

    /// <summary>
    /// Get customers in a group.
    /// </summary>
    Task<CustomerSearchResult> GetGroupMembersAsync(Guid groupId, int limit = 50, string? cursor = null);

    /// <summary>
    /// Lookup customer by email.
    /// </summary>
    Task<CustomerSummary?> LookupByEmailAsync(string email);

    /// <summary>
    /// Lookup customer by phone.
    /// </summary>
    Task<CustomerSummary?> LookupByPhoneAsync(string phone);

    /// <summary>
    /// Lookup customer by loyalty member number.
    /// </summary>
    Task<CustomerSummary?> LookupByMemberNumberAsync(string memberNumber);

    /// <summary>
    /// Get top customers by spend.
    /// </summary>
    Task<IReadOnlyList<CustomerSummary>> GetTopCustomersAsync(
        int limit = 10,
        DateTime? since = null,
        Guid? siteId = null);

    /// <summary>
    /// Get customers with expiring rewards.
    /// </summary>
    Task<IReadOnlyList<CustomerSummary>> GetCustomersWithExpiringRewardsAsync(int days = 7);
}

// ============================================================================
// Payment Batch Grain - Payment batch operations
// ============================================================================

[GenerateSerializer]
public record PaymentSearchFilter(
    [property: Id(0)] Guid? SiteId = null,
    [property: Id(1)] Guid? OrderId = null,
    [property: Id(2)] State.PaymentStatus? Status = null,
    [property: Id(3)] State.PaymentMethod? Method = null,
    [property: Id(4)] DateTime? CreatedAfter = null,
    [property: Id(5)] DateTime? CreatedBefore = null,
    [property: Id(6)] decimal? MinAmount = null,
    [property: Id(7)] decimal? MaxAmount = null,
    [property: Id(8)] string? CardLastFour = null,
    [property: Id(9)] int Limit = 50,
    [property: Id(10)] string? Cursor = null);

[GenerateSerializer]
public record PaymentSummary(
    [property: Id(0)] Guid PaymentId,
    [property: Id(1)] Guid? OrderId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] State.PaymentStatus Status,
    [property: Id(4)] State.PaymentMethod Method,
    [property: Id(5)] decimal Amount,
    [property: Id(6)] decimal TipAmount,
    [property: Id(7)] string? CardBrand,
    [property: Id(8)] string? CardLastFour,
    [property: Id(9)] string? GatewayReference,
    [property: Id(10)] DateTime CreatedAt,
    [property: Id(11)] DateTime? CompletedAt);

[GenerateSerializer]
public record PaymentSearchResult(
    [property: Id(0)] IReadOnlyList<PaymentSummary> Payments,
    [property: Id(1)] string? NextCursor,
    [property: Id(2)] int TotalCount,
    [property: Id(3)] decimal TotalAmount);

/// <summary>
/// Grain for batch payment operations.
/// Key: "{orgId}:paymentbatch"
/// Provides Square-like batch operations for payments.
/// </summary>
public interface IPaymentBatchGrain : IGrainWithStringKey
{
    /// <summary>
    /// Search payments with filters.
    /// Similar to Square's GET /v2/payments with filters
    /// </summary>
    Task<PaymentSearchResult> SearchAsync(PaymentSearchFilter filter);

    /// <summary>
    /// Get payments for an order.
    /// </summary>
    Task<IReadOnlyList<PaymentSummary>> GetByOrderAsync(Guid orderId);

    /// <summary>
    /// Get payments for a date range (for settlement/reporting).
    /// </summary>
    Task<PaymentSearchResult> GetByDateRangeAsync(
        Guid siteId,
        DateTime startDate,
        DateTime endDate,
        State.PaymentMethod? method = null);

    /// <summary>
    /// Get payments in a settlement batch.
    /// </summary>
    Task<IReadOnlyList<PaymentSummary>> GetByBatchAsync(Guid batchId);

    /// <summary>
    /// Get refunds for a date range.
    /// </summary>
    Task<IReadOnlyList<PaymentSummary>> GetRefundsAsync(
        Guid siteId,
        DateTime startDate,
        DateTime endDate);
}
