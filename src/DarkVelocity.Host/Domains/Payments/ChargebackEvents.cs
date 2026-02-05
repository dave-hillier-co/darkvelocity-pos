namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Chargeback events used in event sourcing.
/// </summary>
public interface IChargebackEvent
{
    Guid ChargebackId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record ChargebackReceived : IChargebackEvent
{
    [Id(0)] public Guid ChargebackId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid PaymentId { get; init; }
    [Id(3)] public decimal Amount { get; init; }
    [Id(4)] public string ReasonCode { get; init; } = "";
    [Id(5)] public string ReasonDescription { get; init; } = "";
    [Id(6)] public string ProcessorReference { get; init; } = "";
    [Id(7)] public DateTime DisputeDeadline { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ChargebackAcknowledged : IChargebackEvent
{
    [Id(0)] public Guid ChargebackId { get; init; }
    [Id(1)] public Guid AcknowledgedBy { get; init; }
    [Id(2)] public string Notes { get; init; } = "";
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ChargebackEvidenceUploaded : IChargebackEvent
{
    [Id(0)] public Guid ChargebackId { get; init; }
    [Id(1)] public Guid EvidenceId { get; init; }
    [Id(2)] public string EvidenceType { get; init; } = "";
    [Id(3)] public string Description { get; init; } = "";
    [Id(4)] public string FileReference { get; init; } = "";
    [Id(5)] public Guid UploadedBy { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ChargebackDisputed : IChargebackEvent
{
    [Id(0)] public Guid ChargebackId { get; init; }
    [Id(1)] public string DisputeReference { get; init; } = "";
    [Id(2)] public string DisputeNotes { get; init; } = "";
    [Id(3)] public Guid DisputedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ChargebackAccepted : IChargebackEvent
{
    [Id(0)] public Guid ChargebackId { get; init; }
    [Id(1)] public string AcceptanceNotes { get; init; } = "";
    [Id(2)] public Guid AcceptedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ChargebackResolved : IChargebackEvent
{
    [Id(0)] public Guid ChargebackId { get; init; }
    [Id(1)] public State.ChargebackResolution Resolution { get; init; }
    [Id(2)] public decimal FinalAmount { get; init; }
    [Id(3)] public string ProcessorResolutionCode { get; init; } = "";
    [Id(4)] public string Notes { get; init; } = "";
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ChargebackNoteAdded : IChargebackEvent
{
    [Id(0)] public Guid ChargebackId { get; init; }
    [Id(1)] public Guid NoteId { get; init; }
    [Id(2)] public string Content { get; init; } = "";
    [Id(3)] public Guid AddedBy { get; init; }
    [Id(4)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record ChargebackDeadlineApproaching : IChargebackEvent
{
    [Id(0)] public Guid ChargebackId { get; init; }
    [Id(1)] public int DaysRemaining { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}
