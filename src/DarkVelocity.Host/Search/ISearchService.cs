namespace DarkVelocity.Host.Search;

/// <summary>
/// Service for searching orders, customers, and payments.
/// Abstracts the underlying search implementation (PostgreSQL FTS or dedicated engine).
/// </summary>
public interface ISearchService
{
    Task<SearchResult<OrderSearchDocument>> SearchOrdersAsync(
        Guid orgId,
        OrderSearchQuery query,
        CancellationToken ct = default);

    Task<SearchResult<CustomerSearchDocument>> SearchCustomersAsync(
        Guid orgId,
        CustomerSearchQuery query,
        CancellationToken ct = default);

    Task<SearchResult<PaymentSearchDocument>> SearchPaymentsAsync(
        Guid orgId,
        PaymentSearchQuery query,
        CancellationToken ct = default);
}

/// <summary>
/// Service for indexing documents into the search store.
/// Used by projection grains to update search indexes.
/// </summary>
public interface ISearchIndexer
{
    Task IndexOrderAsync(OrderSearchDocument document, CancellationToken ct = default);
    Task IndexCustomerAsync(CustomerSearchDocument document, CancellationToken ct = default);
    Task IndexPaymentAsync(PaymentSearchDocument document, CancellationToken ct = default);

    Task UpdateOrderStatusAsync(Guid orderId, string status, DateTime? closedAt, CancellationToken ct = default);
    Task UpdatePaymentStatusAsync(Guid paymentId, string status, DateTime? completedAt, CancellationToken ct = default);
    Task UpdateCustomerStatsAsync(Guid customerId, decimal lifetimeSpend, int visitCount, DateTime? lastVisitAt, string segment, CancellationToken ct = default);

    Task DeleteOrderAsync(Guid orderId, CancellationToken ct = default);
    Task DeleteCustomerAsync(Guid customerId, CancellationToken ct = default);
    Task DeletePaymentAsync(Guid paymentId, CancellationToken ct = default);
}
