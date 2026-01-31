using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Streams;

/// <summary>
/// Base interface for all stream events.
/// </summary>
public interface IStreamEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    Guid OrganizationId { get; }
}

/// <summary>
/// Base record for stream events with common metadata.
/// </summary>
[GenerateSerializer]
public abstract record StreamEvent : IStreamEvent
{
    [Id(0)] public Guid EventId { get; init; } = Guid.NewGuid();
    [Id(1)] public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    [Id(2)] public Guid OrganizationId { get; init; }
}

#region User Stream Events

/// <summary>
/// Published when a user is created. Allows other grains (e.g., EmployeeGrain) to react.
/// </summary>
[GenerateSerializer]
public sealed record UserCreatedEvent(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] string Email,
    [property: Id(2)] string DisplayName,
    [property: Id(3)] string? FirstName,
    [property: Id(4)] string? LastName,
    [property: Id(5)] UserType Type
) : StreamEvent;

/// <summary>
/// Published when a user's profile is updated.
/// </summary>
[GenerateSerializer]
public sealed record UserUpdatedEvent(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] string? DisplayName,
    [property: Id(2)] string? FirstName,
    [property: Id(3)] string? LastName,
    [property: Id(4)] List<string> ChangedFields
) : StreamEvent;

/// <summary>
/// Published when a user's status changes (active, inactive, locked).
/// </summary>
[GenerateSerializer]
public sealed record UserStatusChangedEvent(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] UserStatus OldStatus,
    [property: Id(2)] UserStatus NewStatus,
    [property: Id(3)] string? Reason
) : StreamEvent;

/// <summary>
/// Published when a user gains site access.
/// </summary>
[GenerateSerializer]
public sealed record UserSiteAccessGrantedEvent(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] Guid SiteId
) : StreamEvent;

/// <summary>
/// Published when a user loses site access.
/// </summary>
[GenerateSerializer]
public sealed record UserSiteAccessRevokedEvent(
    [property: Id(0)] Guid UserId,
    [property: Id(1)] Guid SiteId
) : StreamEvent;

#endregion

#region Employee Stream Events

/// <summary>
/// Published when an employee is created.
/// </summary>
[GenerateSerializer]
public sealed record EmployeeCreatedEvent(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] Guid DefaultSiteId,
    [property: Id(3)] string EmployeeNumber,
    [property: Id(4)] string FirstName,
    [property: Id(5)] string LastName,
    [property: Id(6)] string Email,
    [property: Id(7)] EmploymentType EmploymentType,
    [property: Id(8)] DateOnly HireDate
) : StreamEvent;

/// <summary>
/// Published when an employee's details are updated.
/// </summary>
[GenerateSerializer]
public sealed record EmployeeUpdatedEvent(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] List<string> ChangedFields
) : StreamEvent;

/// <summary>
/// Published when an employee's status changes.
/// </summary>
[GenerateSerializer]
public sealed record EmployeeStatusChangedEvent(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] EmployeeStatus OldStatus,
    [property: Id(3)] EmployeeStatus NewStatus
) : StreamEvent;

/// <summary>
/// Published when an employee is terminated.
/// </summary>
[GenerateSerializer]
public sealed record EmployeeTerminatedEvent(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] DateOnly TerminationDate,
    [property: Id(3)] string? Reason
) : StreamEvent;

/// <summary>
/// Published when an employee clocks in.
/// </summary>
[GenerateSerializer]
public sealed record EmployeeClockedInEvent(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] DateTime ClockInTime,
    [property: Id(4)] Guid? ShiftId
) : StreamEvent;

/// <summary>
/// Published when an employee clocks out.
/// </summary>
[GenerateSerializer]
public sealed record EmployeeClockedOutEvent(
    [property: Id(0)] Guid EmployeeId,
    [property: Id(1)] Guid UserId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] DateTime ClockOutTime,
    [property: Id(4)] decimal TotalHours
) : StreamEvent;

#endregion

#region Order Stream Events

/// <summary>
/// Published when an order is created.
/// </summary>
[GenerateSerializer]
public sealed record OrderCreatedEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string OrderNumber,
    [property: Id(3)] Guid? ServerId
) : StreamEvent;

/// <summary>
/// Published when a line is added to an order.
/// </summary>
[GenerateSerializer]
public sealed record OrderLineAddedEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid LineId,
    [property: Id(3)] Guid ProductId,
    [property: Id(4)] string ProductName,
    [property: Id(5)] int Quantity,
    [property: Id(6)] decimal UnitPrice,
    [property: Id(7)] decimal LineTotal
) : StreamEvent;

/// <summary>
/// Published when an order is finalized/completed.
/// Triggers inventory consumption and sales aggregation.
/// </summary>
[GenerateSerializer]
public sealed record OrderCompletedEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string OrderNumber,
    [property: Id(3)] decimal Subtotal,
    [property: Id(4)] decimal Tax,
    [property: Id(5)] decimal Total,
    [property: Id(6)] decimal DiscountAmount,
    [property: Id(7)] List<OrderLineSnapshot> Lines,
    [property: Id(8)] Guid? ServerId,
    [property: Id(9)] string? ServerName
) : StreamEvent;

/// <summary>
/// Published when an order is voided.
/// </summary>
[GenerateSerializer]
public sealed record OrderVoidedEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string OrderNumber,
    [property: Id(3)] decimal VoidedAmount,
    [property: Id(4)] string Reason,
    [property: Id(5)] Guid VoidedByUserId
) : StreamEvent;

/// <summary>
/// Snapshot of an order line for stream events.
/// </summary>
[GenerateSerializer]
public sealed record OrderLineSnapshot(
    [property: Id(0)] Guid LineId,
    [property: Id(1)] Guid ProductId,
    [property: Id(2)] string ProductName,
    [property: Id(3)] int Quantity,
    [property: Id(4)] decimal UnitPrice,
    [property: Id(5)] decimal LineTotal,
    [property: Id(6)] Guid? RecipeId
);

#endregion

#region Inventory Stream Events

/// <summary>
/// Published when stock is consumed (e.g., from order completion).
/// </summary>
[GenerateSerializer]
public sealed record StockConsumedEvent(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] decimal Quantity,
    [property: Id(4)] string Unit,
    [property: Id(5)] decimal TotalCost,
    [property: Id(6)] Guid? OrderId,
    [property: Id(7)] string ConsumptionReason
) : StreamEvent;

/// <summary>
/// Published when stock is received.
/// </summary>
[GenerateSerializer]
public sealed record StockReceivedEvent(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] decimal Quantity,
    [property: Id(4)] string Unit,
    [property: Id(5)] decimal UnitCost,
    [property: Id(6)] string? BatchNumber,
    [property: Id(7)] DateOnly? ExpiryDate
) : StreamEvent;

/// <summary>
/// Published when stock levels fall below threshold.
/// </summary>
[GenerateSerializer]
public sealed record LowStockAlertEvent(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] decimal CurrentQuantity,
    [property: Id(4)] decimal ReorderPoint,
    [property: Id(5)] decimal ParLevel
) : StreamEvent;

/// <summary>
/// Published when stock runs out completely.
/// </summary>
[GenerateSerializer]
public sealed record OutOfStockEvent(
    [property: Id(0)] Guid IngredientId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string IngredientName,
    [property: Id(3)] DateTime OutOfStockSince
) : StreamEvent;

#endregion

#region Sales Stream Events

/// <summary>
/// Published when a sale is recorded for aggregation.
/// </summary>
[GenerateSerializer]
public sealed record SaleRecordedEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] DateOnly BusinessDate,
    [property: Id(3)] decimal GrossSales,
    [property: Id(4)] decimal DiscountAmount,
    [property: Id(5)] decimal NetSales,
    [property: Id(6)] decimal Tax,
    [property: Id(7)] decimal TheoreticalCOGS,
    [property: Id(8)] int ItemCount,
    [property: Id(9)] int GuestCount,
    [property: Id(10)] string Channel
) : StreamEvent;

/// <summary>
/// Published when a void is recorded for aggregation.
/// </summary>
[GenerateSerializer]
public sealed record VoidRecordedEvent(
    [property: Id(0)] Guid OrderId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] DateOnly BusinessDate,
    [property: Id(3)] decimal VoidAmount,
    [property: Id(4)] string Reason
) : StreamEvent;

#endregion

#region Alert Stream Events

/// <summary>
/// Generic alert event that can trigger notifications.
/// </summary>
[GenerateSerializer]
public sealed record AlertTriggeredEvent(
    [property: Id(0)] Guid AlertId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] string AlertType,
    [property: Id(3)] string Severity,
    [property: Id(4)] string Title,
    [property: Id(5)] string Message,
    [property: Id(6)] Dictionary<string, string> Metadata
) : StreamEvent;

#endregion
