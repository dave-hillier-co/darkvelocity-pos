namespace DarkVelocity.Host.Events;

/// <summary>
/// Base interface for all GiftCard events used in event sourcing.
/// </summary>
public interface IGiftCardEvent
{
    Guid CardId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record GiftCardIssued : IGiftCardEvent
{
    [Id(0)] public Guid CardId { get; init; }
    [Id(1)] public Guid OrganizationId { get; init; }
    [Id(2)] public string CardNumber { get; init; } = "";
    [Id(3)] public decimal InitialBalance { get; init; }
    [Id(4)] public Guid? PurchasedByCustomerId { get; init; }
    [Id(5)] public Guid? SiteId { get; init; }
    [Id(6)] public Guid? OrderId { get; init; }
    [Id(7)] public DateOnly? ExpiryDate { get; init; }
    [Id(8)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GiftCardActivated : IGiftCardEvent
{
    [Id(0)] public Guid CardId { get; init; }
    [Id(1)] public Guid? ActivatedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GiftCardRedeemed : IGiftCardEvent
{
    [Id(0)] public Guid CardId { get; init; }
    [Id(1)] public Guid TransactionId { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public decimal NewBalance { get; init; }
    [Id(4)] public Guid OrderId { get; init; }
    [Id(5)] public Guid SiteId { get; init; }
    [Id(6)] public Guid? CustomerId { get; init; }
    [Id(7)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GiftCardReloaded : IGiftCardEvent
{
    [Id(0)] public Guid CardId { get; init; }
    [Id(1)] public Guid TransactionId { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public decimal NewBalance { get; init; }
    [Id(4)] public Guid? SiteId { get; init; }
    [Id(5)] public Guid? OrderId { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GiftCardRefundApplied : IGiftCardEvent
{
    [Id(0)] public Guid CardId { get; init; }
    [Id(1)] public Guid TransactionId { get; init; }
    [Id(2)] public decimal Amount { get; init; }
    [Id(3)] public decimal NewBalance { get; init; }
    [Id(4)] public Guid OriginalOrderId { get; init; }
    [Id(5)] public string? Reason { get; init; }
    [Id(6)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GiftCardSuspended : IGiftCardEvent
{
    [Id(0)] public Guid CardId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public Guid? SuspendedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GiftCardResumed : IGiftCardEvent
{
    [Id(0)] public Guid CardId { get; init; }
    [Id(1)] public Guid? ResumedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GiftCardExpired : IGiftCardEvent
{
    [Id(0)] public Guid CardId { get; init; }
    [Id(1)] public decimal ExpiredBalance { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GiftCardTransferred : IGiftCardEvent
{
    [Id(0)] public Guid CardId { get; init; }
    [Id(1)] public Guid? FromCustomerId { get; init; }
    [Id(2)] public Guid? ToCustomerId { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}
