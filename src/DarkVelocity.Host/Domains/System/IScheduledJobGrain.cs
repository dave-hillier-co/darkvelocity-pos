namespace DarkVelocity.Host.Domains.System;

// ============================================================================
// Job Types and Enums
// ============================================================================

public enum JobStatus
{
    Pending,
    Scheduled,
    Running,
    Completed,
    Failed,
    Cancelled,
    Paused
}

public enum JobTriggerType
{
    OneTime,
    Recurring,
    Cron
}

// ============================================================================
// Job Records
// ============================================================================

[GenerateSerializer]
public record ScheduledJob
{
    [Id(0)] public required Guid JobId { get; init; }
    [Id(1)] public required string Name { get; init; }
    [Id(2)] public required string Description { get; init; }
    [Id(3)] public required JobTriggerType TriggerType { get; init; }
    [Id(4)] public required JobStatus Status { get; init; }
    [Id(5)] public required string TargetGrainType { get; init; }
    [Id(6)] public required string TargetGrainKey { get; init; }
    [Id(7)] public required string TargetMethodName { get; init; }
    [Id(8)] public Dictionary<string, string>? Parameters { get; init; }
    [Id(9)] public string? CronExpression { get; init; }
    [Id(10)] public TimeSpan? Interval { get; init; }
    [Id(11)] public DateTime CreatedAt { get; init; }
    [Id(12)] public DateTime? NextRunAt { get; init; }
    [Id(13)] public DateTime? LastRunAt { get; init; }
    [Id(14)] public int ExecutionCount { get; init; }
    [Id(15)] public int FailureCount { get; init; }
    [Id(16)] public int MaxRetries { get; init; }
    [Id(17)] public bool IsEnabled { get; init; }
}

[GenerateSerializer]
public record JobExecution
{
    [Id(0)] public required Guid ExecutionId { get; init; }
    [Id(1)] public required Guid JobId { get; init; }
    [Id(2)] public required DateTime StartedAt { get; init; }
    [Id(3)] public DateTime? CompletedAt { get; init; }
    [Id(4)] public required bool Success { get; init; }
    [Id(5)] public string? ErrorMessage { get; init; }
    [Id(6)] public int DurationMs { get; init; }
}

// ============================================================================
// Job Commands
// ============================================================================

[GenerateSerializer]
public record ScheduleJobCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] string Description,
    [property: Id(2)] string TargetGrainType,
    [property: Id(3)] string TargetGrainKey,
    [property: Id(4)] string TargetMethodName,
    [property: Id(5)] Dictionary<string, string>? Parameters = null,
    [property: Id(6)] int MaxRetries = 3);

[GenerateSerializer]
public record ScheduleOneTimeJobCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] string Description,
    [property: Id(2)] string TargetGrainType,
    [property: Id(3)] string TargetGrainKey,
    [property: Id(4)] string TargetMethodName,
    [property: Id(5)] DateTime RunAt,
    [property: Id(6)] Dictionary<string, string>? Parameters = null,
    [property: Id(7)] int MaxRetries = 3);

[GenerateSerializer]
public record ScheduleRecurringJobCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] string Description,
    [property: Id(2)] string TargetGrainType,
    [property: Id(3)] string TargetGrainKey,
    [property: Id(4)] string TargetMethodName,
    [property: Id(5)] TimeSpan Interval,
    [property: Id(6)] DateTime? StartAt = null,
    [property: Id(7)] Dictionary<string, string>? Parameters = null,
    [property: Id(8)] int MaxRetries = 3);

[GenerateSerializer]
public record ScheduleCronJobCommand(
    [property: Id(0)] string Name,
    [property: Id(1)] string Description,
    [property: Id(2)] string TargetGrainType,
    [property: Id(3)] string TargetGrainKey,
    [property: Id(4)] string TargetMethodName,
    [property: Id(5)] string CronExpression,
    [property: Id(6)] Dictionary<string, string>? Parameters = null,
    [property: Id(7)] int MaxRetries = 3);

// ============================================================================
// Scheduled Job Grain Interface
// ============================================================================

/// <summary>
/// Grain for managing scheduled background jobs.
/// Uses Orleans Reminders for persistence across silo restarts.
/// Key: "{orgId}:jobs:{jobId}"
/// </summary>
public interface IScheduledJobGrain : IGrainWithStringKey, IRemindable
{
    /// <summary>
    /// Schedules a one-time job to run at a specific time.
    /// </summary>
    Task<ScheduledJob> ScheduleAsync(ScheduleOneTimeJobCommand command);

    /// <summary>
    /// Schedules a recurring job to run at a regular interval.
    /// </summary>
    Task<ScheduledJob> ScheduleAsync(ScheduleRecurringJobCommand command);

    /// <summary>
    /// Schedules a job using a cron expression.
    /// </summary>
    Task<ScheduledJob> ScheduleAsync(ScheduleCronJobCommand command);

    /// <summary>
    /// Gets the current job details.
    /// </summary>
    Task<ScheduledJob?> GetJobAsync();

    /// <summary>
    /// Gets recent job executions.
    /// </summary>
    Task<IReadOnlyList<JobExecution>> GetExecutionsAsync(int limit = 20);

    /// <summary>
    /// Cancels the scheduled job.
    /// </summary>
    Task CancelAsync(string? reason = null);

    /// <summary>
    /// Pauses the job (keeps the schedule but doesn't execute).
    /// </summary>
    Task PauseAsync();

    /// <summary>
    /// Resumes a paused job.
    /// </summary>
    Task ResumeAsync();

    /// <summary>
    /// Manually triggers the job to run now.
    /// </summary>
    Task<JobExecution> TriggerAsync();

    /// <summary>
    /// Updates the job schedule (for recurring jobs).
    /// </summary>
    Task UpdateScheduleAsync(TimeSpan? interval = null, string? cronExpression = null);

    /// <summary>
    /// Checks if the job exists.
    /// </summary>
    Task<bool> ExistsAsync();
}

// ============================================================================
// Job Registry Grain Interface
// ============================================================================

/// <summary>
/// Registry grain for tracking all scheduled jobs in an organization.
/// Key: "{orgId}:job-registry"
/// </summary>
public interface IJobRegistryGrain : IGrainWithStringKey
{
    /// <summary>
    /// Initializes the registry for an organization.
    /// </summary>
    Task InitializeAsync(Guid orgId);

    /// <summary>
    /// Registers a new job.
    /// </summary>
    Task RegisterJobAsync(Guid jobId, string name, JobTriggerType triggerType);

    /// <summary>
    /// Unregisters a job.
    /// </summary>
    Task UnregisterJobAsync(Guid jobId);

    /// <summary>
    /// Gets all registered jobs.
    /// </summary>
    Task<IReadOnlyList<JobRegistryEntry>> GetJobsAsync(JobStatus? status = null);

    /// <summary>
    /// Updates job status in the registry.
    /// </summary>
    Task UpdateJobStatusAsync(Guid jobId, JobStatus status, DateTime? nextRunAt = null);

    /// <summary>
    /// Checks if the registry exists.
    /// </summary>
    Task<bool> ExistsAsync();
}

[GenerateSerializer]
public record JobRegistryEntry
{
    [Id(0)] public required Guid JobId { get; init; }
    [Id(1)] public required string Name { get; init; }
    [Id(2)] public required JobTriggerType TriggerType { get; init; }
    [Id(3)] public required JobStatus Status { get; init; }
    [Id(4)] public DateTime? NextRunAt { get; init; }
    [Id(5)] public DateTime? LastRunAt { get; init; }
}
