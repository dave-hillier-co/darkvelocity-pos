namespace DarkVelocity.Host.State;

/// <summary>
/// Defines the container/packaging hierarchy for a SKU.
/// Recursive: a case of 6 bottles of 75cl = { Unit: "case", Quantity: 6, QuantityUnit: "bottle", Inner: { Unit: "bottle", Quantity: 75, QuantityUnit: "cl" } }
/// </summary>
[GenerateSerializer]
public sealed class ContainerDefinition
{
    /// <summary>
    /// The container unit name (e.g., "case", "keg", "bottle", "bag").
    /// </summary>
    [Id(0)] public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// How many of QuantityUnit this container holds.
    /// </summary>
    [Id(1)] public decimal Quantity { get; set; }

    /// <summary>
    /// The unit of the contents (e.g., "bottle", "L", "kg").
    /// </summary>
    [Id(2)] public string QuantityUnit { get; set; } = string.Empty;

    /// <summary>
    /// Inner container definition for recursive containers.
    /// null for leaf containers (e.g., a bottle of 75cl has no inner container).
    /// </summary>
    [Id(3)] public ContainerDefinition? Inner { get; set; }

    /// <summary>
    /// Resolves the total quantity in the leaf unit.
    /// E.g., case of 6 bottles of 75cl = 6 * 75 = 450cl.
    /// </summary>
    public decimal ResolveToLeafQuantity()
    {
        if (Inner == null)
            return Quantity;
        return Quantity * Inner.ResolveToLeafQuantity();
    }

    /// <summary>
    /// Returns the leaf unit (the innermost QuantityUnit).
    /// E.g., case of 6 bottles of 75cl → "cl".
    /// </summary>
    public string GetLeafUnit()
    {
        if (Inner == null)
            return QuantityUnit;
        return Inner.GetLeafUnit();
    }
}

[GenerateSerializer]
public sealed class SkuState
{
    [Id(0)] public Guid SkuId { get; set; }
    [Id(1)] public Guid OrgId { get; set; }
    [Id(2)] public Guid ProductId { get; set; }
    [Id(3)] public string Code { get; set; } = string.Empty;
    [Id(4)] public string? Barcode { get; set; }
    [Id(5)] public string Description { get; set; } = string.Empty;
    [Id(6)] public ContainerDefinition Container { get; set; } = new();
    [Id(7)] public Guid? DefaultSupplierId { get; set; }
    [Id(8)] public bool IsActive { get; set; } = true;
    [Id(9)] public DateTime CreatedAt { get; set; }
}
