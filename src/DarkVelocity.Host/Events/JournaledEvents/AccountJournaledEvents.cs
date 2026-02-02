namespace DarkVelocity.Host.Events.JournaledEvents;

/// <summary>
/// Base interface for all Account (double-entry accounting) journaled events.
/// </summary>
public interface IAccountJournaledEvent
{
    Guid AccountId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record AccountCreatedJournaledEvent : IAccountJournaledEvent
{
    [Id(0)] public Guid AccountId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public string AccountCode { get; init; } = "";
    [Id(3)] public string AccountName { get; init; } = "";
    [Id(4)] public string AccountType { get; init; } = ""; // Asset, Liability, Equity, Revenue, Expense
    [Id(5)] public string? ParentAccountCode { get; init; }
    [Id(6)] public bool IsActive { get; init; } = true;
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record AccountDebitedJournaledEvent : IAccountJournaledEvent
{
    [Id(0)] public Guid AccountId { get; init; }
    [Id(1)] public Guid EntryId { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public decimal NewBalance { get; init; }
    [Id(4)] public string Description { get; init; } = "";
    [Id(5)] public string? ReferenceType { get; init; }
    [Id(6)] public Guid? ReferenceId { get; init; }
    [Id(7)] public Guid PerformedBy { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record AccountCreditedJournaledEvent : IAccountJournaledEvent
{
    [Id(0)] public Guid AccountId { get; init; }
    [Id(1)] public Guid EntryId { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public decimal NewBalance { get; init; }
    [Id(4)] public string Description { get; init; } = "";
    [Id(5)] public string? ReferenceType { get; init; }
    [Id(6)] public Guid? ReferenceId { get; init; }
    [Id(7)] public Guid PerformedBy { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record AccountEntryReversedJournaledEvent : IAccountJournaledEvent
{
    [Id(0)] public Guid AccountId { get; init; }
    [Id(1)] public Guid OriginalEntryId { get; init; }
    [Id(2)] public Guid ReversalEntryId { get; init; }
    [Id(3)] public decimal Amount { get; init; }
    [Id(4)] public decimal NewBalance { get; init; }
    [Id(5)] public string Reason { get; init; } = "";
    [Id(6)] public Guid ReversedBy { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record AccountReconciliationRecordedJournaledEvent : IAccountJournaledEvent
{
    [Id(0)] public Guid AccountId { get; init; }
    [Id(1)] public Guid ReconciliationId { get; init; }
    [Id(2)] public decimal BookBalance { get; init; }
    [Id(3)] public decimal StatementBalance { get; init; }
    [Id(4)] public decimal Variance { get; init; }
    [Id(5)] public bool IsReconciled { get; init; }
    [Id(6)] public DateOnly AsOfDate { get; init; }
    [Id(7)] public Guid ReconciledBy { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record AccountUpdatedJournaledEvent : IAccountJournaledEvent
{
    [Id(0)] public Guid AccountId { get; init; }
    [Id(1)] public string? AccountName { get; init; }
    [Id(2)] public bool? IsActive { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record AccountPeriodClosedJournaledEvent : IAccountJournaledEvent
{
    [Id(0)] public Guid AccountId { get; init; }
    [Id(1)] public int Year { get; init; }
    [Id(2)] public int Month { get; init; }
    [Id(3)] public decimal ClosingBalance { get; init; }
    [Id(4)] public Guid ClosedBy { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}
