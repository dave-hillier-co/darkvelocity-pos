using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

/// <summary>
/// Grain for delivery management.
/// Manages goods receipt and discrepancy tracking using event sourcing.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class DeliveryGrain : JournaledGrain<DeliveryState, IDeliveryEvent>, IDeliveryGrain
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<DeliveryGrain> _logger;

    public DeliveryGrain(IGrainFactory grainFactory, ILogger<DeliveryGrain> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    protected override void TransitionState(DeliveryState state, IDeliveryEvent @event)
    {
        switch (@event)
        {
            case DeliveryCreated e:
                state.DeliveryId = e.DeliveryId;
                state.OrgId = e.OrgId;
                state.DeliveryNumber = e.DeliveryNumber;
                state.SupplierId = e.SupplierId;
                state.SupplierName = e.SupplierName;
                state.PurchaseOrderId = e.PurchaseOrderId;
                state.LocationId = e.LocationId;
                state.ReceivedByUserId = e.ReceivedByUserId;
                state.SupplierInvoiceNumber = e.SupplierInvoiceNumber;
                state.Notes = e.Notes;
                state.Status = DeliveryStatus.Pending;
                state.ReceivedAt = e.OccurredAt;
                state.Version++;
                break;

            case DeliveryLineAdded e:
                state.Lines.Add(new DeliveryLineState
                {
                    LineId = e.LineId,
                    SkuId = e.SkuId,
                    SkuCode = e.SkuCode,
                    ProductName = e.ProductName,
                    PurchaseOrderLineId = e.PurchaseOrderLineId,
                    QuantityReceived = e.QuantityReceived,
                    UnitCost = e.UnitCost,
                    LineTotal = e.QuantityReceived * e.UnitCost,
                    BatchNumber = e.BatchNumber,
                    ExpiryDate = e.ExpiryDate,
                    Notes = e.Notes
                });
                state.TotalValue = state.Lines.Sum(l => l.LineTotal);
                state.Version++;
                break;

            case DeliveryDiscrepancyRecorded e:
                state.Discrepancies.Add(new DeliveryDiscrepancyState
                {
                    DiscrepancyId = e.DiscrepancyId,
                    LineId = e.LineId,
                    Type = e.Type,
                    ExpectedQuantity = e.ExpectedQuantity,
                    ActualQuantity = e.ActualQuantity,
                    Notes = e.Notes
                });
                state.HasDiscrepancies = true;
                state.Version++;
                break;

            case DeliveryAcceptedEvent e:
                state.Status = DeliveryStatus.Accepted;
                state.AcceptedAt = e.OccurredAt;
                state.TotalValue = e.TotalValue;
                state.HasDiscrepancies = e.HasDiscrepancies;
                state.Version++;
                break;

            case DeliveryRejectedEvent e:
                state.Status = DeliveryStatus.Rejected;
                state.RejectedAt = e.OccurredAt;
                state.RejectionReason = e.Reason;
                state.Version++;
                break;
        }
    }

    public async Task<DeliverySnapshot> CreateAsync(CreateDeliveryCommand command)
    {
        if (State.DeliveryId != Guid.Empty)
            throw new InvalidOperationException("Delivery already exists");

        var key = this.GetPrimaryKeyString();
        var (orgId, _, deliveryId) = GrainKeys.ParseOrgEntity(key);

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

        var deliveryNumber = $"DEL-{DateTime.UtcNow:yyyyMMdd}-{deliveryId.ToString()[..8].ToUpperInvariant()}";

        RaiseEvent(new DeliveryCreated
        {
            DeliveryId = deliveryId,
            OrgId = orgId,
            DeliveryNumber = deliveryNumber,
            SupplierId = command.SupplierId,
            SupplierName = supplierName,
            PurchaseOrderId = command.PurchaseOrderId,
            LocationId = command.LocationId,
            ReceivedByUserId = command.ReceivedByUserId,
            SupplierInvoiceNumber = command.SupplierInvoiceNumber,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogInformation("Delivery created: {DeliveryNumber} from {Supplier}",
            deliveryNumber, supplierName);

        return ToSnapshot();
    }

    public async Task AddLineAsync(AddDeliveryLineCommand command)
    {
        EnsureExists();
        EnsureStatus(DeliveryStatus.Pending);

        RaiseEvent(new DeliveryLineAdded
        {
            DeliveryId = State.DeliveryId,
            LineId = command.LineId,
            SkuId = command.SkuId,
            SkuCode = command.SkuCode,
            ProductName = command.ProductName,
            PurchaseOrderLineId = command.PurchaseOrderLineId,
            QuantityReceived = command.QuantityReceived,
            UnitCost = command.UnitCost,
            BatchNumber = command.BatchNumber,
            ExpiryDate = command.ExpiryDate,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task RecordDiscrepancyAsync(RecordDiscrepancyCommand command)
    {
        EnsureExists();
        EnsureStatus(DeliveryStatus.Pending);

        if (!State.Lines.Any(l => l.LineId == command.LineId))
            throw new InvalidOperationException("Line not found");

        RaiseEvent(new DeliveryDiscrepancyRecorded
        {
            DeliveryId = State.DeliveryId,
            DiscrepancyId = command.DiscrepancyId,
            LineId = command.LineId,
            Type = command.Type,
            ExpectedQuantity = command.ExpectedQuantity,
            ActualQuantity = command.ActualQuantity,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task<DeliverySnapshot> AcceptAsync(AcceptDeliveryCommand command)
    {
        EnsureExists();
        EnsureStatus(DeliveryStatus.Pending);

        if (State.Lines.Count == 0)
            throw new InvalidOperationException("Cannot accept delivery with no lines");

        RaiseEvent(new DeliveryAcceptedEvent
        {
            DeliveryId = State.DeliveryId,
            AcceptedByUserId = command.AcceptedByUserId,
            TotalValue = State.TotalValue,
            HasDiscrepancies = State.HasDiscrepancies,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        // Update PO with received quantities if linked
        if (State.PurchaseOrderId.HasValue)
        {
            var orgId = State.OrgId;
            var poGrain = _grainFactory.GetGrain<IPurchaseOrderGrain>(
                GrainKeys.PurchaseOrder(orgId, State.PurchaseOrderId.Value));

            foreach (var line in State.Lines.Where(l => l.PurchaseOrderLineId.HasValue))
            {
                await poGrain.ReceiveLineAsync(new ReceiveLineCommand(
                    line.PurchaseOrderLineId!.Value,
                    line.QuantityReceived));
            }
        }

        _logger.LogInformation("Delivery accepted: {DeliveryNumber}", State.DeliveryNumber);
        return ToSnapshot();
    }

    public async Task<DeliverySnapshot> RejectAsync(RejectDeliveryCommand command)
    {
        EnsureExists();
        EnsureStatus(DeliveryStatus.Pending);

        RaiseEvent(new DeliveryRejectedEvent
        {
            DeliveryId = State.DeliveryId,
            Reason = command.Reason,
            RejectedByUserId = command.RejectedByUserId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogInformation("Delivery rejected: {DeliveryNumber} - {Reason}",
            State.DeliveryNumber, command.Reason);

        return ToSnapshot();
    }

    public Task<DeliverySnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(ToSnapshot());
    }

    public Task<bool> HasDiscrepanciesAsync()
    {
        return Task.FromResult(State.HasDiscrepancies);
    }

    private void EnsureExists()
    {
        if (State.DeliveryId == Guid.Empty)
            throw new InvalidOperationException("Delivery not found");
    }

    private void EnsureStatus(DeliveryStatus expected)
    {
        if (State.Status != expected)
            throw new InvalidOperationException($"Delivery must be in {expected} status, current: {State.Status}");
    }

    private DeliverySnapshot ToSnapshot() => new(
        State.DeliveryId,
        State.DeliveryNumber,
        State.SupplierId,
        State.SupplierName,
        State.PurchaseOrderId,
        State.LocationId,
        State.ReceivedByUserId,
        State.Status,
        State.ReceivedAt,
        State.AcceptedAt,
        State.RejectedAt,
        State.RejectionReason,
        State.TotalValue,
        State.HasDiscrepancies,
        State.SupplierInvoiceNumber,
        State.Lines.Select(l => new DeliveryLineSnapshot(
            l.LineId, l.SkuId, l.SkuCode, l.ProductName, l.PurchaseOrderLineId,
            l.QuantityReceived, l.UnitCost, l.LineTotal,
            l.BatchNumber, l.ExpiryDate, l.Notes)).ToList(),
        State.Discrepancies.Select(d => new DeliveryDiscrepancySnapshot(
            d.DiscrepancyId, d.LineId, d.Type,
            d.ExpectedQuantity, d.ActualQuantity, d.Notes)).ToList(),
        State.Notes);
}
