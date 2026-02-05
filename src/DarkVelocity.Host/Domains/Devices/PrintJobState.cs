using DarkVelocity.Host.Grains;

namespace DarkVelocity.Host.State;

/// <summary>
/// State for a print job grain.
/// </summary>
[GenerateSerializer]
public sealed class PrintJobState
{
    [Id(0)] public Guid JobId { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public Guid DeviceId { get; set; }
    [Id(3)] public Guid PrinterId { get; set; }
    [Id(4)] public PrintJobType JobType { get; set; }
    [Id(5)] public PrintJobStatus Status { get; set; } = PrintJobStatus.Pending;
    [Id(6)] public string Content { get; set; } = string.Empty;
    [Id(7)] public int Copies { get; set; } = 1;
    [Id(8)] public int Priority { get; set; }
    [Id(9)] public int RetryCount { get; set; }
    [Id(10)] public int MaxRetries { get; set; } = 3;
    [Id(11)] public DateTime QueuedAt { get; set; }
    [Id(12)] public DateTime? StartedAt { get; set; }
    [Id(13)] public DateTime? CompletedAt { get; set; }
    [Id(14)] public DateTime? FailedAt { get; set; }
    [Id(15)] public DateTime? NextRetryAt { get; set; }
    [Id(16)] public string? LastError { get; set; }
    [Id(17)] public string? LastErrorCode { get; set; }
    [Id(18)] public Guid? SourceOrderId { get; set; }
    [Id(19)] public string? SourceReference { get; set; }
    [Id(20)] public string? LastPrinterResponse { get; set; }
}

/// <summary>
/// State for a device print queue grain.
/// </summary>
[GenerateSerializer]
public sealed class DevicePrintQueueState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid DeviceId { get; set; }
    [Id(2)] public List<PrintQueueEntry> Queue { get; set; } = [];
    [Id(3)] public List<PrintQueueEntry> History { get; set; } = [];
    [Id(4)] public int MaxHistorySize { get; set; } = 100;
    [Id(5)] public bool Initialized { get; set; }
}

/// <summary>
/// An entry in the print queue.
/// </summary>
[GenerateSerializer]
public sealed class PrintQueueEntry
{
    [Id(0)] public Guid JobId { get; set; }
    [Id(1)] public Guid PrinterId { get; set; }
    [Id(2)] public PrintJobType JobType { get; set; }
    [Id(3)] public PrintJobStatus Status { get; set; }
    [Id(4)] public int Priority { get; set; }
    [Id(5)] public DateTime QueuedAt { get; set; }
    [Id(6)] public DateTime? CompletedAt { get; set; }
    [Id(7)] public Guid? SourceOrderId { get; set; }
    [Id(8)] public string? SourceReference { get; set; }
}
