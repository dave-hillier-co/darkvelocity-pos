using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Providers;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

public class OfflinePaymentQueueGrain : Grain, IOfflinePaymentQueueGrain
{
    private readonly IPersistentState<OfflinePaymentQueueState> _state;
    private readonly IGrainFactory _grainFactory;
    private Lazy<IAsyncStream<IStreamEvent>>? _alertStream;

    public OfflinePaymentQueueGrain(
        [PersistentState("offlinepaymentqueue", "OrleansStorage")]
        IPersistentState<OfflinePaymentQueueState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.IsInitialized)
        {
            InitializeStreams();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    private void InitializeStreams()
    {
        var orgId = _state.State.OrganizationId;
        _alertStream = new Lazy<IAsyncStream<IStreamEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.AlertStreamNamespace, orgId.ToString());
            return streamProvider.GetStream<IStreamEvent>(streamId);
        });
    }

    private IAsyncStream<IStreamEvent>? AlertStream => _alertStream?.Value;

    public async Task InitializeAsync(Guid organizationId, Guid siteId)
    {
        if (_state.State.IsInitialized)
            return;

        _state.State.OrganizationId = organizationId;
        _state.State.SiteId = siteId;
        _state.State.IsInitialized = true;
        await _state.WriteStateAsync();

        InitializeStreams();
    }

    public async Task<PaymentQueuedResult> QueuePaymentAsync(QueuePaymentCommand command)
    {
        EnsureInitialized();

        var queueEntryId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var nextRetry = now.AddSeconds(_state.State.BaseRetryDelaySeconds);

        var entry = new QueuedPaymentEntry
        {
            QueueEntryId = queueEntryId,
            PaymentId = command.PaymentId,
            OrderId = command.OrderId,
            Method = command.Method,
            Amount = command.Amount,
            PaymentData = command.PaymentData,
            Status = OfflinePaymentStatus.Queued,
            QueueReason = command.QueueReason,
            AttemptCount = 0,
            NextRetryAt = nextRetry,
            QueuedAt = now
        };

        _state.State.QueuedPayments.Add(entry);
        _state.State.TotalQueued++;
        await _state.WriteStateAsync();

        return new PaymentQueuedResult(queueEntryId, now, nextRetry);
    }

    public async Task<int> ProcessQueueAsync()
    {
        EnsureInitialized();

        var now = DateTime.UtcNow;
        var pendingPayments = _state.State.GetPendingRetries(now);
        var processedCount = 0;

        foreach (var payment in pendingPayments)
        {
            var success = await ProcessPaymentAsync(payment.QueueEntryId);
            if (success)
                processedCount++;
        }

        return processedCount;
    }

    public async Task<bool> ProcessPaymentAsync(Guid queueEntryId)
    {
        EnsureInitialized();

        var entry = _state.State.QueuedPayments.FirstOrDefault(p => p.QueueEntryId == queueEntryId);
        if (entry == null)
            throw new InvalidOperationException($"Queue entry {queueEntryId} not found");

        if (entry.Status != OfflinePaymentStatus.Queued && entry.Status != OfflinePaymentStatus.Retrying)
            return false;

        // Update status to retrying
        UpdateEntryStatus(entry, OfflinePaymentStatus.Retrying);

        // Record retry attempt
        _state.State.RetryHistory.Add(new RetryAttempt
        {
            QueueEntryId = queueEntryId,
            AttemptNumber = entry.AttemptCount + 1,
            Success = false, // Will be updated after actual attempt
            AttemptedAt = DateTime.UtcNow
        });

        await _state.WriteStateAsync();

        // The actual payment processing would be done by the caller
        // This method just updates the queue state
        return true;
    }

    public async Task RecordSuccessAsync(Guid queueEntryId, string gatewayReference)
    {
        EnsureInitialized();

        var entry = _state.State.QueuedPayments.FirstOrDefault(p => p.QueueEntryId == queueEntryId);
        if (entry == null)
            throw new InvalidOperationException($"Queue entry {queueEntryId} not found");

        var index = _state.State.QueuedPayments.IndexOf(entry);
        _state.State.QueuedPayments[index] = entry with
        {
            Status = OfflinePaymentStatus.Processed,
            ProcessedAt = DateTime.UtcNow,
            GatewayReference = gatewayReference,
            AttemptCount = entry.AttemptCount + 1
        };

        // Update last retry attempt to success
        var lastAttempt = _state.State.RetryHistory
            .LastOrDefault(r => r.QueueEntryId == queueEntryId);
        if (lastAttempt != null)
        {
            var attemptIndex = _state.State.RetryHistory.IndexOf(lastAttempt);
            _state.State.RetryHistory[attemptIndex] = lastAttempt with { Success = true };
        }

        _state.State.TotalProcessed++;
        await _state.WriteStateAsync();
    }

    public async Task RecordFailureAsync(Guid queueEntryId, string errorCode, string errorMessage)
    {
        EnsureInitialized();

        var entry = _state.State.QueuedPayments.FirstOrDefault(p => p.QueueEntryId == queueEntryId);
        if (entry == null)
            throw new InvalidOperationException($"Queue entry {queueEntryId} not found");

        var newAttemptCount = entry.AttemptCount + 1;
        var index = _state.State.QueuedPayments.IndexOf(entry);

        // Update retry attempt
        var lastAttempt = _state.State.RetryHistory
            .LastOrDefault(r => r.QueueEntryId == queueEntryId);
        if (lastAttempt != null)
        {
            var attemptIndex = _state.State.RetryHistory.IndexOf(lastAttempt);
            _state.State.RetryHistory[attemptIndex] = lastAttempt with
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
        }

        if (newAttemptCount >= _state.State.MaxRetryAttempts)
        {
            // Max retries reached - mark as failed
            _state.State.QueuedPayments[index] = entry with
            {
                Status = OfflinePaymentStatus.Failed,
                AttemptCount = newAttemptCount,
                LastErrorCode = errorCode,
                LastErrorMessage = errorMessage,
                NextRetryAt = null
            };

            _state.State.TotalFailed++;

            // Send alert for persistent failure
            await SendFailureAlertAsync(entry, errorCode, errorMessage, newAttemptCount);
        }
        else
        {
            // Schedule next retry with exponential backoff
            var retryDelay = _state.State.CalculateRetryDelay(newAttemptCount);
            var nextRetry = DateTime.UtcNow.AddSeconds(retryDelay);

            _state.State.QueuedPayments[index] = entry with
            {
                Status = OfflinePaymentStatus.Queued,
                AttemptCount = newAttemptCount,
                LastErrorCode = errorCode,
                LastErrorMessage = errorMessage,
                NextRetryAt = nextRetry
            };
        }

        await _state.WriteStateAsync();
    }

    public async Task CancelPaymentAsync(Guid queueEntryId, Guid cancelledBy, string reason)
    {
        EnsureInitialized();

        var entry = _state.State.QueuedPayments.FirstOrDefault(p => p.QueueEntryId == queueEntryId);
        if (entry == null)
            throw new InvalidOperationException($"Queue entry {queueEntryId} not found");

        if (entry.Status == OfflinePaymentStatus.Processed)
            throw new InvalidOperationException("Cannot cancel already processed payment");

        var index = _state.State.QueuedPayments.IndexOf(entry);
        _state.State.QueuedPayments[index] = entry with
        {
            Status = OfflinePaymentStatus.Cancelled
        };

        await _state.WriteStateAsync();
    }

    public Task<QueuedPaymentEntry?> GetPaymentEntryAsync(Guid queueEntryId)
    {
        EnsureInitialized();
        var entry = _state.State.QueuedPayments.FirstOrDefault(p => p.QueueEntryId == queueEntryId);
        return Task.FromResult(entry);
    }

    public Task<List<QueuedPaymentEntry>> GetPendingPaymentsAsync()
    {
        EnsureInitialized();
        var pending = _state.State.QueuedPayments
            .Where(p => p.Status == OfflinePaymentStatus.Queued || p.Status == OfflinePaymentStatus.Retrying)
            .ToList();
        return Task.FromResult(pending);
    }

    public Task<QueueStatistics> GetStatisticsAsync()
    {
        EnsureInitialized();

        var stats = new QueueStatistics
        {
            PendingCount = _state.State.QueuedPayments.Count(p => p.Status == OfflinePaymentStatus.Queued),
            RetryingCount = _state.State.QueuedPayments.Count(p => p.Status == OfflinePaymentStatus.Retrying),
            TotalQueued = _state.State.TotalQueued,
            TotalProcessed = _state.State.TotalProcessed,
            TotalFailed = _state.State.TotalFailed,
            PendingAmount = _state.State.QueuedPayments
                .Where(p => p.Status == OfflinePaymentStatus.Queued || p.Status == OfflinePaymentStatus.Retrying)
                .Sum(p => p.Amount)
        };

        return Task.FromResult(stats);
    }

    public Task<OfflinePaymentQueueState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task ConfigureRetrySettingsAsync(int maxAttempts, int baseDelaySeconds, double backoffMultiplier)
    {
        EnsureInitialized();

        _state.State.MaxRetryAttempts = maxAttempts;
        _state.State.BaseRetryDelaySeconds = baseDelaySeconds;
        _state.State.RetryBackoffMultiplier = backoffMultiplier;

        await _state.WriteStateAsync();
    }

    private void UpdateEntryStatus(QueuedPaymentEntry entry, OfflinePaymentStatus newStatus)
    {
        var index = _state.State.QueuedPayments.IndexOf(entry);
        if (index >= 0)
        {
            _state.State.QueuedPayments[index] = entry with { Status = newStatus };
        }
    }

    private async Task SendFailureAlertAsync(QueuedPaymentEntry entry, string errorCode, string errorMessage, int attempts)
    {
        if (AlertStream == null) return;

        await AlertStream.OnNextAsync(new OfflinePaymentFailureAlertEvent(
            _state.State.OrganizationId,
            _state.State.SiteId,
            entry.PaymentId,
            entry.OrderId,
            entry.Amount,
            errorCode,
            errorMessage,
            attempts)
        {
            OrganizationId = _state.State.OrganizationId
        });
    }

    private void EnsureInitialized()
    {
        if (!_state.State.IsInitialized)
            throw new InvalidOperationException("Queue not initialized. Call InitializeAsync first.");
    }
}

/// <summary>
/// Stream event for offline payment failure alerts.
/// </summary>
[GenerateSerializer]
public record OfflinePaymentFailureAlertEvent(
    [property: Id(0)] Guid OrganizationId,
    [property: Id(1)] Guid SiteId,
    [property: Id(2)] Guid PaymentId,
    [property: Id(3)] Guid OrderId,
    [property: Id(4)] decimal Amount,
    [property: Id(5)] string ErrorCode,
    [property: Id(6)] string ErrorMessage,
    [property: Id(7)] int TotalAttempts) : IStreamEvent
{
    [Id(8)] public Guid EventId { get; init; } = Guid.NewGuid();
    [Id(9)] public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    Guid IStreamEvent.OrganizationId => OrganizationId;
}
