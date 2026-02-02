namespace DarkVelocity.Host.State;

/// <summary>
/// Type of account in the chart of accounts.
/// </summary>
public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Revenue,
    Expense
}

/// <summary>
/// Type of journal entry operation.
/// </summary>
public enum JournalEntryType
{
    /// <summary>Initial account creation with opening balance.</summary>
    Opening,
    /// <summary>Debit entry (increases assets/expenses, decreases liabilities/equity/revenue).</summary>
    Debit,
    /// <summary>Credit entry (decreases assets/expenses, increases liabilities/equity/revenue).</summary>
    Credit,
    /// <summary>Balance adjustment (correction).</summary>
    Adjustment,
    /// <summary>Period closing entry.</summary>
    PeriodClose,
    /// <summary>Reversal of a previous entry.</summary>
    Reversal
}

/// <summary>
/// Status of a journal entry.
/// </summary>
public enum JournalEntryStatus
{
    Pending,
    Posted,
    Reversed
}

/// <summary>
/// Represents a single journal entry in the account ledger.
/// </summary>
[GenerateSerializer]
public record AccountJournalEntry
{
    [Id(0)] public Guid Id { get; init; }
    [Id(1)] public DateTime Timestamp { get; init; }
    [Id(2)] public JournalEntryType EntryType { get; init; }
    [Id(3)] public JournalEntryStatus Status { get; init; }

    /// <summary>
    /// Amount of the entry (always positive; type determines effect).
    /// </summary>
    [Id(4)] public decimal Amount { get; init; }

    /// <summary>
    /// Running balance after this entry.
    /// </summary>
    [Id(5)] public decimal BalanceAfter { get; init; }

    /// <summary>
    /// Description or reason for the entry.
    /// </summary>
    [Id(6)] public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Reference to external document (invoice, order, etc.).
    /// </summary>
    [Id(7)] public string? ReferenceNumber { get; init; }

    /// <summary>
    /// Type of the related entity (Order, Payment, Invoice, etc.).
    /// </summary>
    [Id(8)] public string? ReferenceType { get; init; }

    /// <summary>
    /// ID of the related entity.
    /// </summary>
    [Id(9)] public Guid? ReferenceId { get; init; }

    /// <summary>
    /// Link to accounting service journal entry.
    /// </summary>
    [Id(10)] public Guid? AccountingJournalEntryId { get; init; }

    /// <summary>
    /// User who performed this entry.
    /// </summary>
    [Id(11)] public Guid PerformedBy { get; init; }

    /// <summary>
    /// User who approved this entry (for entries requiring approval).
    /// </summary>
    [Id(12)] public Guid? ApprovedBy { get; init; }

    /// <summary>
    /// ID of the original entry if this is a reversal.
    /// </summary>
    [Id(13)] public Guid? ReversedEntryId { get; init; }

    /// <summary>
    /// ID of the reversal entry if this entry was reversed.
    /// </summary>
    [Id(14)] public Guid? ReversalEntryId { get; init; }

    /// <summary>
    /// Cost center for departmental tracking.
    /// </summary>
    [Id(15)] public Guid? CostCenterId { get; init; }

    /// <summary>
    /// Additional notes or context.
    /// </summary>
    [Id(16)] public string? Notes { get; init; }
}

/// <summary>
/// Summary of account activity for a period.
/// </summary>
[GenerateSerializer]
public record AccountPeriodSummary
{
    [Id(0)] public int Year { get; init; }
    [Id(1)] public int Month { get; init; }
    [Id(2)] public decimal OpeningBalance { get; init; }
    [Id(3)] public decimal TotalDebits { get; init; }
    [Id(4)] public decimal TotalCredits { get; init; }
    [Id(5)] public decimal ClosingBalance { get; init; }
    [Id(6)] public int EntryCount { get; init; }
}

/// <summary>
/// State for the Account Grain with full journal entry tracking.
/// </summary>
[GenerateSerializer]
public sealed class AccountState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }

    /// <summary>
    /// Unique account code (e.g., "1000", "4100", "5200").
    /// </summary>
    [Id(2)] public string AccountCode { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the account.
    /// </summary>
    [Id(3)] public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Main type of account (Asset, Liability, Equity, Revenue, Expense).
    /// </summary>
    [Id(4)] public AccountType AccountType { get; set; }

    /// <summary>
    /// Sub-classification (e.g., "Cash", "Receivables", "Sales").
    /// </summary>
    [Id(5)] public string? SubType { get; set; }

    /// <summary>
    /// Description of the account's purpose.
    /// </summary>
    [Id(6)] public string? Description { get; set; }

    /// <summary>
    /// Current balance of the account.
    /// For Asset/Expense: positive = normal balance.
    /// For Liability/Equity/Revenue: negative stored, displayed as positive.
    /// </summary>
    [Id(7)] public decimal Balance { get; set; }

    /// <summary>
    /// Parent account ID for hierarchical structure.
    /// </summary>
    [Id(8)] public Guid? ParentAccountId { get; set; }

    /// <summary>
    /// Whether this is a system-created account that cannot be deleted.
    /// </summary>
    [Id(9)] public bool IsSystemAccount { get; set; }

    /// <summary>
    /// Whether the account is active.
    /// </summary>
    [Id(10)] public bool IsActive { get; set; } = true;

    /// <summary>
    /// Default tax code for entries to this account.
    /// </summary>
    [Id(11)] public string? TaxCode { get; set; }

    /// <summary>
    /// External reference for ERP mapping (e.g., DATEV account number).
    /// </summary>
    [Id(12)] public string? ExternalReference { get; set; }

    /// <summary>
    /// Currency code (e.g., "USD", "EUR").
    /// </summary>
    [Id(13)] public string Currency { get; set; } = "USD";

    /// <summary>
    /// Journal entries for this account (most recent entries kept).
    /// </summary>
    [Id(14)] public List<AccountJournalEntry> JournalEntries { get; set; } = [];

    /// <summary>
    /// Monthly summaries for historical reference.
    /// </summary>
    [Id(15)] public List<AccountPeriodSummary> PeriodSummaries { get; set; } = [];

    /// <summary>
    /// Total debits ever posted to this account.
    /// </summary>
    [Id(16)] public decimal TotalDebits { get; set; }

    /// <summary>
    /// Total credits ever posted to this account.
    /// </summary>
    [Id(17)] public decimal TotalCredits { get; set; }

    /// <summary>
    /// Count of all entries ever posted.
    /// </summary>
    [Id(18)] public long TotalEntryCount { get; set; }

    /// <summary>
    /// Timestamp of creation.
    /// </summary>
    [Id(19)] public DateTime CreatedAt { get; set; }

    /// <summary>
    /// User who created the account.
    /// </summary>
    [Id(20)] public Guid CreatedBy { get; set; }

    /// <summary>
    /// Timestamp of last modification.
    /// </summary>
    [Id(21)] public DateTime? LastModifiedAt { get; set; }

    /// <summary>
    /// User who last modified the account.
    /// </summary>
    [Id(22)] public Guid? LastModifiedBy { get; set; }

    /// <summary>
    /// Timestamp of last entry.
    /// </summary>
    [Id(23)] public DateTime? LastEntryAt { get; set; }

    /// <summary>
    /// Current accounting period year.
    /// </summary>
    [Id(24)] public int CurrentPeriodYear { get; set; }

    /// <summary>
    /// Current accounting period month.
    /// </summary>
    [Id(25)] public int CurrentPeriodMonth { get; set; }
}
