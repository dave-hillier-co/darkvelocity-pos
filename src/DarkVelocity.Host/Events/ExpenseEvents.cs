using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events;

// ============================================================================
// Expense Events
// ============================================================================

/// <summary>
/// An expense was recorded.
/// </summary>
public sealed record ExpenseRecorded : DomainEvent
{
    public override string EventType => "expense.recorded";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    public required Guid ExpenseId { get; init; }
    public required Guid SiteId { get; init; }
    public required ExpenseCategory Category { get; init; }
    public string? CustomCategory { get; init; }
    public required string Description { get; init; }
    public required decimal Amount { get; init; }
    public required string Currency { get; init; }
    public required DateOnly ExpenseDate { get; init; }
    public string? VendorName { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
    public required Guid RecordedBy { get; init; }
}

/// <summary>
/// An expense was updated.
/// </summary>
public sealed record ExpenseUpdated : DomainEvent
{
    public override string EventType => "expense.updated";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    public required Guid ExpenseId { get; init; }
    public required Guid UpdatedBy { get; init; }
    public string? Description { get; init; }
    public decimal? Amount { get; init; }
    public ExpenseCategory? Category { get; init; }
    public DateOnly? ExpenseDate { get; init; }
}

/// <summary>
/// An expense was approved.
/// </summary>
public sealed record ExpenseApproved : DomainEvent
{
    public override string EventType => "expense.approved";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    public required Guid ExpenseId { get; init; }
    public required Guid ApprovedBy { get; init; }
    public string? Notes { get; init; }
}

/// <summary>
/// An expense was rejected.
/// </summary>
public sealed record ExpenseRejected : DomainEvent
{
    public override string EventType => "expense.rejected";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    public required Guid ExpenseId { get; init; }
    public required Guid RejectedBy { get; init; }
    public required string Reason { get; init; }
}

/// <summary>
/// An expense was marked as paid.
/// </summary>
public sealed record ExpensePaid : DomainEvent
{
    public override string EventType => "expense.paid";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    public required Guid ExpenseId { get; init; }
    public required Guid PaidBy { get; init; }
    public DateOnly? PaymentDate { get; init; }
    public string? ReferenceNumber { get; init; }
    public PaymentMethod? PaymentMethod { get; init; }
}

/// <summary>
/// An expense was voided/cancelled.
/// </summary>
public sealed record ExpenseVoided : DomainEvent
{
    public override string EventType => "expense.voided";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    public required Guid ExpenseId { get; init; }
    public required Guid VoidedBy { get; init; }
    public required string Reason { get; init; }
}

/// <summary>
/// A supporting document was attached to an expense.
/// </summary>
public sealed record ExpenseDocumentAttached : DomainEvent
{
    public override string EventType => "expense.document.attached";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    public required Guid ExpenseId { get; init; }
    public required string DocumentUrl { get; init; }
    public required string Filename { get; init; }
    public required Guid AttachedBy { get; init; }
}

/// <summary>
/// A recurring expense was created.
/// </summary>
public sealed record RecurringExpenseCreated : DomainEvent
{
    public override string EventType => "expense.recurring.created";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    public required Guid ExpenseId { get; init; }
    public required RecurrencePattern Pattern { get; init; }
    public required Guid CreatedBy { get; init; }
}

/// <summary>
/// A recurring expense generated an occurrence.
/// </summary>
public sealed record RecurringExpenseOccurred : DomainEvent
{
    public override string EventType => "expense.recurring.occurred";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => GeneratedExpenseId;

    public required Guid TemplateExpenseId { get; init; }
    public required Guid GeneratedExpenseId { get; init; }
    public required DateOnly OccurrenceDate { get; init; }
    public required int OccurrenceNumber { get; init; }
}
