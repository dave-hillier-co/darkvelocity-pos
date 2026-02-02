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
    private Lazy<IAsyncStream<IStreamEvent>>? _paymentStream;

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
        if (_state.State.OrganizationId != Guid.Empty)
        {
            InitializeStream();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    private void InitializeStream()
    {
        var orgId = _state.State.OrganizationId;
        _paymentStream = new Lazy<IAsyncStream<IStreamEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.PaymentStreamNamespace, orgId.ToString());
            return streamProvider.GetStream<IStreamEvent>(streamId);
        });
    }

    private IAsyncStream<IStreamEvent>? PaymentStream => _paymentStream?.Value;

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
        InitializeStream();

        // Publish payment initiated event
        await PaymentStream!.OnNextAsync(new PaymentInitiatedEvent(
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
        // For gift card payments, the subscriber will credit the refund back to the gift card
        await PaymentStream!.OnNextAsync(new PaymentRefundedEvent(
            _state.State.Id,
            _state.State.SiteId,
            _state.State.OrderId,
            refundId,
            command.Amount,
            _state.State.RefundedAmount,
            _state.State.Method.ToString(),
            command.Reason,
            command.IssuedBy,
            _state.State.GiftCardId)
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
        await PaymentStream!.OnNextAsync(new PaymentVoidedEvent(
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
        // For gift card payments, the subscriber will redeem from the gift card
        await PaymentStream!.OnNextAsync(new PaymentCompletedEvent(
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
            _state.State.CardInfo?.MaskedNumber,
            _state.State.GiftCardId)
        {
            OrganizationId = _state.State.OrganizationId
        });
    }
}

public class CashDrawerGrain : Grain, ICashDrawerGrain
{
    private readonly IPersistentState<CashDrawerState> _state;
    private readonly IGrainFactory _grainFactory;
    private Lazy<ILedgerGrain>? _ledger;

    public CashDrawerGrain(
        [PersistentState("cashdrawer", "OrleansStorage")]
        IPersistentState<CashDrawerState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.OrganizationId != Guid.Empty)
        {
            InitializeLedger();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    private void InitializeLedger()
    {
        var orgId = _state.State.OrganizationId;
        var drawerId = _state.State.Id;
        _ledger = new Lazy<ILedgerGrain>(() =>
        {
            var ledgerKey = GrainKeys.Ledger(orgId, "cashdrawer", drawerId);
            return _grainFactory.GetGrain<ILedgerGrain>(ledgerKey);
        });
    }

    private ILedgerGrain? Ledger => _ledger?.Value;

    public async Task<DrawerOpenedResult> OpenAsync(OpenDrawerCommand command)
    {
        if (_state.State.Status == DrawerStatus.Open)
            throw new InvalidOperationException("Drawer is already open");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, drawerId) = GrainKeys.ParseSiteEntity(key);

        if (_state.State.Id == Guid.Empty)
        {
            _state.State.Id = drawerId;
            _state.State.OrganizationId = command.OrganizationId;
            _state.State.SiteId = command.SiteId;
            _state.State.Name = $"Drawer-{drawerId.ToString()[..8]}";
        }

        _state.State.Status = DrawerStatus.Open;
        _state.State.CurrentUserId = command.UserId;
        _state.State.OpenedAt = DateTime.UtcNow;
        _state.State.OpeningFloat = command.OpeningFloat;
        _state.State.CashIn = 0;
        _state.State.CashOut = 0;
        _state.State.ActualBalance = null;
        _state.State.CashDrops.Clear();
        _state.State.Transactions.Clear();
        _state.State.Version++;

        _state.State.Transactions.Add(new DrawerTransaction
        {
            Id = Guid.NewGuid(),
            Type = DrawerTransactionType.OpeningFloat,
            Amount = command.OpeningFloat,
            Timestamp = DateTime.UtcNow
        });

        await _state.WriteStateAsync();
        InitializeLedger();

        // Initialize ledger and credit opening float
        await Ledger!.InitializeAsync(command.OrganizationId);
        await Ledger!.CreditAsync(
            command.OpeningFloat,
            "opening_float",
            null,
            new Dictionary<string, string>
            {
                ["userId"] = command.UserId.ToString(),
                ["siteId"] = command.SiteId.ToString()
            });

        return new DrawerOpenedResult(_state.State.Id, _state.State.OpenedAt.Value);
    }

    public async Task<CashDrawerState> GetStateAsync()
    {
        // Sync ExpectedBalance from ledger if initialized
        if (_state.State.OrganizationId != Guid.Empty)
        {
            _state.State.ExpectedBalance = await Ledger!.GetBalanceAsync();
        }
        return _state.State;
    }

    public async Task RecordCashInAsync(RecordCashInCommand command)
    {
        EnsureOpen();

        var result = await Ledger!.CreditAsync(
            command.Amount,
            "cash_sale",
            null,
            new Dictionary<string, string>
            {
                ["paymentId"] = command.PaymentId?.ToString() ?? ""
            });

        if (!result.Success)
            throw new InvalidOperationException(result.Error);

        _state.State.CashIn += command.Amount;

        _state.State.Transactions.Add(new DrawerTransaction
        {
            Id = result.TransactionId,
            Type = DrawerTransactionType.CashSale,
            Amount = command.Amount,
            PaymentId = command.PaymentId,
            Timestamp = result.Timestamp
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RecordCashOutAsync(RecordCashOutCommand command)
    {
        EnsureOpen();

        var result = await Ledger!.DebitAsync(
            command.Amount,
            "cash_payout",
            command.Reason,
            null);

        if (!result.Success)
            throw new InvalidOperationException(result.Error ?? "Insufficient cash in drawer");

        _state.State.CashOut += command.Amount;

        _state.State.Transactions.Add(new DrawerTransaction
        {
            Id = result.TransactionId,
            Type = DrawerTransactionType.CashPayout,
            Amount = command.Amount,
            Description = command.Reason,
            Timestamp = result.Timestamp
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RecordDropAsync(CashDropCommand command)
    {
        EnsureOpen();

        var result = await Ledger!.DebitAsync(
            command.Amount,
            "drop",
            command.Notes,
            new Dictionary<string, string>
            {
                ["droppedBy"] = _state.State.CurrentUserId?.ToString() ?? ""
            });

        if (!result.Success)
            throw new InvalidOperationException(result.Error ?? "Insufficient cash for drop");

        var drop = new CashDrop
        {
            Id = result.TransactionId,
            Amount = command.Amount,
            DroppedBy = _state.State.CurrentUserId!.Value,
            DroppedAt = result.Timestamp,
            Notes = command.Notes
        };

        _state.State.CashDrops.Add(drop);

        _state.State.Transactions.Add(new DrawerTransaction
        {
            Id = result.TransactionId,
            Type = DrawerTransactionType.Drop,
            Amount = command.Amount,
            Description = command.Notes,
            Timestamp = result.Timestamp
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task OpenNoSaleAsync(Guid userId, string? reason = null)
    {
        EnsureOpen();

        // No balance change, just record the no-sale event
        _state.State.Transactions.Add(new DrawerTransaction
        {
            Id = Guid.NewGuid(),
            Type = DrawerTransactionType.NoSale,
            Amount = 0,
            Description = reason ?? "No sale",
            Timestamp = DateTime.UtcNow
        });

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task CountAsync(CountDrawerCommand command)
    {
        EnsureOpen();

        _state.State.Status = DrawerStatus.Counting;
        _state.State.ActualBalance = command.CountedAmount;
        _state.State.LastCountedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task<DrawerClosedResult> CloseAsync(CloseDrawerCommand command)
    {
        if (_state.State.Status == DrawerStatus.Closed)
            throw new InvalidOperationException("Drawer is already closed");

        var expectedBalance = await Ledger!.GetBalanceAsync();
        var variance = command.ActualBalance - expectedBalance;

        _state.State.ExpectedBalance = expectedBalance;
        _state.State.ActualBalance = command.ActualBalance;
        _state.State.Status = DrawerStatus.Closed;
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new DrawerClosedResult(expectedBalance, command.ActualBalance, variance);
    }

    public Task<bool> IsOpenAsync() => Task.FromResult(_state.State.Status == DrawerStatus.Open);

    public async Task<decimal> GetExpectedBalanceAsync()
    {
        if (_state.State.OrganizationId == Guid.Empty)
            return 0;
        return await Ledger!.GetBalanceAsync();
    }

    public Task<DrawerStatus> GetStatusAsync() => Task.FromResult(_state.State.Status);
    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);

    private void EnsureOpen()
    {
        if (_state.State.Status != DrawerStatus.Open)
            throw new InvalidOperationException("Drawer is not open");
    }
}
