using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

public class PaymentGrain : Grain, IPaymentGrain
{
    private readonly IPersistentState<PaymentState> _state;
    private readonly IGrainFactory _grainFactory;
    private IAsyncStream<IStreamEvent>? _paymentStream;

    public PaymentGrain(
        [PersistentState("payment", "OrleansStorage")]
        IPersistentState<PaymentState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // Stream will be lazily initialized when first payment operation occurs
        // to avoid initializing for non-existent payments
        return base.OnActivateAsync(cancellationToken);
    }

    private IAsyncStream<IStreamEvent> GetPaymentStream()
    {
        if (_paymentStream == null && _state.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.PaymentStreamNamespace, _state.State.OrganizationId.ToString());
            _paymentStream = streamProvider.GetStream<IStreamEvent>(streamId);
        }
        return _paymentStream!;
    }

    public async Task<PaymentInitiatedResult> InitiateAsync(InitiatePaymentCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Payment already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, paymentId) = GrainKeys.ParseSiteEntity(key);

        _state.State = new PaymentState
        {
            Id = paymentId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            OrderId = command.OrderId,
            Method = command.Method,
            Status = PaymentStatus.Initiated,
            Amount = command.Amount,
            TotalAmount = command.Amount,
            CashierId = command.CashierId,
            CustomerId = command.CustomerId,
            DrawerId = command.DrawerId,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();

        // Publish payment initiated event
        await GetPaymentStream().OnNextAsync(new PaymentInitiatedEvent(
            paymentId,
            _state.State.SiteId,
            _state.State.OrderId,
            _state.State.Amount,
            _state.State.Method.ToString(),
            _state.State.CustomerId,
            _state.State.CashierId)
        {
            OrganizationId = _state.State.OrganizationId
        });

        return new PaymentInitiatedResult(paymentId, _state.State.CreatedAt);
    }

    public Task<PaymentState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task<PaymentCompletedResult> CompleteCashAsync(CompleteCashPaymentCommand command)
    {
        EnsureExists();
        EnsureStatus(PaymentStatus.Initiated);

        _state.State.AmountTendered = command.AmountTendered;
        _state.State.TipAmount = command.TipAmount;
        _state.State.TotalAmount = _state.State.Amount + command.TipAmount;
        _state.State.ChangeGiven = command.AmountTendered - _state.State.TotalAmount;
        _state.State.Status = PaymentStatus.Completed;
        _state.State.CompletedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
        await PublishPaymentCompletedEventAsync();

        return new PaymentCompletedResult(_state.State.TotalAmount, _state.State.ChangeGiven);
    }

    public async Task<PaymentCompletedResult> CompleteCardAsync(ProcessCardPaymentCommand command)
    {
        EnsureExists();

        if (_state.State.Status is not (PaymentStatus.Initiated or PaymentStatus.Authorized))
            throw new InvalidOperationException($"Invalid status for card completion: {_state.State.Status}");

        _state.State.GatewayReference = command.GatewayReference;
        _state.State.AuthorizationCode = command.AuthorizationCode;
        _state.State.CardInfo = command.CardInfo;
        _state.State.GatewayName = command.GatewayName;
        _state.State.TipAmount = command.TipAmount;
        _state.State.TotalAmount = _state.State.Amount + command.TipAmount;
        _state.State.Status = PaymentStatus.Completed;
        _state.State.CapturedAt = DateTime.UtcNow;
        _state.State.CompletedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
        await PublishPaymentCompletedEventAsync();

        return new PaymentCompletedResult(_state.State.TotalAmount, null);
    }

    public async Task<PaymentCompletedResult> CompleteGiftCardAsync(ProcessGiftCardPaymentCommand command)
    {
        EnsureExists();
        EnsureStatus(PaymentStatus.Initiated);

        _state.State.GiftCardId = command.GiftCardId;
        _state.State.GiftCardNumber = command.CardNumber;
        _state.State.TotalAmount = _state.State.Amount;
        _state.State.Status = PaymentStatus.Completed;
        _state.State.CompletedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
        await PublishPaymentCompletedEventAsync();

        return new PaymentCompletedResult(_state.State.TotalAmount, null);
    }

    public async Task RequestAuthorizationAsync()
    {
        EnsureExists();
        EnsureStatus(PaymentStatus.Initiated);

        _state.State.Status = PaymentStatus.Authorizing;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RecordAuthorizationAsync(string authCode, string gatewayRef, CardInfo cardInfo)
    {
        EnsureExists();
        EnsureStatus(PaymentStatus.Authorizing);

        _state.State.AuthorizationCode = authCode;
        _state.State.GatewayReference = gatewayRef;
        _state.State.CardInfo = cardInfo;
        _state.State.Status = PaymentStatus.Authorized;
        _state.State.AuthorizedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RecordDeclineAsync(string declineCode, string reason)
    {
        EnsureExists();
        EnsureStatus(PaymentStatus.Authorizing);

        _state.State.Status = PaymentStatus.Declined;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task CaptureAsync()
    {
        EnsureExists();
        EnsureStatus(PaymentStatus.Authorized);

        _state.State.Status = PaymentStatus.Captured;
        _state.State.CapturedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task<RefundResult> RefundAsync(RefundPaymentCommand command)
    {
        EnsureExists();

        if (_state.State.Status != PaymentStatus.Completed)
            throw new InvalidOperationException("Can only refund completed payments");

        if (command.Amount > _state.State.TotalAmount - _state.State.RefundedAmount)
            throw new InvalidOperationException("Refund amount exceeds available balance");

        var refundId = Guid.NewGuid();
        var refund = new RefundInfo
        {
            RefundId = refundId,
            Amount = command.Amount,
            Reason = command.Reason,
            IssuedBy = command.IssuedBy,
            IssuedAt = DateTime.UtcNow
        };

        _state.State.Refunds.Add(refund);
        _state.State.RefundedAmount += command.Amount;

        if (_state.State.RefundedAmount >= _state.State.TotalAmount)
            _state.State.Status = PaymentStatus.Refunded;
        else
            _state.State.Status = PaymentStatus.PartiallyRefunded;

        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish payment refunded event
        await GetPaymentStream().OnNextAsync(new PaymentRefundedEvent(
            _state.State.Id,
            _state.State.SiteId,
            _state.State.OrderId,
            refundId,
            command.Amount,
            _state.State.RefundedAmount,
            _state.State.Method.ToString(),
            command.Reason,
            command.IssuedBy)
        {
            OrganizationId = _state.State.OrganizationId
        });

        return new RefundResult(refundId, _state.State.RefundedAmount, _state.State.TotalAmount - _state.State.RefundedAmount);
    }

    public Task<RefundResult> PartialRefundAsync(RefundPaymentCommand command)
    {
        return RefundAsync(command);
    }

    public async Task VoidAsync(VoidPaymentCommand command)
    {
        EnsureExists();

        if (_state.State.Status is PaymentStatus.Voided or PaymentStatus.Refunded)
            throw new InvalidOperationException($"Cannot void payment with status: {_state.State.Status}");

        var voidedAmount = _state.State.TotalAmount;
        _state.State.Status = PaymentStatus.Voided;
        _state.State.VoidedBy = command.VoidedBy;
        _state.State.VoidedAt = DateTime.UtcNow;
        _state.State.VoidReason = command.Reason;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish payment voided event
        await GetPaymentStream().OnNextAsync(new PaymentVoidedEvent(
            _state.State.Id,
            _state.State.SiteId,
            _state.State.OrderId,
            voidedAmount,
            _state.State.Method.ToString(),
            command.Reason,
            command.VoidedBy)
        {
            OrganizationId = _state.State.OrganizationId
        });
    }

    public async Task AdjustTipAsync(AdjustTipCommand command)
    {
        EnsureExists();

        if (_state.State.Status != PaymentStatus.Completed)
            throw new InvalidOperationException("Can only adjust tip on completed payments");

        var oldTip = _state.State.TipAmount;
        _state.State.TipAmount = command.NewTipAmount;
        _state.State.TotalAmount = _state.State.Amount + command.NewTipAmount;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task AssignToBatchAsync(Guid batchId)
    {
        EnsureExists();

        _state.State.BatchId = batchId;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);
    public Task<PaymentStatus> GetStatusAsync() => Task.FromResult(_state.State.Status);

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Payment does not exist");
    }

    private void EnsureStatus(PaymentStatus expected)
    {
        if (_state.State.Status != expected)
            throw new InvalidOperationException($"Invalid status. Expected {expected}, got {_state.State.Status}");
    }

    private async Task PublishPaymentCompletedEventAsync()
    {
        // Publish payment completed event via stream
        // This replaces the direct grain call to OrderGrain.RecordPaymentAsync
        // allowing multiple subscribers to react to payment completions
        await GetPaymentStream().OnNextAsync(new PaymentCompletedEvent(
            _state.State.Id,
            _state.State.SiteId,
            _state.State.OrderId,
            _state.State.Amount,
            _state.State.TipAmount,
            _state.State.TotalAmount,
            _state.State.Method.ToString(),
            _state.State.CustomerId,
            _state.State.CashierId,
            _state.State.DrawerId,
            _state.State.GatewayReference,
            _state.State.CardInfo?.MaskedNumber)
        {
            OrganizationId = _state.State.OrganizationId
        });
    }
}

/// <summary>
/// Context for creating cash drawer transactions.
/// </summary>
public record CashDrawerTransactionContext(
    DrawerTransactionType Type,
    Guid? PaymentId = null);

public class CashDrawerGrain : LedgerGrain<CashDrawerState, DrawerTransaction>, ICashDrawerGrain
{
    public CashDrawerGrain(
        [PersistentState("cashdrawer", "OrleansStorage")]
        IPersistentState<CashDrawerState> state) : base(state)
    {
    }

    protected override bool IsInitialized => State.State.Id != Guid.Empty;

    protected override DrawerTransaction CreateTransaction(
        decimal amount,
        decimal balanceAfter,
        string? notes,
        object? context)
    {
        var ctx = context as CashDrawerTransactionContext
            ?? new CashDrawerTransactionContext(DrawerTransactionType.Adjustment);

        return new DrawerTransaction
        {
            Id = Guid.NewGuid(),
            Type = ctx.Type,
            Amount = amount,
            BalanceAfter = balanceAfter,
            PaymentId = ctx.PaymentId,
            Description = notes,
            Timestamp = DateTime.UtcNow
        };
    }

    public async Task<DrawerOpenedResult> OpenAsync(OpenDrawerCommand command)
    {
        if (State.State.Status == DrawerStatus.Open)
            throw new InvalidOperationException("Drawer is already open");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, drawerId) = GrainKeys.ParseSiteEntity(key);

        if (State.State.Id == Guid.Empty)
        {
            State.State.Id = drawerId;
            State.State.OrganizationId = command.OrganizationId;
            State.State.SiteId = command.SiteId;
            State.State.Name = $"Drawer-{drawerId.ToString()[..8]}";
        }

        State.State.Status = DrawerStatus.Open;
        State.State.CurrentUserId = command.UserId;
        State.State.OpenedAt = DateTime.UtcNow;
        State.State.OpeningFloat = command.OpeningFloat;
        State.State.CashIn = 0;
        State.State.CashOut = 0;
        State.State.ExpectedBalance = command.OpeningFloat;
        State.State.ActualBalance = null;
        State.State.CashDrops.Clear();
        State.State.Transactions.Clear();
        State.State.Version++;

        // Record opening float transaction
        State.State.Transactions.Add(new DrawerTransaction
        {
            Id = Guid.NewGuid(),
            Type = DrawerTransactionType.OpeningFloat,
            Amount = command.OpeningFloat,
            BalanceAfter = command.OpeningFloat,
            Timestamp = DateTime.UtcNow
        });

        await State.WriteStateAsync();

        return new DrawerOpenedResult(State.State.Id, State.State.OpenedAt.Value);
    }

    public Task<CashDrawerState> GetStateAsync()
    {
        return Task.FromResult(State.State);
    }

    public async Task RecordCashInAsync(RecordCashInCommand command)
    {
        EnsureOpen();

        // Update CashIn counter before the ledger operation
        State.State.CashIn += command.Amount;

        await CreditAsync(
            command.Amount,
            null,
            new CashDrawerTransactionContext(
                DrawerTransactionType.CashSale,
                PaymentId: command.PaymentId));
    }

    public async Task RecordCashOutAsync(RecordCashOutCommand command)
    {
        EnsureOpen();

        // Update CashOut counter before the ledger operation
        State.State.CashOut += command.Amount;

        await DebitAsync(
            command.Amount,
            command.Reason,
            new CashDrawerTransactionContext(DrawerTransactionType.CashPayout));
    }

    public async Task RecordDropAsync(CashDropCommand command)
    {
        EnsureOpen();

        // Record the cash drop
        var drop = new CashDrop
        {
            Id = Guid.NewGuid(),
            Amount = command.Amount,
            DroppedBy = State.State.CurrentUserId!.Value,
            DroppedAt = DateTime.UtcNow,
            Notes = command.Notes
        };
        State.State.CashDrops.Add(drop);

        await DebitAsync(
            command.Amount,
            command.Notes,
            new CashDrawerTransactionContext(DrawerTransactionType.Drop));
    }

    public async Task OpenNoSaleAsync(Guid userId, string? reason = null)
    {
        EnsureOpen();

        // NoSale doesn't change balance, just records the event
        await RecordTransactionAsync(
            0,
            reason ?? "No sale",
            new CashDrawerTransactionContext(DrawerTransactionType.NoSale));
    }

    public async Task CountAsync(CountDrawerCommand command)
    {
        EnsureOpen();

        State.State.Status = DrawerStatus.Counting;
        State.State.ActualBalance = command.CountedAmount;
        State.State.LastCountedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();
    }

    public async Task<DrawerClosedResult> CloseAsync(CloseDrawerCommand command)
    {
        if (State.State.Status == DrawerStatus.Closed)
            throw new InvalidOperationException("Drawer is already closed");

        var variance = command.ActualBalance - State.State.ExpectedBalance;

        State.State.ActualBalance = command.ActualBalance;
        State.State.Status = DrawerStatus.Closed;
        State.State.Version++;

        await State.WriteStateAsync();

        return new DrawerClosedResult(State.State.ExpectedBalance, command.ActualBalance, variance);
    }

    public Task<bool> IsOpenAsync() => Task.FromResult(State.State.Status == DrawerStatus.Open);
    public Task<decimal> GetExpectedBalanceAsync() => Task.FromResult(State.State.ExpectedBalance);
    public Task<DrawerStatus> GetStatusAsync() => Task.FromResult(State.State.Status);
    public Task<bool> ExistsAsync() => Task.FromResult(IsInitialized);

    private void EnsureOpen()
    {
        if (State.State.Status != DrawerStatus.Open)
            throw new InvalidOperationException("Drawer is not open");
    }
}
