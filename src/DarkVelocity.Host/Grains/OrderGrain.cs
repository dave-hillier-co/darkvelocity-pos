using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Orleans.Runtime;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

public class OrderGrain : DocumentGrain<OrderState, OrderLine>, IOrderGrain
{
    private IAsyncStream<IStreamEvent>? _orderStream;
    private IAsyncStream<IStreamEvent>? _salesStream;
    private static int _orderCounter = 1000;

    public OrderGrain(
        [PersistentState("order", "OrleansStorage")]
        IPersistentState<OrderState> state) : base(state)
    {
    }

    protected override bool IsInitialized => State.State.Id != Guid.Empty;

    protected override decimal GetDocumentTotal() => State.State.GrandTotal;

    protected override void RecalculateTotals() => RecalculateTotalsInternal();

    private IAsyncStream<IStreamEvent> GetOrderStream()
    {
        if (_orderStream == null && State.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var orderStreamId = StreamId.Create(StreamConstants.OrderStreamNamespace, State.State.OrganizationId.ToString());
            _orderStream = streamProvider.GetStream<IStreamEvent>(orderStreamId);
        }
        return _orderStream!;
    }

    private IAsyncStream<IStreamEvent> GetSalesStream()
    {
        if (_salesStream == null && State.State.OrganizationId != Guid.Empty)
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var salesStreamId = StreamId.Create(StreamConstants.SalesStreamNamespace, State.State.OrganizationId.ToString());
            _salesStream = streamProvider.GetStream<IStreamEvent>(salesStreamId);
        }
        return _salesStream!;
    }

    public async Task<OrderCreatedResult> CreateAsync(CreateOrderCommand command)
    {
        if (State.State.Id != Guid.Empty)
            throw new InvalidOperationException("Order already exists");

        var key = this.GetPrimaryKeyString();
        var (orgId, siteId, _, orderId) = GrainKeys.ParseSiteEntity(key);

        var orderNumber = $"ORD-{Interlocked.Increment(ref _orderCounter):D6}";

        State.State = new OrderState
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

        await State.WriteStateAsync();

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

        return new OrderCreatedResult(orderId, orderNumber, State.State.CreatedAt);
    }

    public Task<OrderState> GetStateAsync()
    {
        return Task.FromResult(State.State);
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

        State.State.Lines.Add(line);
        RecalculateTotalsInternal();

        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();

        // Publish line added event
        if (GetOrderStream() != null)
        {
            await GetOrderStream().OnNextAsync(new OrderLineAddedEvent(
                State.State.Id,
                State.State.SiteId,
                lineId,
                command.MenuItemId,
                command.Name,
                command.Quantity,
                command.UnitPrice,
                lineTotal)
            {
                OrganizationId = State.State.OrganizationId
            });
        }

        return new AddLineResult(lineId, lineTotal, State.State.GrandTotal);
    }

    public async Task UpdateLineAsync(UpdateLineCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var line = State.State.Lines.FirstOrDefault(l => l.Id == command.LineId)
            ?? throw new InvalidOperationException("Line not found");

        var updatedLine = line with
        {
            Quantity = command.Quantity ?? line.Quantity,
            Notes = command.Notes ?? line.Notes,
            LineTotal = (command.Quantity ?? line.Quantity) * line.UnitPrice +
                        line.Modifiers.Sum(m => m.Price * m.Quantity)
        };

        var index = State.State.Lines.FindIndex(l => l.Id == command.LineId);
        State.State.Lines[index] = updatedLine;

        RecalculateTotalsInternal();
        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();
    }

    public async Task VoidLineAsync(VoidLineCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var line = State.State.Lines.FirstOrDefault(l => l.Id == command.LineId)
            ?? throw new InvalidOperationException("Line not found");

        var voidedLine = line with
        {
            Status = OrderLineStatus.Voided,
            VoidedBy = command.VoidedBy,
            VoidedAt = DateTime.UtcNow,
            VoidReason = command.Reason
        };

        var index = State.State.Lines.FindIndex(l => l.Id == command.LineId);
        State.State.Lines[index] = voidedLine;

        RecalculateTotalsInternal();
        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();
    }

    public async Task RemoveLineAsync(Guid lineId)
    {
        EnsureExists();
        EnsureNotClosed();

        var removed = State.State.Lines.RemoveAll(l => l.Id == lineId);
        if (removed == 0)
            throw new InvalidOperationException("Line not found");

        RecalculateTotalsInternal();
        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();
    }

    public async Task SendAsync(Guid sentBy)
    {
        EnsureExists();
        EnsureNotClosed();

        if (!State.State.Lines.Any(l => l.Status == OrderLineStatus.Pending))
            throw new InvalidOperationException("No pending items to send");

        foreach (var line in State.State.Lines.Where(l => l.Status == OrderLineStatus.Pending).ToList())
        {
            var index = State.State.Lines.FindIndex(l => l.Id == line.Id);
            State.State.Lines[index] = line with
            {
                Status = OrderLineStatus.Sent,
                SentBy = sentBy,
                SentAt = DateTime.UtcNow
            };
        }

        State.State.Status = OrderStatus.Sent;
        State.State.SentAt = DateTime.UtcNow;
        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();
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
            DiscountType.Percentage => State.State.Subtotal * (command.Value / 100m),
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

        State.State.Discounts.Add(discount);
        RecalculateTotalsInternal();

        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();
    }

    public async Task RemoveDiscountAsync(Guid discountId)
    {
        EnsureExists();
        EnsureNotClosed();

        State.State.Discounts.RemoveAll(d => d.Id == discountId);
        RecalculateTotalsInternal();

        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();
    }

    public async Task AddServiceChargeAsync(string name, decimal rate, bool isTaxable)
    {
        EnsureExists();
        EnsureNotClosed();

        var amount = State.State.Subtotal * (rate / 100m);

        var serviceCharge = new ServiceCharge
        {
            Id = Guid.NewGuid(),
            Name = name,
            Rate = rate,
            Amount = amount,
            IsTaxable = isTaxable
        };

        State.State.ServiceCharges.Add(serviceCharge);
        RecalculateTotalsInternal();

        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();
    }

    public async Task AssignCustomerAsync(Guid customerId, string? customerName)
    {
        EnsureExists();

        State.State.CustomerId = customerId;
        State.State.CustomerName = customerName;
        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();
    }

    public async Task AssignServerAsync(Guid serverId, string serverName)
    {
        EnsureExists();

        State.State.ServerId = serverId;
        State.State.ServerName = serverName;
        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();
    }

    public async Task TransferTableAsync(Guid newTableId, string newTableNumber, Guid transferredBy)
    {
        EnsureExists();
        EnsureNotClosed();

        State.State.TableId = newTableId;
        State.State.TableNumber = newTableNumber;
        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();
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

        State.State.Payments.Add(payment);
        State.State.PaidAmount += amount;
        State.State.TipTotal += tipAmount;
        State.State.BalanceDue = State.State.GrandTotal - State.State.PaidAmount;

        if (State.State.BalanceDue <= 0)
            State.State.Status = OrderStatus.Paid;
        else if (State.State.PaidAmount > 0)
            State.State.Status = OrderStatus.PartiallyPaid;

        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();
    }

    public async Task RemovePaymentAsync(Guid paymentId)
    {
        EnsureExists();

        var payment = State.State.Payments.FirstOrDefault(p => p.PaymentId == paymentId);
        if (payment != null)
        {
            State.State.Payments.Remove(payment);
            State.State.PaidAmount -= payment.Amount;
            State.State.TipTotal -= payment.TipAmount;
            State.State.BalanceDue = State.State.GrandTotal - State.State.PaidAmount;

            if (State.State.PaidAmount <= 0)
                State.State.Status = OrderStatus.Open;
            else if (State.State.BalanceDue > 0)
                State.State.Status = OrderStatus.PartiallyPaid;

            State.State.UpdatedAt = DateTime.UtcNow;
            State.State.Version++;

            await State.WriteStateAsync();
        }
    }

    public async Task CloseAsync(Guid closedBy)
    {
        EnsureExists();

        if (State.State.BalanceDue > 0)
            throw new InvalidOperationException("Cannot close order with outstanding balance");

        State.State.Status = OrderStatus.Closed;
        State.State.ClosedAt = DateTime.UtcNow;
        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();

        // Build order line snapshots
        var lineSnapshots = State.State.Lines
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

        // Publish order completed event (triggers inventory consumption)
        if (GetOrderStream() != null)
        {
            await GetOrderStream().OnNextAsync(new OrderCompletedEvent(
                State.State.Id,
                State.State.SiteId,
                State.State.OrderNumber,
                State.State.Subtotal,
                State.State.TaxTotal,
                State.State.GrandTotal,
                State.State.DiscountTotal,
                lineSnapshots,
                State.State.ServerId,
                State.State.ServerName)
            {
                OrganizationId = State.State.OrganizationId
            });
        }

        // Publish sale recorded event (triggers sales aggregation)
        if (GetSalesStream() != null)
        {
            await GetSalesStream().OnNextAsync(new SaleRecordedEvent(
                State.State.Id,
                State.State.SiteId,
                DateOnly.FromDateTime(State.State.ClosedAt.Value),
                State.State.Subtotal,
                State.State.DiscountTotal,
                State.State.Subtotal - State.State.DiscountTotal,
                State.State.TaxTotal,
                0m, // TheoreticalCOGS - would be calculated from recipes
                lineSnapshots.Sum(l => l.Quantity),
                State.State.GuestCount,
                State.State.Type.ToString())
            {
                OrganizationId = State.State.OrganizationId
            });
        }
    }

    public async Task VoidAsync(VoidOrderCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var voidedAmount = State.State.GrandTotal;

        State.State.Status = OrderStatus.Voided;
        State.State.VoidedBy = command.VoidedBy;
        State.State.VoidedAt = DateTime.UtcNow;
        State.State.VoidReason = command.Reason;
        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();

        // Publish order voided event
        if (GetOrderStream() != null)
        {
            await GetOrderStream().OnNextAsync(new OrderVoidedEvent(
                State.State.Id,
                State.State.SiteId,
                State.State.OrderNumber,
                voidedAmount,
                command.Reason,
                command.VoidedBy)
            {
                OrganizationId = State.State.OrganizationId
            });
        }

        // Publish void recorded event for sales aggregation
        if (GetSalesStream() != null)
        {
            await GetSalesStream().OnNextAsync(new VoidRecordedEvent(
                State.State.Id,
                State.State.SiteId,
                DateOnly.FromDateTime(DateTime.UtcNow),
                voidedAmount,
                command.Reason)
            {
                OrganizationId = State.State.OrganizationId
            });
        }
    }

    public async Task ReopenAsync(Guid reopenedBy, string reason)
    {
        EnsureExists();

        if (State.State.Status != OrderStatus.Closed && State.State.Status != OrderStatus.Voided)
            throw new InvalidOperationException("Can only reopen closed or voided orders");

        State.State.Status = OrderStatus.Open;
        State.State.ClosedAt = null;
        State.State.VoidedBy = null;
        State.State.VoidedAt = null;
        State.State.VoidReason = null;
        State.State.UpdatedAt = DateTime.UtcNow;
        State.State.Version++;

        await State.WriteStateAsync();
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.State.Id != Guid.Empty);
    public Task<OrderStatus> GetStatusAsync() => Task.FromResult(State.State.Status);
    public Task<OrderTotals> GetTotalsAsync() => Task.FromResult(GetTotalsInternal());
    public Task<IReadOnlyList<OrderLine>> GetLinesAsync() => Task.FromResult<IReadOnlyList<OrderLine>>(State.State.Lines);

    public Task<CloneOrderResult> CloneAsync(CloneOrderCommand command)
    {
        EnsureExists();
        // Clone implementation would create a new order grain with copied lines
        // This is a stub - full implementation would use grain factory
        throw new NotImplementedException("Clone functionality not yet implemented");
    }

    private void EnsureExists()
    {
        if (State.State.Id == Guid.Empty)
            throw new InvalidOperationException("Order does not exist");
    }

    private void EnsureNotClosed()
    {
        if (State.State.Status is OrderStatus.Closed or OrderStatus.Voided)
            throw new InvalidOperationException("Order is closed or voided");
    }

    private void RecalculateTotalsInternal()
    {
        var activeLines = State.State.Lines.Where(l => l.Status != OrderLineStatus.Voided);
        State.State.Subtotal = activeLines.Sum(l => l.LineTotal);
        State.State.DiscountTotal = State.State.Discounts.Sum(d => d.Amount);
        State.State.ServiceChargeTotal = State.State.ServiceCharges.Sum(s => s.Amount);

        // Calculate tax (simplified - 10% tax rate)
        var taxableAmount = State.State.Subtotal - State.State.DiscountTotal;
        taxableAmount += State.State.ServiceCharges.Where(s => s.IsTaxable).Sum(s => s.Amount);
        State.State.TaxTotal = taxableAmount * 0.10m;

        State.State.GrandTotal = State.State.Subtotal
            - State.State.DiscountTotal
            + State.State.ServiceChargeTotal
            + State.State.TaxTotal;

        State.State.BalanceDue = State.State.GrandTotal - State.State.PaidAmount;
    }

    private OrderTotals GetTotalsInternal() => new(
        State.State.Subtotal,
        State.State.DiscountTotal,
        State.State.ServiceChargeTotal,
        State.State.TaxTotal,
        State.State.GrandTotal,
        State.State.PaidAmount,
        State.State.BalanceDue);
}
