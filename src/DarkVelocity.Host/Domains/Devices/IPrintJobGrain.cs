namespace DarkVelocity.Host.Grains;

// ============================================================================
// Print Job Types
// ============================================================================

public enum PrintJobType
{
    Receipt,
    KitchenTicket,
    Label,
    Report
}

public enum PrintJobStatus
{
    Pending,
    Printing,
    Completed,
    Failed,
    Cancelled
}

// ============================================================================
// Print Job Commands
// ============================================================================

[GenerateSerializer]
public record QueuePrintJobCommand(
    [property: Id(0)] Guid PrinterId,
    [property: Id(1)] PrintJobType JobType,
    [property: Id(2)] string Content,
    [property: Id(3)] int Copies = 1,
    [property: Id(4)] int Priority = 0,
    [property: Id(5)] Guid? SourceOrderId = null,
    [property: Id(6)] string? SourceReference = null);

[GenerateSerializer]
public record StartPrintJobCommand(
    [property: Id(0)] string? PrinterResponse = null);

[GenerateSerializer]
public record CompletePrintJobCommand(
    [property: Id(0)] string? PrinterResponse = null);

[GenerateSerializer]
public record FailPrintJobCommand(
    [property: Id(0)] string ErrorMessage,
    [property: Id(1)] string? ErrorCode = null);

// ============================================================================
// Print Job Snapshots
// ============================================================================

[GenerateSerializer]
public record PrintJobSnapshot(
    [property: Id(0)] Guid JobId,
    [property: Id(1)] Guid DeviceId,
    [property: Id(2)] Guid PrinterId,
    [property: Id(3)] PrintJobType JobType,
    [property: Id(4)] PrintJobStatus Status,
    [property: Id(5)] string Content,
    [property: Id(6)] int Copies,
    [property: Id(7)] int Priority,
    [property: Id(8)] int RetryCount,
    [property: Id(9)] int MaxRetries,
    [property: Id(10)] DateTime QueuedAt,
    [property: Id(11)] DateTime? StartedAt,
    [property: Id(12)] DateTime? CompletedAt,
    [property: Id(13)] DateTime? FailedAt,
    [property: Id(14)] DateTime? NextRetryAt,
    [property: Id(15)] string? LastError,
    [property: Id(16)] Guid? SourceOrderId,
    [property: Id(17)] string? SourceReference);

[GenerateSerializer]
public record PrintQueueSummary(
    [property: Id(0)] int PendingJobs,
    [property: Id(1)] int PrintingJobs,
    [property: Id(2)] int CompletedJobs,
    [property: Id(3)] int FailedJobs,
    [property: Id(4)] IReadOnlyList<PrintJobSnapshot> ActiveJobs);

// ============================================================================
// Print Job Grain Interface
// ============================================================================

/// <summary>
/// Grain for managing a print job with queue and retry logic.
/// Key: "{orgId}:device:{deviceId}:printjob:{jobId}"
/// </summary>
public interface IPrintJobGrain : IGrainWithStringKey
{
    /// <summary>
    /// Queues a new print job.
    /// </summary>
    Task<PrintJobSnapshot> QueueAsync(QueuePrintJobCommand command);

    /// <summary>
    /// Marks the job as started (printing).
    /// </summary>
    Task<PrintJobSnapshot> StartAsync(StartPrintJobCommand command);

    /// <summary>
    /// Marks the job as completed successfully.
    /// </summary>
    Task<PrintJobSnapshot> CompleteAsync(CompletePrintJobCommand command);

    /// <summary>
    /// Marks the job as failed and schedules retry if applicable.
    /// </summary>
    Task<PrintJobSnapshot> FailAsync(FailPrintJobCommand command);

    /// <summary>
    /// Retries a failed job.
    /// </summary>
    Task<PrintJobSnapshot> RetryAsync();

    /// <summary>
    /// Cancels a pending or failed job.
    /// </summary>
    Task CancelAsync(string? reason = null);

    /// <summary>
    /// Gets the current job snapshot.
    /// </summary>
    Task<PrintJobSnapshot> GetSnapshotAsync();

    /// <summary>
    /// Checks if the job exists.
    /// </summary>
    Task<bool> ExistsAsync();

    /// <summary>
    /// Gets the current status.
    /// </summary>
    Task<PrintJobStatus> GetStatusAsync();
}

// ============================================================================
// Device Print Queue Grain Interface
// ============================================================================

/// <summary>
/// Grain for managing the print queue for a device.
/// Key: "{orgId}:device:{deviceId}:printqueue"
/// </summary>
public interface IDevicePrintQueueGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initializes the queue for a device.
    /// </summary>
    Task InitializeAsync(Guid deviceId);

    /// <summary>
    /// Adds a job to the queue and creates the print job grain.
    /// </summary>
    Task<PrintJobSnapshot> EnqueueAsync(QueuePrintJobCommand command);

    /// <summary>
    /// Gets the next pending job from the queue.
    /// </summary>
    Task<PrintJobSnapshot?> DequeueAsync();

    /// <summary>
    /// Gets all pending jobs in priority order.
    /// </summary>
    Task<IReadOnlyList<PrintJobSnapshot>> GetPendingJobsAsync();

    /// <summary>
    /// Gets queue summary.
    /// </summary>
    Task<PrintQueueSummary> GetSummaryAsync();

    /// <summary>
    /// Gets job history (completed and failed).
    /// </summary>
    Task<IReadOnlyList<PrintJobSnapshot>> GetHistoryAsync(int limit = 50);

    /// <summary>
    /// Clears completed jobs from history.
    /// </summary>
    Task ClearHistoryAsync();

    /// <summary>
    /// Notifies the queue that a job status has changed.
    /// </summary>
    Task NotifyJobStatusChangedAsync(Guid jobId, PrintJobStatus newStatus);
}
