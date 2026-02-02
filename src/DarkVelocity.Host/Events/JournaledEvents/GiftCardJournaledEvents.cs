namespace DarkVelocity.Host.Events.JournaledEvents;

/// <summary>
/// Base interface for all GiftCard journaled events used in event sourcing.
/// </summary>
public interface IGiftCardJournaledEvent
{
    Guid CardId { get; }
    DateTime OccurredAt { get; }
}

[GenerateSerializer]
public sealed record GiftCardIssuedJournaledEvent : IGiftCardJournaledEvent
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
public sealed record GiftCardActivatedJournaledEvent : IGiftCardJournaledEvent
{
    [Id(0)] public Guid CardId { get; init; }
    [Id(1)] public Guid? ActivatedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GiftCardRedeemedJournaledEvent : IGiftCardJournaledEvent
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
public sealed record GiftCardReloadedJournaledEvent : IGiftCardJournaledEvent
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
public sealed record GiftCardRefundAppliedJournaledEvent : IGiftCardJournaledEvent
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
public sealed record GiftCardSuspendedJournaledEvent : IGiftCardJournaledEvent
{
    [Id(0)] public Guid CardId { get; init; }
    [Id(1)] public string Reason { get; init; } = "";
    [Id(2)] public Guid? SuspendedBy { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GiftCardResumedJournaledEvent : IGiftCardJournaledEvent
{
    [Id(0)] public Guid CardId { get; init; }
    [Id(1)] public Guid? ResumedBy { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GiftCardExpiredJournaledEvent : IGiftCardJournaledEvent
{
    [Id(0)] public Guid CardId { get; init; }
    [Id(1)] public decimal ExpiredBalance { get; init; }
    [Id(2)] public DateTime OccurredAt { get; init; }
}

[GenerateSerializer]
public sealed record GiftCardTransferredJournaledEvent : IGiftCardJournaledEvent
{
    [Id(0)] public Guid CardId { get; init; }
    [Id(1)] public Guid? FromCustomerId { get; init; }
    [Id(2)] public Guid? ToCustomerId { get; init; }
    [Id(3)] public DateTime OccurredAt { get; init; }
}
