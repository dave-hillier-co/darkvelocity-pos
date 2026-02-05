using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for subscription domain events.
/// </summary>
public interface ISubscriptionEvent
{
    DateTime OccurredAt { get; }
}

/// <summary>
/// Subscription was created for an organization.
/// </summary>
[GenerateSerializer]
public sealed record SubscriptionCreated : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public SubscriptionPlan Plan { get; init; }
    [Id(2)] public BillingCycle BillingCycle { get; init; }
    [Id(3)] public string? StripeCustomerId { get; init; }
    [Id(4)] public string? StripeSubscriptionId { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Trial period was started.
/// </summary>
[GenerateSerializer]
public sealed record TrialStarted : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public SubscriptionPlan Plan { get; init; }
    [Id(2)] public DateTime TrialEndsAt { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Trial period ended.
/// </summary>
[GenerateSerializer]
public sealed record TrialEnded : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public bool ConvertedToPaid { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Subscription plan was changed.
/// </summary>
[GenerateSerializer]
public sealed record PlanChanged : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public SubscriptionPlan OldPlan { get; init; }
    [Id(2)] public SubscriptionPlan NewPlan { get; init; }
    [Id(3)] public decimal? ProratedAmount { get; init; }
    [Id(4)] public bool Immediate { get; init; }
    [Id(5)] public DateTime? EffectiveAt { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Billing cycle was changed.
/// </summary>
[GenerateSerializer]
public sealed record BillingCycleChanged : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public BillingCycle OldCycle { get; init; }
    [Id(2)] public BillingCycle NewCycle { get; init; }
    [Id(3)] public DateTime EffectiveAt { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Subscription was cancelled.
/// </summary>
[GenerateSerializer]
public sealed record SubscriptionCanceled : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public bool Immediate { get; init; }
    [Id(2)] public DateTime? CancelAt { get; init; }
    [Id(3)] public string? Reason { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Subscription was reactivated after cancellation.
/// </summary>
[GenerateSerializer]
public sealed record SubscriptionReactivated : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Payment method was added.
/// </summary>
[GenerateSerializer]
public sealed record PaymentMethodAdded : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public PaymentMethodInfo PaymentMethod { get; init; } = new();
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Payment method was removed.
/// </summary>
[GenerateSerializer]
public sealed record PaymentMethodRemoved : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public string PaymentMethodId { get; init; } = string.Empty;
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Default payment method was changed.
/// </summary>
[GenerateSerializer]
public sealed record DefaultPaymentMethodChanged : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public string PaymentMethodId { get; init; } = string.Empty;
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Invoice was generated.
/// </summary>
[GenerateSerializer]
public sealed record InvoiceGenerated : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public InvoiceSummary Invoice { get; init; } = new();
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Invoice was paid.
/// </summary>
[GenerateSerializer]
public sealed record InvoicePaid : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public string InvoiceId { get; init; } = string.Empty;
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public string PaymentMethodId { get; init; } = string.Empty;
    [Id(4)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Invoice payment failed.
/// </summary>
[GenerateSerializer]
public sealed record InvoicePaymentFailed : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public string InvoiceId { get; init; } = string.Empty;
    [Id(2)] public string FailureReason { get; init; } = string.Empty;
    [Id(3)] public int AttemptCount { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Subscription became past due.
/// </summary>
[GenerateSerializer]
public sealed record SubscriptionPastDue : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public string InvoiceId { get; init; } = string.Empty;
    [Id(2)] public decimal AmountDue { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Usage metrics were updated.
/// </summary>
[GenerateSerializer]
public sealed record UsageUpdated : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public UsageMetrics Usage { get; init; } = new();
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Usage limit was exceeded.
/// </summary>
[GenerateSerializer]
public sealed record UsageLimitExceeded : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public string LimitType { get; init; } = string.Empty; // sites, users, transactions, api_calls, storage
    [Id(2)] public int CurrentValue { get; init; }
    [Id(3)] public int LimitValue { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Stripe customer was linked.
/// </summary>
[GenerateSerializer]
public sealed record StripeCustomerLinked : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public string StripeCustomerId { get; init; } = string.Empty;
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Billing period was renewed.
/// </summary>
[GenerateSerializer]
public sealed record BillingPeriodRenewed : ISubscriptionEvent
{
    [Id(0)] public Guid OrganizationId { get; init; }
    [Id(1)] public DateTime PeriodStart { get; init; }
    [Id(2)] public DateTime PeriodEnd { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}
