namespace DarkVelocity.Host.Events;

public sealed record PaymentIntentCreated(
    Guid PaymentIntentId,
    Guid AccountId,
    long Amount,
    string Currency,
    string Status
) : IntegrationEvent
{
    public override string EventType => "payment_intent.created";
}

public sealed record PaymentIntentSucceeded(
    Guid PaymentIntentId,
    Guid AccountId,
    long Amount,
    string Currency
) : IntegrationEvent
{
    public override string EventType => "payment_intent.succeeded";
}

public sealed record PaymentIntentFailed(
    Guid PaymentIntentId,
    Guid AccountId,
    string DeclineCode,
    string DeclineMessage
) : IntegrationEvent
{
    public override string EventType => "payment_intent.payment_failed";
}

public sealed record PaymentIntentCanceled(
    Guid PaymentIntentId,
    Guid AccountId,
    string? CancellationReason
) : IntegrationEvent
{
    public override string EventType => "payment_intent.canceled";
}

public sealed record PaymentIntentRequiresAction(
    Guid PaymentIntentId,
    Guid AccountId,
    string ActionType,
    string? RedirectUrl
) : IntegrationEvent
{
    public override string EventType => "payment_intent.requires_action";
}

public sealed record PaymentIntentAmountCapturableUpdated(
    Guid PaymentIntentId,
    Guid AccountId,
    long AmountCapturable
) : IntegrationEvent
{
    public override string EventType => "payment_intent.amount_capturable_updated";
}
