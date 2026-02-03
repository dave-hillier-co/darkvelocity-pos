using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Contracts;

/// <summary>
/// Request to create a new expense.
/// </summary>
public record CreateExpenseRequest
{
    /// <summary>Category of expense</summary>
    public required ExpenseCategory Category { get; init; }

    /// <summary>Custom category name if Category is Other</summary>
    public string? CustomCategory { get; init; }

    /// <summary>Description of the expense</summary>
    public required string Description { get; init; }

    /// <summary>Expense amount</summary>
    public required decimal Amount { get; init; }

    /// <summary>Currency code (default: USD)</summary>
    public string? Currency { get; init; }

    /// <summary>Date the expense was incurred</summary>
    public required DateOnly ExpenseDate { get; init; }

    /// <summary>User recording the expense</summary>
    public required Guid RecordedBy { get; init; }

    /// <summary>Vendor/payee ID</summary>
    public Guid? VendorId { get; init; }

    /// <summary>Vendor/payee name</summary>
    public string? VendorName { get; init; }

    /// <summary>Payment method used</summary>
    public PaymentMethod? PaymentMethod { get; init; }

    /// <summary>Reference number (check #, etc.)</summary>
    public string? ReferenceNumber { get; init; }

    /// <summary>Tax amount if tracked separately</summary>
    public decimal? TaxAmount { get; init; }

    /// <summary>Whether this expense is tax deductible</summary>
    public bool? IsTaxDeductible { get; init; }

    /// <summary>Notes or additional details</summary>
    public string? Notes { get; init; }

    /// <summary>Tags for filtering/grouping</summary>
    public IReadOnlyList<string>? Tags { get; init; }
}

/// <summary>
/// Request to update an expense.
/// </summary>
public record UpdateExpenseRequest
{
    /// <summary>User making the update</summary>
    public required Guid UpdatedBy { get; init; }

    /// <summary>New category</summary>
    public ExpenseCategory? Category { get; init; }

    /// <summary>Custom category name</summary>
    public string? CustomCategory { get; init; }

    /// <summary>New description</summary>
    public string? Description { get; init; }

    /// <summary>New amount</summary>
    public decimal? Amount { get; init; }

    /// <summary>New expense date</summary>
    public DateOnly? ExpenseDate { get; init; }

    /// <summary>Vendor/payee ID</summary>
    public Guid? VendorId { get; init; }

    /// <summary>Vendor/payee name</summary>
    public string? VendorName { get; init; }

    /// <summary>Payment method</summary>
    public PaymentMethod? PaymentMethod { get; init; }

    /// <summary>Reference number</summary>
    public string? ReferenceNumber { get; init; }

    /// <summary>Tax amount</summary>
    public decimal? TaxAmount { get; init; }

    /// <summary>Whether tax deductible</summary>
    public bool? IsTaxDeductible { get; init; }

    /// <summary>Notes</summary>
    public string? Notes { get; init; }

    /// <summary>Tags</summary>
    public IReadOnlyList<string>? Tags { get; init; }
}

/// <summary>
/// Request to approve an expense.
/// </summary>
public record ApproveExpenseRequest
{
    /// <summary>User approving the expense</summary>
    public required Guid ApprovedBy { get; init; }

    /// <summary>Optional approval notes</summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Request to reject an expense.
/// </summary>
public record RejectExpenseRequest
{
    /// <summary>User rejecting the expense</summary>
    public required Guid RejectedBy { get; init; }

    /// <summary>Reason for rejection</summary>
    public required string Reason { get; init; }
}

/// <summary>
/// Request to mark an expense as paid.
/// </summary>
public record MarkExpensePaidRequest
{
    /// <summary>User marking as paid</summary>
    public required Guid PaidBy { get; init; }

    /// <summary>Payment date</summary>
    public DateOnly? PaymentDate { get; init; }

    /// <summary>Payment reference number</summary>
    public string? ReferenceNumber { get; init; }

    /// <summary>Payment method used</summary>
    public PaymentMethod? PaymentMethod { get; init; }
}

/// <summary>
/// Request to attach a document to an expense.
/// </summary>
public record AttachDocumentRequest
{
    /// <summary>URL of the uploaded document</summary>
    public required string DocumentUrl { get; init; }

    /// <summary>Original filename</summary>
    public required string Filename { get; init; }

    /// <summary>User attaching the document</summary>
    public required Guid AttachedBy { get; init; }
}
