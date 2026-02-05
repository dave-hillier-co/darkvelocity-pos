using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.Providers;
using Orleans.Streams;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class InventoryTransferGrain : JournaledGrain<InventoryTransferState, IInventoryTransferEvent>, IInventoryTransferGrain
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<InventoryTransferGrain> _logger;
    private Lazy<IAsyncStream<IStreamEvent>>? _inventoryStream;

    public InventoryTransferGrain(
        IGrainFactory grainFactory,
        ILogger<InventoryTransferGrain> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (State.OrganizationId != Guid.Empty)
        {
            InitializeLazyFields();
        }
        return base.OnActivateAsync(cancellationToken);
    }

    protected override void TransitionState(InventoryTransferState state, IInventoryTransferEvent @event)
    {
        switch (@event)
        {
            case TransferRequested e:
                state.TransferId = e.TransferId;
                state.OrganizationId = e.OrganizationId;
                state.SourceSiteId = e.SourceSiteId;
                state.DestinationSiteId = e.DestinationSiteId;
                state.TransferNumber = e.TransferNumber;
                state.RequestedBy = e.RequestedBy;
                state.RequestedAt = e.OccurredAt;
                state.RequestedDeliveryDate = e.RequestedDeliveryDate;
                state.Notes = e.Notes;
                state.Status = TransferStatus.Requested;
                break;

            case TransferLineAdded e:
                state.Lines.Add(new InventoryTransferLineState
                {
                    LineId = e.LineId,
                    IngredientId = e.IngredientId,
                    IngredientName = e.IngredientName,
                    RequestedQuantity = e.Quantity,
                    Unit = e.Unit
                });
                break;

            case TransferApproved e:
                state.Status = TransferStatus.Approved;
                state.ApprovedBy = e.ApprovedBy;
                state.ApprovedAt = e.OccurredAt;
                state.ApprovalNotes = e.Notes;
                break;

            case TransferRejected e:
                state.Status = TransferStatus.Rejected;
                state.RejectedBy = e.RejectedBy;
                state.RejectedAt = e.OccurredAt;
                state.RejectionReason = e.Reason;
                break;

            case TransferShipped e:
                state.Status = TransferStatus.Shipped;
                state.ShippedBy = e.ShippedBy;
                state.ShippedAt = e.OccurredAt;
                state.EstimatedArrival = e.EstimatedArrival;
                state.TrackingNumber = e.TrackingNumber;
                state.Carrier = e.Carrier;
                state.ShippingNotes = e.Notes;
                break;

            case TransferLineShipped e:
                var shipLine = state.Lines.FirstOrDefault(l => l.LineId == e.LineId);
                if (shipLine != null)
                {
                    shipLine.ShippedQuantity = e.ShippedQuantity;
                    shipLine.UnitCost = e.UnitCost;
                    shipLine.ShippedValue = e.ShippedValue;
                }
                state.TotalShippedValue = state.Lines.Sum(l => l.ShippedValue);
                break;

            case TransferItemReceived e:
                var recvLine = state.Lines.FirstOrDefault(l => l.LineId == e.LineId);
                if (recvLine != null)
                {
                    recvLine.ReceivedQuantity = e.ReceivedQuantity;
                    recvLine.ReceivedValue = e.ReceivedQuantity * recvLine.UnitCost;
                    recvLine.Condition = e.Condition;
                    recvLine.Notes = e.Notes;
                }
                break;

            case TransferReceived e:
                state.Status = TransferStatus.Received;
                state.ReceivedBy = e.ReceivedBy;
                state.ReceivedAt = e.OccurredAt;
                state.ReceiptNotes = e.Notes;
                state.TotalReceivedValue = e.TotalReceivedValue;
                state.TotalVarianceValue = e.TotalVarianceValue;
                break;

            case TransferCancelled e:
                state.Status = TransferStatus.Cancelled;
                state.CancelledBy = e.CancelledBy;
                state.CancelledAt = e.OccurredAt;
                state.CancellationReason = e.Reason;
                state.StockReturnedToSource = e.StockReturnedToSource;
                break;
        }
    }

    private void InitializeLazyFields()
    {
        var orgId = State.OrganizationId;

        _inventoryStream = new Lazy<IAsyncStream<IStreamEvent>>(() =>
        {
            var streamProvider = this.GetStreamProvider(StreamConstants.DefaultStreamProvider);
            var streamId = StreamId.Create(StreamConstants.InventoryStreamNamespace, orgId.ToString());
            return streamProvider.GetStream<IStreamEvent>(streamId);
        });
    }

    private IAsyncStream<IStreamEvent> InventoryStream => _inventoryStream!.Value;

    public async Task RequestAsync(RequestTransferCommand command)
    {
        if (State.TransferId != Guid.Empty)
            throw new InvalidOperationException("Transfer already exists");

        if (command.SourceSiteId == command.DestinationSiteId)
            throw new InvalidOperationException("Source and destination sites cannot be the same");

        if (command.Lines.Count == 0)
            throw new InvalidOperationException("Transfer must have at least one line item");

        var transferId = Guid.Parse(this.GetPrimaryKeyString().Split(':').Last());

        RaiseEvent(new TransferRequested
        {
            TransferId = transferId,
            OrganizationId = command.OrganizationId,
            SourceSiteId = command.SourceSiteId,
            DestinationSiteId = command.DestinationSiteId,
            TransferNumber = command.TransferNumber,
            RequestedBy = command.RequestedBy,
            RequestedDeliveryDate = command.RequestedDeliveryDate,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });

        // Add line items
        foreach (var line in command.Lines)
        {
            RaiseEvent(new TransferLineAdded
            {
                TransferId = transferId,
                LineId = Guid.NewGuid(),
                IngredientId = line.IngredientId,
                IngredientName = line.IngredientName,
                Quantity = line.Quantity,
                Unit = line.Unit,
                OccurredAt = DateTime.UtcNow
            });
        }

        await ConfirmEvents();
        InitializeLazyFields();

        // Publish stream event
        await InventoryStream.OnNextAsync(new InventoryTransferStatusChangedEvent(
            transferId,
            command.SourceSiteId,
            command.DestinationSiteId,
            "Requested",
            command.Notes)
        {
            OrganizationId = command.OrganizationId
        });

        _logger.LogInformation(
            "Transfer {TransferNumber} requested from site {Source} to site {Dest} by {RequestedBy}",
            command.TransferNumber,
            command.SourceSiteId,
            command.DestinationSiteId,
            command.RequestedBy);
    }

    public Task<InventoryTransferState> GetStateAsync()
    {
        return Task.FromResult(State);
    }

    public Task<TransferSummary> GetSummaryAsync()
    {
        EnsureExists();

        return Task.FromResult(new TransferSummary
        {
            TransferId = State.TransferId,
            TransferNumber = State.TransferNumber,
            Status = State.Status,
            SourceSiteId = State.SourceSiteId,
            DestinationSiteId = State.DestinationSiteId,
            RequestedAt = State.RequestedAt,
            ShippedAt = State.ShippedAt,
            ReceivedAt = State.ReceivedAt,
            TotalLines = State.Lines.Count,
            TotalValue = State.TotalShippedValue,
            TotalVariance = State.TotalVarianceValue
        });
    }

    public async Task ApproveAsync(ApproveTransferCommand command)
    {
        EnsureExists();

        if (State.Status != TransferStatus.Requested)
            throw new InvalidOperationException($"Cannot approve transfer in status {State.Status}");

        RaiseEvent(new TransferApproved
        {
            TransferId = State.TransferId,
            ApprovedBy = command.ApprovedBy,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        await InventoryStream.OnNextAsync(new InventoryTransferStatusChangedEvent(
            State.TransferId,
            State.SourceSiteId,
            State.DestinationSiteId,
            "Approved",
            command.Notes)
        {
            OrganizationId = State.OrganizationId
        });

        _logger.LogInformation(
            "Transfer {TransferNumber} approved by {ApprovedBy}",
            State.TransferNumber,
            command.ApprovedBy);
    }

    public async Task RejectAsync(RejectTransferCommand command)
    {
        EnsureExists();

        if (State.Status != TransferStatus.Requested)
            throw new InvalidOperationException($"Cannot reject transfer in status {State.Status}");

        RaiseEvent(new TransferRejected
        {
            TransferId = State.TransferId,
            RejectedBy = command.RejectedBy,
            Reason = command.Reason,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        await InventoryStream.OnNextAsync(new InventoryTransferStatusChangedEvent(
            State.TransferId,
            State.SourceSiteId,
            State.DestinationSiteId,
            "Rejected",
            command.Reason)
        {
            OrganizationId = State.OrganizationId
        });

        _logger.LogInformation(
            "Transfer {TransferNumber} rejected by {RejectedBy}. Reason: {Reason}",
            State.TransferNumber,
            command.RejectedBy,
            command.Reason);
    }

    public async Task ShipAsync(ShipTransferCommand command)
    {
        EnsureExists();

        if (State.Status != TransferStatus.Approved)
            throw new InvalidOperationException($"Cannot ship transfer in status {State.Status}");

        // Deduct stock from source site inventory
        foreach (var line in State.Lines)
        {
            var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(
                GrainKeys.Inventory(State.OrganizationId, State.SourceSiteId, line.IngredientId));

            var inventoryState = await inventoryGrain.GetStateAsync();
            var unitCost = inventoryState.WeightedAverageCost;

            await inventoryGrain.TransferOutAsync(new TransferOutCommand(
                line.RequestedQuantity,
                State.DestinationSiteId,
                State.TransferId,
                command.ShippedBy));

            RaiseEvent(new TransferLineShipped
            {
                TransferId = State.TransferId,
                LineId = line.LineId,
                IngredientId = line.IngredientId,
                ShippedQuantity = line.RequestedQuantity,
                UnitCost = unitCost,
                ShippedValue = line.RequestedQuantity * unitCost,
                OccurredAt = DateTime.UtcNow
            });
        }

        RaiseEvent(new TransferShipped
        {
            TransferId = State.TransferId,
            ShippedBy = command.ShippedBy,
            EstimatedArrival = command.EstimatedArrival,
            TrackingNumber = command.TrackingNumber,
            Carrier = command.Carrier,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        await InventoryStream.OnNextAsync(new InventoryTransferStatusChangedEvent(
            State.TransferId,
            State.SourceSiteId,
            State.DestinationSiteId,
            "Shipped",
            command.Notes)
        {
            OrganizationId = State.OrganizationId
        });

        _logger.LogInformation(
            "Transfer {TransferNumber} shipped by {ShippedBy}. Total value: {Value:C}",
            State.TransferNumber,
            command.ShippedBy,
            State.TotalShippedValue);
    }

    public async Task ReceiveItemAsync(ReceiveTransferItemCommand command)
    {
        EnsureExists();

        if (State.Status != TransferStatus.Shipped)
            throw new InvalidOperationException($"Cannot receive items for transfer in status {State.Status}");

        var line = State.Lines.FirstOrDefault(l => l.IngredientId == command.IngredientId)
            ?? throw new InvalidOperationException($"Ingredient {command.IngredientId} not found in transfer");

        var variance = command.ReceivedQuantity - line.ShippedQuantity;
        var varianceValue = variance * line.UnitCost;

        RaiseEvent(new TransferItemReceived
        {
            TransferId = State.TransferId,
            LineId = line.LineId,
            IngredientId = command.IngredientId,
            ReceivedQuantity = command.ReceivedQuantity,
            ShippedQuantity = line.ShippedQuantity,
            Variance = variance,
            VarianceValue = varianceValue,
            ReceivedBy = command.ReceivedBy,
            Condition = command.Condition,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogInformation(
            "Transfer {TransferNumber}: received {Received} of {IngredientName} (shipped: {Shipped}, variance: {Variance})",
            State.TransferNumber,
            command.ReceivedQuantity,
            line.IngredientName,
            line.ShippedQuantity,
            variance);
    }

    public async Task FinalizeReceiptAsync(FinalizeTransferReceiptCommand command)
    {
        EnsureExists();

        if (State.Status != TransferStatus.Shipped)
            throw new InvalidOperationException($"Cannot finalize receipt for transfer in status {State.Status}");

        // Check all items have been received
        var unreceived = State.Lines.Where(l => l.ReceivedQuantity == 0 && l.ShippedQuantity > 0).ToList();
        if (unreceived.Count > 0)
        {
            // Auto-receive missing items with shipped quantity
            foreach (var line in unreceived)
            {
                RaiseEvent(new TransferItemReceived
                {
                    TransferId = State.TransferId,
                    LineId = line.LineId,
                    IngredientId = line.IngredientId,
                    ReceivedQuantity = line.ShippedQuantity,
                    ShippedQuantity = line.ShippedQuantity,
                    Variance = 0,
                    VarianceValue = 0,
                    ReceivedBy = command.ReceivedBy,
                    OccurredAt = DateTime.UtcNow
                });
            }
        }

        // Credit stock to destination site inventory
        foreach (var line in State.Lines.Where(l => l.ShippedQuantity > 0))
        {
            var receivedQty = line.ReceivedQuantity > 0 ? line.ReceivedQuantity : line.ShippedQuantity;

            var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(
                GrainKeys.Inventory(State.OrganizationId, State.DestinationSiteId, line.IngredientId));

            await inventoryGrain.ReceiveTransferAsync(new ReceiveTransferCommand(
                receivedQty,
                line.UnitCost,
                State.SourceSiteId,
                State.TransferId));
        }

        var totalReceivedValue = State.Lines.Sum(l =>
        {
            var qty = l.ReceivedQuantity > 0 ? l.ReceivedQuantity : l.ShippedQuantity;
            return qty * l.UnitCost;
        });
        var totalVariance = totalReceivedValue - State.TotalShippedValue;
        var linesWithVariance = State.Lines.Count(l =>
            l.ReceivedQuantity > 0 && l.ReceivedQuantity != l.ShippedQuantity);

        RaiseEvent(new TransferReceived
        {
            TransferId = State.TransferId,
            ReceivedBy = command.ReceivedBy,
            TotalShippedValue = State.TotalShippedValue,
            TotalReceivedValue = totalReceivedValue,
            TotalVarianceValue = totalVariance,
            LinesWithVariance = linesWithVariance,
            Notes = command.Notes,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        await InventoryStream.OnNextAsync(new InventoryTransferStatusChangedEvent(
            State.TransferId,
            State.SourceSiteId,
            State.DestinationSiteId,
            "Received",
            command.Notes)
        {
            OrganizationId = State.OrganizationId
        });

        _logger.LogInformation(
            "Transfer {TransferNumber} received. Shipped: {Shipped:C}, Received: {Received:C}, Variance: {Variance:C}",
            State.TransferNumber,
            State.TotalShippedValue,
            totalReceivedValue,
            totalVariance);
    }

    public async Task CancelAsync(CancelTransferCommand command)
    {
        EnsureExists();

        if (State.Status == TransferStatus.Received)
            throw new InvalidOperationException("Cannot cancel a received transfer");

        if (State.Status == TransferStatus.Cancelled)
            throw new InvalidOperationException("Transfer already cancelled");

        var previousStatus = State.Status;

        // If already shipped and we need to return stock, credit it back to source
        if (previousStatus == TransferStatus.Shipped && command.ReturnStockToSource)
        {
            foreach (var line in State.Lines.Where(l => l.ShippedQuantity > 0))
            {
                var inventoryGrain = _grainFactory.GetGrain<IInventoryGrain>(
                    GrainKeys.Inventory(State.OrganizationId, State.SourceSiteId, line.IngredientId));

                // Receive it back as a transfer
                await inventoryGrain.ReceiveTransferAsync(new ReceiveTransferCommand(
                    line.ShippedQuantity,
                    line.UnitCost,
                    State.DestinationSiteId, // "from" the cancelled destination
                    State.TransferId));
            }
        }

        RaiseEvent(new TransferCancelled
        {
            TransferId = State.TransferId,
            CancelledBy = command.CancelledBy,
            Reason = command.Reason,
            StockReturnedToSource = command.ReturnStockToSource && previousStatus == TransferStatus.Shipped,
            PreviousStatus = previousStatus,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        await InventoryStream.OnNextAsync(new InventoryTransferStatusChangedEvent(
            State.TransferId,
            State.SourceSiteId,
            State.DestinationSiteId,
            "Cancelled",
            command.Reason)
        {
            OrganizationId = State.OrganizationId
        });

        _logger.LogInformation(
            "Transfer {TransferNumber} cancelled by {CancelledBy}. Reason: {Reason}. Stock returned: {Returned}",
            State.TransferNumber,
            command.CancelledBy,
            command.Reason,
            command.ReturnStockToSource && previousStatus == TransferStatus.Shipped);
    }

    public Task<IReadOnlyList<TransferLineState>> GetLinesAsync()
    {
        EnsureExists();

        var lines = State.Lines.Select(l => new TransferLineState
        {
            LineId = l.LineId,
            IngredientId = l.IngredientId,
            IngredientName = l.IngredientName,
            RequestedQuantity = l.RequestedQuantity,
            ShippedQuantity = l.ShippedQuantity,
            ReceivedQuantity = l.ReceivedQuantity,
            UnitCost = l.UnitCost,
            Unit = l.Unit,
            Condition = l.Condition,
            Notes = l.Notes
        }).ToList();

        return Task.FromResult<IReadOnlyList<TransferLineState>>(lines);
    }

    public Task<IReadOnlyList<TransferLineVariance>> GetVariancesAsync()
    {
        EnsureExists();

        var variances = State.Lines
            .Where(l => l.ShippedQuantity > 0 && l.ReceivedQuantity > 0 && l.ReceivedQuantity != l.ShippedQuantity)
            .Select(l =>
            {
                var variance = l.ReceivedQuantity - l.ShippedQuantity;
                var variancePercentage = l.ShippedQuantity != 0
                    ? (variance / l.ShippedQuantity) * 100
                    : 0;
                return new TransferLineVariance(
                    l.IngredientId,
                    l.IngredientName,
                    l.ShippedQuantity,
                    l.ReceivedQuantity,
                    variance,
                    variancePercentage,
                    variance * l.UnitCost);
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<TransferLineVariance>>(variances);
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.TransferId != Guid.Empty);

    private void EnsureExists()
    {
        if (State.TransferId == Guid.Empty)
            throw new InvalidOperationException("Transfer not found");
    }
}
