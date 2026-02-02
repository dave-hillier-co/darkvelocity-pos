namespace DarkVelocity.Host.Events.JournaledEvents;

/// <summary>
/// Base interface for all Expense journaled events used in event sourcing.
/// </summary>
public interface IExpenseJournaledEvent
{
    Guid ExpenseId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record ExpenseCreatedJournaledEvent : IExpenseJournaledEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid SiteId { get; init; }
    [Id(3)] public string Description { get; init; } = "";
    [Id(4)] public decimal Amount { get; init; }
    [Id(5)] public string Category { get; init; } = "";
    [Id(6)] public Guid? VendorId { get; init; }
    [Id(7)] public string? VendorName { get; init; }
    [Id(8)] public DateOnly ExpenseDate { get; init; }
    [Id(9)] public string? ReceiptUrl { get; init; }
    [Id(10)] public Guid SubmittedBy { get; init; }
    [Id(11)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseUpdatedJournaledEvent : IExpenseJournaledEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public string? Description { get; init; }
    [Id(2)] public decimal? Amount { get; init; }
    [Id(3)] public string? Category { get; init; }
    [Id(4)] public DateOnly? ExpenseDate { get; init; }
    [Id(5)] public string? ReceiptUrl { get; init; }
    [Id(6)] public Guid UpdatedBy { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseSubmittedForApprovalJournaledEvent : IExpenseJournaledEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public Guid SubmittedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseApprovedJournaledEvent : IExpenseJournaledEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public Guid ApprovedBy { get; init; }
    [Id(2)] public string? Notes { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseRejectedJournaledEvent : IExpenseJournaledEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public Guid RejectedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseReimbursedJournaledEvent : IExpenseJournaledEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public string PaymentMethod { get; init; } = "";
    [Id(2)] public string? PaymentReference { get; init; }
    [Id(3)] public Guid ProcessedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseCancelledJournaledEvent : IExpenseJournaledEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public Guid CancelledBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseReceiptAttachedJournaledEvent : IExpenseJournaledEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public string ReceiptUrl { get; init; } = "";
    [Id(2)] public string? FileName { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ExpensePaidJournaledEvent : IExpenseJournaledEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public Guid PaidBy { get; init; }
    [Id(2)] public DateTime PaymentDate { get; init; }
    [Id(3)] public string? ReferenceNumber { get; init; }
    [Id(4)] public string? PaymentMethod { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseVoidedJournaledEvent : IExpenseJournaledEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public Guid VoidedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseRecurrenceSetJournaledEvent : IExpenseJournaledEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public string Frequency { get; init; } = "";
    [Id(2)] public int Interval { get; init; }
    [Id(3)] public Guid SetBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}
