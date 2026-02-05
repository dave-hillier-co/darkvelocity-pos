using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record ReceiveChargebackCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid PaymentId,
    [property: Id(2)] decimal Amount,
    [property: Id(3)] string ReasonCode,
    [property: Id(4)] string ReasonDescription,
    [property: Id(5)] string ProcessorReference,
    [property: Id(6)] DateTime DisputeDeadline);

[GenerateSerializer]
public record UploadEvidenceCommand(
    [property: Id(0)] string EvidenceType,
    [property: Id(1)] string Description,
    [property: Id(2)] string FileReference,
    [property: Id(3)] Guid UploadedBy);

[GenerateSerializer]
public record DisputeChargebackCommand(
    [property: Id(0)] string DisputeNotes,
    [property: Id(1)] Guid DisputedBy);

[GenerateSerializer]
public record ResolveChargebackCommand(
    [property: Id(0)] ChargebackResolution Resolution,
    [property: Id(1)] decimal FinalAmount,
    [property: Id(2)] string ProcessorResolutionCode,
    [property: Id(3)] string Notes);

[GenerateSerializer]
public record ChargebackReceivedResult(
    [property: Id(0)] Guid ChargebackId,
    [property: Id(1)] DateTime DisputeDeadline,
    [property: Id(2)] int DaysToRespond);

[GenerateSerializer]
public record EvidenceUploadedResult(
    [property: Id(0)] Guid EvidenceId,
    [property: Id(1)] DateTime UploadedAt);

[GenerateSerializer]
public record ChargebackSummary
{
    [Id(0)] public Guid ChargebackId { get; init; }
    [Id(1)] public Guid PaymentId { get; init; }
    [Id(2)] public ChargebackStatus Status { get; init; }
    [Id(3)] public decimal Amount { get; init; }
    [Id(4)] public string ReasonCode { get; init; } = "";
    [Id(5)] public string ReasonDescription { get; init; } = "";
    [Id(6)] public DateTime DisputeDeadline { get; init; }
    [Id(7)] public int DaysUntilDeadline { get; init; }
    [Id(8)] public int EvidenceCount { get; init; }
    [Id(9)] public ChargebackResolution? Resolution { get; init; }
    [Id(10)] public decimal? FinalAmount { get; init; }
}

public interface IChargebackGrain : IGrainWithStringKey
{
    /// <summary>
    /// Records receipt of a chargeback from processor.
    /// Uses "Receive" naming as chargebacks are external facts, not commands.
    /// </summary>
    Task<ChargebackReceivedResult> ReceiveAsync(ReceiveChargebackCommand command);

    /// <summary>
    /// Gets the current state of the chargeback.
    /// </summary>
    Task<ChargebackState> GetStateAsync();

    /// <summary>
    /// Acknowledges the chargeback has been reviewed.
    /// </summary>
    Task AcknowledgeAsync(Guid acknowledgedBy, string notes);

    /// <summary>
    /// Uploads evidence to support dispute.
    /// </summary>
    Task<EvidenceUploadedResult> UploadEvidenceAsync(UploadEvidenceCommand command);

    /// <summary>
    /// Submits dispute to processor.
    /// </summary>
    Task DisputeAsync(DisputeChargebackCommand command);

    /// <summary>
    /// Accepts the chargeback (no dispute).
    /// </summary>
    Task AcceptAsync(Guid acceptedBy, string notes);

    /// <summary>
    /// Records the final resolution from processor.
    /// </summary>
    Task ResolveAsync(ResolveChargebackCommand command);

    /// <summary>
    /// Adds a note to the chargeback.
    /// </summary>
    Task AddNoteAsync(string content, Guid addedBy);

    /// <summary>
    /// Gets a summary of the chargeback.
    /// </summary>
    Task<ChargebackSummary> GetSummaryAsync();

    /// <summary>
    /// Gets all evidence uploaded for this chargeback.
    /// </summary>
    Task<List<ChargebackEvidence>> GetEvidenceAsync();

    /// <summary>
    /// Checks if the chargeback exists.
    /// </summary>
    Task<bool> ExistsAsync();

    /// <summary>
    /// Gets the current status.
    /// </summary>
    Task<ChargebackStatus> GetStatusAsync();

    /// <summary>
    /// Gets days until dispute deadline.
    /// </summary>
    Task<int> GetDaysUntilDeadlineAsync();
}
