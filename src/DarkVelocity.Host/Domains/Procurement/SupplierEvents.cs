namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all Supplier events used in event sourcing.
/// </summary>
public interface ISupplierEvent
{
    Guid SupplierId { get; }
    DateTimeOffset OccurredAt { get; }
}

[GenerateSerializer]
public sealed record SupplierCreated : ISupplierEvent
{
    [Id(0)] public Guid SupplierId { get; init; }
    [Id(1)] public Guid OrgId { get; init; }
    [Id(2)] public string Code { get; init; } = "";
    [Id(3)] public string Name { get; init; } = "";
    [Id(4)] public string? ContactName { get; init; }
    [Id(5)] public string? ContactEmail { get; init; }
    [Id(6)] public string? ContactPhone { get; init; }
    [Id(7)] public string? Address { get; init; }
    [Id(8)] public int PaymentTermsDays { get; init; }
    [Id(9)] public int LeadTimeDays { get; init; }
    [Id(10)] public string? Notes { get; init; }
    [Id(11)] public DateTimeOffset OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record SupplierUpdated : ISupplierEvent
{
    [Id(0)] public Guid SupplierId { get; init; }
    [Id(1)] public string? Name { get; init; }
    [Id(2)] public string? ContactName { get; init; }
    [Id(3)] public string? ContactEmail { get; init; }
    [Id(4)] public string? ContactPhone { get; init; }
    [Id(5)] public string? Address { get; init; }
    [Id(6)] public int? PaymentTermsDays { get; init; }
    [Id(7)] public int? LeadTimeDays { get; init; }
    [Id(8)] public string? Notes { get; init; }
    [Id(9)] public bool? IsActive { get; init; }
    [Id(10)] public DateTimeOffset OccurredAt { get; init; }
}

/// <summary>
/// A SKU was added to or updated in the supplier's catalog.
/// </summary>
[GenerateSerializer]
public sealed record SupplierSkuAdded : ISupplierEvent
{
    [Id(0)] public Guid SupplierId { get; init; }
    [Id(1)] public Guid SkuId { get; init; }
    [Id(2)] public string SkuCode { get; init; } = "";
    [Id(3)] public string ProductName { get; init; } = "";
    [Id(4)] public string? SupplierProductCode { get; init; }
    [Id(5)] public decimal UnitPrice { get; init; }
    [Id(6)] public string Unit { get; init; } = "";
    [Id(7)] public int? MinOrderQuantity { get; init; }
    [Id(8)] public int? LeadTimeDays { get; init; }
    [Id(9)] public DateTimeOffset OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record SupplierSkuRemoved : ISupplierEvent
{
    [Id(0)] public Guid SupplierId { get; init; }
    [Id(1)] public Guid SkuId { get; init; }
    [Id(2)] public DateTimeOffset OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record SupplierSkuPriceUpdated : ISupplierEvent
{
    [Id(0)] public Guid SupplierId { get; init; }
    [Id(1)] public Guid SkuId { get; init; }
    [Id(2)] public decimal NewPrice { get; init; }
    [Id(3)] public decimal PreviousPrice { get; init; }
    [Id(4)] public DateTimeOffset OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record SupplierPurchaseRecorded : ISupplierEvent
{
    [Id(0)] public Guid SupplierId { get; init; }
    [Id(1)] public decimal Amount { get; init; }
    [Id(2)] public bool OnTime { get; init; }
    [Id(3)] public DateTimeOffset OccurredAt { get; init; }
}
