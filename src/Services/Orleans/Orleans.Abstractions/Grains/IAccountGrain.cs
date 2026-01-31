using DarkVelocity.Orleans.Abstractions.State;

namespace DarkVelocity.Orleans.Abstractions.Grains;

#region Commands

/// <summary>
/// Command to create/initialize an account.
/// </summary>
public record CreateAccountCommand(
    Guid OrganizationId,
    Guid AccountId,
    string AccountCode,
    string Name,
    AccountType AccountType,
    Guid CreatedBy,
    string? SubType = null,
    string? Description = null,
    Guid? ParentAccountId = null,
    bool IsSystemAccount = false,
    string? TaxCode = null,
    string? ExternalReference = null,
    string Currency = "USD",
    decimal OpeningBalance = 0);

/// <summary>
/// Command to post a debit entry.
/// </summary>
public record PostDebitCommand(
    decimal Amount,
    string Description,
    Guid PerformedBy,
    string? ReferenceNumber = null,
    string? ReferenceType = null,
    Guid? ReferenceId = null,
    Guid? AccountingJournalEntryId = null,
    Guid? CostCenterId = null,
    string? Notes = null);

/// <summary>
/// Command to post a credit entry.
/// </summary>
public record PostCreditCommand(
    decimal Amount,
    string Description,
    Guid PerformedBy,
    string? ReferenceNumber = null,
    string? ReferenceType = null,
    Guid? ReferenceId = null,
    Guid? AccountingJournalEntryId = null,
    Guid? CostCenterId = null,
    string? Notes = null);

/// <summary>
/// Command to adjust the account balance.
/// </summary>
public record AdjustBalanceCommand(
    decimal NewBalance,
    string Reason,
    Guid AdjustedBy,
    Guid? ApprovedBy = null,
    string? Notes = null);

/// <summary>
/// Command to reverse a previous entry.
/// </summary>
public record ReverseEntryCommand(
    Guid EntryId,
    string Reason,
    Guid ReversedBy);

/// <summary>
/// Command to close a period.
/// </summary>
public record ClosePeriodCommand(
    int Year,
    int Month,
    Guid ClosedBy,
    decimal? ClosingBalance = null);

/// <summary>
/// Command to update account details.
/// </summary>
public record UpdateAccountCommand(
    string? Name = null,
    string? Description = null,
    string? SubType = null,
    string? TaxCode = null,
    string? ExternalReference = null,
    Guid? ParentAccountId = null,
    Guid UpdatedBy = default);

#endregion

#region Results

/// <summary>
/// Result of creating an account.
/// </summary>
public record CreateAccountResult(
    Guid AccountId,
    string AccountCode,
    decimal Balance);

/// <summary>
/// Result of posting an entry.
/// </summary>
public record PostEntryResult(
    Guid EntryId,
    decimal Amount,
    decimal NewBalance,
    JournalEntryType EntryType);

/// <summary>
/// Result of reversing an entry.
/// </summary>
public record ReverseEntryResult(
    Guid ReversalEntryId,
    Guid OriginalEntryId,
    decimal Amount,
    decimal NewBalance);

/// <summary>
/// Summary information about an account.
/// </summary>
public record AccountSummary(
    Guid AccountId,
    string AccountCode,
    string Name,
    AccountType AccountType,
    decimal Balance,
    decimal TotalDebits,
    decimal TotalCredits,
    long TotalEntryCount,
    DateTime? LastEntryAt,
    bool IsActive);

/// <summary>
/// Balance information for an account.
/// </summary>
public record AccountBalance(
    decimal Balance,
    decimal TotalDebits,
    decimal TotalCredits,
    DateTime? LastEntryAt);

#endregion

/// <summary>
/// Grain interface for managing accounts with full journal entry tracking.
/// Provides double-entry accounting support with complete audit trail.
/// </summary>
public interface IAccountGrain : IGrainWithStringKey
{
    #region Lifecycle

    /// <summary>
    /// Creates/initializes the account.
    /// </summary>
    Task<CreateAccountResult> CreateAsync(CreateAccountCommand command);

    /// <summary>
    /// Gets the full account state.
    /// </summary>
    Task<AccountState> GetStateAsync();

    /// <summary>
    /// Checks if the account exists.
    /// </summary>
    Task<bool> ExistsAsync();

    /// <summary>
    /// Updates account details (name, description, etc.).
    /// </summary>
    Task UpdateAsync(UpdateAccountCommand command);

    /// <summary>
    /// Activates a deactivated account.
    /// </summary>
    Task ActivateAsync(Guid activatedBy);

    /// <summary>
    /// Deactivates an account (soft delete).
    /// </summary>
    Task DeactivateAsync(Guid deactivatedBy);

    #endregion

    #region Journal Entries

    /// <summary>
    /// Posts a debit entry to the account.
    /// Increases balance for Asset/Expense accounts.
    /// Decreases balance for Liability/Equity/Revenue accounts.
    /// </summary>
    Task<PostEntryResult> PostDebitAsync(PostDebitCommand command);

    /// <summary>
    /// Posts a credit entry to the account.
    /// Decreases balance for Asset/Expense accounts.
    /// Increases balance for Liability/Equity/Revenue accounts.
    /// </summary>
    Task<PostEntryResult> PostCreditAsync(PostCreditCommand command);

    /// <summary>
    /// Adjusts the account balance with a correcting entry.
    /// </summary>
    Task<PostEntryResult> AdjustBalanceAsync(AdjustBalanceCommand command);

    /// <summary>
    /// Reverses a previous entry.
    /// </summary>
    Task<ReverseEntryResult> ReverseEntryAsync(ReverseEntryCommand command);

    /// <summary>
    /// Closes the current period and creates a period summary.
    /// </summary>
    Task<AccountPeriodSummary> ClosePeriodAsync(ClosePeriodCommand command);

    #endregion

    #region Queries

    /// <summary>
    /// Gets the current account balance.
    /// </summary>
    Task<decimal> GetBalanceAsync();

    /// <summary>
    /// Gets detailed balance information.
    /// </summary>
    Task<AccountBalance> GetBalanceInfoAsync();

    /// <summary>
    /// Gets a summary of the account.
    /// </summary>
    Task<AccountSummary> GetSummaryAsync();

    /// <summary>
    /// Gets recent journal entries.
    /// </summary>
    Task<IReadOnlyList<AccountJournalEntry>> GetRecentEntriesAsync(int count = 50);

    /// <summary>
    /// Gets journal entries within a date range.
    /// </summary>
    Task<IReadOnlyList<AccountJournalEntry>> GetEntriesInRangeAsync(DateTime from, DateTime to);

    /// <summary>
    /// Gets a specific journal entry by ID.
    /// </summary>
    Task<AccountJournalEntry?> GetEntryAsync(Guid entryId);

    /// <summary>
    /// Gets entries by reference.
    /// </summary>
    Task<IReadOnlyList<AccountJournalEntry>> GetEntriesByReferenceAsync(string referenceType, Guid referenceId);

    /// <summary>
    /// Gets period summaries.
    /// </summary>
    Task<IReadOnlyList<AccountPeriodSummary>> GetPeriodSummariesAsync(int? year = null);

    /// <summary>
    /// Gets the balance at a specific point in time.
    /// </summary>
    Task<decimal> GetBalanceAtAsync(DateTime pointInTime);

    #endregion
}
