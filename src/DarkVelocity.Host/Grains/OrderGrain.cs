using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

public class OrderGrain : Grain, IOrderGrain
{
    private readonly IPersistentState<OrderState> _state;
    private IAsyncStream<IStreamEvent>? _orderStream;
    private IAsyncStream<IStreamEvent>? _salesStream;
    private static int _orderCounter = 1000;

    public OrderGrain(
        [PersistentState("order", "OrleansStorage")]
        IPersistentState<OrderState> state)
    {
        _state = state;
    }

    private IAsyncStream<IStreamEvent> GetOrderStream()
    {
        if (_orderStream == null && _state.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var orderStreamId = StreamId.Create(StreamConstants.OrderStreamNamespace, _state.State.OrganizationId.ToString());
            _orderStream = streamProvider.GetStream<IStreamEvent>(orderStreamId);
        }
        return _orderStream!;
    }

    private IAsyncStream<IStreamEvent> GetSalesStream()
    {
        if (_salesStream == null && _state.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var salesStreamId = StreamId.Create(StreamConstants.SalesStreamNamespace, _state.State.OrganizationId.ToString());
            _salesStream = streamProvider.GetStream<IStreamEvent>(salesStreamId);
        }
        return _salesStream!;
    }

    public async Task<OrderCreatedResult> CreateAsync(CreateOrderCommand command)
    {
        if (_state.State.Id != Guid.Empty)
            throw new InvalidOperationException("Order already exists");

        var key = this.GetPrimaryKeyString();
        var (orgId, siteId, _, orderId) = GrainKeys.ParseSiteEntity(key);

        var orderNumber = $"ORD-{Interlocked.Increment(ref _orderCounter):D6}";

        _state.State = new OrderState
        {
            Id = orderId,
            OrganizationId = orgId,
            SiteId = siteId,
            OrderNumber = orderNumber,
            Status = OrderStatus.Open,
            Type = command.Type,
            TableId = command.TableId,
            TableNumber = command.TableNumber,
            CustomerId = command.CustomerId,
            GuestCount = command.GuestCount,
            CreatedBy = command.CreatedBy,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();

        // Publish order created event
        if (GetOrderStream() != null)
        {
            await GetOrderStream().OnNextAsync(new OrderCreatedEvent(
                orderId,
                siteId,
                orderNumber,
                command.CreatedBy)
            {
                OrganizationId = orgId
            });
        }

        return new OrderCreatedResult(orderId, orderNumber, _state.State.CreatedAt);
    }

    public Task<OrderState> GetStateAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task<AddLineResult> AddLineAsync(AddLineCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var lineId = Guid.NewGuid();
        var lineTotal = command.UnitPrice * command.Quantity;

        // Add modifier costs
        var modifierTotal = command.Modifiers?.Sum(m => m.Price * m.Quantity) ?? 0;
        lineTotal += modifierTotal;

        var line = new OrderLine
        {
            Id = lineId,
            MenuItemId = command.MenuItemId,
            Name = command.Name,
            Quantity = command.Quantity,
            UnitPrice = command.UnitPrice,
            LineTotal = lineTotal,
            Notes = command.Notes,
            Modifiers = command.Modifiers ?? [],
            Status = OrderLineStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _state.State.Lines.Add(line);
        RecalculateTotalsInternal();

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish line added event
        if (GetOrderStream() != null)
        {
            await GetOrderStream().OnNextAsync(new OrderLineAddedEvent(
                _state.State.Id,
                _state.State.SiteId,
                lineId,
                command.MenuItemId,
                command.Name,
                command.Quantity,
                command.UnitPrice,
                lineTotal)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }

        return new AddLineResult(lineId, lineTotal, _state.State.GrandTotal);
    }

    public async Task UpdateLineAsync(UpdateLineCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var line = _state.State.Lines.FirstOrDefault(l => l.Id == command.LineId)
            ?? throw new InvalidOperationException("Line not found");

        var updatedLine = line with
        {
            Quantity = command.Quantity ?? line.Quantity,
            Notes = command.Notes ?? line.Notes,
            LineTotal = (command.Quantity ?? line.Quantity) * line.UnitPrice +
                        line.Modifiers.Sum(m => m.Price * m.Quantity)
        };

        var index = _state.State.Lines.FindIndex(l => l.Id == command.LineId);
        _state.State.Lines[index] = updatedLine;

        RecalculateTotalsInternal();
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task VoidLineAsync(VoidLineCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var line = _state.State.Lines.FirstOrDefault(l => l.Id == command.LineId)
            ?? throw new InvalidOperationException("Line not found");

        var voidedLine = line with
        {
            Status = OrderLineStatus.Voided,
            VoidedBy = command.VoidedBy,
            VoidedAt = DateTime.UtcNow,
            VoidReason = command.Reason
        };

        var index = _state.State.Lines.FindIndex(l => l.Id == command.LineId);
        _state.State.Lines[index] = voidedLine;

        RecalculateTotalsInternal();
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RemoveLineAsync(Guid lineId)
    {
        EnsureExists();
        EnsureNotClosed();

        var removed = _state.State.Lines.RemoveAll(l => l.Id == lineId);
        if (removed == 0)
            throw new InvalidOperationException("Line not found");

        RecalculateTotalsInternal();
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task SendAsync(Guid sentBy)
    {
        EnsureExists();
        EnsureNotClosed();

        var pendingLines = _state.State.Lines.Where(l => l.Status == OrderLineStatus.Pending).ToList();
        if (!pendingLines.Any())
            throw new InvalidOperationException("No pending items to send");

        foreach (var line in pendingLines)
        {
            var index = _state.State.Lines.FindIndex(l => l.Id == line.Id);
            _state.State.Lines[index] = line with
            {
                Status = OrderLineStatus.Sent,
                SentBy = sentBy,
                SentAt = DateTime.UtcNow
            };
        }

        _state.State.Status = OrderStatus.Sent;
        _state.State.SentAt = DateTime.UtcNow;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish OrderSentToKitchenEvent for kitchen domain to create tickets
        if (GetOrderStream() != null)
        {
            var kitchenLines = pendingLines.Select(l => new KitchenLineItem(
                LineId: l.Id,
                MenuItemId: l.MenuItemId,
                Name: l.Name,
                Quantity: l.Quantity,
                Modifiers: l.Modifiers?.Select(m => m.Name).ToList(),
                SpecialInstructions: l.Notes)).ToList();

            await GetOrderStream().OnNextAsync(new OrderSentToKitchenEvent(
                OrderId: _state.State.Id,
                SiteId: _state.State.SiteId,
                OrderNumber: _state.State.OrderNumber,
                OrderType: _state.State.Type.ToString(),
                TableNumber: _state.State.TableNumber,
                GuestCount: _state.State.GuestCount,
                ServerId: _state.State.ServerId,
                ServerName: _state.State.ServerName,
                Lines: kitchenLines)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }
    }

    public Task<OrderTotals> RecalculateTotalsAsync()
    {
        EnsureExists();
        RecalculateTotalsInternal();
        return Task.FromResult(GetTotalsInternal());
    }

    public async Task ApplyDiscountAsync(ApplyDiscountCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var discountAmount = command.Type switch
        {
            DiscountType.Percentage => _state.State.Subtotal * (command.Value / 100m),
            DiscountType.FixedAmount => command.Value,
            _ => command.Value
        };

        var discount = new OrderDiscount
        {
            Id = Guid.NewGuid(),
            DiscountId = command.DiscountId,
            Name = command.Name,
            Type = command.Type,
            Value = command.Value,
            Amount = discountAmount,
            AppliedBy = command.AppliedBy,
            AppliedAt = DateTime.UtcNow,
            Reason = command.Reason,
            ApprovedBy = command.ApprovedBy
        };

        _state.State.Discounts.Add(discount);
        RecalculateTotalsInternal();

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RemoveDiscountAsync(Guid discountId)
    {
        EnsureExists();
        EnsureNotClosed();

        _state.State.Discounts.RemoveAll(d => d.Id == discountId);
        RecalculateTotalsInternal();

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task AddServiceChargeAsync(string name, decimal rate, bool isTaxable)
    {
        EnsureExists();
        EnsureNotClosed();

        var amount = _state.State.Subtotal * (rate / 100m);

        var serviceCharge = new ServiceCharge
        {
            Id = Guid.NewGuid(),
            Name = name,
            Rate = rate,
            Amount = amount,
            IsTaxable = isTaxable
        };

        _state.State.ServiceCharges.Add(serviceCharge);
        RecalculateTotalsInternal();

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task AssignCustomerAsync(Guid customerId, string? customerName)
    {
        EnsureExists();

        _state.State.CustomerId = customerId;
        _state.State.CustomerName = customerName;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task AssignServerAsync(Guid serverId, string serverName)
    {
        EnsureExists();

        _state.State.ServerId = serverId;
        _state.State.ServerName = serverName;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task TransferTableAsync(Guid newTableId, string newTableNumber, Guid transferredBy)
    {
        EnsureExists();
        EnsureNotClosed();

        _state.State.TableId = newTableId;
        _state.State.TableNumber = newTableNumber;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RecordPaymentAsync(Guid paymentId, decimal amount, decimal tipAmount, string method)
    {
        EnsureExists();

        var payment = new OrderPaymentSummary
        {
            PaymentId = paymentId,
            Amount = amount,
            TipAmount = tipAmount,
            Method = method,
            PaidAt = DateTime.UtcNow
        };

        _state.State.Payments.Add(payment);
        _state.State.PaidAmount += amount;
        _state.State.TipTotal += tipAmount;
        _state.State.BalanceDue = _state.State.GrandTotal - _state.State.PaidAmount;

        if (_state.State.BalanceDue <= 0)
            _state.State.Status = OrderStatus.Paid;
        else if (_state.State.PaidAmount > 0)
            _state.State.Status = OrderStatus.PartiallyPaid;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RemovePaymentAsync(Guid paymentId)
    {
        EnsureExists();

        var payment = _state.State.Payments.FirstOrDefault(p => p.PaymentId == paymentId);
        if (payment != null)
        {
            _state.State.Payments.Remove(payment);
            _state.State.PaidAmount -= payment.Amount;
            _state.State.TipTotal -= payment.TipAmount;
            _state.State.BalanceDue = _state.State.GrandTotal - _state.State.PaidAmount;

            if (_state.State.PaidAmount <= 0)
                _state.State.Status = OrderStatus.Open;
            else if (_state.State.BalanceDue > 0)
                _state.State.Status = OrderStatus.PartiallyPaid;

            _state.State.UpdatedAt = DateTime.UtcNow;
            _state.State.Version++;

            await _state.WriteStateAsync();
        }
    }

    public async Task CloseAsync(Guid closedBy)
    {
        EnsureExists();

        if (_state.State.BalanceDue > 0)
            throw new InvalidOperationException("Cannot close order with outstanding balance");

        _state.State.Status = OrderStatus.Closed;
        _state.State.ClosedAt = DateTime.UtcNow;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Build order line snapshots
        var lineSnapshots = _state.State.Lines
            .Where(l => l.Status != OrderLineStatus.Voided)
            .Select(l => new OrderLineSnapshot(
                l.Id,
                l.MenuItemId,
                l.Name,
                l.Quantity,
                l.UnitPrice,
                l.LineTotal,
                null)) // RecipeId would come from menu item lookup
            .ToList();

        // Publish order completed event (triggers loyalty, inventory, reporting)
        if (GetOrderStream() != null)
        {
            await GetOrderStream().OnNextAsync(new OrderCompletedEvent(
                _state.State.Id,
                _state.State.SiteId,
                _state.State.OrderNumber,
                _state.State.Subtotal,
                _state.State.TaxTotal,
                _state.State.GrandTotal,
                _state.State.DiscountTotal,
                lineSnapshots,
                _state.State.ServerId,
                _state.State.ServerName,
                _state.State.CustomerId,
                _state.State.CustomerName,
                _state.State.GuestCount,
                _state.State.Type.ToString())
            {
                OrganizationId = _state.State.OrganizationId
            });
        }

        // Publish sale recorded event (triggers sales aggregation)
        if (GetSalesStream() != null)
        {
            await GetSalesStream().OnNextAsync(new SaleRecordedEvent(
                _state.State.Id,
                _state.State.SiteId,
                DateOnly.FromDateTime(_state.State.ClosedAt.Value),
                _state.State.Subtotal,
                _state.State.DiscountTotal,
                _state.State.Subtotal - _state.State.DiscountTotal,
                _state.State.TaxTotal,
                0m, // TheoreticalCOGS - would be calculated from recipes
                lineSnapshots.Sum(l => l.Quantity),
                _state.State.GuestCount,
                _state.State.Type.ToString())
            {
                OrganizationId = _state.State.OrganizationId
            });
        }
    }

    public async Task VoidAsync(VoidOrderCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var voidedAmount = _state.State.GrandTotal;

        _state.State.Status = OrderStatus.Voided;
        _state.State.VoidedBy = command.VoidedBy;
        _state.State.VoidedAt = DateTime.UtcNow;
        _state.State.VoidReason = command.Reason;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();

        // Publish order voided event
        if (GetOrderStream() != null)
        {
            await GetOrderStream().OnNextAsync(new OrderVoidedEvent(
                _state.State.Id,
                _state.State.SiteId,
                _state.State.OrderNumber,
                voidedAmount,
                command.Reason,
                command.VoidedBy)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }

        // Publish void recorded event for sales aggregation
        if (GetSalesStream() != null)
        {
            await GetSalesStream().OnNextAsync(new VoidRecordedEvent(
                _state.State.Id,
                _state.State.SiteId,
                DateOnly.FromDateTime(DateTime.UtcNow),
                voidedAmount,
                command.Reason)
            {
                OrganizationId = _state.State.OrganizationId
            });
        }
    }

    public async Task ReopenAsync(Guid reopenedBy, string reason)
    {
        EnsureExists();

        if (_state.State.Status != OrderStatus.Closed && _state.State.Status != OrderStatus.Voided)
            throw new InvalidOperationException("Can only reopen closed or voided orders");

        _state.State.Status = OrderStatus.Open;
        _state.State.ClosedAt = null;
        _state.State.VoidedBy = null;
        _state.State.VoidedAt = null;
        _state.State.VoidReason = null;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(_state.State.Id != Guid.Empty);
    public Task<OrderStatus> GetStatusAsync() => Task.FromResult(_state.State.Status);
    public Task<OrderTotals> GetTotalsAsync() => Task.FromResult(GetTotalsInternal());
    public Task<IReadOnlyList<OrderLine>> GetLinesAsync() => Task.FromResult<IReadOnlyList<OrderLine>>(_state.State.Lines);

    private void EnsureExists()
    {
        if (_state.State.Id == Guid.Empty)
            throw new InvalidOperationException("Order does not exist");
    }

    private void EnsureNotClosed()
    {
        if (_state.State.Status is OrderStatus.Closed or OrderStatus.Voided)
            throw new InvalidOperationException("Order is closed or voided");
    }

    private void RecalculateTotalsInternal()
    {
        var activeLines = _state.State.Lines.Where(l => l.Status != OrderLineStatus.Voided);
        _state.State.Subtotal = activeLines.Sum(l => l.LineTotal);
        _state.State.DiscountTotal = _state.State.Discounts.Sum(d => d.Amount);
        _state.State.ServiceChargeTotal = _state.State.ServiceCharges.Sum(s => s.Amount);

        // Calculate tax (simplified - 10% tax rate)
        var taxableAmount = _state.State.Subtotal - _state.State.DiscountTotal;
        taxableAmount += _state.State.ServiceCharges.Where(s => s.IsTaxable).Sum(s => s.Amount);
        _state.State.TaxTotal = taxableAmount * 0.10m;

        _state.State.GrandTotal = _state.State.Subtotal
            - _state.State.DiscountTotal
            + _state.State.ServiceChargeTotal
            + _state.State.TaxTotal;

        _state.State.BalanceDue = _state.State.GrandTotal - _state.State.PaidAmount;
    }

    private OrderTotals GetTotalsInternal() => new(
        _state.State.Subtotal,
        _state.State.DiscountTotal,
        _state.State.ServiceChargeTotal,
        _state.State.TaxTotal,
        _state.State.GrandTotal,
        _state.State.PaidAmount,
        _state.State.BalanceDue);
}
