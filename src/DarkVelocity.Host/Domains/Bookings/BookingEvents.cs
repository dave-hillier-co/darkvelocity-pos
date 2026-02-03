using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Booking events used in event sourcing.
/// </summary>
public interface IBookingEvent
{
    Guid BookingId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record BookingCreated : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SiteId { get; init; }
    [Id(3)] public string CustomerName { get; init; } = "";
    [Id(4)] public string? CustomerEmail { get; init; }
    [Id(5)] public string? CustomerPhone { get; init; }
    [Id(6)] public Guid? CustomerId { get; init; }
    [Id(7)] public DateTime BookingDateTime { get; init; }
    [Id(8)] public int PartySize { get; init; }
    [Id(9)] public string? SpecialRequests { get; init; }
    [Id(10)] public string? Source { get; init; }
    [Id(11)] public Guid CreatedBy { get; init; }
    [Id(12)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingConfirmed : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public string? ConfirmationMethod { get; init; }
    [Id(2)] public Guid? ConfirmedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingModified : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public DateTime? NewDateTime { get; init; }
    [Id(2)] public int? NewPartySize { get; init; }
    [Id(3)] public string? NewSpecialRequests { get; init; }
    [Id(4)] public Guid ModifiedBy { get; init; }
    [Id(5)] public string? Reason { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingCancelled : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public Guid? CancelledBy { get; init; }
    [Id(3)] public decimal? DepositToRefund { get; init; }
    [Id(4)] public decimal? DepositToForfeit { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingDepositRequired : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public decimal AmountRequired { get; init; }
    [Id(2)] public DateTime DueBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingDepositPaid : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public Guid PaymentId { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public string PaymentMethod { get; init; } = "";
    [Id(4)] public string? PaymentReference { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingDepositRefunded : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public decimal Amount { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public Guid RefundedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingDepositForfeited : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public decimal Amount { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public Guid ProcessedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingGuestArrived : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public int? ActualPartySize { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingSeated : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public Guid? TableId { get; init; }
    [Id(2)] public string? TableNumber { get; init; }
    [Id(3)] public int ActualPartySize { get; init; }
    [Id(4)] public Guid SeatedBy { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingLinkedToOrder : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public Guid OrderId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingDepositAppliedToOrder : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public Guid OrderId { get; init; }
    [Id(2)] public decimal AmountApplied { get; init; }
    [Id(3)] public decimal RemainingDeposit { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingDeparted : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingNoShow : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public decimal? DepositToForfeit { get; init; }
    [Id(2)] public Guid MarkedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record BookingTableAssigned : IBookingEvent
{
    [Id(0)] public Guid BookingId { get; init; }
    [Id(1)] public Guid TableId { get; init; }
    [Id(2)] public string TableNumber { get; init; } = "";
    [Id(3)] public Guid? AssignedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}
