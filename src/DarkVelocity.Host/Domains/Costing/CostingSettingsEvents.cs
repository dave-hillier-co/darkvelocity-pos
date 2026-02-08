namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all CostingSettings events used in event sourcing.
/// </summary>
public interface ICostingSettingsEvent
{
    Guid SettingsId { get; }
    DateTimeOffset OccurredAt { get; }
}

[GenerateSerializer]
public sealed record CostingSettingsInitialized : ICostingSettingsEvent
{
    [Id(0)] public Guid SettingsId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public Guid LocationId { get; init; }
    [Id(3)] public DateTimeOffset OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record CostingSettingsUpdated : ICostingSettingsEvent
{
    [Id(0)] public Guid SettingsId { get; init; }
    [Id(1)] public decimal? TargetFoodCostPercent { get; init; }
    [Id(2)] public decimal? TargetBeverageCostPercent { get; init; }
    [Id(3)] public decimal? MinimumMarginPercent { get; init; }
    [Id(4)] public decimal? WarningMarginPercent { get; init; }
    [Id(5)] public decimal? PriceChangeAlertThreshold { get; init; }
    [Id(6)] public decimal? CostIncreaseAlertThreshold { get; init; }
    [Id(7)] public bool? AutoRecalculateCosts { get; init; }
    [Id(8)] public bool? AutoCreateSnapshots { get; init; }
    [Id(9)] public int? SnapshotFrequencyDays { get; init; }
    [Id(10)] public DateTimeOffset OccurredAt { get; init; }
}
