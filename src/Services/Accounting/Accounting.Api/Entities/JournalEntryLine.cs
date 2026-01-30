using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Accounting.Api.Entities;

/// <summary>
/// Represents a single debit or credit line in a journal entry.
/// </summary>
public class JournalEntryLine : BaseEntity
{
    public Guid JournalEntryId { get; set; }
    public JournalEntry JournalEntry { get; set; } = null!;

    /// <summary>
    /// Account code for this line
    /// </summary>
    public required string AccountCode { get; set; }

    /// <summary>
    /// Account name (denormalized for reporting)
    /// </summary>
    public required string AccountName { get; set; }

    /// <summary>
    /// Debit amount (0 if this is a credit line)
    /// </summary>
    public decimal DebitAmount { get; set; }

    /// <summary>
    /// Credit amount (0 if this is a debit line)
    /// </summary>
    public decimal CreditAmount { get; set; }

    /// <summary>
    /// Tax code for this line (e.g., "A" for 19%, "B" for 7% in Germany)
    /// </summary>
    public string? TaxCode { get; set; }

    /// <summary>
    /// Tax amount if applicable
    /// </summary>
    public decimal? TaxAmount { get; set; }

    /// <summary>
    /// Cost center for departmental reporting
    /// </summary>
    public Guid? CostCenterId { get; set; }
    public CostCenter? CostCenter { get; set; }

    /// <summary>
    /// Line-specific description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Sequence order within the entry
    /// </summary>
    public int LineNumber { get; set; }
}
