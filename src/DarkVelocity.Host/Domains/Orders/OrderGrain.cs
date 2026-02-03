using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Journaled grain for Order management with full event sourcing.
/// All state changes are recorded as events and can be replayed.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class OrderGrain : JournaledGrain<OrderState, IOrderEvent>, IOrderGrain
{
    private Lazy<IAsyncStream<IStreamEvent>>? _orderStream;
    private static int _orderCounter = 1000;

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (State.OrganizationId != Guid.Empty)
        {
            InitializeStreams();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    private void InitializeStreams()
    {
        var orgId = State.OrganizationId;
        _orderStream = new Lazy<IAsyncStream<IStreamEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.OrderStreamNamespace, orgId.ToString());
            return streamProvider.GetStream<IStreamEvent>(streamId);
        });
    }

    private IAsyncStream<IStreamEvent>? OrderStream => _orderStream?.Value;

    /// <summary>
    /// Applies an event to the grain state. This is the core of event sourcing.
    /// </summary>
    protected override void TransitionState(OrderState state, IOrderEvent @event)
    {
        switch (@event)
        {
            case OrderCreated e:
                state.Id = e.OrderId;
                state.OrganizationId = e.OrganizationId;
                state.SiteId = e.SiteId;
                state.OrderNumber = e.OrderNumber;
                state.Status = OrderStatus.Open;
                state.Type = e.Type;
                state.TableId = e.TableId;
                state.TableNumber = e.TableNumber;
                state.CustomerId = e.CustomerId;
                state.GuestCount = e.GuestCount;
                state.CreatedBy = e.CreatedBy;
                state.CreatedAt = e.OccurredAt;
                break;

            case OrderLineAdded e:
                state.Lines.Add(new OrderLine
                {
                    Id = e.LineId,
                    MenuItemId = e.MenuItemId,
                    Name = e.Name,
                    Quantity = e.Quantity,
                    UnitPrice = e.UnitPrice,
                    LineTotal = e.LineTotal,
                    Notes = e.Notes,
                    Modifiers = e.Modifiers,
                    Status = OrderLineStatus.Pending,
                    CreatedAt = e.OccurredAt
                });
                state.RecalculateTotals();
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderLineUpdated e:
                var lineToUpdate = state.Lines.FirstOrDefault(l => l.Id == e.LineId);
                if (lineToUpdate != null)
                {
                    var index = state.Lines.FindIndex(l => l.Id == e.LineId);
                    state.Lines[index] = lineToUpdate with
                    {
                        Quantity = e.Quantity ?? lineToUpdate.Quantity,
                        Notes = e.Notes ?? lineToUpdate.Notes,
                        LineTotal = (e.Quantity ?? lineToUpdate.Quantity) * lineToUpdate.UnitPrice +
                                    lineToUpdate.Modifiers.Sum(m => m.Price * m.Quantity)
                    };
                    state.RecalculateTotals();
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderLineVoided e:
                var lineToVoid = state.Lines.FirstOrDefault(l => l.Id == e.LineId);
                if (lineToVoid != null)
                {
                    var index = state.Lines.FindIndex(l => l.Id == e.LineId);
                    state.Lines[index] = lineToVoid with
                    {
                        Status = OrderLineStatus.Voided,
                        VoidedBy = e.VoidedBy,
                        VoidedAt = e.OccurredAt,
                        VoidReason = e.Reason
                    };
                    state.RecalculateTotals();
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderLineRemoved e:
                state.Lines.RemoveAll(l => l.Id == e.LineId);
                state.RecalculateTotals();
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderSent e:
                foreach (var lineId in e.SentLineIds)
                {
                    var line = state.Lines.FirstOrDefault(l => l.Id == lineId);
                    if (line != null)
                    {
                        var index = state.Lines.FindIndex(l => l.Id == lineId);
                        state.Lines[index] = line with
                        {
                            Status = OrderLineStatus.Sent,
                            SentBy = e.SentBy,
                            SentAt = e.OccurredAt
                        };
                    }
                }
                state.Status = OrderStatus.Sent;
                state.SentAt = e.OccurredAt;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderDiscountApplied e:
                state.Discounts.Add(new OrderDiscount
                {
                    Id = e.DiscountInstanceId,
                    DiscountId = e.DiscountId,
                    Name = e.Name,
                    Type = e.Type,
                    Value = e.Value,
                    Amount = e.Amount,
                    AppliedBy = e.AppliedBy,
                    AppliedAt = e.OccurredAt,
                    Reason = e.Reason,
                    ApprovedBy = e.ApprovedBy
                });
                state.RecalculateTotals();
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderDiscountRemoved e:
                state.Discounts.RemoveAll(d => d.Id == e.DiscountInstanceId);
                state.RecalculateTotals();
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderServiceChargeAdded e:
                state.ServiceCharges.Add(new ServiceCharge
                {
                    Id = e.ServiceChargeId,
                    Name = e.Name,
                    Rate = e.Rate,
                    Amount = e.Amount,
                    IsTaxable = e.IsTaxable
                });
                state.RecalculateTotals();
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderCustomerAssigned e:
                state.CustomerId = e.CustomerId;
                state.CustomerName = e.CustomerName;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderServerAssigned e:
                state.ServerId = e.ServerId;
                state.ServerName = e.ServerName;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderTableTransferred e:
                state.TableId = e.NewTableId;
                state.TableNumber = e.NewTableNumber;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderPaymentRecorded e:
                state.Payments.Add(new OrderPaymentSummary
                {
                    PaymentId = e.PaymentId,
                    Amount = e.Amount,
                    TipAmount = e.TipAmount,
                    Method = e.Method,
                    PaidAt = e.OccurredAt
                });
                state.PaidAmount += e.Amount;
                state.TipTotal += e.TipAmount;
                state.BalanceDue = state.GrandTotal - state.PaidAmount;
                if (state.BalanceDue <= 0)
                    state.Status = OrderStatus.Paid;
                else if (state.PaidAmount > 0)
                    state.Status = OrderStatus.PartiallyPaid;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderPaymentRemoved e:
                state.Payments.RemoveAll(p => p.PaymentId == e.PaymentId);
                state.PaidAmount -= e.Amount;
                state.TipTotal -= e.TipAmount;
                state.BalanceDue = state.GrandTotal - state.PaidAmount;
                if (state.PaidAmount <= 0)
                    state.Status = OrderStatus.Open;
                else if (state.BalanceDue > 0)
                    state.Status = OrderStatus.PartiallyPaid;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderClosed e:
                state.Status = OrderStatus.Closed;
                state.ClosedAt = e.OccurredAt;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderVoided e:
                state.Status = OrderStatus.Voided;
                state.VoidedBy = e.VoidedBy;
                state.VoidedAt = e.OccurredAt;
                state.VoidReason = e.Reason;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderReopened e:
                state.Status = OrderStatus.Open;
                state.ClosedAt = null;
                state.VoidedBy = null;
                state.VoidedAt = null;
                state.VoidReason = null;
                state.UpdatedAt = e.OccurredAt;
                break;
        }
    }

    public async Task<OrderCreatedResult> CreateAsync(CreateOrderCommand command)
    {
        if (State.Id != Guid.Empty)
            throw new InvalidOperationException("Order already exists");

        if (command.GuestCount <= 0)
            throw new ArgumentException("Guest count must be greater than zero", nameof(command));

        var key = this.GetPrimaryKeyString();
        var (orgId, siteId, _, orderId) = GrainKeys.ParseSiteEntity(key);

        var orderNumber = $"ORD-{Interlocked.Increment(ref _orderCounter):D6}";
        var now = DateTime.UtcNow;

        RaiseEvent(new OrderCreated
        {
            OrderId = orderId,
            OrganizationId = orgId,
            SiteId = siteId,
            OrderNumber = orderNumber,
            Type = command.Type,
            TableId = command.TableId,
            TableNumber = command.TableNumber,
            CustomerId = command.CustomerId,
            GuestCount = command.GuestCount,
            CreatedBy = command.CreatedBy,
            OccurredAt = now
        });

        await ConfirmEvents();
        InitializeStreams();

        // Publish order created event
        if (OrderStream != null)
        {
            await OrderStream.OnNextAsync(new OrderCreatedEvent(
                orderId,
                siteId,
                orderNumber,
                command.CreatedBy)
            {
                OrganizationId = orgId
            });
        }

        return new OrderCreatedResult(orderId, orderNumber, State.CreatedAt);
    }

    public Task<OrderState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public async Task<AddLineResult> AddLineAsync(AddLineCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Name cannot be empty", nameof(command));

        if (command.UnitPrice < 0)
            throw new ArgumentException("Unit price cannot be negative", nameof(command));

        if (command.Quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(command));

        var lineId = Guid.NewGuid();
        var lineTotal = command.UnitPrice * command.Quantity;
        var modifierTotal = command.Modifiers?.Sum(m => m.Price * m.Quantity) ?? 0;
        lineTotal += modifierTotal;
        var now = DateTime.UtcNow;

        RaiseEvent(new OrderLineAdded
        {
            OrderId = State.Id,
            LineId = lineId,
            MenuItemId = command.MenuItemId,
            Name = command.Name,
            Quantity = command.Quantity,
            UnitPrice = command.UnitPrice,
            LineTotal = lineTotal,
            Notes = command.Notes,
            Modifiers = command.Modifiers ?? [],
            OccurredAt = now
        });

        await ConfirmEvents();

        // Publish line added event
        if (OrderStream != null)
        {
            await OrderStream.OnNextAsync(new OrderLineAddedEvent(
                State.Id,
                State.SiteId,
                lineId,
                command.MenuItemId,
                command.Name,
                command.Quantity,
                command.UnitPrice,
                lineTotal)
            {
                OrganizationId = State.OrganizationId
            });
        }

        return new AddLineResult(lineId, lineTotal, State.GrandTotal);
    }

    public async Task UpdateLineAsync(UpdateLineCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var line = State.Lines.FirstOrDefault(l => l.Id == command.LineId)
            ?? throw new InvalidOperationException("Line not found");

        RaiseEvent(new OrderLineUpdated
        {
            OrderId = State.Id,
            LineId = command.LineId,
            Quantity = command.Quantity,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task VoidLineAsync(VoidLineCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var line = State.Lines.FirstOrDefault(l => l.Id == command.LineId)
            ?? throw new InvalidOperationException("Line not found");

        RaiseEvent(new OrderLineVoided
        {
            OrderId = State.Id,
            LineId = command.LineId,
            VoidedBy = command.VoidedBy,
            Reason = command.Reason,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task RemoveLineAsync(Guid lineId)
    {
        EnsureExists();
        EnsureNotClosed();

        var exists = State.Lines.Any(l => l.Id == lineId);
        if (!exists)
            throw new InvalidOperationException("Line not found");

        RaiseEvent(new OrderLineRemoved
        {
            OrderId = State.Id,
            LineId = lineId,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task SendAsync(Guid sentBy)
    {
        EnsureExists();
        EnsureNotClosed();

        var pendingLines = State.Lines.Where(l => l.Status == OrderLineStatus.Pending).ToList();
        if (!pendingLines.Any())
            throw new InvalidOperationException("No pending items to send");

        var pendingLineIds = pendingLines.Select(l => l.Id).ToList();

        RaiseEvent(new OrderSent
        {
            OrderId = State.Id,
            SentBy = sentBy,
            SentLineIds = pendingLineIds,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();

        // Publish OrderSentToKitchenEvent for kitchen domain to create tickets
        if (OrderStream != null)
        {
            var kitchenLines = pendingLines.Select(l => new KitchenLineItem(
                LineId: l.Id,
                MenuItemId: l.MenuItemId,
                Name: l.Name,
                Quantity: l.Quantity,
                Modifiers: l.Modifiers?.Select(m => m.Name).ToList(),
                SpecialInstructions: l.Notes)).ToList();

            await OrderStream.OnNextAsync(new OrderSentToKitchenEvent(
                OrderId: State.Id,
                SiteId: State.SiteId,
                OrderNumber: State.OrderNumber,
                OrderType: State.Type.ToString(),
                TableNumber: State.TableNumber,
                GuestCount: State.GuestCount,
                ServerId: State.ServerId,
                ServerName: State.ServerName,
                Lines: kitchenLines)
            {
                OrganizationId = State.OrganizationId
            });
        }
    }

    public Task<OrderTotals> RecalculateTotalsAsync()
    {
        EnsureExists();
        // State is automatically recalculated via TransitionState
        return Task.FromResult(GetTotalsInternal());
    }

    public async Task ApplyDiscountAsync(ApplyDiscountCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var discountAmount = command.Type switch
        {
            DiscountType.Percentage => State.Subtotal * (command.Value / 100m),
            DiscountType.FixedAmount => command.Value,
            _ => command.Value
        };

        RaiseEvent(new OrderDiscountApplied
        {
            OrderId = State.Id,
            DiscountInstanceId = Guid.NewGuid(),
            DiscountId = command.DiscountId,
            Name = command.Name,
            Type = command.Type,
            Value = command.Value,
            Amount = discountAmount,
            AppliedBy = command.AppliedBy,
            Reason = command.Reason,
            ApprovedBy = command.ApprovedBy,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task RemoveDiscountAsync(Guid discountId)
    {
        EnsureExists();
        EnsureNotClosed();

        RaiseEvent(new OrderDiscountRemoved
        {
            OrderId = State.Id,
            DiscountInstanceId = discountId,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task AddServiceChargeAsync(string name, decimal rate, bool isTaxable)
    {
        EnsureExists();
        EnsureNotClosed();

        var amount = State.Subtotal * (rate / 100m);

        RaiseEvent(new OrderServiceChargeAdded
        {
            OrderId = State.Id,
            ServiceChargeId = Guid.NewGuid(),
            Name = name,
            Rate = rate,
            Amount = amount,
            IsTaxable = isTaxable,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task AssignCustomerAsync(Guid customerId, string? customerName)
    {
        EnsureExists();

        RaiseEvent(new OrderCustomerAssigned
        {
            OrderId = State.Id,
            CustomerId = customerId,
            CustomerName = customerName,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task AssignServerAsync(Guid serverId, string serverName)
    {
        EnsureExists();

        RaiseEvent(new OrderServerAssigned
        {
            OrderId = State.Id,
            ServerId = serverId,
            ServerName = serverName,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task TransferTableAsync(Guid newTableId, string newTableNumber, Guid transferredBy)
    {
        EnsureExists();
        EnsureNotClosed();

        RaiseEvent(new OrderTableTransferred
        {
            OrderId = State.Id,
            NewTableId = newTableId,
            NewTableNumber = newTableNumber,
            TransferredBy = transferredBy,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task RecordPaymentAsync(Guid paymentId, decimal amount, decimal tipAmount, string method)
    {
        EnsureExists();

        // Allow zero payments only when balance is already zero (e.g., 100% discount scenarios)
        if (amount < 0)
            throw new ArgumentException("Payment amount cannot be negative", nameof(amount));
        if (amount == 0 && State.BalanceDue > 0)
            throw new ArgumentException("Payment amount must be greater than zero when balance is due", nameof(amount));

        if (tipAmount < 0)
            throw new ArgumentException("Tip amount cannot be negative", nameof(tipAmount));

        RaiseEvent(new OrderPaymentRecorded
        {
            OrderId = State.Id,
            PaymentId = paymentId,
            Amount = amount,
            TipAmount = tipAmount,
            Method = method,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task RemovePaymentAsync(Guid paymentId)
    {
        EnsureExists();

        var payment = State.Payments.FirstOrDefault(p => p.PaymentId == paymentId);
        if (payment != null)
        {
            RaiseEvent(new OrderPaymentRemoved
            {
                OrderId = State.Id,
                PaymentId = paymentId,
                Amount = payment.Amount,
                TipAmount = payment.TipAmount,
                OccurredAt = DateTime.UtcNow
            });

            await ConfirmEvents();
        }
    }

    public async Task CloseAsync(Guid closedBy)
    {
        EnsureExists();

        if (State.BalanceDue > 0)
            throw new InvalidOperationException("Cannot close order with outstanding balance");

        RaiseEvent(new OrderClosed
        {
            OrderId = State.Id,
            ClosedBy = closedBy,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();

        // Build order line snapshots
        var lineSnapshots = State.Lines
            .Where(l => l.Status != OrderLineStatus.Voided)
            .Select(l => new OrderLineSnapshot(
                l.Id,
                l.MenuItemId,
                l.Name,
                l.Quantity,
                l.UnitPrice,
                l.LineTotal,
                null))
            .ToList();

        var businessDate = DateOnly.FromDateTime(State.ClosedAt!.Value);

        // Publish order completed event - single source of truth
        // Downstream subscribers (Sales, Loyalty, Inventory) react to this event
        if (OrderStream != null)
        {
            await OrderStream.OnNextAsync(new OrderCompletedEvent(
                State.Id,
                State.SiteId,
                State.OrderNumber,
                State.Subtotal,
                State.TaxTotal,
                State.GrandTotal,
                State.DiscountTotal,
                lineSnapshots,
                State.ServerId,
                State.ServerName,
                State.CustomerId,
                State.CustomerName,
                State.GuestCount,
                State.Type.ToString(),
                businessDate)
            {
                OrganizationId = State.OrganizationId
            });
        }
    }

    public async Task VoidAsync(VoidOrderCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var voidedAmount = State.GrandTotal;
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow);

        RaiseEvent(new OrderVoided
        {
            OrderId = State.Id,
            VoidedBy = command.VoidedBy,
            Reason = command.Reason,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();

        // Publish order voided event - single source of truth
        // Downstream subscribers (Sales, Loyalty) react to this event
        if (OrderStream != null)
        {
            await OrderStream.OnNextAsync(new OrderVoidedEvent(
                State.Id,
                State.SiteId,
                State.OrderNumber,
                voidedAmount,
                command.Reason,
                command.VoidedBy,
                businessDate,
                State.CustomerId)
            {
                OrganizationId = State.OrganizationId
            });
        }
    }

    public async Task ReopenAsync(Guid reopenedBy, string reason)
    {
        EnsureExists();

        if (State.Status != OrderStatus.Closed && State.Status != OrderStatus.Voided)
            throw new InvalidOperationException("Can only reopen closed or voided orders");

        RaiseEvent(new OrderReopened
        {
            OrderId = State.Id,
            ReopenedBy = reopenedBy,
            Reason = reason,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.Id != Guid.Empty);
    public Task<OrderStatus> GetStatusAsync() => Task.FromResult(State.Status);
    public Task<OrderTotals> GetTotalsAsync() => Task.FromResult(GetTotalsInternal());
    public Task<IReadOnlyList<OrderLine>> GetLinesAsync() => Task.FromResult<IReadOnlyList<OrderLine>>(State.Lines);

    /// <summary>
    /// Gets the event sourcing version for this grain.
    /// </summary>
    public Task<int> GetVersionAsync() => Task.FromResult(Version);

    public async Task<CloneOrderResult> CloneAsync(CloneOrderCommand command)
    {
        EnsureExists();

        // Clone logic would create a new order grain - not part of this grain's responsibility
        // Return info about what would be cloned
        var activeLines = State.Lines.Where(l => l.Status != OrderLineStatus.Voided).ToList();

        // Note: Actual cloning should be orchestrated by a service/controller that creates a new OrderGrain
        throw new NotImplementedException("Clone should be orchestrated by the calling service");
    }

    private void EnsureExists()
    {
        if (State.Id == Guid.Empty)
            throw new InvalidOperationException("Order does not exist");
    }

    private void EnsureNotClosed()
    {
        if (State.Status is OrderStatus.Closed or OrderStatus.Voided)
            throw new InvalidOperationException("Order is closed or voided");
    }

    private OrderTotals GetTotalsInternal() => new(
        State.Subtotal,
        State.DiscountTotal,
        State.ServiceChargeTotal,
        State.TaxTotal,
        State.GrandTotal,
        State.PaidAmount,
        State.BalanceDue);
}
