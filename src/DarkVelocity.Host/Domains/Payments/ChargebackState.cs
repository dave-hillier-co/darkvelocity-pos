namespace DarkVelocity.Host.State;

public enum ChargebackStatus
{
    Pending,
    Acknowledged,
    EvidenceGathering,
    Disputed,
    Accepted,
    Won,
    Lost,
    Expired
}

public enum ChargebackResolution
{
    WonByMerchant,
    WonByCardholder,
    PartiallyWonByMerchant,
    AcceptedByMerchant,
    Expired
}

[GenerateSerializer]
public record ChargebackEvidence
{
    [Id(0)] public Guid EvidenceId { get; init; }
    [Id(1)] public string EvidenceType { get; init; } = "";
    [Id(2)] public string Description { get; init; } = "";
    [Id(3)] public string FileReference { get; init; } = "";
    [Id(4)] public Guid UploadedBy { get; init; }
    [Id(5)] public DateTime UploadedAt { get; init; }
}

[GenerateSerializer]
public record ChargebackNote
{
    [Id(0)] public Guid NoteId { get; init; }
    [Id(1)] public string Content { get; init; } = "";
    [Id(2)] public Guid AddedBy { get; init; }
    [Id(3)] public DateTime AddedAt { get; init; }
}

[GenerateSerializer]
public sealed class ChargebackState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid PaymentId { get; set; }
    [Id(3)] public ChargebackStatus Status { get; set; } = ChargebackStatus.Pending;

    // Dispute details
    [Id(4)] public decimal Amount { get; set; }
    [Id(5)] public string ReasonCode { get; set; } = "";
    [Id(6)] public string ReasonDescription { get; set; } = "";
    [Id(7)] public string ProcessorReference { get; set; } = "";
    [Id(8)] public DateTime DisputeDeadline { get; set; }

    // Evidence and notes
    [Id(9)] public List<ChargebackEvidence> Evidence { get; set; } = [];
    [Id(10)] public List<ChargebackNote> Notes { get; set; } = [];

    // Resolution
    [Id(11)] public ChargebackResolution? Resolution { get; set; }
    [Id(12)] public decimal? FinalAmount { get; set; }
    [Id(13)] public string? ProcessorResolutionCode { get; set; }
    [Id(14)] public string? DisputeReference { get; set; }

    // Timestamps
    [Id(15)] public DateTime ReceivedAt { get; set; }
    [Id(16)] public DateTime? AcknowledgedAt { get; set; }
    [Id(17)] public DateTime? DisputedAt { get; set; }
    [Id(18)] public DateTime? ResolvedAt { get; set; }

    // Users
    [Id(19)] public Guid? AcknowledgedBy { get; set; }
    [Id(20)] public Guid? DisputedBy { get; set; }
    [Id(21)] public Guid? AcceptedBy { get; set; }

    /// <summary>
    /// Gets days remaining until deadline.
    /// </summary>
    public int DaysUntilDeadline => Math.Max(0, (DisputeDeadline.Date - DateTime.UtcNow.Date).Days);

    /// <summary>
    /// Checks if deadline is approaching (within 3 days).
    /// </summary>
    public bool IsDeadlineApproaching => DaysUntilDeadline <= 3 && DaysUntilDeadline > 0;

    /// <summary>
    /// Checks if deadline has passed.
    /// </summary>
    public bool IsDeadlinePassed => DisputeDeadline < DateTime.UtcNow;
}
