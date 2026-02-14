using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class SkuGrain : JournaledGrain<SkuState, ISkuEvent>, ISkuGrain
{
    private readonly ILogger<SkuGrain> _logger;

    public SkuGrain(ILogger<SkuGrain> logger)
    {
        _logger = logger;
    }

    protected override void TransitionState(SkuState state, ISkuEvent @event)
    {
        switch (@event)
        {
            case SkuRegistered e:
                state.SkuId = e.SkuId;
                state.OrgId = e.OrgId;
                state.ProductId = e.ProductId;
                state.Code = e.Code;
                state.Description = e.Description;
                state.Container = e.Container;
                state.Barcode = e.Barcode;
                state.DefaultSupplierId = e.DefaultSupplierId;
                state.IsActive = true;
                state.CreatedAt = e.OccurredAt;
                break;

            case SkuUpdated e:
                if (e.Code != null) state.Code = e.Code;
                if (e.Description != null) state.Description = e.Description;
                if (e.Container != null) state.Container = e.Container;
                if (e.DefaultSupplierId.HasValue) state.DefaultSupplierId = e.DefaultSupplierId;
                break;

            case SkuDeactivated:
                state.IsActive = false;
                break;

            case SkuBarcodeAssigned e:
                state.Barcode = e.Barcode;
                break;
        }
    }

    public async Task<SkuSnapshot> RegisterAsync(RegisterSkuCommand command)
    {
        if (State.SkuId != Guid.Empty)
            throw new InvalidOperationException("SKU already registered");

        var skuId = Guid.Parse(this.GetPrimaryKeyString().Split(':').Last());

        RaiseEvent(new SkuRegistered
        {
            SkuId = skuId,
            OrgId = command.OrgId,
            ProductId = command.ProductId,
            Code = command.Code,
            Description = command.Description,
            Container = command.Container,
            Barcode = command.Barcode,
            DefaultSupplierId = command.DefaultSupplierId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogInformation("SKU registered: {Code} - {Description}", command.Code, command.Description);
        return ToSnapshot();
    }

    public async Task<SkuSnapshot> UpdateAsync(UpdateSkuCommand command)
    {
        EnsureExists();

        RaiseEvent(new SkuUpdated
        {
            SkuId = State.SkuId,
            Code = command.Code,
            Description = command.Description,
            Container = command.Container,
            DefaultSupplierId = command.DefaultSupplierId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        return ToSnapshot();
    }

    public async Task DeactivateAsync(string reason)
    {
        EnsureExists();
        if (!State.IsActive)
            throw new InvalidOperationException("SKU already deactivated");

        RaiseEvent(new SkuDeactivated
        {
            SkuId = State.SkuId,
            Reason = reason,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task AssignBarcodeAsync(string barcode)
    {
        EnsureExists();

        RaiseEvent(new SkuBarcodeAssigned
        {
            SkuId = State.SkuId,
            Barcode = barcode,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<SkuSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(ToSnapshot());
    }

    public Task<decimal> GetBaseUnitQuantityAsync()
    {
        EnsureExists();
        return Task.FromResult(State.Container.ResolveToLeafQuantity());
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.SkuId != Guid.Empty);

    private void EnsureExists()
    {
        if (State.SkuId == Guid.Empty)
            throw new InvalidOperationException("SKU not registered");
    }

    private SkuSnapshot ToSnapshot() => new(
        State.SkuId,
        State.OrgId,
        State.ProductId,
        State.Code,
        State.Barcode,
        State.Description,
        State.Container,
        State.DefaultSupplierId,
        State.IsActive,
        State.CreatedAt,
        State.Container.ResolveToLeafQuantity(),
        State.Container.GetLeafUnit());
}
