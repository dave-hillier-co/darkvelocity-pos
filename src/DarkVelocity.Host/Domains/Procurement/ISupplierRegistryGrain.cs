namespace DarkVelocity.Host.Grains;

[GenerateSerializer]
public sealed class SupplierRegistryEntry
{
    [Id(0)] public Guid SupplierId { get; set; }
    [Id(1)] public string Code { get; set; } = string.Empty;
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string ContactEmail { get; set; } = string.Empty;
    [Id(4)] public int PaymentTermsDays { get; set; }
    [Id(5)] public int LeadTimeDays { get; set; }
    [Id(6)] public bool IsActive { get; set; } = true;
    [Id(7)] public DateTimeOffset LastModified { get; set; } = DateTimeOffset.UtcNow;
}

[GenerateSerializer]
public sealed class SupplierRegistryState
{
    [Id(0)] public Guid OrgId { get; set; }
    [Id(1)] public bool IsCreated { get; set; }
    [Id(2)] public Dictionary<Guid, SupplierRegistryEntry> Suppliers { get; set; } = [];
}

/// <summary>
/// Registry grain for listing and searching suppliers within an organization.
/// Key: "{orgId}:supplierregistry"
/// </summary>
public interface ISupplierRegistryGrain : IGrainWithStringKey
{
    Task RegisterSupplierAsync(SupplierSnapshot snapshot);
    Task UpdateSupplierAsync(SupplierSnapshot snapshot);
    Task UnregisterSupplierAsync(Guid supplierId);
    Task<IReadOnlyList<SupplierRegistryEntry>> GetSuppliersAsync(bool includeInactive = false);
    Task<IReadOnlyList<SupplierRegistryEntry>> SearchSuppliersAsync(string query, int take = 20);
}
