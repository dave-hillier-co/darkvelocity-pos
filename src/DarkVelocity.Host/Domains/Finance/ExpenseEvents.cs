using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Expense events used in event sourcing.
/// </summary>
public interface IExpenseEvent
{
    Guid ExpenseId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record ExpenseCreated : IExpenseEvent
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
public sealed record ExpenseUpdated : IExpenseEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public string? Description { get; init; }
    [Id(2)] public decimal? Amount { get; init; }
    [Id(3)] public string? Category { get; init; }
    [Id(4)] public DateOnly? ExpenseDate { get; init; }
    [Id(5)] public string? ReceiptUrl { get; init; }
    [Id(6)] public Guid UpdatedBy { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
    [Id(8)] public Guid OrganizationId { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseSubmittedForApproval : IExpenseEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public Guid SubmittedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseApproved : IExpenseEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public Guid ApprovedBy { get; init; }
    [Id(2)] public string? Notes { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
    [Id(4)] public Guid OrganizationId { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseRejected : IExpenseEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public Guid RejectedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
    [Id(4)] public Guid OrganizationId { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseReimbursed : IExpenseEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public string PaymentMethod { get; init; } = "";
    [Id(2)] public string? PaymentReference { get; init; }
    [Id(3)] public Guid ProcessedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseCancelled : IExpenseEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public Guid CancelledBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseReceiptAttached : IExpenseEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public string ReceiptUrl { get; init; } = "";
    [Id(2)] public string? FileName { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ExpensePaid : IExpenseEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public Guid PaidBy { get; init; }
    [Id(2)] public DateOnly? PaymentDate { get; init; }
    [Id(3)] public string? ReferenceNumber { get; init; }
    [Id(4)] public string? PaymentMethod { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
    [Id(6)] public Guid OrganizationId { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseVoided : IExpenseEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public Guid VoidedBy { get; init; }
    [Id(2)] public string Reason { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
    [Id(4)] public Guid OrganizationId { get; init; }
}

[GenerateSerializer]
public sealed record ExpenseRecurrenceSet : IExpenseEvent
{
    [Id(0)] public Guid ExpenseId { get; init; }
    [Id(1)] public string Frequency { get; init; } = "";
    [Id(2)] public int Interval { get; init; }
    [Id(3)] public Guid SetBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

// ============================================================================
// Domain Events (for Kafka publishing)
// ============================================================================

/// <summary>
/// Domain event: Expense recorded (for Kafka publishing).
/// </summary>
[GenerateSerializer]
public sealed record ExpenseRecorded : DomainEvent
{
    public override string EventType => "expense.recorded";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    [Id(100)] public required Guid ExpenseId { get; init; }
    [Id(101)] public required Guid OrganizationId { get; init; }
    [Id(102)] public required string Category { get; init; }
    [Id(103)] public string? CustomCategory { get; init; }
    [Id(104)] public required string Description { get; init; }
    [Id(105)] public required decimal Amount { get; init; }
    [Id(106)] public required string Currency { get; init; }
    [Id(107)] public required DateOnly ExpenseDate { get; init; }
    [Id(108)] public string? VendorName { get; init; }
    [Id(109)] public string? PaymentMethod { get; init; }
    [Id(110)] public required Guid RecordedBy { get; init; }
}

/// <summary>
/// Domain event: Expense document attached.
/// </summary>
[GenerateSerializer]
public sealed record ExpenseDocumentAttached : DomainEvent
{
    public override string EventType => "expense.document.attached";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    [Id(100)] public required Guid ExpenseId { get; init; }
    [Id(101)] public required Guid OrganizationId { get; init; }
    [Id(102)] public required string DocumentUrl { get; init; }
    [Id(103)] public string? Filename { get; init; }
    [Id(104)] public required Guid AttachedBy { get; init; }
}

/// <summary>
/// Domain event: Recurring expense created.
/// </summary>
[GenerateSerializer]
public sealed record RecurringExpenseCreated : DomainEvent
{
    public override string EventType => "expense.recurring.created";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    [Id(100)] public required Guid ExpenseId { get; init; }
    [Id(101)] public required Guid OrganizationId { get; init; }
    [Id(102)] public required Guid CreatedBy { get; init; }
    [Id(103)] public RecurrencePattern? Pattern { get; init; }
}

/// <summary>
/// Domain event: Expense updated.
/// </summary>
[GenerateSerializer]
public sealed record ExpenseUpdatedDomainEvent : DomainEvent
{
    public override string EventType => "expense.updated";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    [Id(100)] public required Guid ExpenseId { get; init; }
    [Id(101)] public required Guid OrganizationId { get; init; }
    [Id(102)] public string? Description { get; init; }
    [Id(103)] public decimal? Amount { get; init; }
    [Id(104)] public string? Category { get; init; }
    [Id(105)] public DateOnly? ExpenseDate { get; init; }
    [Id(106)] public required Guid UpdatedBy { get; init; }
}

/// <summary>
/// Domain event: Expense approved.
/// </summary>
[GenerateSerializer]
public sealed record ExpenseApprovedDomainEvent : DomainEvent
{
    public override string EventType => "expense.approved";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    [Id(100)] public required Guid ExpenseId { get; init; }
    [Id(101)] public required Guid OrganizationId { get; init; }
    [Id(102)] public required Guid ApprovedBy { get; init; }
    [Id(103)] public string? Notes { get; init; }
}

/// <summary>
/// Domain event: Expense rejected.
/// </summary>
[GenerateSerializer]
public sealed record ExpenseRejectedDomainEvent : DomainEvent
{
    public override string EventType => "expense.rejected";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    [Id(100)] public required Guid ExpenseId { get; init; }
    [Id(101)] public required Guid OrganizationId { get; init; }
    [Id(102)] public required Guid RejectedBy { get; init; }
    [Id(103)] public required string Reason { get; init; }
}

/// <summary>
/// Domain event: Expense paid.
/// </summary>
[GenerateSerializer]
public sealed record ExpensePaidDomainEvent : DomainEvent
{
    public override string EventType => "expense.paid";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    [Id(100)] public required Guid ExpenseId { get; init; }
    [Id(101)] public required Guid OrganizationId { get; init; }
    [Id(102)] public required Guid PaidBy { get; init; }
    [Id(103)] public DateOnly? PaymentDate { get; init; }
    [Id(104)] public string? ReferenceNumber { get; init; }
    [Id(105)] public string? PaymentMethod { get; init; }
}

/// <summary>
/// Domain event: Expense voided.
/// </summary>
[GenerateSerializer]
public sealed record ExpenseVoidedDomainEvent : DomainEvent
{
    public override string EventType => "expense.voided";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    [Id(100)] public required Guid ExpenseId { get; init; }
    [Id(101)] public required Guid OrganizationId { get; init; }
    [Id(102)] public required Guid VoidedBy { get; init; }
    [Id(103)] public required string Reason { get; init; }
}
