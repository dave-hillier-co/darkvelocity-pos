using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public record QueuePaymentCommand(
    [property: Id(0)] Guid PaymentId,
    [property: Id(1)] Guid OrderId,
    [property: Id(2)] PaymentMethod Method,
    [property: Id(3)] decimal Amount,
    [property: Id(4)] string PaymentData,
    [property: Id(5)] string QueueReason);

[GenerateSerializer]
public record PaymentQueuedResult(
    [property: Id(0)] Guid QueueEntryId,
    [property: Id(1)] DateTime QueuedAt,
    [property: Id(2)] DateTime? NextRetryAt);

[GenerateSerializer]
public record QueueStatistics
{
    [Id(0)] public int PendingCount { get; init; }
    [Id(1)] public int RetryingCount { get; init; }
    [Id(2)] public int TotalQueued { get; init; }
    [Id(3)] public int TotalProcessed { get; init; }
    [Id(4)] public int TotalFailed { get; init; }
    [Id(5)] public decimal PendingAmount { get; init; }
}

public interface IOfflinePaymentQueueGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initializes the queue for a site.
    /// </summary>
    Task InitializeAsync(Guid organizationId, Guid siteId);

    /// <summary>
    /// Queues a payment for later processing.
    /// </summary>
    Task<PaymentQueuedResult> QueuePaymentAsync(QueuePaymentCommand command);

    /// <summary>
    /// Processes all pending payments in the queue.
    /// Called when connectivity is restored.
    /// </summary>
    Task<int> ProcessQueueAsync();

    /// <summary>
    /// Processes a single queued payment.
    /// </summary>
    Task<bool> ProcessPaymentAsync(Guid queueEntryId);

    /// <summary>
    /// Records a successful payment processing.
    /// </summary>
    Task RecordSuccessAsync(Guid queueEntryId, string gatewayReference);

    /// <summary>
    /// Records a failed payment attempt.
    /// </summary>
    Task RecordFailureAsync(Guid queueEntryId, string errorCode, string errorMessage);

    /// <summary>
    /// Cancels a queued payment.
    /// </summary>
    Task CancelPaymentAsync(Guid queueEntryId, Guid cancelledBy, string reason);

    /// <summary>
    /// Gets a specific queued payment entry.
    /// </summary>
    Task<QueuedPaymentEntry?> GetPaymentEntryAsync(Guid queueEntryId);

    /// <summary>
    /// Gets all pending payments.
    /// </summary>
    Task<List<QueuedPaymentEntry>> GetPendingPaymentsAsync();

    /// <summary>
    /// Gets queue statistics.
    /// </summary>
    Task<QueueStatistics> GetStatisticsAsync();

    /// <summary>
    /// Gets the full state.
    /// </summary>
    Task<OfflinePaymentQueueState> GetStateAsync();

    /// <summary>
    /// Configures retry settings.
    /// </summary>
    Task ConfigureRetrySettingsAsync(int maxAttempts, int baseDelaySeconds, double backoffMultiplier);
}
