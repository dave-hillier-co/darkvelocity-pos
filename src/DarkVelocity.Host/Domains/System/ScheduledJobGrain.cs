using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Domains.System;

/// <summary>
/// Grain for managing a scheduled background job.
/// Uses Orleans Reminders for persistence across silo restarts.
/// </summary>
public class ScheduledJobGrain : Grain, IScheduledJobGrain
{
    private readonly IPersistentState<ScheduledJobState> _state;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ScheduledJobGrain> _logger;
    private IAsyncStream<IStreamEvent>? _jobStream;

    private const string ReminderName = "job-execution-reminder";
    private const int MaxExecutionHistory = 50;

    public ScheduledJobGrain(
        [PersistentState("scheduled-job", "OrleansStorage")]
        IPersistentState<ScheduledJobState> state,
        IGrainFactory grainFactory,
        ILogger<ScheduledJobGrain> logger)
    {
        _state = state;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IAsyncStream<IStreamEvent> GetJobStream()
    {
        if (_jobStream == null && _state.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.ScheduledJobStreamNamespace, _state.State.OrganizationId.ToString());
            _jobStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _jobStream!;
    }

    public async Task<ScheduledJob> ScheduleAsync(ScheduleOneTimeJobCommand command)
    {
        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var jobId = Guid.Parse(parts[2]);

        _state.State = CreateJobState(
            orgId, jobId, command.Name, command.Description,
            JobTriggerType.OneTime,
            command.TargetGrainType, command.TargetGrainKey, command.TargetMethodName,
            command.Parameters, command.MaxRetries);

        _state.State.NextRunAt = command.RunAt;

        await _state.WriteStateAsync();

        // Register reminder for execution
        await RegisterReminderIfNeeded();

        // Publish scheduled event
        await GetJobStream().OnNextAsync(new JobScheduledEvent(
            jobId, command.Name, command.RunAt.ToString("O"), command.RunAt)
        {
            OrganizationId = orgId
        });

        // Register with job registry
        await RegisterWithRegistry(orgId, jobId, command.Name, JobTriggerType.OneTime);

        return ToScheduledJob(_state.State);
    }

    public async Task<ScheduledJob> ScheduleAsync(ScheduleRecurringJobCommand command)
    {
        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var jobId = Guid.Parse(parts[2]);

        _state.State = CreateJobState(
            orgId, jobId, command.Name, command.Description,
            JobTriggerType.Recurring,
            command.TargetGrainType, command.TargetGrainKey, command.TargetMethodName,
            command.Parameters, command.MaxRetries);

        _state.State.Interval = command.Interval;
        _state.State.NextRunAt = command.StartAt ?? DateTime.UtcNow.Add(command.Interval);

        await _state.WriteStateAsync();

        // Register reminder for execution
        await RegisterReminderIfNeeded();

        // Publish scheduled event
        await GetJobStream().OnNextAsync(new JobScheduledEvent(
            jobId, command.Name, $"Every {command.Interval}", _state.State.NextRunAt.Value)
        {
            OrganizationId = orgId
        });

        // Register with job registry
        await RegisterWithRegistry(orgId, jobId, command.Name, JobTriggerType.Recurring);

        return ToScheduledJob(_state.State);
    }

    public async Task<ScheduledJob> ScheduleAsync(ScheduleCronJobCommand command)
    {
        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var jobId = Guid.Parse(parts[2]);

        _state.State = CreateJobState(
            orgId, jobId, command.Name, command.Description,
            JobTriggerType.Cron,
            command.TargetGrainType, command.TargetGrainKey, command.TargetMethodName,
            command.Parameters, command.MaxRetries);

        _state.State.CronExpression = command.CronExpression;
        _state.State.NextRunAt = CalculateNextCronRun(command.CronExpression, DateTime.UtcNow);

        await _state.WriteStateAsync();

        // Register reminder for execution
        await RegisterReminderIfNeeded();

        // Publish scheduled event
        await GetJobStream().OnNextAsync(new JobScheduledEvent(
            jobId, command.Name, command.CronExpression, _state.State.NextRunAt ?? DateTime.UtcNow)
        {
            OrganizationId = orgId
        });

        // Register with job registry
        await RegisterWithRegistry(orgId, jobId, command.Name, JobTriggerType.Cron);

        return ToScheduledJob(_state.State);
    }

    public Task<ScheduledJob?> GetJobAsync()
    {
        if (_state.State.JobId == Guid.Empty)
            return Task.FromResult<ScheduledJob?>(null);

        return Task.FromResult<ScheduledJob?>(ToScheduledJob(_state.State));
    }

    public Task<IReadOnlyList<JobExecution>> GetExecutionsAsync(int limit = 20)
    {
        var executions = _state.State.ExecutionHistory
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<JobExecution>>(executions);
    }

    public async Task CancelAsync(string? reason = null)
    {
        EnsureExists();

        _state.State.Status = JobStatus.Cancelled;
        _state.State.IsEnabled = false;

        // Unregister the reminder
        await UnregisterReminderIfExists();

        await _state.WriteStateAsync();

        // Publish cancelled event
        await GetJobStream().OnNextAsync(new JobCancelledEvent(
            _state.State.JobId, _state.State.Name, null, reason)
        {
            OrganizationId = _state.State.OrganizationId
        });

        // Update registry
        await UpdateRegistryStatus(JobStatus.Cancelled, null);

        _logger.LogInformation(
            "Job {JobId} '{JobName}' has been cancelled. Reason: {Reason}",
            _state.State.JobId, _state.State.Name, reason ?? "Not specified");
    }

    public async Task PauseAsync()
    {
        EnsureExists();

        _state.State.Status = JobStatus.Paused;

        // Unregister the reminder (keeps the schedule)
        await UnregisterReminderIfExists();

        await _state.WriteStateAsync();

        // Update registry
        await UpdateRegistryStatus(JobStatus.Paused, _state.State.NextRunAt);

        _logger.LogInformation(
            "Job {JobId} '{JobName}' has been paused",
            _state.State.JobId, _state.State.Name);
    }

    public async Task ResumeAsync()
    {
        EnsureExists();

        if (_state.State.Status != JobStatus.Paused)
            throw new InvalidOperationException($"Job is not paused. Current status: {_state.State.Status}");

        _state.State.Status = JobStatus.Scheduled;

        // Re-register the reminder
        await RegisterReminderIfNeeded();

        await _state.WriteStateAsync();

        // Update registry
        await UpdateRegistryStatus(JobStatus.Scheduled, _state.State.NextRunAt);

        _logger.LogInformation(
            "Job {JobId} '{JobName}' has been resumed",
            _state.State.JobId, _state.State.Name);
    }

    public async Task<JobExecution> TriggerAsync()
    {
        EnsureExists();

        return await ExecuteJobAsync();
    }

    public async Task UpdateScheduleAsync(TimeSpan? interval = null, string? cronExpression = null)
    {
        EnsureExists();

        if (interval.HasValue)
        {
            if (_state.State.TriggerType != JobTriggerType.Recurring)
                throw new InvalidOperationException("Can only update interval for recurring jobs");

            _state.State.Interval = interval.Value;
            _state.State.NextRunAt = DateTime.UtcNow.Add(interval.Value);
        }

        if (cronExpression != null)
        {
            if (_state.State.TriggerType != JobTriggerType.Cron)
                throw new InvalidOperationException("Can only update cron expression for cron jobs");

            _state.State.CronExpression = cronExpression;
            _state.State.NextRunAt = CalculateNextCronRun(cronExpression, DateTime.UtcNow);
        }

        // Re-register the reminder with new schedule
        await UnregisterReminderIfExists();
        await RegisterReminderIfNeeded();

        await _state.WriteStateAsync();

        // Update registry
        await UpdateRegistryStatus(_state.State.Status, _state.State.NextRunAt);
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.JobId != Guid.Empty);

    // IRemindable implementation
    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName != ReminderName)
            return;

        if (!_state.State.IsEnabled || _state.State.Status == JobStatus.Paused || _state.State.Status == JobStatus.Cancelled)
            return;

        _logger.LogInformation(
            "Reminder triggered for job {JobId} '{JobName}'",
            _state.State.JobId, _state.State.Name);

        await ExecuteJobAsync();

        // Schedule next execution based on trigger type
        if (_state.State.TriggerType == JobTriggerType.Recurring && _state.State.Interval.HasValue)
        {
            _state.State.NextRunAt = DateTime.UtcNow.Add(_state.State.Interval.Value);
            await _state.WriteStateAsync();
            await UpdateRegistryStatus(_state.State.Status, _state.State.NextRunAt);
        }
        else if (_state.State.TriggerType == JobTriggerType.Cron && _state.State.CronExpression != null)
        {
            _state.State.NextRunAt = CalculateNextCronRun(_state.State.CronExpression, DateTime.UtcNow);
            await _state.WriteStateAsync();
            await UpdateRegistryStatus(_state.State.Status, _state.State.NextRunAt);
        }
        else if (_state.State.TriggerType == JobTriggerType.OneTime)
        {
            // One-time job completed
            _state.State.Status = JobStatus.Completed;
            _state.State.IsEnabled = false;
            await UnregisterReminderIfExists();
            await _state.WriteStateAsync();
            await UpdateRegistryStatus(JobStatus.Completed, null);
        }
    }

    // ============================================================================
    // Private Helper Methods
    // ============================================================================

    private static ScheduledJobState CreateJobState(
        Guid orgId, Guid jobId, string name, string description,
        JobTriggerType triggerType,
        string targetGrainType, string targetGrainKey, string targetMethodName,
        Dictionary<string, string>? parameters, int maxRetries)
    {
        return new ScheduledJobState
        {
            OrganizationId = orgId,
            JobId = jobId,
            Name = name,
            Description = description,
            TriggerType = triggerType,
            Status = JobStatus.Scheduled,
            TargetGrainType = targetGrainType,
            TargetGrainKey = targetGrainKey,
            TargetMethodName = targetMethodName,
            Parameters = parameters,
            CreatedAt = DateTime.UtcNow,
            IsEnabled = true,
            MaxRetries = maxRetries,
            Version = 1
        };
    }

    private async Task<JobExecution> ExecuteJobAsync()
    {
        var executionId = Guid.NewGuid();
        var startTime = DateTime.UtcNow;

        _state.State.Status = JobStatus.Running;
        _state.State.LastRunAt = startTime;
        await _state.WriteStateAsync();

        // Publish started event
        await GetJobStream().OnNextAsync(new JobStartedEvent(
            _state.State.JobId, _state.State.Name, executionId)
        {
            OrganizationId = _state.State.OrganizationId
        });

        bool success;
        string? errorMessage = null;

        try
        {
            // Execute the target grain method
            await InvokeTargetGrainAsync();
            success = true;
            _state.State.ExecutionCount++;

            _logger.LogInformation(
                "Job {JobId} '{JobName}' execution {ExecutionId} completed successfully",
                _state.State.JobId, _state.State.Name, executionId);
        }
        catch (Exception ex)
        {
            success = false;
            errorMessage = ex.Message;
            _state.State.FailureCount++;

            _logger.LogError(ex,
                "Job {JobId} '{JobName}' execution {ExecutionId} failed",
                _state.State.JobId, _state.State.Name, executionId);
        }

        var completedAt = DateTime.UtcNow;
        var durationMs = (int)(completedAt - startTime).TotalMilliseconds;

        var execution = new JobExecution
        {
            ExecutionId = executionId,
            JobId = _state.State.JobId,
            StartedAt = startTime,
            CompletedAt = completedAt,
            Success = success,
            ErrorMessage = errorMessage,
            DurationMs = durationMs
        };

        // Add to execution history
        _state.State.ExecutionHistory.Insert(0, execution);
        if (_state.State.ExecutionHistory.Count > MaxExecutionHistory)
        {
            _state.State.ExecutionHistory.RemoveAt(_state.State.ExecutionHistory.Count - 1);
        }

        _state.State.Status = success ? JobStatus.Scheduled : JobStatus.Failed;

        await _state.WriteStateAsync();

        // Publish completed event
        await GetJobStream().OnNextAsync(new JobCompletedEvent(
            _state.State.JobId, _state.State.Name, executionId, success, errorMessage, durationMs)
        {
            OrganizationId = _state.State.OrganizationId
        });

        return execution;
    }

    private async Task InvokeTargetGrainAsync()
    {
        // Note: In a real implementation, you would use reflection or a more sophisticated
        // mechanism to invoke the target grain method. This is a simplified version.
        // For production, consider using a job handler interface pattern.

        _logger.LogDebug(
            "Invoking {GrainType}.{MethodName} on grain key {GrainKey}",
            _state.State.TargetGrainType, _state.State.TargetMethodName, _state.State.TargetGrainKey);

        // For now, we just log the invocation. In production, you would:
        // 1. Use GrainFactory to get the grain by type and key
        // 2. Use reflection or a predefined interface to invoke the method
        // 3. Pass any parameters from _state.State.Parameters

        // Simulate some work
        await Task.Delay(100);
    }

    private async Task RegisterReminderIfNeeded()
    {
        if (!_state.State.NextRunAt.HasValue)
            return;

        var dueTime = _state.State.NextRunAt.Value - DateTime.UtcNow;
        if (dueTime < TimeSpan.Zero)
            dueTime = TimeSpan.FromSeconds(1); // Run immediately if past due

        var period = _state.State.TriggerType switch
        {
            JobTriggerType.Recurring => _state.State.Interval ?? TimeSpan.FromMinutes(1),
            JobTriggerType.OneTime => TimeSpan.FromMilliseconds(-1), // No repeat
            _ => TimeSpan.FromMinutes(1) // For cron, we'll re-register after each execution
        };

        // Orleans reminders require a minimum period of 1 minute
        if (period > TimeSpan.Zero && period < TimeSpan.FromMinutes(1))
            period = TimeSpan.FromMinutes(1);

        await this.RegisterOrUpdateReminder(ReminderName, dueTime, period);

        _logger.LogDebug(
            "Registered reminder for job {JobId}. DueTime: {DueTime}, Period: {Period}",
            _state.State.JobId, dueTime, period);
    }

    private async Task UnregisterReminderIfExists()
    {
        try
        {
            var reminder = await this.GetReminder(ReminderName);
            if (reminder != null)
            {
                await this.UnregisterReminder(reminder);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unregister reminder for job {JobId}", _state.State.JobId);
        }
    }

    private async Task RegisterWithRegistry(Guid orgId, Guid jobId, string name, JobTriggerType triggerType)
    {
        try
        {
            var registry = _grainFactory.GetGrain<IJobRegistryGrain>($"{orgId}:job-registry");
            await registry.RegisterJobAsync(jobId, name, triggerType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to register job {JobId} with registry", jobId);
        }
    }

    private async Task UpdateRegistryStatus(JobStatus status, DateTime? nextRunAt)
    {
        try
        {
            var registry = _grainFactory.GetGrain<IJobRegistryGrain>($"{_state.State.OrganizationId}:job-registry");
            await registry.UpdateJobStatusAsync(_state.State.JobId, status, nextRunAt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update job {JobId} status in registry", _state.State.JobId);
        }
    }

    private static DateTime? CalculateNextCronRun(string cronExpression, DateTime from)
    {
        // Simplified cron parsing - in production use a library like NCrontab
        // This handles basic patterns like "0 * * * *" (hourly), "0 0 * * *" (daily)
        try
        {
            var parts = cronExpression.Split(' ');
            if (parts.Length < 5)
                return from.AddHours(1); // Default to hourly

            // Very basic parsing for common patterns
            var minute = parts[0] == "*" ? from.Minute : int.Parse(parts[0]);
            var hour = parts[1] == "*" ? from.Hour : int.Parse(parts[1]);

            var next = new DateTime(from.Year, from.Month, from.Day, hour, minute, 0, DateTimeKind.Utc);

            // If the time has passed today, move to tomorrow
            if (next <= from)
            {
                if (parts[1] == "*")
                    next = next.AddHours(1);
                else
                    next = next.AddDays(1);
            }

            return next;
        }
        catch
        {
            // On any parse error, default to 1 hour from now
            return from.AddHours(1);
        }
    }

    private static ScheduledJob ToScheduledJob(ScheduledJobState state) => new()
    {
        JobId = state.JobId,
        Name = state.Name,
        Description = state.Description,
        TriggerType = state.TriggerType,
        Status = state.Status,
        TargetGrainType = state.TargetGrainType,
        TargetGrainKey = state.TargetGrainKey,
        TargetMethodName = state.TargetMethodName,
        Parameters = state.Parameters,
        CronExpression = state.CronExpression,
        Interval = state.Interval,
        CreatedAt = state.CreatedAt,
        NextRunAt = state.NextRunAt,
        LastRunAt = state.LastRunAt,
        ExecutionCount = state.ExecutionCount,
        FailureCount = state.FailureCount,
        MaxRetries = state.MaxRetries,
        IsEnabled = state.IsEnabled
    };

    private void EnsureExists()
    {
        if (_state.State.JobId == Guid.Empty)
            throw new InvalidOperationException("Scheduled job does not exist");
    }
}

/// <summary>
/// State for the scheduled job grain.
/// </summary>
[GenerateSerializer]
public sealed class ScheduledJobState
{
    [Id(0)] public Guid OrganizationId { get; set; }
    [Id(1)] public Guid JobId { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string Description { get; set; } = string.Empty;
    [Id(4)] public JobTriggerType TriggerType { get; set; }
    [Id(5)] public JobStatus Status { get; set; }
    [Id(6)] public string TargetGrainType { get; set; } = string.Empty;
    [Id(7)] public string TargetGrainKey { get; set; } = string.Empty;
    [Id(8)] public string TargetMethodName { get; set; } = string.Empty;
    [Id(9)] public Dictionary<string, string>? Parameters { get; set; }
    [Id(10)] public string? CronExpression { get; set; }
    [Id(11)] public TimeSpan? Interval { get; set; }
    [Id(12)] public DateTime CreatedAt { get; set; }
    [Id(13)] public DateTime? NextRunAt { get; set; }
    [Id(14)] public DateTime? LastRunAt { get; set; }
    [Id(15)] public int ExecutionCount { get; set; }
    [Id(16)] public int FailureCount { get; set; }
    [Id(17)] public int MaxRetries { get; set; }
    [Id(18)] public bool IsEnabled { get; set; }
    [Id(19)] public List<JobExecution> ExecutionHistory { get; set; } = [];
    [Id(20)] public int Version { get; set; }
}
