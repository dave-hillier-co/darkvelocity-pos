using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Grains;

public record CreateOrderCommand(
    Guid OrganizationId,
    Guid SiteId,
    Guid CreatedBy,
    OrderType Type,
    Guid? TableId = null,
    string? TableNumber = null,
    Guid? CustomerId = null,
    int GuestCount = 1);

public record AddLineCommand(
    Guid MenuItemId,
    string Name,
    int Quantity,
    decimal UnitPrice,
    string? Notes = null,
    List<OrderLineModifier>? Modifiers = null);

public record UpdateLineCommand(
    Guid LineId,
    int? Quantity = null,
    string? Notes = null);

public record ApplyDiscountCommand(
    string Name,
    DiscountType Type,
    decimal Value,
    Guid AppliedBy,
    Guid? DiscountId = null,
    string? Reason = null,
    Guid? ApprovedBy = null);

public record VoidOrderCommand(Guid VoidedBy, string Reason);
public record VoidLineCommand(Guid LineId, Guid VoidedBy, string Reason);

public record OrderCreatedResult(Guid Id, string OrderNumber, DateTime CreatedAt);
public record AddLineResult(Guid LineId, decimal LineTotal, decimal GrandTotal);
public record OrderTotals(
    decimal Subtotal,
    decimal DiscountTotal,
    decimal ServiceChargeTotal,
    decimal TaxTotal,
    decimal GrandTotal,
    decimal PaidAmount,
    decimal BalanceDue);

public interface IOrderGrain : IGrainWithStringKey
{
    Task<OrderCreatedResult> CreateAsync(CreateOrderCommand command);
    Task<OrderState> GetStateAsync();

    // Line management
    Task<AddLineResult> AddLineAsync(AddLineCommand command);
    Task UpdateLineAsync(UpdateLineCommand command);
    Task VoidLineAsync(VoidLineCommand command);
    Task RemoveLineAsync(Guid lineId);

    // Order operations
    Task SendAsync(Guid sentBy);
    Task<OrderTotals> RecalculateTotalsAsync();
    Task ApplyDiscountAsync(ApplyDiscountCommand command);
    Task RemoveDiscountAsync(Guid discountId);
    Task AddServiceChargeAsync(string name, decimal rate, bool isTaxable);

    // Customer
    Task AssignCustomerAsync(Guid customerId, string? customerName);
    Task AssignServerAsync(Guid serverId, string serverName);
    Task TransferTableAsync(Guid newTableId, string newTableNumber, Guid transferredBy);

    // Payment recording
    Task RecordPaymentAsync(Guid paymentId, decimal amount, decimal tipAmount, string method);
    Task RemovePaymentAsync(Guid paymentId);

    // Completion
    Task CloseAsync(Guid closedBy);
    Task VoidAsync(VoidOrderCommand command);
    Task ReopenAsync(Guid reopenedBy, string reason);

    // Queries
    Task<bool> ExistsAsync();
    Task<OrderStatus> GetStatusAsync();
    Task<OrderTotals> GetTotalsAsync();
    Task<IReadOnlyList<OrderLine>> GetLinesAsync();
}
