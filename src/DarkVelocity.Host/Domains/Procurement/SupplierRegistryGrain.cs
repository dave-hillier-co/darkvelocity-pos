using DarkVelocity.Host.Grains;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

public class SupplierRegistryGrain : Grain, ISupplierRegistryGrain
{
    private readonly IPersistentState<SupplierRegistryState> _state;

    private SupplierRegistryState State => _state.State;

    public SupplierRegistryGrain(
        [PersistentState("supplierRegistry", "OrleansStorage")]
        IPersistentState<SupplierRegistryState> state)
    {
        _state = state;
    }

    public async Task RegisterSupplierAsync(SupplierSnapshot snapshot)
    {
        State.Suppliers[snapshot.SupplierId] = new SupplierRegistryEntry
        {
            SupplierId = snapshot.SupplierId,
            Code = snapshot.Code,
            Name = snapshot.Name,
            ContactEmail = snapshot.ContactEmail,
            PaymentTermsDays = snapshot.PaymentTermsDays,
            LeadTimeDays = snapshot.LeadTimeDays,
            IsActive = snapshot.IsActive,
            LastModified = DateTimeOffset.UtcNow
        };

        await _state.WriteStateAsync();
    }

    public async Task UpdateSupplierAsync(SupplierSnapshot snapshot)
    {
        if (State.Suppliers.TryGetValue(snapshot.SupplierId, out var entry))
        {
            entry.Code = snapshot.Code;
            entry.Name = snapshot.Name;
            entry.ContactEmail = snapshot.ContactEmail;
            entry.PaymentTermsDays = snapshot.PaymentTermsDays;
            entry.LeadTimeDays = snapshot.LeadTimeDays;
            entry.IsActive = snapshot.IsActive;
            entry.LastModified = DateTimeOffset.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task UnregisterSupplierAsync(Guid supplierId)
    {
        State.Suppliers.Remove(supplierId);
        await _state.WriteStateAsync();
    }

    public Task<IReadOnlyList<SupplierRegistryEntry>> GetSuppliersAsync(bool includeInactive = false)
    {
        var suppliers = State.Suppliers.Values
            .Where(s => includeInactive || s.IsActive)
            .OrderBy(s => s.Name)
            .ToList();

        return Task.FromResult<IReadOnlyList<SupplierRegistryEntry>>(suppliers);
    }

    public Task<IReadOnlyList<SupplierRegistryEntry>> SearchSuppliersAsync(string query, int take = 20)
    {
        var normalizedQuery = query.ToLowerInvariant();
        var suppliers = State.Suppliers.Values
            .Where(s => s.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                     || s.Code.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Name)
            .Take(take)
            .ToList();

        return Task.FromResult<IReadOnlyList<SupplierRegistryEntry>>(suppliers);
    }
}
