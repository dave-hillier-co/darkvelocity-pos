using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Accounting.Api.Entities;

/// <summary>
/// Represents a double-entry journal entry in the accounting system.
/// </summary>
public class JournalEntry : BaseEntity, ILocationScoped
{
    public Guid TenantId { get; set; }

    public Guid LocationId { get; set; }

    /// <summary>
    /// Sequential entry number within the location (e.g., "JE-2026-00001")
    /// </summary>
    public required string EntryNumber { get; set; }

    /// <summary>
    /// Date the entry applies to
    /// </summary>
    public DateOnly EntryDate { get; set; }

    /// <summary>
    /// Timestamp when the entry was posted to the ledger
    /// </summary>
    public DateTime PostedAt { get; set; }

    /// <summary>
    /// Type of source document that generated this entry
    /// </summary>
    public JournalEntrySourceType SourceType { get; set; }

    /// <summary>
    /// ID of the source document (Order, Payment, etc.)
    /// </summary>
    public Guid? SourceId { get; set; }

    /// <summary>
    /// Human-readable description of the entry
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Sum of all debit amounts (should equal TotalCredit)
    /// </summary>
    public decimal TotalDebit { get; set; }

    /// <summary>
    /// Sum of all credit amounts (should equal TotalDebit)
    /// </summary>
    public decimal TotalCredit { get; set; }

    /// <summary>
    /// Currency code (ISO 4217)
    /// </summary>
    public string Currency { get; set; } = "EUR";

    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Pending;

    /// <summary>
    /// Reference to the entry that reverses this entry
    /// </summary>
    public Guid? ReversedByEntryId { get; set; }
    public JournalEntry? ReversedByEntry { get; set; }

    /// <summary>
    /// If this is a reversal, reference to the original entry
    /// </summary>
    public Guid? ReversesEntryId { get; set; }
    public JournalEntry? ReversesEntry { get; set; }

    /// <summary>
    /// Link to the fiscal transaction (for compliance)
    /// </summary>
    public Guid? FiscalTransactionId { get; set; }

    /// <summary>
    /// Accounting period this entry belongs to
    /// </summary>
    public Guid? AccountingPeriodId { get; set; }
    public AccountingPeriod? AccountingPeriod { get; set; }

    /// <summary>
    /// Individual line items (debits and credits)
    /// </summary>
    public ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
}

public enum JournalEntrySourceType
{
    Manual,
    Order,
    Payment,
    CashMovement,
    Adjustment,
    GiftCard,
    Inventory,
    Delivery,
    Reconciliation
}

public enum JournalEntryStatus
{
    Pending,
    Posted,
    Reversed
}
