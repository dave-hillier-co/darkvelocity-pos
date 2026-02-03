using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class PaymentGrain : JournaledGrain<PaymentState, IPaymentEvent>, IPaymentGrain
{
    private readonly IGrainFactory _grainFactory;
    private Lazy<IAsyncStream<IStreamEvent>>? _paymentStream;

    public PaymentGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (State.OrganizationId != Guid.Empty)
        {
            InitializeStream();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    protected override void TransitionState(PaymentState state, IPaymentEvent @event)
    {
        switch (@event)
        {
            case PaymentInitiated e:
                state.Id = e.PaymentId;
                state.OrganizationId = e.OrganizationId;
                state.SiteId = e.SiteId;
                state.OrderId = e.OrderId;
                state.Method = e.Method;
                state.Status = PaymentStatus.Initiated;
                state.Amount = e.Amount;
                state.TotalAmount = e.Amount;
                state.CashierId = e.CashierId;
                state.CustomerId = e.CustomerId;
                state.DrawerId = e.DrawerId;
                state.CreatedAt = e.OccurredAt;
                break;

            case PaymentAuthorizationRequested:
                state.Status = PaymentStatus.Authorizing;
                break;

            case PaymentAuthorized e:
                state.AuthorizationCode = e.AuthorizationCode;
                state.GatewayReference = e.GatewayReference;
                state.CardInfo = e.CardInfo;
                state.Status = PaymentStatus.Authorized;
                state.AuthorizedAt = e.OccurredAt;
                break;

            case PaymentDeclined:
                state.Status = PaymentStatus.Declined;
                break;

            case PaymentCaptured e:
                state.Status = PaymentStatus.Captured;
                state.CapturedAt = e.OccurredAt;
                break;

            case CashPaymentCompleted e:
                state.AmountTendered = e.AmountTendered;
                state.TipAmount = e.TipAmount;
                state.TotalAmount = e.TotalAmount;
                state.ChangeGiven = e.ChangeGiven;
                state.Status = PaymentStatus.Completed;
                state.CompletedAt = e.OccurredAt;
                break;

            case CardPaymentCompleted e:
                state.GatewayReference = e.GatewayReference;
                state.AuthorizationCode = e.AuthorizationCode;
                state.CardInfo = e.CardInfo;
                state.GatewayName = e.GatewayName;
                state.TipAmount = e.TipAmount;
                state.TotalAmount = e.TotalAmount;
                state.Status = PaymentStatus.Completed;
                state.CapturedAt = e.OccurredAt;
                state.CompletedAt = e.OccurredAt;
                break;

            case GiftCardPaymentCompleted e:
                state.GiftCardId = e.GiftCardId;
                state.GiftCardNumber = e.CardNumber;
                state.TotalAmount = e.TotalAmount;
                state.Status = PaymentStatus.Completed;
                state.CompletedAt = e.OccurredAt;
                break;

            case PaymentVoided e:
                state.Status = PaymentStatus.Voided;
                state.VoidedBy = e.VoidedBy;
                state.VoidedAt = e.OccurredAt;
                state.VoidReason = e.Reason;
                break;

            case PaymentRefunded e:
                var refund = new RefundInfo
                {
                    RefundId = e.RefundId,
                    Amount = e.Amount,
                    Reason = e.Reason,
                    IssuedBy = e.IssuedBy,
                    IssuedAt = e.OccurredAt,
                    GatewayReference = e.GatewayReference
                };
                state.Refunds.Add(refund);
                state.RefundedAmount += e.Amount;
                if (state.RefundedAmount >= state.TotalAmount)
                    state.Status = PaymentStatus.Refunded;
                else
                    state.Status = PaymentStatus.PartiallyRefunded;
                break;

            case PaymentTipAdded e:
                state.TipAmount = e.TipAmount;
                state.TotalAmount = e.NewTotalAmount;
                break;

            case PaymentBatchAssigned e:
                state.BatchId = e.BatchId;
                break;
        }
    }

    private void InitializeStream()
    {
        var orgId = State.OrganizationId;
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
        if (State.Id != Guid.Empty)
            throw new InvalidOperationException("Payment already exists");

        var key = this.GetPrimaryKeyString();
        var (_, _, _, paymentId) = GrainKeys.ParseSiteEntity(key);
        var now = DateTime.UtcNow;

        RaiseEvent(new PaymentInitiated
        {
            PaymentId = paymentId,
            OrganizationId = command.OrganizationId,
            SiteId = command.SiteId,
            OrderId = command.OrderId,
            Method = command.Method,
            Amount = command.Amount,
            CashierId = command.CashierId,
            CustomerId = command.CustomerId,
            DrawerId = command.DrawerId,
            OccurredAt = now
        });
        await ConfirmEvents();

        InitializeStream();

        // Publish stream event for integration
        await PaymentStream!.OnNextAsync(new PaymentInitiatedEvent(
            paymentId,
            State.SiteId,
            State.OrderId,
            State.Amount,
            State.Method.ToString(),
            State.CustomerId,
            State.CashierId)
        {
            OrganizationId = State.OrganizationId
        });

        return new PaymentInitiatedResult(paymentId, State.CreatedAt);
    }

    public Task<PaymentState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public async Task<PaymentCompletedResult> CompleteCashAsync(CompleteCashPaymentCommand command)
    {
        EnsureExists();
        EnsureStatus(PaymentStatus.Initiated);

        var totalAmount = State.Amount + command.TipAmount;
        var changeGiven = command.AmountTendered - totalAmount;

        RaiseEvent(new CashPaymentCompleted
        {
            PaymentId = State.Id,
            AmountTendered = command.AmountTendered,
            TipAmount = command.TipAmount,
            TotalAmount = totalAmount,
            ChangeGiven = changeGiven,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        await PublishPaymentCompletedEventAsync();

        return new PaymentCompletedResult(State.TotalAmount, State.ChangeGiven);
    }

    public async Task<PaymentCompletedResult> CompleteCardAsync(ProcessCardPaymentCommand command)
    {
        EnsureExists();

        if (State.Status is not (PaymentStatus.Initiated or PaymentStatus.Authorized))
            throw new InvalidOperationException($"Invalid status for card completion: {State.Status}");

        var totalAmount = State.Amount + command.TipAmount;

        RaiseEvent(new CardPaymentCompleted
        {
            PaymentId = State.Id,
            GatewayReference = command.GatewayReference,
            AuthorizationCode = command.AuthorizationCode,
            CardInfo = command.CardInfo,
            GatewayName = command.GatewayName,
            TipAmount = command.TipAmount,
            TotalAmount = totalAmount,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        await PublishPaymentCompletedEventAsync();

        return new PaymentCompletedResult(State.TotalAmount, null);
    }

    public async Task<PaymentCompletedResult> CompleteGiftCardAsync(ProcessGiftCardPaymentCommand command)
    {
        EnsureExists();
        EnsureStatus(PaymentStatus.Initiated);

        RaiseEvent(new GiftCardPaymentCompleted
        {
            PaymentId = State.Id,
            GiftCardId = command.GiftCardId,
            CardNumber = command.CardNumber,
            TotalAmount = State.Amount,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        await PublishPaymentCompletedEventAsync();

        return new PaymentCompletedResult(State.TotalAmount, null);
    }

    public async Task RequestAuthorizationAsync()
    {
        EnsureExists();
        EnsureStatus(PaymentStatus.Initiated);

        RaiseEvent(new PaymentAuthorizationRequested
        {
            PaymentId = State.Id,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task RecordAuthorizationAsync(string authCode, string gatewayRef, CardInfo cardInfo)
    {
        EnsureExists();
        EnsureStatus(PaymentStatus.Authorizing);

        RaiseEvent(new PaymentAuthorized
        {
            PaymentId = State.Id,
            AuthorizationCode = authCode,
            GatewayReference = gatewayRef,
            CardInfo = cardInfo,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task RecordDeclineAsync(string declineCode, string reason)
    {
        EnsureExists();
        EnsureStatus(PaymentStatus.Authorizing);

        RaiseEvent(new PaymentDeclined
        {
            PaymentId = State.Id,
            DeclineCode = declineCode,
            Reason = reason,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task CaptureAsync()
    {
        EnsureExists();
        EnsureStatus(PaymentStatus.Authorized);

        RaiseEvent(new PaymentCaptured
        {
            PaymentId = State.Id,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task<RefundResult> RefundAsync(RefundPaymentCommand command)
    {
        EnsureExists();

        if (State.Status is not (PaymentStatus.Completed or PaymentStatus.PartiallyRefunded))
            throw new InvalidOperationException("Can only refund completed payments");

        if (command.Amount <= 0)
            throw new InvalidOperationException("Refund amount must be positive");

        if (command.Amount > State.TotalAmount - State.RefundedAmount)
            throw new InvalidOperationException("Refund amount exceeds available balance");

        var refundId = Guid.NewGuid();

        RaiseEvent(new PaymentRefunded
        {
            PaymentId = State.Id,
            RefundId = refundId,
            Amount = command.Amount,
            Reason = command.Reason,
            IssuedBy = command.IssuedBy,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Publish stream event for integration
        // For gift card payments, the subscriber will credit the refund back to the gift card
        await PaymentStream!.OnNextAsync(new PaymentRefundedEvent(
            State.Id,
            State.SiteId,
            State.OrderId,
            refundId,
            command.Amount,
            State.RefundedAmount,
            State.Method.ToString(),
            command.Reason,
            command.IssuedBy,
            State.GiftCardId)
        {
            OrganizationId = State.OrganizationId
        });

        return new RefundResult(refundId, State.RefundedAmount, State.TotalAmount - State.RefundedAmount);
    }

    public Task<RefundResult> PartialRefundAsync(RefundPaymentCommand command)
    {
        return RefundAsync(command);
    }

    public async Task VoidAsync(VoidPaymentCommand command)
    {
        EnsureExists();

        if (State.Status is PaymentStatus.Voided or PaymentStatus.Refunded)
            throw new InvalidOperationException($"Cannot void payment with status: {State.Status}");

        var voidedAmount = State.TotalAmount;

        RaiseEvent(new PaymentVoided
        {
            PaymentId = State.Id,
            VoidedBy = command.VoidedBy,
            Reason = command.Reason,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Publish stream event for integration
        await PaymentStream!.OnNextAsync(new PaymentVoidedEvent(
            State.Id,
            State.SiteId,
            State.OrderId,
            voidedAmount,
            State.Method.ToString(),
            command.Reason,
            command.VoidedBy)
        {
            OrganizationId = State.OrganizationId
        });
    }

    public async Task AdjustTipAsync(AdjustTipCommand command)
    {
        EnsureExists();

        if (State.Status != PaymentStatus.Completed)
            throw new InvalidOperationException("Can only adjust tip on completed payments");

        var newTotalAmount = State.Amount + command.NewTipAmount;

        RaiseEvent(new PaymentTipAdded
        {
            PaymentId = State.Id,
            TipAmount = command.NewTipAmount,
            NewTotalAmount = newTotalAmount,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task AssignToBatchAsync(Guid batchId)
    {
        EnsureExists();

        RaiseEvent(new PaymentBatchAssigned
        {
            PaymentId = State.Id,
            BatchId = batchId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.Id != Guid.Empty);
    public Task<PaymentStatus> GetStatusAsync() => Task.FromResult(State.Status);

    private void EnsureExists()
    {
        if (State.Id == Guid.Empty)
            throw new InvalidOperationException("Payment does not exist");
    }

    private void EnsureStatus(PaymentStatus expected)
    {
        if (State.Status != expected)
            throw new InvalidOperationException($"Invalid status. Expected {expected}, got {State.Status}");
    }

    private async Task PublishPaymentCompletedEventAsync()
    {
        // Publish payment completed event via stream for integration
        // This replaces the direct grain call to OrderGrain.RecordPaymentAsync
        // allowing multiple subscribers to react to payment completions
        // For gift card payments, the subscriber will redeem from the gift card
        await PaymentStream!.OnNextAsync(new PaymentCompletedEvent(
            State.Id,
            State.SiteId,
            State.OrderId,
            State.Amount,
            State.TipAmount,
            State.TotalAmount,
            State.Method.ToString(),
            State.CustomerId,
            State.CashierId,
            State.DrawerId,
            State.GatewayReference,
            State.CardInfo?.MaskedNumber,
            State.GiftCardId)
        {
            OrganizationId = State.OrganizationId
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
                ["paymentId"] = command.PaymentId.ToString()
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
