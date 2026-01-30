using DarkVelocity.Shared.Infrastructure.Entities;

namespace DarkVelocity.Accounting.Api.Entities;

/// <summary>
/// Represents an accounting period for financial reporting and closing.
/// </summary>
public class AccountingPeriod : BaseEntity, ILocationScoped
{
    public Guid TenantId { get; set; }

    public Guid LocationId { get; set; }

    public PeriodType PeriodType { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public PeriodStatus Status { get; set; } = PeriodStatus.Open;

    public DateTime? ClosedAt { get; set; }

    public Guid? ClosedByUserId { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Journal entries posted to this period
    /// </summary>
    public ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
}

public enum PeriodType
{
    Daily,
    Weekly,
    Monthly,
    Quarterly,
    Yearly
}

public enum PeriodStatus
{
    Open,
    Closing,
    Closed,
    Locked
}
