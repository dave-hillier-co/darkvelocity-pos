using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events.JournaledEvents;

/// <summary>
/// Base interface for all Payment journaled events used in event sourcing.
/// </summary>
public interface IPaymentJournaledEvent
{
    Guid PaymentId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record PaymentInitiatedJournaledEvent : IPaymentJournaledEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SiteId { get; init; }
    [Id(3)] public Guid OrderId { get; init; }
    [Id(4)] public PaymentMethod Method { get; init; }
    [Id(5)] public decimal Amount { get; init; }
    [Id(6)] public Guid CashierId { get; init; }
    [Id(7)] public Guid? CustomerId { get; init; }
    [Id(8)] public Guid? DrawerId { get; init; }
    [Id(9)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentAuthorizationRequestedJournaledEvent : IPaymentJournaledEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentAuthorizedJournaledEvent : IPaymentJournaledEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public string AuthorizationCode { get; init; } = "";
    [Id(2)] public string GatewayReference { get; init; } = "";
    [Id(3)] public CardInfo? CardInfo { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentDeclinedJournaledEvent : IPaymentJournaledEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public string DeclineCode { get; init; } = "";
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentCapturedJournaledEvent : IPaymentJournaledEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CashPaymentCompletedJournaledEvent : IPaymentJournaledEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public decimal AmountTendered { get; init; }
    [Id(2)] public decimal TipAmount { get; init; }
    [Id(3)] public decimal TotalAmount { get; init; }
    [Id(4)] public decimal ChangeGiven { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CardPaymentCompletedJournaledEvent : IPaymentJournaledEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public string GatewayReference { get; init; } = "";
    [Id(2)] public string? AuthorizationCode { get; init; }
    [Id(3)] public CardInfo? CardInfo { get; init; }
    [Id(4)] public string? GatewayName { get; init; }
    [Id(5)] public decimal TipAmount { get; init; }
    [Id(6)] public decimal TotalAmount { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GiftCardPaymentCompletedJournaledEvent : IPaymentJournaledEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public Guid GiftCardId { get; init; }
    [Id(2)] public string CardNumber { get; init; } = "";
    [Id(3)] public decimal TotalAmount { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentVoidedJournaledEvent : IPaymentJournaledEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public Guid VoidedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentRefundedJournaledEvent : IPaymentJournaledEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public Guid RefundId { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public string Reason { get; init; } = "";
    [Id(4)] public Guid IssuedBy { get; init; }
    [Id(5)] public string? GatewayReference { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentTipAddedJournaledEvent : IPaymentJournaledEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public decimal TipAmount { get; init; }
    [Id(2)] public decimal NewTotalAmount { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentBatchAssignedJournaledEvent : IPaymentJournaledEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public Guid BatchId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}
