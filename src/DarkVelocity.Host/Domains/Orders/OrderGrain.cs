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
                    CreatedAt = e.OccurredAt,
                    TaxRate = e.TaxRate,
                    TaxAmount = e.TaxAmount,
                    IsBundle = e.IsBundle,
                    BundleComponents = e.BundleComponents,
                    Seat = e.Seat
                });
                state.RecalculateTotals();
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderLineUpdated e:
                var lineToUpdate = state.Lines.FirstOrDefault(l => l.Id == e.LineId);
                if (lineToUpdate != null)
                {
                    var index = state.Lines.FindIndex(l => l.Id == e.LineId);
                    var newQuantity = e.Quantity ?? lineToUpdate.Quantity;
                    var newLineTotal = newQuantity * lineToUpdate.UnitPrice +
                                       lineToUpdate.Modifiers.Sum(m => m.Price * m.Quantity);
                    var newTaxAmount = newLineTotal * (lineToUpdate.TaxRate / 100m);
                    state.Lines[index] = lineToUpdate with
                    {
                        Quantity = newQuantity,
                        Notes = e.Notes ?? lineToUpdate.Notes,
                        LineTotal = newLineTotal,
                        TaxAmount = newTaxAmount
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

            case OrderSplitByItems e:
                // Remove the lines that were moved to the new order
                state.Lines.RemoveAll(l => e.MovedLineIds.Contains(l.Id));
                state.ChildOrders.Add(new SplitOrderReference
                {
                    OrderId = e.NewOrderId,
                    OrderNumber = e.NewOrderNumber,
                    LineIds = e.MovedLineIds,
                    SplitAt = e.OccurredAt
                });
                state.RecalculateTotals();
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderCreatedFromSplit e:
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
                state.ParentOrderId = e.ParentOrderId;
                state.ParentOrderNumber = e.ParentOrderNumber;
                // Add all lines from the split
                foreach (var line in e.Lines)
                {
                    state.Lines.Add(line);
                }
                state.RecalculateTotals();
                break;

            // Hold/Fire workflow events
            case OrderItemsHeld e:
                foreach (var lineId in e.LineIds)
                {
                    var line = state.Lines.FirstOrDefault(l => l.Id == lineId);
                    if (line != null)
                    {
                        var index = state.Lines.FindIndex(l => l.Id == lineId);
                        state.Lines[index] = line with
                        {
                            IsHeld = true,
                            HeldAt = e.OccurredAt,
                            HeldBy = e.HeldBy,
                            HoldReason = e.Reason
                        };
                    }
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderItemsReleased e:
                foreach (var lineId in e.LineIds)
                {
                    var line = state.Lines.FirstOrDefault(l => l.Id == lineId);
                    if (line != null)
                    {
                        var index = state.Lines.FindIndex(l => l.Id == lineId);
                        state.Lines[index] = line with
                        {
                            IsHeld = false,
                            HeldAt = null,
                            HeldBy = null,
                            HoldReason = null
                        };
                    }
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderItemsCourseSet e:
                foreach (var lineId in e.LineIds)
                {
                    var line = state.Lines.FirstOrDefault(l => l.Id == lineId);
                    if (line != null)
                    {
                        var index = state.Lines.FindIndex(l => l.Id == lineId);
                        state.Lines[index] = line with { CourseNumber = e.CourseNumber };
                    }
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderItemsFired e:
                foreach (var lineId in e.LineIds)
                {
                    var line = state.Lines.FirstOrDefault(l => l.Id == lineId);
                    if (line != null)
                    {
                        var index = state.Lines.FindIndex(l => l.Id == lineId);
                        state.Lines[index] = line with
                        {
                            Status = OrderLineStatus.Sent,
                            IsHeld = false,
                            HeldAt = null,
                            HeldBy = null,
                            HoldReason = null,
                            FiredAt = e.OccurredAt,
                            FiredBy = e.FiredBy,
                            SentAt = e.OccurredAt,
                            SentBy = e.FiredBy
                        };
                    }
                }
                if (state.Status == OrderStatus.Open)
                    state.Status = OrderStatus.Sent;
                state.SentAt ??= e.OccurredAt;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderCourseFired e:
                foreach (var lineId in e.FiredLineIds)
                {
                    var line = state.Lines.FirstOrDefault(l => l.Id == lineId);
                    if (line != null)
                    {
                        var index = state.Lines.FindIndex(l => l.Id == lineId);
                        state.Lines[index] = line with
                        {
                            Status = OrderLineStatus.Sent,
                            IsHeld = false,
                            HeldAt = null,
                            HeldBy = null,
                            HoldReason = null,
                            FiredAt = e.OccurredAt,
                            FiredBy = e.FiredBy,
                            SentAt = e.OccurredAt,
                            SentBy = e.FiredBy
                        };
                    }
                }
                if (state.Status == OrderStatus.Open)
                    state.Status = OrderStatus.Sent;
                state.SentAt ??= e.OccurredAt;
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderAllItemsFired e:
                foreach (var lineId in e.FiredLineIds)
                {
                    var line = state.Lines.FirstOrDefault(l => l.Id == lineId);
                    if (line != null)
                    {
                        var index = state.Lines.FindIndex(l => l.Id == lineId);
                        state.Lines[index] = line with
                        {
                            Status = OrderLineStatus.Sent,
                            IsHeld = false,
                            HeldAt = null,
                            HeldBy = null,
                            HoldReason = null,
                            FiredAt = e.OccurredAt,
                            FiredBy = e.FiredBy,
                            SentAt = e.OccurredAt,
                            SentBy = e.FiredBy
                        };
                    }
                }
                if (state.Status == OrderStatus.Open)
                    state.Status = OrderStatus.Sent;
                state.SentAt ??= e.OccurredAt;
                state.UpdatedAt = e.OccurredAt;
                break;

            // Seat Assignment
            case SeatAssigned e:
                var lineForSeat = state.Lines.FirstOrDefault(l => l.Id == e.LineId);
                if (lineForSeat != null)
                {
                    var index = state.Lines.FindIndex(l => l.Id == e.LineId);
                    state.Lines[index] = lineForSeat with { Seat = e.SeatNumber };
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            // Line-Level Discounts
            case LineDiscountApplied e:
                var lineForDiscount = state.Lines.FirstOrDefault(l => l.Id == e.LineId);
                if (lineForDiscount != null)
                {
                    var index = state.Lines.FindIndex(l => l.Id == e.LineId);
                    state.Lines[index] = lineForDiscount with
                    {
                        LineDiscountAmount = e.Amount,
                        LineDiscountReason = e.Reason,
                        LineDiscountType = e.DiscountType,
                        LineDiscountApprovedBy = e.ApprovedBy
                    };
                    state.RecalculateTotals();
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            case LineDiscountRemoved e:
                var lineForDiscountRemoval = state.Lines.FirstOrDefault(l => l.Id == e.LineId);
                if (lineForDiscountRemoval != null)
                {
                    var index = state.Lines.FindIndex(l => l.Id == e.LineId);
                    state.Lines[index] = lineForDiscountRemoval with
                    {
                        LineDiscountAmount = 0,
                        LineDiscountReason = null,
                        LineDiscountType = null,
                        LineDiscountApprovedBy = null
                    };
                    state.RecalculateTotals();
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            // Price Override
            case PriceOverridden e:
                var lineForOverride = state.Lines.FirstOrDefault(l => l.Id == e.LineId);
                if (lineForOverride != null)
                {
                    var index = state.Lines.FindIndex(l => l.Id == e.LineId);
                    var newLineTotal = e.NewPrice * lineForOverride.Quantity +
                                       lineForOverride.Modifiers.Sum(m => m.Price * m.Quantity);
                    var newTaxAmount = newLineTotal * (lineForOverride.TaxRate / 100m);
                    state.Lines[index] = lineForOverride with
                    {
                        UnitPrice = e.NewPrice,
                        LineTotal = newLineTotal,
                        TaxAmount = newTaxAmount,
                        OriginalPrice = e.OriginalPrice,
                        PriceOverrideReason = e.Reason,
                        PriceOverrideApprovedBy = e.ApprovedBy
                    };
                    state.RecalculateTotals();
                }
                state.UpdatedAt = e.OccurredAt;
                break;

            // Order Merge
            case OrderMerged e:
                // Add all merged lines
                foreach (var line in e.MergedLines)
                {
                    state.Lines.Add(line);
                }
                // Add all merged discounts
                foreach (var discount in e.MergedDiscounts)
                {
                    state.Discounts.Add(discount);
                }
                // Add all merged payments
                foreach (var payment in e.MergedPayments)
                {
                    state.Payments.Add(payment);
                    state.PaidAmount += payment.Amount;
                    state.TipTotal += payment.TipAmount;
                }
                state.RecalculateTotals();
                state.UpdatedAt = e.OccurredAt;
                break;

            case OrderMergedAway e:
                state.Status = OrderStatus.Closed;
                state.ClosedAt = e.OccurredAt;
                state.Notes = string.IsNullOrEmpty(state.Notes)
                    ? $"Merged into order {e.TargetOrderNumber}"
                    : $"{state.Notes}\nMerged into order {e.TargetOrderNumber}";
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

        if (command.TaxRate < 0)
            throw new ArgumentException("Tax rate cannot be negative", nameof(command));

        var lineId = Guid.NewGuid();
        var lineTotal = command.UnitPrice * command.Quantity;
        var modifierTotal = command.Modifiers?.Sum(m => m.Price * m.Quantity) ?? 0;
        lineTotal += modifierTotal;

        // Add bundle component price adjustments (e.g., upgrade fees)
        var bundleComponentTotal = command.BundleComponents?.Sum(c => c.PriceAdjustment * c.Quantity) ?? 0;
        lineTotal += bundleComponentTotal;

        // Calculate tax amount for this line (tax rate is a percentage, e.g., 10.0 for 10%)
        var taxAmount = lineTotal * (command.TaxRate / 100m);
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
            OccurredAt = now,
            TaxRate = command.TaxRate,
            TaxAmount = taxAmount,
            IsBundle = command.IsBundle,
            BundleComponents = command.BundleComponents ?? [],
            Seat = command.Seat
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

        // Capture line details before voiding for kitchen notification
        var lineMenuItemId = line.MenuItemId;
        var lineName = line.Name;
        var lineQuantity = line.Quantity;
        var lineWasSent = line.Status == OrderLineStatus.Sent;

        RaiseEvent(new OrderLineVoided
        {
            OrderId = State.Id,
            LineId = command.LineId,
            VoidedBy = command.VoidedBy,
            Reason = command.Reason,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();

        // Notify kitchen if item was sent to kitchen (for KDS void display)
        if (lineWasSent && OrderStream != null)
        {
            await OrderStream.OnNextAsync(new KitchenItemVoidedEvent(
                OrderId: State.Id,
                SiteId: State.SiteId,
                OrderNumber: State.OrderNumber,
                LineId: command.LineId,
                MenuItemId: lineMenuItemId,
                ItemName: lineName,
                Quantity: lineQuantity,
                VoidReason: command.Reason,
                VoidedBy: command.VoidedBy)
            {
                OrganizationId = State.OrganizationId
            });
        }
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

        // Build line snapshots for inventory reversal if requested
        IReadOnlyList<OrderLineSnapshot>? lineSnapshots = null;
        if (command.ReverseInventory)
        {
            lineSnapshots = State.Lines
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
        }

        // Publish order voided event - single source of truth
        // Downstream subscribers (Sales, Loyalty, Inventory) react to this event
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
                State.CustomerId,
                command.ReverseInventory,
                lineSnapshots)
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

    // Bill Splitting Implementation

    public async Task<SplitByItemsResult> SplitByItemsAsync(SplitByItemsCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        if (command.LineIds == null || command.LineIds.Count == 0)
            throw new ArgumentException("At least one line must be specified for splitting", nameof(command));

        // Validate all lines exist and are not voided
        var linesToMove = State.Lines
            .Where(l => command.LineIds.Contains(l.Id) && l.Status != OrderLineStatus.Voided)
            .ToList();

        if (linesToMove.Count != command.LineIds.Count)
            throw new InvalidOperationException("One or more specified lines do not exist or are voided");

        // Cannot split if it would leave the original order empty
        var remainingLines = State.Lines
            .Where(l => !command.LineIds.Contains(l.Id) && l.Status != OrderLineStatus.Voided)
            .ToList();

        if (remainingLines.Count == 0)
            throw new InvalidOperationException("Cannot split all lines - at least one line must remain on the original order");

        // Create the new order for the split items
        var newOrderId = Guid.NewGuid();
        var newOrderNumber = $"ORD-{Interlocked.Increment(ref _orderCounter):D6}-S";
        var now = DateTime.UtcNow;

        // Create the new order grain
        var newOrderGrain = GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(State.OrganizationId, State.SiteId, newOrderId));

        var createFromSplitResult = await newOrderGrain.CreateFromSplitAsync(new CreateFromSplitCommand(
            State.OrganizationId,
            State.SiteId,
            State.Id,
            State.OrderNumber,
            State.Type,
            linesToMove,
            command.SplitBy,
            State.TableId,
            State.TableNumber,
            State.CustomerId,
            command.GuestCount ?? 1));

        // Record the split on this (source) order
        RaiseEvent(new OrderSplitByItems
        {
            OrderId = State.Id,
            NewOrderId = newOrderId,
            NewOrderNumber = createFromSplitResult.OrderNumber,
            MovedLineIds = command.LineIds,
            SplitBy = command.SplitBy,
            OccurredAt = now
        });

        await ConfirmEvents();

        // Get the totals for both orders
        var newOrderState = await newOrderGrain.GetStateAsync();

        return new SplitByItemsResult(
            newOrderId,
            createFromSplitResult.OrderNumber,
            linesToMove.Count,
            newOrderState.GrandTotal,
            State.GrandTotal);
    }

    public async Task<OrderCreatedResult> CreateFromSplitAsync(CreateFromSplitCommand command)
    {
        if (State.Id != Guid.Empty)
            throw new InvalidOperationException("Order already exists");

        var key = this.GetPrimaryKeyString();
        var (orgId, siteId, _, orderId) = GrainKeys.ParseSiteEntity(key);

        var orderNumber = $"ORD-{Interlocked.Increment(ref _orderCounter):D6}-S";
        var now = DateTime.UtcNow;

        RaiseEvent(new OrderCreatedFromSplit
        {
            OrderId = orderId,
            OrganizationId = orgId,
            SiteId = siteId,
            OrderNumber = orderNumber,
            Type = command.Type,
            ParentOrderId = command.ParentOrderId,
            ParentOrderNumber = command.ParentOrderNumber,
            TableId = command.TableId,
            TableNumber = command.TableNumber,
            CustomerId = command.CustomerId,
            GuestCount = command.GuestCount,
            CreatedBy = command.CreatedBy,
            Lines = command.Lines,
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

    public Task<SplitPaymentResult> CalculateSplitByPeopleAsync(int numberOfPeople)
    {
        EnsureExists();

        if (numberOfPeople <= 0)
            throw new ArgumentException("Number of people must be greater than zero", nameof(numberOfPeople));

        var balanceDue = State.BalanceDue;
        if (balanceDue <= 0)
        {
            return Task.FromResult(new SplitPaymentResult(
                State.GrandTotal,
                balanceDue,
                [],
                false));
        }

        // Calculate equal shares with proper rounding
        var baseShare = Math.Floor(balanceDue / numberOfPeople * 100) / 100; // Round down to 2 decimal places
        var remainder = balanceDue - (baseShare * numberOfPeople);

        var shares = new List<SplitShare>();
        for (int i = 0; i < numberOfPeople; i++)
        {
            // Add remainder to the first share to ensure total matches
            var shareAmount = i == 0 ? baseShare + remainder : baseShare;
            var shareTax = State.TaxTotal / numberOfPeople;
            if (i == 0)
                shareTax = State.TaxTotal - (Math.Floor(State.TaxTotal / numberOfPeople * 100) / 100 * (numberOfPeople - 1));

            shares.Add(new SplitShare
            {
                ShareNumber = i + 1,
                Amount = shareAmount - shareTax,
                Tax = shareTax,
                Total = shareAmount,
                Label = $"Guest {i + 1}"
            });
        }

        return Task.FromResult(new SplitPaymentResult(
            State.GrandTotal,
            balanceDue,
            shares,
            true));
    }

    public Task<SplitPaymentResult> CalculateSplitByAmountsAsync(List<decimal> amounts)
    {
        EnsureExists();

        if (amounts == null || amounts.Count == 0)
            throw new ArgumentException("At least one amount must be specified", nameof(amounts));

        if (amounts.Any(a => a < 0))
            throw new ArgumentException("Amounts cannot be negative", nameof(amounts));

        var balanceDue = State.BalanceDue;
        var totalSpecified = amounts.Sum();

        // Validate that amounts sum to balance due (with small tolerance for rounding)
        var isValid = Math.Abs(totalSpecified - balanceDue) < 0.01m;

        var shares = new List<SplitShare>();
        var taxRatio = State.TaxTotal / State.GrandTotal;

        for (int i = 0; i < amounts.Count; i++)
        {
            var shareAmount = amounts[i];
            var shareTax = Math.Round(shareAmount * taxRatio, 2);

            shares.Add(new SplitShare
            {
                ShareNumber = i + 1,
                Amount = shareAmount - shareTax,
                Tax = shareTax,
                Total = shareAmount,
                Label = $"Payment {i + 1}"
            });
        }

        return Task.FromResult(new SplitPaymentResult(
            State.GrandTotal,
            balanceDue,
            shares,
            isValid));
    }

    #region Hold/Fire Implementation

    public async Task HoldItemsAsync(HoldItemsCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        if (command.LineIds == null || command.LineIds.Count == 0)
            throw new ArgumentException("At least one line must be specified", nameof(command));

        // Validate all lines exist and are pending
        var linesToHold = State.Lines
            .Where(l => command.LineIds.Contains(l.Id) && l.Status == OrderLineStatus.Pending)
            .ToList();

        if (linesToHold.Count == 0)
            throw new InvalidOperationException("No valid pending items to hold");

        var validLineIds = linesToHold.Select(l => l.Id).ToList();

        RaiseEvent(new OrderItemsHeld
        {
            OrderId = State.Id,
            LineIds = validLineIds,
            HeldBy = command.HeldBy,
            Reason = command.Reason,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task ReleaseItemsAsync(ReleaseItemsCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        if (command.LineIds == null || command.LineIds.Count == 0)
            throw new ArgumentException("At least one line must be specified", nameof(command));

        // Validate all lines exist and are held
        var linesToRelease = State.Lines
            .Where(l => command.LineIds.Contains(l.Id) && l.IsHeld)
            .ToList();

        if (linesToRelease.Count == 0)
            throw new InvalidOperationException("No valid held items to release");

        var validLineIds = linesToRelease.Select(l => l.Id).ToList();

        RaiseEvent(new OrderItemsReleased
        {
            OrderId = State.Id,
            LineIds = validLineIds,
            ReleasedBy = command.ReleasedBy,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task SetItemCourseAsync(SetItemCourseCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        if (command.LineIds == null || command.LineIds.Count == 0)
            throw new ArgumentException("At least one line must be specified", nameof(command));

        if (command.CourseNumber < 1)
            throw new ArgumentException("Course number must be at least 1", nameof(command));

        // Validate all lines exist
        var linesToUpdate = State.Lines
            .Where(l => command.LineIds.Contains(l.Id) && l.Status != OrderLineStatus.Voided)
            .ToList();

        if (linesToUpdate.Count == 0)
            throw new InvalidOperationException("No valid items to set course");

        var validLineIds = linesToUpdate.Select(l => l.Id).ToList();

        RaiseEvent(new OrderItemsCourseSet
        {
            OrderId = State.Id,
            LineIds = validLineIds,
            CourseNumber = command.CourseNumber,
            SetBy = command.SetBy,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task<FireResult> FireItemsAsync(FireItemsCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        if (command.LineIds == null || command.LineIds.Count == 0)
            throw new ArgumentException("At least one line must be specified", nameof(command));

        // Get items that can be fired (pending or held, not already sent)
        var linesToFire = State.Lines
            .Where(l => command.LineIds.Contains(l.Id) &&
                       l.Status == OrderLineStatus.Pending &&
                       l.Status != OrderLineStatus.Voided)
            .ToList();

        if (linesToFire.Count == 0)
            throw new InvalidOperationException("No valid items to fire");

        var validLineIds = linesToFire.Select(l => l.Id).ToList();
        var now = DateTime.UtcNow;

        RaiseEvent(new OrderItemsFired
        {
            OrderId = State.Id,
            LineIds = validLineIds,
            FiredBy = command.FiredBy,
            OccurredAt = now
        });

        await ConfirmEvents();

        // Publish to kitchen
        await PublishItemsFiredToKitchenAsync(linesToFire, command.FiredBy);

        return new FireResult(validLineIds.Count, validLineIds, now);
    }

    public async Task<FireResult> FireCourseAsync(FireCourseCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        if (command.CourseNumber < 1)
            throw new ArgumentException("Course number must be at least 1", nameof(command));

        // Get all pending items in the specified course
        var linesToFire = State.Lines
            .Where(l => l.CourseNumber == command.CourseNumber &&
                       l.Status == OrderLineStatus.Pending &&
                       l.Status != OrderLineStatus.Voided)
            .ToList();

        if (linesToFire.Count == 0)
            throw new InvalidOperationException($"No pending items in course {command.CourseNumber}");

        var lineIds = linesToFire.Select(l => l.Id).ToList();
        var now = DateTime.UtcNow;

        RaiseEvent(new OrderCourseFired
        {
            OrderId = State.Id,
            CourseNumber = command.CourseNumber,
            FiredLineIds = lineIds,
            FiredBy = command.FiredBy,
            OccurredAt = now
        });

        await ConfirmEvents();

        // Publish to kitchen
        await PublishItemsFiredToKitchenAsync(linesToFire, command.FiredBy);

        return new FireResult(lineIds.Count, lineIds, now);
    }

    public async Task<FireResult> FireAllAsync(Guid firedBy)
    {
        EnsureExists();
        EnsureNotClosed();

        // Get all pending items (held or not)
        var linesToFire = State.Lines
            .Where(l => l.Status == OrderLineStatus.Pending &&
                       l.Status != OrderLineStatus.Voided)
            .ToList();

        if (linesToFire.Count == 0)
            throw new InvalidOperationException("No pending items to fire");

        var lineIds = linesToFire.Select(l => l.Id).ToList();
        var now = DateTime.UtcNow;

        RaiseEvent(new OrderAllItemsFired
        {
            OrderId = State.Id,
            FiredLineIds = lineIds,
            FiredBy = firedBy,
            OccurredAt = now
        });

        await ConfirmEvents();

        // Publish to kitchen
        await PublishItemsFiredToKitchenAsync(linesToFire, firedBy);

        return new FireResult(lineIds.Count, lineIds, now);
    }

    public Task<HoldSummary> GetHoldSummaryAsync()
    {
        EnsureExists();

        var heldItems = State.Lines
            .Where(l => l.IsHeld && l.Status == OrderLineStatus.Pending)
            .ToList();

        var heldByCourseCounts = heldItems
            .GroupBy(l => l.CourseNumber)
            .ToDictionary(g => g.Key, g => g.Count());

        return Task.FromResult(new HoldSummary(
            heldItems.Count,
            heldByCourseCounts,
            heldItems.Select(l => l.Id).ToList()));
    }

    public Task<IReadOnlyList<OrderLine>> GetHeldItemsAsync()
    {
        EnsureExists();

        var heldItems = State.Lines
            .Where(l => l.IsHeld && l.Status == OrderLineStatus.Pending)
            .ToList();

        return Task.FromResult<IReadOnlyList<OrderLine>>(heldItems);
    }

    public Task<Dictionary<int, int>> GetCourseSummaryAsync()
    {
        EnsureExists();

        var courseCounts = State.Lines
            .Where(l => l.Status != OrderLineStatus.Voided)
            .GroupBy(l => l.CourseNumber)
            .ToDictionary(g => g.Key, g => g.Count());

        return Task.FromResult(courseCounts);
    }

    private async Task PublishItemsFiredToKitchenAsync(List<OrderLine> lines, Guid firedBy)
    {
        if (OrderStream == null || lines.Count == 0)
            return;

        var kitchenLines = lines.Select(l => new KitchenLineItem(
            LineId: l.Id,
            MenuItemId: l.MenuItemId,
            Name: l.Name,
            Quantity: l.Quantity,
            Modifiers: l.Modifiers?.Select(m => m.Name).ToList(),
            SpecialInstructions: l.Notes)).ToList();

        await OrderStream.OnNextAsync(new OrderItemsFiredToKitchenEvent(
            OrderId: State.Id,
            SiteId: State.SiteId,
            OrderNumber: State.OrderNumber,
            OrderType: State.Type.ToString(),
            TableNumber: State.TableNumber,
            GuestCount: State.GuestCount,
            ServerId: State.ServerId,
            ServerName: State.ServerName,
            FiredBy: firedBy,
            Lines: kitchenLines)
        {
            OrganizationId = State.OrganizationId
        });
    }

    #endregion

    #region Seat Assignment Implementation

    public async Task AssignSeatAsync(AssignSeatCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        if (command.SeatNumber < 1)
            throw new ArgumentException("Seat number must be at least 1", nameof(command));

        var line = State.Lines.FirstOrDefault(l => l.Id == command.LineId)
            ?? throw new InvalidOperationException("Line not found");

        if (line.Status == OrderLineStatus.Voided)
            throw new InvalidOperationException("Cannot assign seat to voided item");

        RaiseEvent(new SeatAssigned
        {
            OrderId = State.Id,
            LineId = command.LineId,
            SeatNumber = command.SeatNumber,
            AssignedBy = command.AssignedBy,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    #endregion

    #region Line Discount Implementation

    public async Task ApplyLineDiscountAsync(ApplyLineDiscountCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var line = State.Lines.FirstOrDefault(l => l.Id == command.LineId)
            ?? throw new InvalidOperationException("Line not found");

        if (line.Status == OrderLineStatus.Voided)
            throw new InvalidOperationException("Cannot apply discount to voided item");

        if (command.Value < 0)
            throw new ArgumentException("Discount value cannot be negative", nameof(command));

        // Calculate discount amount based on type
        var discountAmount = command.DiscountType switch
        {
            DiscountType.Percentage => line.LineTotal * (command.Value / 100m),
            DiscountType.FixedAmount => command.Value,
            _ => command.Value
        };

        // Ensure discount doesn't exceed line total
        discountAmount = Math.Min(discountAmount, line.LineTotal);

        RaiseEvent(new LineDiscountApplied
        {
            OrderId = State.Id,
            LineId = command.LineId,
            DiscountType = command.DiscountType,
            Value = command.Value,
            Amount = discountAmount,
            Reason = command.Reason,
            AppliedBy = command.AppliedBy,
            ApprovedBy = command.ApprovedBy,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    public async Task RemoveLineDiscountAsync(Guid lineId, Guid removedBy)
    {
        EnsureExists();
        EnsureNotClosed();

        var line = State.Lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw new InvalidOperationException("Line not found");

        if (line.LineDiscountAmount == 0)
            throw new InvalidOperationException("No line discount to remove");

        RaiseEvent(new LineDiscountRemoved
        {
            OrderId = State.Id,
            LineId = lineId,
            RemovedBy = removedBy,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    #endregion

    #region Price Override Implementation

    public async Task OverridePriceAsync(OverridePriceCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        var line = State.Lines.FirstOrDefault(l => l.Id == command.LineId)
            ?? throw new InvalidOperationException("Line not found");

        if (line.Status == OrderLineStatus.Voided)
            throw new InvalidOperationException("Cannot override price of voided item");

        if (command.NewPrice < 0)
            throw new ArgumentException("New price cannot be negative", nameof(command));

        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ArgumentException("Reason is required for price override", nameof(command));

        var originalPrice = line.OriginalPrice ?? line.UnitPrice;

        RaiseEvent(new PriceOverridden
        {
            OrderId = State.Id,
            LineId = command.LineId,
            OriginalPrice = originalPrice,
            NewPrice = command.NewPrice,
            Reason = command.Reason,
            OverriddenBy = command.OverriddenBy,
            ApprovedBy = command.ApprovedBy,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    #endregion

    #region Order Merge Implementation

    public async Task<MergeOrderResult> MergeFromOrderAsync(MergeFromOrderCommand command)
    {
        EnsureExists();
        EnsureNotClosed();

        // Get the source order
        var sourceOrderGrain = GrainFactory.GetGrain<IOrderGrain>(
            GrainKeys.Order(State.OrganizationId, State.SiteId, command.SourceOrderId));

        var sourceState = await sourceOrderGrain.GetStateAsync();

        if (sourceState.Id == Guid.Empty)
            throw new InvalidOperationException("Source order does not exist");

        if (sourceState.Status is OrderStatus.Closed or OrderStatus.Voided)
            throw new InvalidOperationException("Cannot merge from closed or voided order");

        if (sourceState.SiteId != State.SiteId)
            throw new InvalidOperationException("Cannot merge orders from different sites");

        // Get active lines, discounts, and payments from source
        var activeLines = sourceState.Lines
            .Where(l => l.Status != OrderLineStatus.Voided)
            .ToList();

        var now = DateTime.UtcNow;

        // Raise merge event on this (target) order
        RaiseEvent(new OrderMerged
        {
            OrderId = State.Id,
            SourceOrderId = command.SourceOrderId,
            SourceOrderNumber = sourceState.OrderNumber,
            MergedLines = activeLines,
            MergedDiscounts = sourceState.Discounts,
            MergedPayments = sourceState.Payments,
            MergedBy = command.MergedBy,
            OccurredAt = now
        });

        await ConfirmEvents();

        // Mark the source order as merged away
        await sourceOrderGrain.MarkAsMergedAsync(State.Id, State.OrderNumber, command.MergedBy);

        // Publish merge event for kitchen notification
        if (OrderStream != null)
        {
            await OrderStream.OnNextAsync(new OrdersMergedEvent(
                TargetOrderId: State.Id,
                SourceOrderId: command.SourceOrderId,
                SiteId: State.SiteId,
                TargetOrderNumber: State.OrderNumber,
                SourceOrderNumber: sourceState.OrderNumber,
                LinesMerged: activeLines.Count,
                MergedBy: command.MergedBy)
            {
                OrganizationId = State.OrganizationId
            });
        }

        return new MergeOrderResult(
            activeLines.Count,
            sourceState.Payments.Count,
            sourceState.Discounts.Count,
            State.GrandTotal);
    }

    public async Task MarkAsMergedAsync(Guid targetOrderId, string targetOrderNumber, Guid mergedBy)
    {
        EnsureExists();
        EnsureNotClosed();

        RaiseEvent(new OrderMergedAway
        {
            OrderId = State.Id,
            TargetOrderId = targetOrderId,
            TargetOrderNumber = targetOrderNumber,
            MergedBy = mergedBy,
            OccurredAt = DateTime.UtcNow
        });

        await ConfirmEvents();
    }

    #endregion

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
