using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Accounting.Api.Entities;

/// <summary>
/// Represents a reconciliation record for cash, bank, or card settlements.
/// </summary>
public class Reconciliation : BaseEntity, ILocationScoped
{
    public Guid TenantId { get; set; }

    public Guid LocationId { get; set; }

    public ReconciliationType ReconciliationType { get; set; }

    public DateOnly Date { get; set; }

    /// <summary>
    /// Expected amount based on system records
    /// </summary>
    public decimal ExpectedAmount { get; set; }

    /// <summary>
    /// Actual counted/reported amount
    /// </summary>
    public decimal ActualAmount { get; set; }

    /// <summary>
    /// Difference between expected and actual (positive = over, negative = short)
    /// </summary>
    public decimal Variance { get; set; }

    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Pending;

    public DateTime? ResolvedAt { get; set; }

    public Guid? ResolvedByUserId { get; set; }

    public string? ResolutionNotes { get; set; }

    /// <summary>
    /// Currency code (ISO 4217)
    /// </summary>
    public string Currency { get; set; } = "EUR";

    /// <summary>
    /// Reference to related journal entries (stored as JSON)
    /// </summary>
    public string? RelatedEntryIdsJson { get; set; }

    /// <summary>
    /// External reference (e.g., bank statement reference, settlement batch ID)
    /// </summary>
    public string? ExternalReference { get; set; }
}

public enum ReconciliationType
{
    CashDrawer,
    BankDeposit,
    CardSettlement,
    GiftCardLiability
}

public enum ReconciliationStatus
{
    Pending,
    Matched,
    Variance,
    Investigated,
    Resolved
}
