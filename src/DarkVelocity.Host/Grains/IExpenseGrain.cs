using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Commands
// ============================================================================

/// <summary>
/// Command to record a new expense.
/// </summary>
[GenerateSerializer]
public record RecordExpenseCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid ExpenseId,
    [property: Id(3)] ExpenseCategory Category,
    [property: Id(4)] string Description,
    [property: Id(5)] decimal Amount,
    [property: Id(6)] DateOnly ExpenseDate,
    [property: Id(7)] Guid RecordedBy,
    [property: Id(8)] string Currency = "USD",
    [property: Id(9)] string? CustomCategory = null,
    [property: Id(10)] Guid? VendorId = null,
    [property: Id(11)] string? VendorName = null,
    [property: Id(12)] PaymentMethod? PaymentMethod = null,
    [property: Id(13)] string? ReferenceNumber = null,
    [property: Id(14)] decimal? TaxAmount = null,
    [property: Id(15)] bool IsTaxDeductible = false,
    [property: Id(16)] string? Notes = null,
    [property: Id(17)] IReadOnlyList<string>? Tags = null);

/// <summary>
/// Command to update an expense.
/// </summary>
[GenerateSerializer]
public record UpdateExpenseCommand(
    [property: Id(0)] Guid UpdatedBy,
    [property: Id(1)] ExpenseCategory? Category = null,
    [property: Id(2)] string? CustomCategory = null,
    [property: Id(3)] string? Description = null,
    [property: Id(4)] decimal? Amount = null,
    [property: Id(5)] DateOnly? ExpenseDate = null,
    [property: Id(6)] Guid? VendorId = null,
    [property: Id(7)] string? VendorName = null,
    [property: Id(8)] PaymentMethod? PaymentMethod = null,
    [property: Id(9)] string? ReferenceNumber = null,
    [property: Id(10)] decimal? TaxAmount = null,
    [property: Id(11)] bool? IsTaxDeductible = null,
    [property: Id(12)] string? Notes = null,
    [property: Id(13)] IReadOnlyList<string>? Tags = null);

/// <summary>
/// Command to approve an expense.
/// </summary>
[GenerateSerializer]
public record ApproveExpenseCommand(
    [property: Id(0)] Guid ApprovedBy,
    [property: Id(1)] string? Notes = null);

/// <summary>
/// Command to reject an expense.
/// </summary>
[GenerateSerializer]
public record RejectExpenseCommand(
    [property: Id(0)] Guid RejectedBy,
    [property: Id(1)] string Reason);

/// <summary>
/// Command to mark an expense as paid.
/// </summary>
[GenerateSerializer]
public record MarkExpensePaidCommand(
    [property: Id(0)] Guid PaidBy,
    [property: Id(1)] DateOnly? PaymentDate = null,
    [property: Id(2)] string? ReferenceNumber = null,
    [property: Id(3)] PaymentMethod? PaymentMethod = null);

/// <summary>
/// Command to void an expense.
/// </summary>
[GenerateSerializer]
public record VoidExpenseCommand(
    [property: Id(0)] Guid VoidedBy,
    [property: Id(1)] string Reason);

/// <summary>
/// Command to attach a document to an expense.
/// </summary>
[GenerateSerializer]
public record AttachDocumentCommand(
    [property: Id(0)] string DocumentUrl,
    [property: Id(1)] string Filename,
    [property: Id(2)] Guid AttachedBy);

/// <summary>
/// Command to set up recurring expense.
/// </summary>
[GenerateSerializer]
public record SetRecurrenceCommand(
    [property: Id(0)] RecurrencePattern Pattern,
    [property: Id(1)] Guid SetBy);

// ============================================================================
// Results
// ============================================================================

/// <summary>
/// Snapshot of expense state for API responses.
/// </summary>
[GenerateSerializer]
public record ExpenseSnapshot(
    [property: Id(0)] Guid ExpenseId,
    [property: Id(1)] Guid OrganizationId,
    [property: Id(2)] Guid SiteId,
    [property: Id(3)] ExpenseCategory Category,
    [property: Id(4)] string? CustomCategory,
    [property: Id(5)] string Description,
    [property: Id(6)] decimal Amount,
    [property: Id(7)] string Currency,
    [property: Id(8)] DateOnly ExpenseDate,
    [property: Id(9)] Guid? VendorId,
    [property: Id(10)] string? VendorName,
    [property: Id(11)] PaymentMethod? PaymentMethod,
    [property: Id(12)] string? ReferenceNumber,
    [property: Id(13)] string? DocumentUrl,
    [property: Id(14)] string? DocumentFilename,
    [property: Id(15)] bool IsRecurring,
    [property: Id(16)] decimal? TaxAmount,
    [property: Id(17)] bool IsTaxDeductible,
    [property: Id(18)] string? Notes,
    [property: Id(19)] IReadOnlyList<string> Tags,
    [property: Id(20)] ExpenseStatus Status,
    [property: Id(21)] Guid? ApprovedBy,
    [property: Id(22)] DateTime? ApprovedAt,
    [property: Id(23)] DateTime CreatedAt,
    [property: Id(24)] Guid CreatedBy,
    [property: Id(25)] int Version);

/// <summary>
/// Summary of an expense for listing.
/// </summary>
[GenerateSerializer]
public record ExpenseSummary(
    [property: Id(0)] Guid ExpenseId,
    [property: Id(1)] ExpenseCategory Category,
    [property: Id(2)] string Description,
    [property: Id(3)] decimal Amount,
    [property: Id(4)] string Currency,
    [property: Id(5)] DateOnly ExpenseDate,
    [property: Id(6)] string? VendorName,
    [property: Id(7)] ExpenseStatus Status,
    [property: Id(8)] bool HasDocument);

// ============================================================================
// Grain Interface
// ============================================================================

/// <summary>
/// Grain representing an expense record.
/// </summary>
public interface IExpenseGrain : IGrainWithStringKey
{
    /// <summary>
    /// Record a new expense.
    /// </summary>
    Task<ExpenseSnapshot> RecordAsync(RecordExpenseCommand command);

    /// <summary>
    /// Update an existing expense.
    /// </summary>
    Task<ExpenseSnapshot> UpdateAsync(UpdateExpenseCommand command);

    /// <summary>
    /// Approve the expense.
    /// </summary>
    Task<ExpenseSnapshot> ApproveAsync(ApproveExpenseCommand command);

    /// <summary>
    /// Reject the expense.
    /// </summary>
    Task<ExpenseSnapshot> RejectAsync(RejectExpenseCommand command);

    /// <summary>
    /// Mark the expense as paid.
    /// </summary>
    Task<ExpenseSnapshot> MarkPaidAsync(MarkExpensePaidCommand command);

    /// <summary>
    /// Void/cancel the expense.
    /// </summary>
    Task VoidAsync(VoidExpenseCommand command);

    /// <summary>
    /// Attach a supporting document.
    /// </summary>
    Task<ExpenseSnapshot> AttachDocumentAsync(AttachDocumentCommand command);

    /// <summary>
    /// Set up recurring expense pattern.
    /// </summary>
    Task<ExpenseSnapshot> SetRecurrenceAsync(SetRecurrenceCommand command);

    /// <summary>
    /// Get the current state snapshot.
    /// </summary>
    Task<ExpenseSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Check if the expense exists.
    /// </summary>
    Task<bool> ExistsAsync();
}

// ============================================================================
// Expense Index Grain
// ============================================================================

/// <summary>
/// Query parameters for listing expenses.
/// </summary>
[GenerateSerializer]
public record ExpenseQuery(
    [property: Id(0)] DateOnly? FromDate = null,
    [property: Id(1)] DateOnly? ToDate = null,
    [property: Id(2)] ExpenseCategory? Category = null,
    [property: Id(3)] ExpenseStatus? Status = null,
    [property: Id(4)] string? VendorName = null,
    [property: Id(5)] decimal? MinAmount = null,
    [property: Id(6)] decimal? MaxAmount = null,
    [property: Id(7)] IReadOnlyList<string>? Tags = null,
    [property: Id(8)] int Skip = 0,
    [property: Id(9)] int Take = 50);

/// <summary>
/// Result of expense query.
/// </summary>
[GenerateSerializer]
public record ExpenseQueryResult(
    [property: Id(0)] IReadOnlyList<ExpenseSummary> Expenses,
    [property: Id(1)] int TotalCount,
    [property: Id(2)] decimal TotalAmount);

/// <summary>
/// Expense totals by category.
/// </summary>
[GenerateSerializer]
public record ExpenseCategoryTotal(
    [property: Id(0)] ExpenseCategory Category,
    [property: Id(1)] string? CustomCategory,
    [property: Id(2)] int Count,
    [property: Id(3)] decimal TotalAmount);

/// <summary>
/// Grain for indexing and querying expenses at site level.
/// </summary>
public interface IExpenseIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Register a new expense in the index.
    /// </summary>
    Task RegisterExpenseAsync(ExpenseSummary expense);

    /// <summary>
    /// Update an expense in the index.
    /// </summary>
    Task UpdateExpenseAsync(ExpenseSummary expense);

    /// <summary>
    /// Remove an expense from the index (voided).
    /// </summary>
    Task RemoveExpenseAsync(Guid expenseId);

    /// <summary>
    /// Query expenses.
    /// </summary>
    Task<ExpenseQueryResult> QueryAsync(ExpenseQuery query);

    /// <summary>
    /// Get expense totals by category for a date range.
    /// </summary>
    Task<IReadOnlyList<ExpenseCategoryTotal>> GetCategoryTotalsAsync(
        DateOnly fromDate,
        DateOnly toDate);

    /// <summary>
    /// Get total expenses for a date range.
    /// </summary>
    Task<decimal> GetTotalAsync(DateOnly fromDate, DateOnly toDate);
}
