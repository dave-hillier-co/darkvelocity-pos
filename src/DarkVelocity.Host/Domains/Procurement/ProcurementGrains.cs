using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Supplier Grain
// ============================================================================

/// <summary>
/// Grain for supplier management.
/// Manages supplier information, SKU catalog, and performance metrics.
/// Uses event sourcing via JournaledGrain for state persistence.
/// </summary>
[LogConsistencyProvider(ProviderName = "LogStorage")]
public class SupplierGrain : JournaledGrain<SupplierState, ISupplierEvent>, ISupplierGrain
{
    private readonly IGrainFactory _grainFactory;

    public SupplierGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    protected override void TransitionState(SupplierState state, ISupplierEvent @event)
    {
        switch (@event)
        {
            case SupplierCreated e:
                state.OrgId = e.OrgId;
                state.SupplierId = e.SupplierId;
                state.Code = e.Code;
                state.Name = e.Name;
                state.ContactName = e.ContactName ?? "";
                state.ContactEmail = e.ContactEmail ?? "";
                state.ContactPhone = e.ContactPhone ?? "";
                state.Address = e.Address ?? "";
                state.PaymentTermsDays = e.PaymentTermsDays;
                state.LeadTimeDays = e.LeadTimeDays;
                state.Notes = e.Notes;
                state.IsActive = true;
                break;

            case SupplierUpdated e:
                if (e.Name != null) state.Name = e.Name;
                if (e.ContactName != null) state.ContactName = e.ContactName;
                if (e.ContactEmail != null) state.ContactEmail = e.ContactEmail;
                if (e.ContactPhone != null) state.ContactPhone = e.ContactPhone;
                if (e.Address != null) state.Address = e.Address;
                if (e.PaymentTermsDays.HasValue) state.PaymentTermsDays = e.PaymentTermsDays.Value;
                if (e.LeadTimeDays.HasValue) state.LeadTimeDays = e.LeadTimeDays.Value;
                if (e.Notes != null) state.Notes = e.Notes;
                if (e.IsActive.HasValue) state.IsActive = e.IsActive.Value;
                break;

            case SupplierSkuAdded e:
                var existingSku = state.Catalog.FirstOrDefault(i => i.SkuId == e.SkuId);
                if (existingSku != null)
                {
                    existingSku.SupplierProductCode = e.SupplierProductCode ?? "";
                    existingSku.UnitPrice = e.UnitPrice;
                    existingSku.Unit = e.Unit;
                    existingSku.MinOrderQuantity = e.MinOrderQuantity ?? 0;
                    existingSku.LeadTimeDays = e.LeadTimeDays ?? 0;
                }
                else
                {
                    state.Catalog.Add(new SupplierCatalogItemState
                    {
                        SkuId = e.SkuId,
                        SkuCode = e.SkuCode,
                        ProductName = e.ProductName,
                        SupplierProductCode = e.SupplierProductCode ?? "",
                        UnitPrice = e.UnitPrice,
                        Unit = e.Unit,
                        MinOrderQuantity = e.MinOrderQuantity ?? 0,
                        LeadTimeDays = e.LeadTimeDays ?? 0
                    });
                }
                break;

            case SupplierSkuRemoved e:
                state.Catalog.RemoveAll(i => i.SkuId == e.SkuId);
                break;

            case SupplierSkuPriceUpdated e:
                var skuToUpdate = state.Catalog.FirstOrDefault(i => i.SkuId == e.SkuId);
                if (skuToUpdate != null)
                {
                    skuToUpdate.UnitPrice = e.NewPrice;
                }
                break;

            case SupplierPurchaseRecorded e:
                state.TotalPurchasesYtd += e.Amount;
                state.TotalDeliveries++;
                if (e.OnTime) state.OnTimeDeliveries++;
                break;
        }
    }

    public async Task<SupplierSnapshot> CreateAsync(CreateSupplierCommand command)
    {
        if (State.SupplierId != Guid.Empty)
            throw new InvalidOperationException("Supplier already exists");

        var key = this.GetPrimaryKeyString();
        var (orgId, _, supplierId) = GrainKeys.ParseOrgEntity(key);

        RaiseEvent(new SupplierCreated
        {
            SupplierId = supplierId,
            OrgId = orgId,
            Code = command.Code,
            Name = command.Name,
            ContactName = command.ContactName,
            ContactEmail = command.ContactEmail,
            ContactPhone = command.ContactPhone,
            Address = command.Address,
            PaymentTermsDays = command.PaymentTermsDays,
            LeadTimeDays = command.LeadTimeDays,
            Notes = command.Notes,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await ConfirmEvents();

        var snapshot = CreateSnapshot();
        await GetRegistry().RegisterSupplierAsync(snapshot);
        return snapshot;
    }

    public async Task<SupplierSnapshot> UpdateAsync(UpdateSupplierCommand command)
    {
        EnsureInitialized();

        RaiseEvent(new SupplierUpdated
        {
            SupplierId = State.SupplierId,
            Name = command.Name,
            ContactName = command.ContactName,
            ContactEmail = command.ContactEmail,
            ContactPhone = command.ContactPhone,
            Address = command.Address,
            PaymentTermsDays = command.PaymentTermsDays,
            LeadTimeDays = command.LeadTimeDays,
            Notes = command.Notes,
            IsActive = command.IsActive,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await ConfirmEvents();

        var snapshot = CreateSnapshot();
        await GetRegistry().UpdateSupplierAsync(snapshot);
        return snapshot;
    }

    public async Task AddSkuAsync(SupplierCatalogItem item)
    {
        EnsureInitialized();

        RaiseEvent(new SupplierSkuAdded
        {
            SupplierId = State.SupplierId,
            SkuId = item.SkuId,
            SkuCode = item.SkuCode,
            ProductName = item.ProductName,
            SupplierProductCode = item.SupplierProductCode,
            UnitPrice = item.UnitPrice,
            Unit = item.Unit,
            MinOrderQuantity = item.MinOrderQuantity,
            LeadTimeDays = item.LeadTimeDays,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task RemoveSkuAsync(Guid skuId)
    {
        EnsureInitialized();

        RaiseEvent(new SupplierSkuRemoved
        {
            SupplierId = State.SupplierId,
            SkuId = skuId,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task UpdateSkuPriceAsync(Guid skuId, decimal newPrice)
    {
        EnsureInitialized();

        var sku = State.Catalog.FirstOrDefault(i => i.SkuId == skuId)
            ?? throw new InvalidOperationException("SKU not found in supplier catalog");

        RaiseEvent(new SupplierSkuPriceUpdated
        {
            SupplierId = State.SupplierId,
            SkuId = skuId,
            NewPrice = newPrice,
            PreviousPrice = sku.UnitPrice,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<SupplierSnapshot> GetSnapshotAsync()
    {
        EnsureInitialized();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<decimal> GetSkuPriceAsync(Guid skuId)
    {
        EnsureInitialized();

        var sku = State.Catalog.FirstOrDefault(i => i.SkuId == skuId)
            ?? throw new InvalidOperationException("SKU not found in supplier catalog");

        return Task.FromResult(sku.UnitPrice);
    }

    public async Task RecordPurchaseAsync(decimal amount, bool onTime)
    {
        EnsureInitialized();

        RaiseEvent(new SupplierPurchaseRecorded
        {
            SupplierId = State.SupplierId,
            Amount = amount,
            OnTime = onTime,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<int> GetVersionAsync() => Task.FromResult(Version);

    private SupplierSnapshot CreateSnapshot()
    {
        var onTimePercent = State.TotalDeliveries > 0
            ? State.OnTimeDeliveries * 100 / State.TotalDeliveries
            : 100;

        return new SupplierSnapshot(
            SupplierId: State.SupplierId,
            Code: State.Code,
            Name: State.Name,
            ContactName: State.ContactName,
            ContactEmail: State.ContactEmail,
            ContactPhone: State.ContactPhone,
            Address: State.Address,
            PaymentTermsDays: State.PaymentTermsDays,
            LeadTimeDays: State.LeadTimeDays,
            Notes: State.Notes,
            IsActive: State.IsActive,
            Catalog: State.Catalog.Select(i => new SupplierCatalogItem(
                SkuId: i.SkuId,
                SkuCode: i.SkuCode,
                ProductName: i.ProductName,
                SupplierProductCode: i.SupplierProductCode,
                UnitPrice: i.UnitPrice,
                Unit: i.Unit,
                MinOrderQuantity: i.MinOrderQuantity,
                LeadTimeDays: i.LeadTimeDays)).ToList(),
            TotalPurchasesYtd: State.TotalPurchasesYtd,
            OnTimeDeliveryPercent: onTimePercent);
    }

    private ISupplierRegistryGrain GetRegistry()
    {
        return _grainFactory.GetGrain<ISupplierRegistryGrain>(
            GrainKeys.SupplierRegistry(State.OrgId));
    }

    private void EnsureInitialized()
    {
        if (State.SupplierId == Guid.Empty)
            throw new InvalidOperationException("Supplier grain not initialized");
    }
}
