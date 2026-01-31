namespace DarkVelocity.Shared.Infrastructure.Events;

public sealed record PaymentCreated(
    Guid PaymentId,
    Guid OrderId,
    Guid LocationId,
    decimal Amount,
    string PaymentMethod,
    string Currency
) : IntegrationEvent
{
    public override string EventType => "payments.payment.created";
}

public sealed record PaymentCompleted(
    Guid PaymentId,
    Guid OrderId,
    Guid LocationId,
    decimal Amount,
    string PaymentMethod,
    string Currency,
    string? TransactionReference
) : IntegrationEvent
{
    public override string EventType => "payments.payment.completed";
}

public sealed record PaymentRefunded(
    Guid PaymentId,
    Guid OriginalPaymentId,
    Guid OrderId,
    Guid LocationId,
    decimal Amount,
    string PaymentMethod,
    string Currency,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "payments.payment.refunded";
}

public sealed record PaymentVoided(
    Guid PaymentId,
    Guid OrderId,
    Guid LocationId,
    decimal Amount,
    Guid VoidedByUserId,
    string? Reason
) : IntegrationEvent
{
    public override string EventType => "payments.payment.voided";
}
