using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain for purchase order management.
/// Manages PO lifecycle from draft to received using event sourcing.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class PurchaseOrderGrain : JournaledGrain<PurchaseOrderState, IPurchaseOrderEvent>, IPurchaseOrderGrain
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PurchaseOrderGrain> _logger;

    public PurchaseOrderGrain(IGrainFactory grainFactory, ILogger<PurchaseOrderGrain> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    protected override void TransitionState(PurchaseOrderState state, IPurchaseOrderEvent @event)
    {
        switch (@event)
        {
            case PurchaseOrderDrafted e:
                state.PurchaseOrderId = e.PurchaseOrderId;
                state.OrgId = e.OrgId;
                state.OrderNumber = e.OrderNumber;
                state.SupplierId = e.SupplierId;
                state.SupplierName = e.SupplierName;
                state.LocationId = e.LocationId;
                state.CreatedByUserId = e.CreatedByUserId;
                state.ExpectedDeliveryDate = e.ExpectedDeliveryDate;
                state.Notes = e.Notes;
                state.Status = PurchaseOrderStatus.Draft;
                state.Version++;
                break;

            case PurchaseOrderLineAdded e:
                state.Lines.Add(new PurchaseOrderLineState
                {
                    LineId = e.LineId,
                    SkuId = e.SkuId,
                    SkuCode = e.SkuCode,
                    ProductName = e.ProductName,
                    QuantityOrdered = e.QuantityOrdered,
                    UnitPrice = e.UnitPrice,
                    LineTotal = e.QuantityOrdered * e.UnitPrice,
                    Notes = e.Notes
                });
                RecalculateTotal(state);
                state.Version++;
                break;

            case PurchaseOrderLineUpdated e:
                var line = state.Lines.FirstOrDefault(l => l.LineId == e.LineId);
                if (line != null)
                {
                    if (e.QuantityOrdered.HasValue) line.QuantityOrdered = e.QuantityOrdered.Value;
                    if (e.UnitPrice.HasValue) line.UnitPrice = e.UnitPrice.Value;
                    if (e.Notes != null) line.Notes = e.Notes;
                    line.LineTotal = line.QuantityOrdered * line.UnitPrice;
                }
                RecalculateTotal(state);
                state.Version++;
                break;

            case PurchaseOrderLineRemoved e:
                state.Lines.RemoveAll(l => l.LineId == e.LineId);
                RecalculateTotal(state);
                state.Version++;
                break;

            case PurchaseOrderSubmittedEvent e:
                state.Status = PurchaseOrderStatus.Submitted;
                state.SubmittedAt = e.OccurredAt;
                state.OrderTotal = e.OrderTotal;
                state.Version++;
                break;

            case PurchaseOrderLineReceived e:
                var receivedLine = state.Lines.FirstOrDefault(l => l.LineId == e.LineId);
                if (receivedLine != null)
                {
                    receivedLine.QuantityReceived += e.QuantityReceived;
                }
                state.Status = PurchaseOrderStatus.PartiallyReceived;
                state.Version++;
                break;

            case PurchaseOrderFullyReceived e:
                state.Status = PurchaseOrderStatus.Received;
                state.ReceivedAt = e.OccurredAt;
                state.Version++;
                break;

            case PurchaseOrderCancelledEvent e:
                state.Status = PurchaseOrderStatus.Cancelled;
                state.CancelledAt = e.OccurredAt;
                state.CancellationReason = e.Reason;
                state.Version++;
                break;
        }
    }

    private static void RecalculateTotal(PurchaseOrderState state)
    {
        state.OrderTotal = state.Lines.Sum(l => l.LineTotal);
    }

    public async Task<PurchaseOrderSnapshot> CreateAsync(CreatePurchaseOrderCommand command)
    {
        if (State.PurchaseOrderId != Guid.Empty)
            throw new InvalidOperationException("Purchase order already exists");

        var key = this.GetPrimaryKeyString();
        var (orgId, _, poId) = GrainKeys.ParseOrgEntity(key);

        // Get supplier name (graceful fallback if supplier not yet registered)
        string supplierName;
        try
        {
            var supplierGrain = _grainFactory.GetGrain<ISupplierGrain>(
                GrainKeys.Supplier(orgId, command.SupplierId));
            var supplierSnapshot = await supplierGrain.GetSnapshotAsync();
            supplierName = supplierSnapshot.Name;
        }
        catch (InvalidOperationException)
        {
            supplierName = command.SupplierId.ToString();
        }

        var orderNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{poId.ToString()[..8].ToUpperInvariant()}";

        RaiseEvent(new PurchaseOrderDrafted
        {
            PurchaseOrderId = poId,
            OrgId = orgId,
            OrderNumber = orderNumber,
            SupplierId = command.SupplierId,
            SupplierName = supplierName,
            LocationId = command.LocationId,
            CreatedByUserId = command.CreatedByUserId,
            ExpectedDeliveryDate = command.ExpectedDeliveryDate,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogInformation("Purchase order created: {OrderNumber} for supplier {Supplier}",
            orderNumber, supplierName);

        return ToSnapshot();
    }

    public async Task AddLineAsync(AddPurchaseOrderLineCommand command)
    {
        EnsureExists();
        EnsureStatus(PurchaseOrderStatus.Draft);

        RaiseEvent(new PurchaseOrderLineAdded
        {
            PurchaseOrderId = State.PurchaseOrderId,
            LineId = command.LineId,
            SkuId = command.SkuId,
            SkuCode = command.SkuCode,
            ProductName = command.ProductName,
            QuantityOrdered = command.QuantityOrdered,
            UnitPrice = command.UnitPrice,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task UpdateLineAsync(UpdatePurchaseOrderLineCommand command)
    {
        EnsureExists();
        EnsureStatus(PurchaseOrderStatus.Draft);

        if (!State.Lines.Any(l => l.LineId == command.LineId))
            throw new InvalidOperationException("Line not found");

        RaiseEvent(new PurchaseOrderLineUpdated
        {
            PurchaseOrderId = State.PurchaseOrderId,
            LineId = command.LineId,
            QuantityOrdered = command.QuantityOrdered,
            UnitPrice = command.UnitPrice,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task RemoveLineAsync(Guid lineId)
    {
        EnsureExists();
        EnsureStatus(PurchaseOrderStatus.Draft);

        if (!State.Lines.Any(l => l.LineId == lineId))
            throw new InvalidOperationException("Line not found");

        RaiseEvent(new PurchaseOrderLineRemoved
        {
            PurchaseOrderId = State.PurchaseOrderId,
            LineId = lineId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task<PurchaseOrderSnapshot> SubmitAsync(SubmitPurchaseOrderCommand command)
    {
        EnsureExists();
        EnsureStatus(PurchaseOrderStatus.Draft);

        if (State.Lines.Count == 0)
            throw new InvalidOperationException("Cannot submit purchase order with no lines");

        RaiseEvent(new PurchaseOrderSubmittedEvent
        {
            PurchaseOrderId = State.PurchaseOrderId,
            SubmittedByUserId = command.SubmittedByUserId,
            OrderTotal = State.OrderTotal,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogInformation("Purchase order submitted: {OrderNumber}", State.OrderNumber);
        return ToSnapshot();
    }

    public async Task ReceiveLineAsync(ReceiveLineCommand command)
    {
        EnsureExists();

        if (State.Status != PurchaseOrderStatus.Submitted && State.Status != PurchaseOrderStatus.PartiallyReceived)
            throw new InvalidOperationException($"Cannot receive lines in status {State.Status}");

        var line = State.Lines.FirstOrDefault(l => l.LineId == command.LineId)
            ?? throw new InvalidOperationException("Line not found");

        RaiseEvent(new PurchaseOrderLineReceived
        {
            PurchaseOrderId = State.PurchaseOrderId,
            LineId = command.LineId,
            QuantityReceived = command.QuantityReceived,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Check if fully received
        if (State.Lines.All(l => l.QuantityReceived >= l.QuantityOrdered))
        {
            RaiseEvent(new PurchaseOrderFullyReceived
            {
                PurchaseOrderId = State.PurchaseOrderId,
                OccurredAt = DateTime.UtcNow
            });
            await ConfirmEvents();

            _logger.LogInformation("Purchase order fully received: {OrderNumber}", State.OrderNumber);
        }
    }

    public async Task<PurchaseOrderSnapshot> CancelAsync(CancelPurchaseOrderCommand command)
    {
        EnsureExists();

        if (State.Status == PurchaseOrderStatus.Received || State.Status == PurchaseOrderStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel purchase order in status {State.Status}");

        RaiseEvent(new PurchaseOrderCancelledEvent
        {
            PurchaseOrderId = State.PurchaseOrderId,
            Reason = command.Reason,
            CancelledByUserId = command.CancelledByUserId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogInformation("Purchase order cancelled: {OrderNumber} - {Reason}", State.OrderNumber, command.Reason);
        return ToSnapshot();
    }

    public Task<PurchaseOrderSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(ToSnapshot());
    }

    public Task<decimal> GetTotalAsync()
    {
        EnsureExists();
        return Task.FromResult(State.OrderTotal);
    }

    public Task<bool> IsFullyReceivedAsync()
    {
        return Task.FromResult(State.Status == PurchaseOrderStatus.Received);
    }

    private void EnsureExists()
    {
        if (State.PurchaseOrderId == Guid.Empty)
            throw new InvalidOperationException("Purchase order not found");
    }

    private void EnsureStatus(PurchaseOrderStatus expected)
    {
        if (State.Status != expected)
            throw new InvalidOperationException($"Purchase order must be in {expected} status, current: {State.Status}");
    }

    private PurchaseOrderSnapshot ToSnapshot() => new(
        State.PurchaseOrderId,
        State.OrderNumber,
        State.SupplierId,
        State.SupplierName,
        State.LocationId,
        State.CreatedByUserId,
        State.Status,
        State.ExpectedDeliveryDate,
        State.SubmittedAt,
        State.ReceivedAt,
        State.CancelledAt,
        State.CancellationReason,
        State.OrderTotal,
        State.Lines.Select(l => new PurchaseOrderLineSnapshot(
            l.LineId, l.SkuId, l.SkuCode, l.ProductName,
            l.QuantityOrdered, l.QuantityReceived,
            l.UnitPrice, l.LineTotal, l.Notes)).ToList(),
        State.Notes);
}
