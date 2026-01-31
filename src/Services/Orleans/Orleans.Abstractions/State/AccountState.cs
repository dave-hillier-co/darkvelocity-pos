namespace DarkVelocity.Orleans.Abstractions.State;

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
public record AccountJournalEntry
{
    public Guid Id { get; init; }
    public DateTime Timestamp { get; init; }
    public JournalEntryType EntryType { get; init; }
    public JournalEntryStatus Status { get; init; }

    /// <summary>
    /// Amount of the entry (always positive; type determines effect).
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// Running balance after this entry.
    /// </summary>
    public decimal BalanceAfter { get; init; }

    /// <summary>
    /// Description or reason for the entry.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Reference to external document (invoice, order, etc.).
    /// </summary>
    public string? ReferenceNumber { get; init; }

    /// <summary>
    /// Type of the related entity (Order, Payment, Invoice, etc.).
    /// </summary>
    public string? ReferenceType { get; init; }

    /// <summary>
    /// ID of the related entity.
    /// </summary>
    public Guid? ReferenceId { get; init; }

    /// <summary>
    /// Link to accounting service journal entry.
    /// </summary>
    public Guid? AccountingJournalEntryId { get; init; }

    /// <summary>
    /// User who performed this entry.
    /// </summary>
    public Guid PerformedBy { get; init; }

    /// <summary>
    /// User who approved this entry (for entries requiring approval).
    /// </summary>
    public Guid? ApprovedBy { get; init; }

    /// <summary>
    /// ID of the original entry if this is a reversal.
    /// </summary>
    public Guid? ReversedEntryId { get; init; }

    /// <summary>
    /// ID of the reversal entry if this entry was reversed.
    /// </summary>
    public Guid? ReversalEntryId { get; init; }

    /// <summary>
    /// Cost center for departmental tracking.
    /// </summary>
    public Guid? CostCenterId { get; init; }

    /// <summary>
    /// Additional notes or context.
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Summary of account activity for a period.
/// </summary>
public record AccountPeriodSummary
{
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal TotalDebits { get; init; }
    public decimal TotalCredits { get; init; }
    public decimal ClosingBalance { get; init; }
    public int EntryCount { get; init; }
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

    /// <summary>
    /// State version for optimistic concurrency.
    /// </summary>
    [Id(26)] public int Version { get; set; }
}
