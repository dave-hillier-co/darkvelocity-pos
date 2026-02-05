using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record OpenBatchCommand(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] DateOnly BusinessDate,
    [property: Id(3)] Guid OpenedBy);

[GenerateSerializer]
public record AddPaymentToBatchCommand(
    [property: Id(0)] Guid PaymentId,
    [property: Id(1)] decimal Amount,
    [property: Id(2)] PaymentMethod Method,
    [property: Id(3)] string? GatewayReference = null);

[GenerateSerializer]
public record CloseBatchCommand([property: Id(0)] Guid ClosedBy);

[GenerateSerializer]
public record SettleBatchCommand(
    [property: Id(0)] string SettlementReference,
    [property: Id(1)] decimal ProcessingFees);

[GenerateSerializer]
public record BatchOpenedResult([property: Id(0)] Guid BatchId, [property: Id(1)] string BatchNumber, [property: Id(2)] DateTime CreatedAt);

[GenerateSerializer]
public record BatchClosedResult([property: Id(0)] Guid BatchId, [property: Id(1)] decimal TotalAmount, [property: Id(2)] int PaymentCount);

[GenerateSerializer]
public record BatchSettledResult(
    [property: Id(0)] Guid BatchId,
    [property: Id(1)] string SettlementReference,
    [property: Id(2)] decimal SettledAmount,
    [property: Id(3)] decimal ProcessingFees,
    [property: Id(4)] decimal NetAmount);

[GenerateSerializer]
public record SettlementReport
{
    [Id(0)] public Guid BatchId { get; init; }
    [Id(1)] public string BatchNumber { get; init; } = "";
    [Id(2)] public DateOnly BusinessDate { get; init; }
    [Id(3)] public SettlementBatchStatus Status { get; init; }
    [Id(4)] public decimal TotalAmount { get; init; }
    [Id(5)] public int PaymentCount { get; init; }
    [Id(6)] public List<PaymentMethodTotal> TotalsByMethod { get; init; } = [];
    [Id(7)] public decimal ProcessingFees { get; init; }
    [Id(8)] public decimal NetAmount { get; init; }
    [Id(9)] public string? SettlementReference { get; init; }
    [Id(10)] public DateTime? SettledAt { get; init; }
}

public interface ISettlementBatchGrain : IGrainWithStringKey
{
    /// <summary>
    /// Opens a new settlement batch for a business day.
    /// </summary>
    Task<BatchOpenedResult> OpenAsync(OpenBatchCommand command);

    /// <summary>
    /// Gets the current state of the batch.
    /// </summary>
    Task<SettlementBatchState> GetStateAsync();

    /// <summary>
    /// Adds a payment to the batch.
    /// </summary>
    Task AddPaymentAsync(AddPaymentToBatchCommand command);

    /// <summary>
    /// Removes a payment from the batch.
    /// </summary>
    Task RemovePaymentAsync(Guid paymentId, string reason);

    /// <summary>
    /// Closes the batch for settlement.
    /// </summary>
    Task<BatchClosedResult> CloseAsync(CloseBatchCommand command);

    /// <summary>
    /// Reopens a closed batch for adjustments.
    /// </summary>
    Task ReopenAsync(Guid reopenedBy, string reason);

    /// <summary>
    /// Records successful settlement.
    /// </summary>
    Task<BatchSettledResult> RecordSettlementAsync(SettleBatchCommand command);

    /// <summary>
    /// Records a settlement failure.
    /// </summary>
    Task RecordSettlementFailureAsync(string errorCode, string errorMessage);

    /// <summary>
    /// Generates a settlement report.
    /// </summary>
    Task<SettlementReport> GetSettlementReportAsync();

    /// <summary>
    /// Gets totals grouped by payment method.
    /// </summary>
    Task<List<PaymentMethodTotal>> GetTotalsByMethodAsync();

    /// <summary>
    /// Checks if the batch exists.
    /// </summary>
    Task<bool> ExistsAsync();

    /// <summary>
    /// Gets the current status.
    /// </summary>
    Task<SettlementBatchStatus> GetStatusAsync();
}
