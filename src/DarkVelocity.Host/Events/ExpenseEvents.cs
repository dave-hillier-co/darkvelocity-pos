using DarkVelocity.Host.State;
using Orleans;

namespace DarkVelocity.Host.Events;

// ============================================================================
// Expense Events
// ============================================================================

/// <summary>
/// An expense was recorded.
/// </summary>
[GenerateSerializer]
public sealed record ExpenseRecorded : DomainEvent
{
    public override string EventType => "expense.recorded";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    [Id(100)] public required Guid ExpenseId { get; init; }
    [Id(101)] public required Guid OrganizationId { get; init; }
    [Id(102)] public new required Guid SiteId { get; init; }
    [Id(103)] public required ExpenseCategory Category { get; init; }
    [Id(104)] public string? CustomCategory { get; init; }
    [Id(105)] public required string Description { get; init; }
    [Id(106)] public required decimal Amount { get; init; }
    [Id(107)] public required string Currency { get; init; }
    [Id(108)] public required DateOnly ExpenseDate { get; init; }
    [Id(109)] public string? VendorName { get; init; }
    [Id(110)] public PaymentMethod? PaymentMethod { get; init; }
    [Id(111)] public required Guid RecordedBy { get; init; }
}

/// <summary>
/// An expense was updated.
/// </summary>
[GenerateSerializer]
public sealed record ExpenseUpdated : DomainEvent
{
    public override string EventType => "expense.updated";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    [Id(100)] public required Guid ExpenseId { get; init; }
    [Id(101)] public required Guid OrganizationId { get; init; }
    [Id(102)] public required Guid UpdatedBy { get; init; }
    [Id(103)] public string? Description { get; init; }
    [Id(104)] public decimal? Amount { get; init; }
    [Id(105)] public ExpenseCategory? Category { get; init; }
    [Id(106)] public DateOnly? ExpenseDate { get; init; }
}

/// <summary>
/// An expense was approved.
/// </summary>
[GenerateSerializer]
public sealed record ExpenseApproved : DomainEvent
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
/// An expense was rejected.
/// </summary>
[GenerateSerializer]
public sealed record ExpenseRejected : DomainEvent
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
/// An expense was marked as paid.
/// </summary>
[GenerateSerializer]
public sealed record ExpensePaid : DomainEvent
{
    public override string EventType => "expense.paid";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    [Id(100)] public required Guid ExpenseId { get; init; }
    [Id(101)] public required Guid OrganizationId { get; init; }
    [Id(102)] public required Guid PaidBy { get; init; }
    [Id(103)] public DateOnly? PaymentDate { get; init; }
    [Id(104)] public string? ReferenceNumber { get; init; }
    [Id(105)] public PaymentMethod? PaymentMethod { get; init; }
}

/// <summary>
/// An expense was voided/cancelled.
/// </summary>
[GenerateSerializer]
public sealed record ExpenseVoided : DomainEvent
{
    public override string EventType => "expense.voided";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    [Id(100)] public required Guid ExpenseId { get; init; }
    [Id(101)] public required Guid OrganizationId { get; init; }
    [Id(102)] public required Guid VoidedBy { get; init; }
    [Id(103)] public required string Reason { get; init; }
}

/// <summary>
/// A supporting document was attached to an expense.
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
    [Id(103)] public required string Filename { get; init; }
    [Id(104)] public required Guid AttachedBy { get; init; }
}

/// <summary>
/// A recurring expense was created.
/// </summary>
[GenerateSerializer]
public sealed record RecurringExpenseCreated : DomainEvent
{
    public override string EventType => "expense.recurring.created";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => ExpenseId;

    [Id(100)] public required Guid ExpenseId { get; init; }
    [Id(101)] public required Guid OrganizationId { get; init; }
    [Id(102)] public required RecurrencePattern Pattern { get; init; }
    [Id(103)] public required Guid CreatedBy { get; init; }
}

/// <summary>
/// A recurring expense generated an occurrence.
/// </summary>
[GenerateSerializer]
public sealed record RecurringExpenseOccurred : DomainEvent
{
    public override string EventType => "expense.recurring.occurred";
    public override string AggregateType => "Expense";
    public override Guid AggregateId => GeneratedExpenseId;

    [Id(100)] public required Guid TemplateExpenseId { get; init; }
    [Id(101)] public required Guid GeneratedExpenseId { get; init; }
    [Id(102)] public required DateOnly OccurrenceDate { get; init; }
    [Id(103)] public required int OccurrenceNumber { get; init; }
}
