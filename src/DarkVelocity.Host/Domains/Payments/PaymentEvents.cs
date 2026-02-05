using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Payment events used in event sourcing.
/// </summary>
public interface IPaymentEvent
{
    Guid PaymentId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record PaymentInitiated : IPaymentEvent
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
public sealed record PaymentAuthorizationRequested : IPaymentEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentAuthorized : IPaymentEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public string AuthorizationCode { get; init; } = "";
    [Id(2)] public string GatewayReference { get; init; } = "";
    [Id(3)] public CardInfo? CardInfo { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentDeclined : IPaymentEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public string DeclineCode { get; init; } = "";
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentCaptured : IPaymentEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CashPaymentCompleted : IPaymentEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public decimal AmountTendered { get; init; }
    [Id(2)] public decimal TipAmount { get; init; }
    [Id(3)] public decimal TotalAmount { get; init; }
    [Id(4)] public decimal ChangeGiven { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CardPaymentCompleted : IPaymentEvent
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
public sealed record GiftCardPaymentCompleted : IPaymentEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public Guid GiftCardId { get; init; }
    [Id(2)] public string CardNumber { get; init; } = "";
    [Id(3)] public decimal TotalAmount { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentVoided : IPaymentEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public Guid VoidedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentRefunded : IPaymentEvent
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
public sealed record PaymentTipAdded : IPaymentEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public decimal TipAmount { get; init; }
    [Id(2)] public decimal NewTotalAmount { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentBatchAssigned : IPaymentEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public Guid BatchId { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentRetryScheduled : IPaymentEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public int AttemptNumber { get; init; }
    [Id(2)] public DateTime ScheduledFor { get; init; }
    [Id(3)] public string FailureReason { get; init; } = "";
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentRetryAttempted : IPaymentEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public int AttemptNumber { get; init; }
    [Id(2)] public bool Success { get; init; }
    [Id(3)] public string? ErrorCode { get; init; }
    [Id(4)] public string? ErrorMessage { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record PaymentRetryExhausted : IPaymentEvent
{
    [Id(0)] public Guid PaymentId { get; init; }
    [Id(1)] public int TotalAttempts { get; init; }
    [Id(2)] public string FinalErrorCode { get; init; } = "";
    [Id(3)] public string FinalErrorMessage { get; init; } = "";
    [Id(4)] public DateTime OccurredAt { get; init; }
}
