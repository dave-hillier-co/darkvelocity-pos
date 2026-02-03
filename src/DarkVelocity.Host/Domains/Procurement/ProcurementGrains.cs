using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Supplier Grain
// ============================================================================

/// <summary>
/// Grain for supplier management.
/// Manages supplier information, pricing, and performance metrics.
/// </summary>
public class SupplierGrain : Grain, ISupplierGrain
{
    private readonly IPersistentState<SupplierState> _state;

    public SupplierGrain(
        [PersistentState("supplier", "OrleansStorage")]
        IPersistentState<SupplierState> state)
    {
        _state = state;
    }

    public async Task<SupplierSnapshot> CreateAsync(CreateSupplierCommand command)
    {
        if (_state.State.SupplierId != Guid.Empty)
            throw new InvalidOperationException("Supplier already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var supplierId = Guid.Parse(parts[2]);

        _state.State = new SupplierState
        {
            OrgId = orgId,
            SupplierId = supplierId,
            Code = command.Code,
            Name = command.Name,
            ContactName = command.ContactName,
            ContactEmail = command.ContactEmail,
            ContactPhone = command.ContactPhone,
            Address = command.Address,
            PaymentTermsDays = command.PaymentTermsDays,
            LeadTimeDays = command.LeadTimeDays,
            Notes = command.Notes,
            IsActive = true,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<SupplierSnapshot> UpdateAsync(UpdateSupplierCommand command)
    {
        EnsureInitialized();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.ContactName != null) _state.State.ContactName = command.ContactName;
        if (command.ContactEmail != null) _state.State.ContactEmail = command.ContactEmail;
        if (command.ContactPhone != null) _state.State.ContactPhone = command.ContactPhone;
        if (command.Address != null) _state.State.Address = command.Address;
        if (command.PaymentTermsDays.HasValue) _state.State.PaymentTermsDays = command.PaymentTermsDays.Value;
        if (command.LeadTimeDays.HasValue) _state.State.LeadTimeDays = command.LeadTimeDays.Value;
        if (command.Notes != null) _state.State.Notes = command.Notes;
        if (command.IsActive.HasValue) _state.State.IsActive = command.IsActive.Value;

        _state.State.Version++;
        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task AddIngredientAsync(SupplierIngredient ingredient)
    {
        EnsureInitialized();

        var existing = _state.State.Ingredients.FirstOrDefault(i => i.IngredientId == ingredient.IngredientId);
        if (existing != null)
        {
            existing.SupplierSku = ingredient.SupplierSku;
            existing.UnitPrice = ingredient.UnitPrice;
            existing.Unit = ingredient.Unit;
            existing.MinOrderQuantity = ingredient.MinOrderQuantity;
            existing.LeadTimeDays = ingredient.LeadTimeDays;
        }
        else
        {
            _state.State.Ingredients.Add(new SupplierIngredientState
            {
                IngredientId = ingredient.IngredientId,
                IngredientName = ingredient.IngredientName,
                Sku = ingredient.Sku,
                SupplierSku = ingredient.SupplierSku,
                UnitPrice = ingredient.UnitPrice,
                Unit = ingredient.Unit,
                MinOrderQuantity = ingredient.MinOrderQuantity,
                LeadTimeDays = ingredient.LeadTimeDays
            });
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RemoveIngredientAsync(Guid ingredientId)
    {
        EnsureInitialized();

        _state.State.Ingredients.RemoveAll(i => i.IngredientId == ingredientId);
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task UpdateIngredientPriceAsync(Guid ingredientId, decimal newPrice)
    {
        EnsureInitialized();

        var ingredient = _state.State.Ingredients.FirstOrDefault(i => i.IngredientId == ingredientId)
            ?? throw new InvalidOperationException("Ingredient not found");

        ingredient.UnitPrice = newPrice;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public Task<SupplierSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<decimal> GetIngredientPriceAsync(Guid ingredientId)
    {
        EnsureInitialized();

        var ingredient = _state.State.Ingredients.FirstOrDefault(i => i.IngredientId == ingredientId)
            ?? throw new InvalidOperationException("Ingredient not found");

        return Task.FromResult(ingredient.UnitPrice);
    }

    public async Task RecordPurchaseAsync(decimal amount, bool onTime)
    {
        EnsureInitialized();

        _state.State.TotalPurchasesYtd += amount;
        _state.State.TotalDeliveries++;
        if (onTime) _state.State.OnTimeDeliveries++;

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    private SupplierSnapshot CreateSnapshot()
    {
        var onTimePercent = _state.State.TotalDeliveries > 0
            ? _state.State.OnTimeDeliveries * 100 / _state.State.TotalDeliveries
            : 100;

        return new SupplierSnapshot(
            SupplierId: _state.State.SupplierId,
            Code: _state.State.Code,
            Name: _state.State.Name,
            ContactName: _state.State.ContactName,
            ContactEmail: _state.State.ContactEmail,
            ContactPhone: _state.State.ContactPhone,
            Address: _state.State.Address,
            PaymentTermsDays: _state.State.PaymentTermsDays,
            LeadTimeDays: _state.State.LeadTimeDays,
            Notes: _state.State.Notes,
            IsActive: _state.State.IsActive,
            Ingredients: _state.State.Ingredients.Select(i => new SupplierIngredient(
                IngredientId: i.IngredientId,
                IngredientName: i.IngredientName,
                Sku: i.Sku,
                SupplierSku: i.SupplierSku,
                UnitPrice: i.UnitPrice,
                Unit: i.Unit,
                MinOrderQuantity: i.MinOrderQuantity,
                LeadTimeDays: i.LeadTimeDays)).ToList(),
            TotalPurchasesYtd: _state.State.TotalPurchasesYtd,
            OnTimeDeliveryPercent: onTimePercent);
    }

    private void EnsureInitialized()
    {
        if (_state.State.SupplierId == Guid.Empty)
            throw new InvalidOperationException("Supplier grain not initialized");
    }
}

// ============================================================================
// Purchase Order Grain
// ============================================================================

/// <summary>
/// Grain for purchase order management.
/// Manages PO lifecycle from draft to received.
/// </summary>
public class PurchaseOrderGrain : Grain, IPurchaseOrderGrain
{
    private readonly IPersistentState<PurchaseOrderState> _state;
    private static int _orderCounter = 1000;

    public PurchaseOrderGrain(
        [PersistentState("purchaseOrder", "OrleansStorage")]
        IPersistentState<PurchaseOrderState> state)
    {
        _state = state;
    }

    public async Task<PurchaseOrderSnapshot> CreateAsync(CreatePurchaseOrderCommand command)
    {
        if (_state.State.PurchaseOrderId != Guid.Empty)
            throw new InvalidOperationException("Purchase order already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var poId = Guid.Parse(parts[2]);

        _state.State = new PurchaseOrderState
        {
            OrgId = orgId,
            PurchaseOrderId = poId,
            OrderNumber = $"PO-{DateTime.UtcNow:yyyyMMdd}-{Interlocked.Increment(ref _orderCounter)}",
            SupplierId = command.SupplierId,
            LocationId = command.LocationId,
            CreatedByUserId = command.CreatedByUserId,
            Status = PurchaseOrderStatus.Draft,
            ExpectedDeliveryDate = command.ExpectedDeliveryDate,
            Notes = command.Notes,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task AddLineAsync(AddPurchaseOrderLineCommand command)
    {
        EnsureInitialized();

        if (_state.State.Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException("Cannot modify a submitted purchase order");

        var lineTotal = command.QuantityOrdered * command.UnitPrice;

        _state.State.Lines.Add(new PurchaseOrderLineState
        {
            LineId = command.LineId,
            IngredientId = command.IngredientId,
            IngredientName = command.IngredientName,
            QuantityOrdered = command.QuantityOrdered,
            UnitPrice = command.UnitPrice,
            LineTotal = lineTotal,
            Notes = command.Notes
        });

        RecalculateTotal();
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task UpdateLineAsync(UpdatePurchaseOrderLineCommand command)
    {
        EnsureInitialized();

        if (_state.State.Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException("Cannot modify a submitted purchase order");

        var line = _state.State.Lines.FirstOrDefault(l => l.LineId == command.LineId)
            ?? throw new InvalidOperationException("Line not found");

        if (command.QuantityOrdered.HasValue) line.QuantityOrdered = command.QuantityOrdered.Value;
        if (command.UnitPrice.HasValue) line.UnitPrice = command.UnitPrice.Value;
        if (command.Notes != null) line.Notes = command.Notes;

        line.LineTotal = line.QuantityOrdered * line.UnitPrice;

        RecalculateTotal();
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RemoveLineAsync(Guid lineId)
    {
        EnsureInitialized();

        if (_state.State.Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException("Cannot modify a submitted purchase order");

        _state.State.Lines.RemoveAll(l => l.LineId == lineId);
        RecalculateTotal();
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task<PurchaseOrderSnapshot> SubmitAsync(SubmitPurchaseOrderCommand command)
    {
        EnsureInitialized();

        if (_state.State.Status != PurchaseOrderStatus.Draft)
            throw new InvalidOperationException("Purchase order is not in draft status");

        if (_state.State.Lines.Count == 0)
            throw new InvalidOperationException("Cannot submit an empty purchase order");

        _state.State.Status = PurchaseOrderStatus.Submitted;
        _state.State.SubmittedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task ReceiveLineAsync(ReceiveLineCommand command)
    {
        EnsureInitialized();

        if (_state.State.Status == PurchaseOrderStatus.Draft ||
            _state.State.Status == PurchaseOrderStatus.Cancelled)
            throw new InvalidOperationException("Cannot receive against this purchase order");

        var line = _state.State.Lines.FirstOrDefault(l => l.LineId == command.LineId)
            ?? throw new InvalidOperationException("Line not found");

        line.QuantityReceived += command.QuantityReceived;

        // Update status based on received quantities
        var allReceived = _state.State.Lines.All(l => l.QuantityReceived >= l.QuantityOrdered);
        var anyReceived = _state.State.Lines.Any(l => l.QuantityReceived > 0);

        if (allReceived)
        {
            _state.State.Status = PurchaseOrderStatus.Received;
            _state.State.ReceivedAt = DateTime.UtcNow;
        }
        else if (anyReceived)
        {
            _state.State.Status = PurchaseOrderStatus.PartiallyReceived;
        }

        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task<PurchaseOrderSnapshot> CancelAsync(CancelPurchaseOrderCommand command)
    {
        EnsureInitialized();

        if (_state.State.Status == PurchaseOrderStatus.Received ||
            _state.State.Status == PurchaseOrderStatus.Cancelled)
            throw new InvalidOperationException("Cannot cancel this purchase order");

        _state.State.Status = PurchaseOrderStatus.Cancelled;
        _state.State.CancelledAt = DateTime.UtcNow;
        _state.State.CancellationReason = command.Reason;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<PurchaseOrderSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<decimal> GetTotalAsync()
    {
        EnsureInitialized();
        return Task.FromResult(_state.State.OrderTotal);
    }

    public Task<bool> IsFullyReceivedAsync()
    {
        EnsureInitialized();
        return Task.FromResult(_state.State.Status == PurchaseOrderStatus.Received);
    }

    private void RecalculateTotal()
    {
        _state.State.OrderTotal = _state.State.Lines.Sum(l => l.LineTotal);
    }

    private PurchaseOrderSnapshot CreateSnapshot()
    {
        return new PurchaseOrderSnapshot(
            PurchaseOrderId: _state.State.PurchaseOrderId,
            OrderNumber: _state.State.OrderNumber,
            SupplierId: _state.State.SupplierId,
            SupplierName: _state.State.SupplierName,
            LocationId: _state.State.LocationId,
            CreatedByUserId: _state.State.CreatedByUserId,
            Status: _state.State.Status,
            ExpectedDeliveryDate: _state.State.ExpectedDeliveryDate,
            SubmittedAt: _state.State.SubmittedAt,
            ReceivedAt: _state.State.ReceivedAt,
            CancelledAt: _state.State.CancelledAt,
            CancellationReason: _state.State.CancellationReason,
            OrderTotal: _state.State.OrderTotal,
            Lines: _state.State.Lines.Select(l => new PurchaseOrderLineSnapshot(
                LineId: l.LineId,
                IngredientId: l.IngredientId,
                IngredientName: l.IngredientName,
                QuantityOrdered: l.QuantityOrdered,
                QuantityReceived: l.QuantityReceived,
                UnitPrice: l.UnitPrice,
                LineTotal: l.LineTotal,
                Notes: l.Notes)).ToList(),
            Notes: _state.State.Notes);
    }

    private void EnsureInitialized()
    {
        if (_state.State.PurchaseOrderId == Guid.Empty)
            throw new InvalidOperationException("Purchase order grain not initialized");
    }
}

// ============================================================================
// Delivery Grain
// ============================================================================

/// <summary>
/// Grain for delivery management.
/// Manages goods receipt and discrepancy tracking.
/// </summary>
public class DeliveryGrain : Grain, IDeliveryGrain
{
    private readonly IPersistentState<DeliveryState> _state;
    private static int _deliveryCounter = 1000;

    public DeliveryGrain(
        [PersistentState("delivery", "OrleansStorage")]
        IPersistentState<DeliveryState> state)
    {
        _state = state;
    }

    public async Task<DeliverySnapshot> CreateAsync(CreateDeliveryCommand command)
    {
        if (_state.State.DeliveryId != Guid.Empty)
            throw new InvalidOperationException("Delivery already exists");

        var key = this.GetPrimaryKeyString();
        var parts = key.Split(':');
        var orgId = Guid.Parse(parts[0]);
        var deliveryId = Guid.Parse(parts[2]);

        _state.State = new DeliveryState
        {
            OrgId = orgId,
            DeliveryId = deliveryId,
            DeliveryNumber = $"DEL-{DateTime.UtcNow:yyyyMMdd}-{Interlocked.Increment(ref _deliveryCounter)}",
            SupplierId = command.SupplierId,
            PurchaseOrderId = command.PurchaseOrderId,
            LocationId = command.LocationId,
            ReceivedByUserId = command.ReceivedByUserId,
            Status = DeliveryStatus.Pending,
            ReceivedAt = DateTime.UtcNow,
            SupplierInvoiceNumber = command.SupplierInvoiceNumber,
            Notes = command.Notes,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task AddLineAsync(AddDeliveryLineCommand command)
    {
        EnsureInitialized();

        if (_state.State.Status != DeliveryStatus.Pending)
            throw new InvalidOperationException("Cannot modify a processed delivery");

        var lineTotal = command.QuantityReceived * command.UnitCost;

        _state.State.Lines.Add(new DeliveryLineState
        {
            LineId = command.LineId,
            IngredientId = command.IngredientId,
            IngredientName = command.IngredientName,
            PurchaseOrderLineId = command.PurchaseOrderLineId,
            QuantityReceived = command.QuantityReceived,
            UnitCost = command.UnitCost,
            LineTotal = lineTotal,
            BatchNumber = command.BatchNumber,
            ExpiryDate = command.ExpiryDate,
            Notes = command.Notes
        });

        _state.State.TotalValue += lineTotal;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task RecordDiscrepancyAsync(RecordDiscrepancyCommand command)
    {
        EnsureInitialized();

        if (_state.State.Status != DeliveryStatus.Pending)
            throw new InvalidOperationException("Cannot record discrepancies on a processed delivery");

        _state.State.Discrepancies.Add(new DeliveryDiscrepancyState
        {
            DiscrepancyId = command.DiscrepancyId,
            LineId = command.LineId,
            Type = command.Type,
            ExpectedQuantity = command.ExpectedQuantity,
            ActualQuantity = command.ActualQuantity,
            Notes = command.Notes
        });

        _state.State.HasDiscrepancies = true;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task<DeliverySnapshot> AcceptAsync(AcceptDeliveryCommand command)
    {
        EnsureInitialized();

        if (_state.State.Status != DeliveryStatus.Pending)
            throw new InvalidOperationException("Delivery is not pending");

        _state.State.Status = DeliveryStatus.Accepted;
        _state.State.AcceptedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<DeliverySnapshot> RejectAsync(RejectDeliveryCommand command)
    {
        EnsureInitialized();

        if (_state.State.Status != DeliveryStatus.Pending)
            throw new InvalidOperationException("Delivery is not pending");

        _state.State.Status = DeliveryStatus.Rejected;
        _state.State.RejectedAt = DateTime.UtcNow;
        _state.State.RejectionReason = command.Reason;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<DeliverySnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<bool> HasDiscrepanciesAsync()
    {
        EnsureInitialized();
        return Task.FromResult(_state.State.HasDiscrepancies);
    }

    private DeliverySnapshot CreateSnapshot()
    {
        return new DeliverySnapshot(
            DeliveryId: _state.State.DeliveryId,
            DeliveryNumber: _state.State.DeliveryNumber,
            SupplierId: _state.State.SupplierId,
            SupplierName: _state.State.SupplierName,
            PurchaseOrderId: _state.State.PurchaseOrderId,
            LocationId: _state.State.LocationId,
            ReceivedByUserId: _state.State.ReceivedByUserId,
            Status: _state.State.Status,
            ReceivedAt: _state.State.ReceivedAt,
            AcceptedAt: _state.State.AcceptedAt,
            RejectedAt: _state.State.RejectedAt,
            RejectionReason: _state.State.RejectionReason,
            TotalValue: _state.State.TotalValue,
            HasDiscrepancies: _state.State.HasDiscrepancies,
            SupplierInvoiceNumber: _state.State.SupplierInvoiceNumber,
            Lines: _state.State.Lines.Select(l => new DeliveryLineSnapshot(
                LineId: l.LineId,
                IngredientId: l.IngredientId,
                IngredientName: l.IngredientName,
                PurchaseOrderLineId: l.PurchaseOrderLineId,
                QuantityReceived: l.QuantityReceived,
                UnitCost: l.UnitCost,
                LineTotal: l.LineTotal,
                BatchNumber: l.BatchNumber,
                ExpiryDate: l.ExpiryDate,
                Notes: l.Notes)).ToList(),
            Discrepancies: _state.State.Discrepancies.Select(d => new DeliveryDiscrepancySnapshot(
                DiscrepancyId: d.DiscrepancyId,
                LineId: d.LineId,
                Type: d.Type,
                ExpectedQuantity: d.ExpectedQuantity,
                ActualQuantity: d.ActualQuantity,
                Notes: d.Notes)).ToList(),
            Notes: _state.State.Notes);
    }

    private void EnsureInitialized()
    {
        if (_state.State.DeliveryId == Guid.Empty)
            throw new InvalidOperationException("Delivery grain not initialized");
    }
}
