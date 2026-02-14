using DarkVelocity.Host.State;

namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all SKU events used in event sourcing.
/// </summary>
public interface ISkuEvent
{
    Guid SkuId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record SkuRegistered : ISkuEvent
{
    [Id(0)] public Guid SkuId { get; init; }
    [Id(1)] public Guid OrgId { get; init; }
    [Id(2)] public Guid ProductId { get; init; }
    [Id(3)] public string Code { get; init; } = "";
    [Id(4)] public string Description { get; init; } = "";
    [Id(5)] public ContainerDefinition Container { get; init; } = null!;
    [Id(6)] public string? Barcode { get; init; }
    [Id(7)] public Guid? DefaultSupplierId { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record SkuUpdated : ISkuEvent
{
    [Id(0)] public Guid SkuId { get; init; }
    [Id(1)] public string? Code { get; init; }
    [Id(2)] public string? Description { get; init; }
    [Id(3)] public ContainerDefinition? Container { get; init; }
    [Id(4)] public Guid? DefaultSupplierId { get; init; }
    [Id(5)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record SkuDeactivated : ISkuEvent
{
    [Id(0)] public Guid SkuId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record SkuBarcodeAssigned : ISkuEvent
{
    [Id(0)] public Guid SkuId { get; init; }
    [Id(1)] public string Barcode { get; init; } = "";
    [Id(2)] public DateTime OccurredAt { get; init; }
}
