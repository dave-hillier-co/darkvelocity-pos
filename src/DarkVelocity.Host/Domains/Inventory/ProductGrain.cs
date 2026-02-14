using DarkVelocity.Host.Events;
using DarkVelocity.Host.State;
using Microsoft.Extensions.Logging;
using Orleans.EventSourcing;
using Orleans.Providers;

namespace DarkVelocity.Host.Grains;

[LogConsistencyProvider(ProviderName = "LogStorage")]
public class ProductGrain : JournaledGrain<ProductState, IProductEvent>, IProductGrain
{
    private readonly ILogger<ProductGrain> _logger;

    public ProductGrain(ILogger<ProductGrain> logger)
    {
        _logger = logger;
    }

    protected override void TransitionState(ProductState state, IProductEvent @event)
    {
        switch (@event)
        {
            case ProductRegistered e:
                state.ProductId = e.ProductId;
                state.OrgId = e.OrgId;
                state.Name = e.Name;
                state.Description = e.Description;
                state.BaseUnit = e.BaseUnit;
                state.Category = e.Category;
                state.Tags = e.Tags;
                state.ShelfLifeDays = e.ShelfLifeDays;
                state.StorageRequirements = e.StorageRequirements;
                state.IsActive = true;
                state.CreatedAt = e.OccurredAt;
                break;

            case ProductUpdated e:
                if (e.Name != null) state.Name = e.Name;
                if (e.Description != null) state.Description = e.Description;
                if (e.Category != null) state.Category = e.Category;
                if (e.Tags != null) state.Tags = e.Tags;
                if (e.ShelfLifeDays.HasValue) state.ShelfLifeDays = e.ShelfLifeDays;
                if (e.StorageRequirements != null) state.StorageRequirements = e.StorageRequirements;
                break;

            case ProductDeactivated:
                state.IsActive = false;
                break;

            case ProductReactivated:
                state.IsActive = true;
                break;

            case ProductAllergensUpdated e:
                state.Allergens = e.Allergens;
                break;
        }
    }

    public async Task<ProductSnapshot> RegisterAsync(RegisterProductCommand command)
    {
        if (State.ProductId != Guid.Empty)
            throw new InvalidOperationException("Product already registered");

        var productId = Guid.Parse(this.GetPrimaryKeyString().Split(':').Last());

        RaiseEvent(new ProductRegistered
        {
            ProductId = productId,
            OrgId = command.OrgId,
            Name = command.Name,
            Description = command.Description,
            BaseUnit = command.BaseUnit,
            Category = command.Category,
            Tags = command.Tags ?? [],
            ShelfLifeDays = command.ShelfLifeDays,
            StorageRequirements = command.StorageRequirements,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        _logger.LogInformation("Product registered: {Name} ({Category})", command.Name, command.Category);
        return ToSnapshot();
    }

    public async Task<ProductSnapshot> UpdateAsync(UpdateProductCommand command)
    {
        EnsureExists();

        RaiseEvent(new ProductUpdated
        {
            ProductId = State.ProductId,
            Name = command.Name,
            Description = command.Description,
            Category = command.Category,
            Tags = command.Tags,
            ShelfLifeDays = command.ShelfLifeDays,
            StorageRequirements = command.StorageRequirements,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();

        return ToSnapshot();
    }

    public async Task DeactivateAsync(string reason)
    {
        EnsureExists();
        if (!State.IsActive)
            throw new InvalidOperationException("Product already deactivated");

        RaiseEvent(new ProductDeactivated
        {
            ProductId = State.ProductId,
            Reason = reason,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task ReactivateAsync()
    {
        EnsureExists();
        if (State.IsActive)
            throw new InvalidOperationException("Product already active");

        RaiseEvent(new ProductReactivated
        {
            ProductId = State.ProductId,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public async Task UpdateAllergensAsync(List<string> allergens)
    {
        EnsureExists();

        RaiseEvent(new ProductAllergensUpdated
        {
            ProductId = State.ProductId,
            Allergens = allergens,
            OccurredAt = DateTime.UtcNow
        });
        await ConfirmEvents();
    }

    public Task<ProductSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(ToSnapshot());
    }

    public Task<bool> ExistsAsync() => Task.FromResult(State.ProductId != Guid.Empty);

    private void EnsureExists()
    {
        if (State.ProductId == Guid.Empty)
            throw new InvalidOperationException("Product not registered");
    }

    private ProductSnapshot ToSnapshot() => new(
        State.ProductId,
        State.OrgId,
        State.Name,
        State.Description,
        State.BaseUnit,
        State.Category,
        State.Tags,
        State.ShelfLifeDays,
        State.StorageRequirements,
        State.Allergens,
        State.IsActive,
        State.CreatedAt);
}
