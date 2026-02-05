namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for print job events.
/// </summary>
public interface IPrintJobEvent
{
    Guid JobId { get; }
    DateTime OccurredAt { get; }
}

/// <summary>
/// Event raised when a print job is queued.
/// </summary>
[GenerateSerializer]
public record PrintJobQueued : IPrintJobEvent
{
    [Id(0)] public Guid JobId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid DeviceId { get; init; }
    [Id(3)] public Guid PrinterId { get; init; }
    [Id(4)] public DarkVelocity.Host.Grains.PrintJobType JobType { get; init; }
    [Id(5)] public string Content { get; init; } = string.Empty;
    [Id(6)] public int Copies { get; init; }
    [Id(7)] public int Priority { get; init; }
    [Id(8)] public Guid? SourceOrderId { get; init; }
    [Id(9)] public string? SourceReference { get; init; }
    [Id(10)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event raised when a print job starts printing.
/// </summary>
[GenerateSerializer]
public record PrintJobStarted : IPrintJobEvent
{
    [Id(0)] public Guid JobId { get; init; }
    [Id(1)] public string? PrinterResponse { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event raised when a print job completes successfully.
/// </summary>
[GenerateSerializer]
public record PrintJobCompleted : IPrintJobEvent
{
    [Id(0)] public Guid JobId { get; init; }
    [Id(1)] public string? PrinterResponse { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event raised when a print job fails.
/// </summary>
[GenerateSerializer]
public record PrintJobFailed : IPrintJobEvent
{
    [Id(0)] public Guid JobId { get; init; }
    [Id(1)] public string ErrorMessage { get; init; } = string.Empty;
    [Id(2)] public string? ErrorCode { get; init; }
    [Id(3)] public int RetryCount { get; init; }
    [Id(4)] public DateTime? NextRetryAt { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event raised when a print job is retried.
/// </summary>
[GenerateSerializer]
public record PrintJobRetried : IPrintJobEvent
{
    [Id(0)] public Guid JobId { get; init; }
    [Id(1)] public int RetryAttempt { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

/// <summary>
/// Event raised when a print job is cancelled.
/// </summary>
[GenerateSerializer]
public record PrintJobCancelled : IPrintJobEvent
{
    [Id(0)] public Guid JobId { get; init; }
    [Id(1)] public string? Reason { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}
