using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

#region Commands

/// <summary>
/// Command to create/initialize an account.
/// </summary>
[GenerateSerializer]
public record CreateAccountCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid AccountId,
    [property: Id(2)] string AccountCode,
    [property: Id(3)] string Name,
    [property: Id(4)] AccountType AccountType,
    [property: Id(5)] Guid CreatedBy,
    [property: Id(6)] string? SubType = null,
    [property: Id(7)] string? Description = null,
    [property: Id(8)] Guid? ParentAccountId = null,
    [property: Id(9)] bool IsSystemAccount = false,
    [property: Id(10)] string? TaxCode = null,
    [property: Id(11)] string? ExternalReference = null,
    [property: Id(12)] string Currency = "USD",
    [property: Id(13)] decimal OpeningBalance = 0);

/// <summary>
/// Command to post a debit entry.
/// </summary>
[GenerateSerializer]
public record PostDebitCommand(
    [property: Id(0)] decimal Amount,
    [property: Id(1)] string Description,
    [property: Id(2)] Guid PerformedBy,
    [property: Id(3)] string? ReferenceNumber = null,
    [property: Id(4)] string? ReferenceType = null,
    [property: Id(5)] Guid? ReferenceId = null,
    [property: Id(6)] Guid? AccountingJournalEntryId = null,
    [property: Id(7)] Guid? CostCenterId = null,
    [property: Id(8)] string? Notes = null);

/// <summary>
/// Command to post a credit entry.
/// </summary>
[GenerateSerializer]
public record PostCreditCommand(
    [property: Id(0)] decimal Amount,
    [property: Id(1)] string Description,
    [property: Id(2)] Guid PerformedBy,
    [property: Id(3)] string? ReferenceNumber = null,
    [property: Id(4)] string? ReferenceType = null,
    [property: Id(5)] Guid? ReferenceId = null,
    [property: Id(6)] Guid? AccountingJournalEntryId = null,
    [property: Id(7)] Guid? CostCenterId = null,
    [property: Id(8)] string? Notes = null);

/// <summary>
/// Command to adjust the account balance.
/// </summary>
[GenerateSerializer]
public record AdjustBalanceCommand(
    [property: Id(0)] decimal NewBalance,
    [property: Id(1)] string Reason,
    [property: Id(2)] Guid AdjustedBy,
    [property: Id(3)] Guid? ApprovedBy = null,
    [property: Id(4)] string? Notes = null);

/// <summary>
/// Command to reverse a previous entry.
/// </summary>
[GenerateSerializer]
public record ReverseEntryCommand(
    [property: Id(0)] Guid EntryId,
    [property: Id(1)] string Reason,
    [property: Id(2)] Guid ReversedBy);

/// <summary>
/// Command to close a period.
/// </summary>
[GenerateSerializer]
public record ClosePeriodCommand(
    [property: Id(0)] int Year,
    [property: Id(1)] int Month,
    [property: Id(2)] Guid ClosedBy,
    [property: Id(3)] decimal? ClosingBalance = null);

/// <summary>
/// Command to update account details.
/// </summary>
[GenerateSerializer]
public record UpdateAccountCommand(
    [property: Id(0)] string? Name = null,
    [property: Id(1)] string? Description = null,
    [property: Id(2)] string? SubType = null,
    [property: Id(3)] string? TaxCode = null,
    [property: Id(4)] string? ExternalReference = null,
    [property: Id(5)] Guid? ParentAccountId = null,
    [property: Id(6)] Guid UpdatedBy = default);

#endregion

#region Results

/// <summary>
/// Result of creating an account.
/// </summary>
[GenerateSerializer]
public record CreateAccountResult(
    [property: Id(0)] Guid AccountId,
    [property: Id(1)] string AccountCode,
    [property: Id(2)] decimal Balance);

/// <summary>
/// Result of posting an entry.
/// </summary>
[GenerateSerializer]
public record PostEntryResult(
    [property: Id(0)] Guid EntryId,
    [property: Id(1)] decimal Amount,
    [property: Id(2)] decimal NewBalance,
    [property: Id(3)] JournalEntryType EntryType);

/// <summary>
/// Result of reversing an entry.
/// </summary>
[GenerateSerializer]
public record ReverseEntryResult(
    [property: Id(0)] Guid ReversalEntryId,
    [property: Id(1)] Guid OriginalEntryId,
    [property: Id(2)] decimal Amount,
    [property: Id(3)] decimal NewBalance);

/// <summary>
/// Summary information about an account.
/// </summary>
[GenerateSerializer]
public record AccountSummary(
    [property: Id(0)] Guid AccountId,
    [property: Id(1)] string AccountCode,
    [property: Id(2)] string Name,
    [property: Id(3)] AccountType AccountType,
    [property: Id(4)] decimal Balance,
    [property: Id(5)] decimal TotalDebits,
    [property: Id(6)] decimal TotalCredits,
    [property: Id(7)] long TotalEntryCount,
    [property: Id(8)] DateTime? LastEntryAt,
    [property: Id(9)] bool IsActive);

/// <summary>
/// Balance information for an account.
/// </summary>
[GenerateSerializer]
public record AccountBalance(
    [property: Id(0)] decimal Balance,
    [property: Id(1)] decimal TotalDebits,
    [property: Id(2)] decimal TotalCredits,
    [property: Id(3)] DateTime? LastEntryAt);

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
