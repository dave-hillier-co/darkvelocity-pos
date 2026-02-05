namespace DarkVelocity.Host.State;

public enum OfflinePaymentStatus
{
    Queued,
    Retrying,
    Processed,
    Failed,
    Cancelled
}

[GenerateSerializer]
public record QueuedPaymentEntry
{
    [Id(0)] public Guid QueueEntryId { get; init; }
    [Id(1)] public Guid PaymentId { get; init; }
    [Id(2)] public Guid OrderId { get; init; }
    [Id(3)] public PaymentMethod Method { get; init; }
    [Id(4)] public decimal Amount { get; init; }
    [Id(5)] public string PaymentData { get; init; } = "";
    [Id(6)] public OfflinePaymentStatus Status { get; init; }
    [Id(7)] public string QueueReason { get; init; } = "";
    [Id(8)] public int AttemptCount { get; init; }
    [Id(9)] public DateTime? NextRetryAt { get; init; }
    [Id(10)] public DateTime QueuedAt { get; init; }
    [Id(11)] public DateTime? ProcessedAt { get; init; }
    [Id(12)] public string? LastErrorCode { get; init; }
    [Id(13)] public string? LastErrorMessage { get; init; }
    [Id(14)] public string? GatewayReference { get; init; }
}

[GenerateSerializer]
public record RetryAttempt
{
    [Id(0)] public Guid QueueEntryId { get; init; }
    [Id(1)] public int AttemptNumber { get; init; }
    [Id(2)] public bool Success { get; init; }
    [Id(3)] public string? ErrorCode { get; init; }
    [Id(4)] public string? ErrorMessage { get; init; }
    [Id(5)] public DateTime AttemptedAt { get; init; }
}

[GenerateSerializer]
public sealed class OfflinePaymentQueueState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid SiteId { get; set; }
    [Id(2)] public bool IsInitialized { get; set; }

    [Id(3)] public List<QueuedPaymentEntry> QueuedPayments { get; set; } = [];
    [Id(4)] public List<RetryAttempt> RetryHistory { get; set; } = [];

    // Configuration
    [Id(5)] public int MaxRetryAttempts { get; set; } = 5;
    [Id(6)] public int BaseRetryDelaySeconds { get; set; } = 30;
    [Id(7)] public double RetryBackoffMultiplier { get; set; } = 2.0;
    [Id(8)] public int MaxRetryDelaySeconds { get; set; } = 3600; // 1 hour max

    // Statistics
    [Id(9)] public int TotalQueued { get; set; }
    [Id(10)] public int TotalProcessed { get; set; }
    [Id(11)] public int TotalFailed { get; set; }

    /// <summary>
    /// Calculate next retry delay using exponential backoff.
    /// </summary>
    public int CalculateRetryDelay(int attemptNumber)
    {
        var delay = (int)(BaseRetryDelaySeconds * Math.Pow(RetryBackoffMultiplier, attemptNumber - 1));
        return Math.Min(delay, MaxRetryDelaySeconds);
    }

    /// <summary>
    /// Get all payments pending retry.
    /// </summary>
    public List<QueuedPaymentEntry> GetPendingRetries(DateTime now)
    {
        return QueuedPayments
            .Where(p => p.Status == OfflinePaymentStatus.Queued || p.Status == OfflinePaymentStatus.Retrying)
            .Where(p => !p.NextRetryAt.HasValue || p.NextRetryAt <= now)
            .OrderBy(p => p.QueuedAt)
            .ToList();
    }
}
