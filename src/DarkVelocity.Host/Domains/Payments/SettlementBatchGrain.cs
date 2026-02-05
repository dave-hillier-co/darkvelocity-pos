using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class SettlementBatchGrain : JournaledGrain<SettlementBatchState, ISettlementBatchEvent>, ISettlementBatchGrain
{
    private readonly IGrainFactory _grainFactory;

    public SettlementBatchGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    protected override void TransitionState(SettlementBatchState state, ISettlementBatchEvent @event)
    {
        switch (@event)
        {
            case BatchOpened e:
                state.Id = e.BatchId;
                state.OrganizationId = e.OrganizationId;
                state.SiteId = e.SiteId;
                state.BusinessDate = e.BusinessDate;
                state.BatchNumber = e.BatchNumber;
                state.Status = SettlementBatchStatus.Open;
                state.OpenedBy = e.OpenedBy;
                state.CreatedAt = e.OccurredAt;
                break;

            case PaymentAddedToBatch e:
                state.Payments.Add(new BatchPaymentEntry
                {
                    PaymentId = e.PaymentId,
                    Amount = e.Amount,
                    Method = e.Method,
                    GatewayReference = e.GatewayReference,
                    AddedAt = e.OccurredAt
                });
                state.TotalAmount += e.Amount;
                state.PaymentCount++;
                break;

            case PaymentRemovedFromBatch e:
                var payment = state.Payments.FirstOrDefault(p => p.PaymentId == e.PaymentId);
                if (payment != null)
                {
                    state.Payments.Remove(payment);
                    state.TotalAmount -= payment.Amount;
                    state.PaymentCount--;
                }
                break;

            case BatchClosed e:
                state.Status = SettlementBatchStatus.Closed;
                state.ClosedBy = e.ClosedBy;
                state.ClosedAt = e.OccurredAt;
                break;

            case BatchSettled e:
                state.Status = SettlementBatchStatus.Settled;
                state.SettlementReference = e.SettlementReference;
                state.SettledAmount = e.SettledAmount;
                state.ProcessingFees = e.ProcessingFees;
                state.NetAmount = e.NetAmount;
                state.SettledAt = e.OccurredAt;
                break;

            case BatchSettlementFailed e:
                state.Status = SettlementBatchStatus.Failed;
                state.LastErrorCode = e.ErrorCode;
                state.LastErrorMessage = e.ErrorMessage;
                state.SettlementAttempts = e.RetryCount;
                break;

            case BatchReopened:
                state.Status = SettlementBatchStatus.Open;
                state.ClosedBy = null;
                state.ClosedAt = null;
                break;
        }
    }

    public async Task<BatchOpenedResult> OpenAsync(OpenBatchCommand command)
    {
        if (State.Id != Guid.Empty)
            throw new InvalidOperationException("Batch already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, batchId) = GrainKeys.ParseSiteEntity(key);
        var batchNumber = GenerateBatchNumber(command.BusinessDate);
        var now = DateTime.UtcNow;

        RaiseEvent(new BatchOpened
        {
            BatchId = batchId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            BusinessDate = command.BusinessDate,
            BatchNumber = batchNumber,
            OpenedBy = command.OpenedBy,
            OccurredAt = now
        });
        await ConfirmEvents();

        return new BatchOpenedResult(batchId, batchNumber, now);
    }

    public Task<SettlementBatchState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public async Task AddPaymentAsync(AddPaymentToBatchCommand command)
    {
        EnsureExists();
        EnsureStatus(SettlementBatchStatus.Open);

        if (State.Payments.Any(p => p.PaymentId == command.PaymentId))
            throw new InvalidOperationException("Payment already in batch");

        RaiseEvent(new PaymentAddedToBatch
        {
            BatchId = State.Id,
            PaymentId = command.PaymentId,
            Amount = command.Amount,
            Method = command.Method,
            GatewayReference = command.GatewayReference,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Update the payment to track which batch it belongs to
        var paymentKey = GrainKeys.Payment(State.OrganizationId, State.SiteId, command.PaymentId);
        var paymentGrain = _grainFactory.GetGrain<IPaymentGrain>(paymentKey);
        await paymentGrain.AssignToBatchAsync(State.Id);
    }

    public async Task RemovePaymentAsync(Guid paymentId, string reason)
    {
        EnsureExists();
        EnsureStatus(SettlementBatchStatus.Open);

        if (!State.Payments.Any(p => p.PaymentId == paymentId))
            throw new InvalidOperationException("Payment not in batch");

        RaiseEvent(new PaymentRemovedFromBatch
        {
            BatchId = State.Id,
            PaymentId = paymentId,
            Reason = reason,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task<BatchClosedResult> CloseAsync(CloseBatchCommand command)
    {
        EnsureExists();
        EnsureStatus(SettlementBatchStatus.Open);

        RaiseEvent(new BatchClosed
        {
            BatchId = State.Id,
            ClosedBy = command.ClosedBy,
            TotalAmount = State.TotalAmount,
            PaymentCount = State.PaymentCount,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        return new BatchClosedResult(State.Id, State.TotalAmount, State.PaymentCount);
    }

    public async Task ReopenAsync(Guid reopenedBy, string reason)
    {
        EnsureExists();

        if (State.Status != SettlementBatchStatus.Closed && State.Status != SettlementBatchStatus.Failed)
            throw new InvalidOperationException($"Cannot reopen batch with status: {State.Status}");

        RaiseEvent(new BatchReopened
        {
            BatchId = State.Id,
            ReopenedBy = reopenedBy,
            Reason = reason,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task<BatchSettledResult> RecordSettlementAsync(SettleBatchCommand command)
    {
        EnsureExists();

        if (State.Status != SettlementBatchStatus.Closed && State.Status != SettlementBatchStatus.Failed)
            throw new InvalidOperationException($"Cannot settle batch with status: {State.Status}");

        var netAmount = State.TotalAmount - command.ProcessingFees;

        RaiseEvent(new BatchSettled
        {
            BatchId = State.Id,
            SettlementReference = command.SettlementReference,
            SettledAmount = State.TotalAmount,
            ProcessingFees = command.ProcessingFees,
            NetAmount = netAmount,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        return new BatchSettledResult(
            State.Id,
            command.SettlementReference,
            State.TotalAmount,
            command.ProcessingFees,
            netAmount);
    }

    public async Task RecordSettlementFailureAsync(string errorCode, string errorMessage)
    {
        EnsureExists();

        RaiseEvent(new BatchSettlementFailed
        {
            BatchId = State.Id,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            RetryCount = State.SettlementAttempts + 1,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<SettlementReport> GetSettlementReportAsync()
    {
        EnsureExists();

        var report = new SettlementReport
        {
            BatchId = State.Id,
            BatchNumber = State.BatchNumber,
            BusinessDate = State.BusinessDate,
            Status = State.Status,
            TotalAmount = State.TotalAmount,
            PaymentCount = State.PaymentCount,
            TotalsByMethod = State.GetTotalsByMethod(),
            ProcessingFees = State.ProcessingFees,
            NetAmount = State.NetAmount,
            SettlementReference = State.SettlementReference,
            SettledAt = State.SettledAt
        };

        return Task.FromResult(report);
    }

    public Task<List<PaymentMethodTotal>> GetTotalsByMethodAsync()
    {
        EnsureExists();
        return Task.FromResult(State.GetTotalsByMethod());
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.Id != Guid.Empty);
    public Task<SettlementBatchStatus> GetStatusAsync() => Task.FromResult(State.Status);

    private void EnsureExists()
    {
        if (State.Id == Guid.Empty)
            throw new InvalidOperationException("Batch does not exist");
    }

    private void EnsureStatus(SettlementBatchStatus expected)
    {
        if (State.Status != expected)
            throw new InvalidOperationException($"Invalid status. Expected {expected}, got {State.Status}");
    }

    private static string GenerateBatchNumber(DateOnly businessDate)
    {
        return $"BATCH-{businessDate:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
    }
}
