namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Customer Account events used in event sourcing.
/// </summary>
public interface ICustomerAccountEvent
{
    Guid CustomerId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record CustomerAccountOpened : ICustomerAccountEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public decimal CreditLimit { get; init; }
    [Id(3)] public int PaymentTermsDays { get; init; }
    [Id(4)] public Guid OpenedBy { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerAccountCharged : ICustomerAccountEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid TransactionId { get; init; }
    [Id(2)] public Guid OrderId { get; init; }
    [Id(3)] public decimal Amount { get; init; }
    [Id(4)] public string Description { get; init; } = "";
    [Id(5)] public Guid ChargedBy { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerAccountPaymentApplied : ICustomerAccountEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid TransactionId { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public State.PaymentMethod Method { get; init; }
    [Id(4)] public string? Reference { get; init; }
    [Id(5)] public Guid AppliedBy { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerAccountCreditApplied : ICustomerAccountEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid TransactionId { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public string Reason { get; init; } = "";
    [Id(4)] public Guid AppliedBy { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerAccountCreditLimitChanged : ICustomerAccountEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public decimal OldLimit { get; init; }
    [Id(2)] public decimal NewLimit { get; init; }
    [Id(3)] public string Reason { get; init; } = "";
    [Id(4)] public Guid ChangedBy { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerAccountSuspended : ICustomerAccountEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public Guid SuspendedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerAccountReactivated : ICustomerAccountEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid ReactivatedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerAccountClosed : ICustomerAccountEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public Guid ClosedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CustomerAccountStatementGenerated : ICustomerAccountEvent
{
    [Id(0)] public Guid CustomerId { get; init; }
    [Id(1)] public Guid StatementId { get; init; }
    [Id(2)] public DateOnly PeriodStart { get; init; }
    [Id(3)] public DateOnly PeriodEnd { get; init; }
    [Id(4)] public decimal OpeningBalance { get; init; }
    [Id(5)] public decimal ClosingBalance { get; init; }
    [Id(6)] public decimal TotalCharges { get; init; }
    [Id(7)] public decimal TotalPayments { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}
