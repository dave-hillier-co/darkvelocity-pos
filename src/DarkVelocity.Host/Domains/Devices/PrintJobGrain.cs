using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Journaled grain for print job management with event sourcing.
/// All state changes are recorded as events and can be replayed.
/// Key: "{orgId}:device:{deviceId}:printjob:{jobId}"
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class PrintJobGrain : JournaledGrain<PrintJobState, IPrintJobEvent>, IPrintJobGrain
{
    /// <summary>
    /// Applies an event to the grain state. This is the core of event sourcing.
    /// </summary>
    protected override void TransitionState(PrintJobState state, IPrintJobEvent @event)
    {
        switch (@event)
        {
            case PrintJobQueued e:
                state.JobId = e.JobId;
                state.OrganizationId = e.OrganizationId;
                state.DeviceId = e.DeviceId;
                state.PrinterId = e.PrinterId;
                state.JobType = e.JobType;
                state.Content = e.Content;
                state.Copies = e.Copies;
                state.Priority = e.Priority;
                state.SourceOrderId = e.SourceOrderId;
                state.SourceReference = e.SourceReference;
                state.Status = PrintJobStatus.Pending;
                state.QueuedAt = e.OccurredAt;
                state.RetryCount = 0;
                break;

            case PrintJobStarted e:
                state.Status = PrintJobStatus.Printing;
                state.StartedAt = e.OccurredAt;
                state.LastPrinterResponse = e.PrinterResponse;
                break;

            case PrintJobCompleted e:
                state.Status = PrintJobStatus.Completed;
                state.CompletedAt = e.OccurredAt;
                state.LastPrinterResponse = e.PrinterResponse;
                state.NextRetryAt = null;
                break;

            case PrintJobFailed e:
                state.Status = PrintJobStatus.Failed;
                state.FailedAt = e.OccurredAt;
                state.LastError = e.ErrorMessage;
                state.LastErrorCode = e.ErrorCode;
                state.RetryCount = e.RetryCount;
                state.NextRetryAt = e.NextRetryAt;
                break;

            case PrintJobRetried e:
                state.Status = PrintJobStatus.Pending;
                state.RetryCount = e.RetryAttempt;
                state.StartedAt = null;
                state.FailedAt = null;
                break;

            case PrintJobCancelled e:
                state.Status = PrintJobStatus.Cancelled;
                state.NextRetryAt = null;
                break;
        }
    }

    public async Task<PrintJobSnapshot> QueueAsync(QueuePrintJobCommand command)
    {
        if (State.JobId != Guid.Empty)
            throw new InvalidOperationException("Print job already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        // Key format: {orgId}:device:{deviceId}:printjob:{jobId}
        var orgId = Guid.Parse(parts[0]);
        var deviceId = Guid.Parse(parts[2]);
        var jobId = Guid.Parse(parts[4]);

        var now = DateTime.UtcNow;

        RaiseEvent(new PrintJobQueued
        {
            JobId = jobId,
            OrganizationId = orgId,
            DeviceId = deviceId,
            PrinterId = command.PrinterId,
            JobType = command.JobType,
            Content = command.Content,
            Copies = command.Copies,
            Priority = command.Priority,
            SourceOrderId = command.SourceOrderId,
            SourceReference = command.SourceReference,
            OccurredAt = now
        });

        await ConfirmEvents();
        return CreateSnapshot();
    }

    public async Task<PrintJobSnapshot> StartAsync(StartPrintJobCommand command)
    {
        EnsureExists();

        if (State.Status != PrintJobStatus.Pending)
            throw new InvalidOperationException($"Cannot start job in status {State.Status}");

        RaiseEvent(new PrintJobStarted
        {
            JobId = State.JobId,
            PrinterResponse = command.PrinterResponse,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
        return CreateSnapshot();
    }

    public async Task<PrintJobSnapshot> CompleteAsync(CompletePrintJobCommand command)
    {
        EnsureExists();

        if (State.Status != PrintJobStatus.Printing)
            throw new InvalidOperationException($"Cannot complete job in status {State.Status}");

        RaiseEvent(new PrintJobCompleted
        {
            JobId = State.JobId,
            PrinterResponse = command.PrinterResponse,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();

        // Notify the queue about status change
        await NotifyQueueAsync(PrintJobStatus.Completed);

        return CreateSnapshot();
    }

    public async Task<PrintJobSnapshot> FailAsync(FailPrintJobCommand command)
    {
        EnsureExists();

        if (State.Status != PrintJobStatus.Printing && State.Status != PrintJobStatus.Pending)
            throw new InvalidOperationException($"Cannot fail job in status {State.Status}");

        var newRetryCount = State.RetryCount + 1;
        DateTime? nextRetryAt = null;

        // Calculate next retry with exponential backoff
        if (newRetryCount < State.MaxRetries)
        {
            var backoffSeconds = Math.Pow(2, newRetryCount) * 5; // 10s, 20s, 40s
            nextRetryAt = DateTime.UtcNow.AddSeconds(backoffSeconds);
        }

        RaiseEvent(new PrintJobFailed
        {
            JobId = State.JobId,
            ErrorMessage = command.ErrorMessage,
            ErrorCode = command.ErrorCode,
            RetryCount = newRetryCount,
            NextRetryAt = nextRetryAt,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();

        // Notify the queue about status change
        await NotifyQueueAsync(PrintJobStatus.Failed);

        return CreateSnapshot();
    }

    public async Task<PrintJobSnapshot> RetryAsync()
    {
        EnsureExists();

        if (State.Status != PrintJobStatus.Failed)
            throw new InvalidOperationException($"Cannot retry job in status {State.Status}");

        if (State.RetryCount >= State.MaxRetries)
            throw new InvalidOperationException("Maximum retry count exceeded");

        RaiseEvent(new PrintJobRetried
        {
            JobId = State.JobId,
            RetryAttempt = State.RetryCount,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();

        // Notify the queue about status change
        await NotifyQueueAsync(PrintJobStatus.Pending);

        return CreateSnapshot();
    }

    public async Task CancelAsync(string? reason = null)
    {
        EnsureExists();

        if (State.Status == PrintJobStatus.Completed || State.Status == PrintJobStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel job in status {State.Status}");

        RaiseEvent(new PrintJobCancelled
        {
            JobId = State.JobId,
            Reason = reason,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();

        // Notify the queue about status change
        await NotifyQueueAsync(PrintJobStatus.Cancelled);
    }

    public Task<PrintJobSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(State.JobId != Guid.Empty);
    }

    public Task<PrintJobStatus> GetStatusAsync()
    {
        return Task.FromResult(State.Status);
    }

    private PrintJobSnapshot CreateSnapshot()
    {
        return new PrintJobSnapshot(
            JobId: State.JobId,
            DeviceId: State.DeviceId,
            PrinterId: State.PrinterId,
            JobType: State.JobType,
            Status: State.Status,
            Content: State.Content,
            Copies: State.Copies,
            Priority: State.Priority,
            RetryCount: State.RetryCount,
            MaxRetries: State.MaxRetries,
            QueuedAt: State.QueuedAt,
            StartedAt: State.StartedAt,
            CompletedAt: State.CompletedAt,
            FailedAt: State.FailedAt,
            NextRetryAt: State.NextRetryAt,
            LastError: State.LastError,
            SourceOrderId: State.SourceOrderId,
            SourceReference: State.SourceReference);
    }

    private async Task NotifyQueueAsync(PrintJobStatus newStatus)
    {
        var queueGrain = GrainFactory.GetGrain<IDevicePrintQueueGrain>(
            $"{State.OrganizationId}:device:{State.DeviceId}:printqueue");
        await queueGrain.NotifyJobStatusChangedAsync(State.JobId, newStatus);
    }

    private void EnsureExists()
    {
        if (State.JobId == Guid.Empty)
            throw new InvalidOperationException("Print job does not exist");
    }
}

/// <summary>
/// Grain for managing the print queue for a device.
/// Uses persistent state (not event sourced) for queue management.
/// Key: "{orgId}:device:{deviceId}:printqueue"
/// </summary>
public class DevicePrintQueueGrain : Grain, IDevicePrintQueueGrain
{
    private readonly IPersistentState<DevicePrintQueueState> _state;

    public DevicePrintQueueGrain(
        [PersistentState("devicePrintQueue", "OrleansStorage")]
        IPersistentState<DevicePrintQueueState> state)
    {
        _state = state;
    }

    public async Task InitializeAsync(Guid deviceId)
    {
        if (_state.State.Initialized)
            return;

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);

        _state.State.OrganizationId = orgId;
        _state.State.DeviceId = deviceId;
        _state.State.Initialized = true;

        await _state.WriteStateAsync();
    }

    public async Task<PrintJobSnapshot> EnqueueAsync(QueuePrintJobCommand command)
    {
        EnsureInitialized();

        var jobId = Guid.NewGuid();
        var jobGrain = GrainFactory.GetGrain<IPrintJobGrain>(
            $"{_state.State.OrganizationId}:device:{_state.State.DeviceId}:printjob:{jobId}");

        var snapshot = await jobGrain.QueueAsync(command);

        // Add to local queue
        _state.State.Queue.Add(new PrintQueueEntry
        {
            JobId = jobId,
            PrinterId = command.PrinterId,
            JobType = command.JobType,
            Status = PrintJobStatus.Pending,
            Priority = command.Priority,
            QueuedAt = DateTime.UtcNow,
            SourceOrderId = command.SourceOrderId,
            SourceReference = command.SourceReference
        });

        // Sort by priority (higher first) then by queue time (older first)
        _state.State.Queue = _state.State.Queue
            .OrderByDescending(e => e.Priority)
            .ThenBy(e => e.QueuedAt)
            .ToList();

        await _state.WriteStateAsync();

        return snapshot;
    }

    public async Task<PrintJobSnapshot?> DequeueAsync()
    {
        EnsureInitialized();

        var nextEntry = _state.State.Queue
            .FirstOrDefault(e => e.Status == PrintJobStatus.Pending);

        if (nextEntry == null)
            return null;

        var jobGrain = GrainFactory.GetGrain<IPrintJobGrain>(
            $"{_state.State.OrganizationId}:device:{_state.State.DeviceId}:printjob:{nextEntry.JobId}");

        return await jobGrain.GetSnapshotAsync();
    }

    public async Task<IReadOnlyList<PrintJobSnapshot>> GetPendingJobsAsync()
    {
        EnsureInitialized();

        var pendingEntries = _state.State.Queue
            .Where(e => e.Status == PrintJobStatus.Pending || e.Status == PrintJobStatus.Printing)
            .ToList();

        var snapshots = new List<PrintJobSnapshot>();
        foreach (var entry in pendingEntries)
        {
            var jobGrain = GrainFactory.GetGrain<IPrintJobGrain>(
                $"{_state.State.OrganizationId}:device:{_state.State.DeviceId}:printjob:{entry.JobId}");

            if (await jobGrain.ExistsAsync())
            {
                snapshots.Add(await jobGrain.GetSnapshotAsync());
            }
        }

        return snapshots;
    }

    public async Task<PrintQueueSummary> GetSummaryAsync()
    {
        EnsureInitialized();

        var pendingJobs = _state.State.Queue.Count(e => e.Status == PrintJobStatus.Pending);
        var printingJobs = _state.State.Queue.Count(e => e.Status == PrintJobStatus.Printing);
        var completedJobs = _state.State.History.Count(e => e.Status == PrintJobStatus.Completed);
        var failedJobs = _state.State.Queue.Count(e => e.Status == PrintJobStatus.Failed)
                       + _state.State.History.Count(e => e.Status == PrintJobStatus.Failed);

        var activeJobs = await GetPendingJobsAsync();

        return new PrintQueueSummary(
            PendingJobs: pendingJobs,
            PrintingJobs: printingJobs,
            CompletedJobs: completedJobs,
            FailedJobs: failedJobs,
            ActiveJobs: activeJobs);
    }

    public async Task<IReadOnlyList<PrintJobSnapshot>> GetHistoryAsync(int limit = 50)
    {
        EnsureInitialized();

        var historyEntries = _state.State.History
            .OrderByDescending(e => e.CompletedAt)
            .Take(limit)
            .ToList();

        var snapshots = new List<PrintJobSnapshot>();
        foreach (var entry in historyEntries)
        {
            var jobGrain = GrainFactory.GetGrain<IPrintJobGrain>(
                $"{_state.State.OrganizationId}:device:{_state.State.DeviceId}:printjob:{entry.JobId}");

            if (await jobGrain.ExistsAsync())
            {
                snapshots.Add(await jobGrain.GetSnapshotAsync());
            }
        }

        return snapshots;
    }

    public async Task ClearHistoryAsync()
    {
        EnsureInitialized();
        _state.State.History.Clear();
        await _state.WriteStateAsync();
    }

    public async Task NotifyJobStatusChangedAsync(Guid jobId, PrintJobStatus newStatus)
    {
        EnsureInitialized();

        var entry = _state.State.Queue.FirstOrDefault(e => e.JobId == jobId);
        if (entry == null)
            return;

        entry.Status = newStatus;

        // Move to history if completed, failed (max retries), or cancelled
        if (newStatus == PrintJobStatus.Completed || newStatus == PrintJobStatus.Cancelled)
        {
            entry.CompletedAt = DateTime.UtcNow;
            _state.State.Queue.Remove(entry);
            _state.State.History.Insert(0, entry);

            // Trim history
            while (_state.State.History.Count > _state.State.MaxHistorySize)
            {
                _state.State.History.RemoveAt(_state.State.History.Count - 1);
            }
        }

        await _state.WriteStateAsync();
    }

    private void EnsureInitialized()
    {
        if (!_state.State.Initialized)
            throw new InvalidOperationException("Device print queue not initialized");
    }
}
