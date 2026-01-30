namespace DarkVelocity.Shared.Contracts.Events;

/// <summary>
/// Published when a new gift card is issued (created but not yet activated)
/// </summary>
public sealed record GiftCardIssued(
    Guid CardId,
    Guid TenantId,
    Guid LocationId,
    Guid ProgramId,
    string CardNumber,
    decimal InitialBalance,
    string CurrencyCode,
    string CardType,
    DateTime? ExpiryDate,
    DateTime IssuedAt,
    Guid? IssuedByUserId
) : IntegrationEvent
{
    public override string EventType => "giftcards.card.issued";
}

/// <summary>
/// Published when a gift card is activated and ready for use
/// </summary>
public sealed record GiftCardActivated(
    Guid CardId,
    Guid TenantId,
    Guid LocationId,
    string CardNumber,
    decimal Balance,
    string CurrencyCode,
    DateTime ActivatedAt,
    Guid? ActivatedByUserId
) : IntegrationEvent
{
    public override string EventType => "giftcards.card.activated";
}

/// <summary>
/// Published when a gift card balance is used for payment
/// </summary>
public sealed record GiftCardRedeemed(
    Guid CardId,
    Guid TenantId,
    Guid LocationId,
    Guid TransactionId,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    Guid? OrderId,
    Guid? PaymentId,
    DateTime RedeemedAt,
    Guid? UserId
) : IntegrationEvent
{
    public override string EventType => "giftcards.card.redeemed";
}

/// <summary>
/// Published when additional value is added to a gift card
/// </summary>
public sealed record GiftCardReloaded(
    Guid CardId,
    Guid TenantId,
    Guid LocationId,
    Guid TransactionId,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    DateTime ReloadedAt,
    Guid? UserId
) : IntegrationEvent
{
    public override string EventType => "giftcards.card.reloaded";
}

/// <summary>
/// Published when a gift card expires
/// </summary>
public sealed record GiftCardExpired(
    Guid CardId,
    Guid TenantId,
    Guid LocationId,
    Guid ProgramId,
    decimal FinalBalance,
    string CurrencyCode,
    DateTime ExpiredAt
) : IntegrationEvent
{
    public override string EventType => "giftcards.card.expired";
}

/// <summary>
/// Published when a gift card balance is manually adjusted
/// </summary>
public sealed record GiftCardBalanceAdjusted(
    Guid CardId,
    Guid TenantId,
    Guid LocationId,
    Guid TransactionId,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string Reason,
    DateTime AdjustedAt,
    Guid? UserId
) : IntegrationEvent
{
    public override string EventType => "giftcards.card.adjusted";
}

/// <summary>
/// Published when a refund is applied to a gift card
/// </summary>
public sealed record GiftCardRefunded(
    Guid CardId,
    Guid TenantId,
    Guid LocationId,
    Guid TransactionId,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    Guid? OrderId,
    string? Reason,
    DateTime RefundedAt,
    Guid? UserId
) : IntegrationEvent
{
    public override string EventType => "giftcards.card.refunded";
}

/// <summary>
/// Published when a gift card is suspended
/// </summary>
public sealed record GiftCardSuspended(
    Guid CardId,
    Guid TenantId,
    string CardNumber,
    string Reason,
    DateTime SuspendedAt,
    Guid? SuspendedByUserId
) : IntegrationEvent
{
    public override string EventType => "giftcards.card.suspended";
}

/// <summary>
/// Published when a gift card suspension is lifted
/// </summary>
public sealed record GiftCardResumed(
    Guid CardId,
    Guid TenantId,
    string CardNumber,
    decimal CurrentBalance,
    DateTime ResumedAt,
    Guid? ResumedByUserId
) : IntegrationEvent
{
    public override string EventType => "giftcards.card.resumed";
}

/// <summary>
/// Published when a gift card is fully depleted (balance reaches zero)
/// </summary>
public sealed record GiftCardDepleted(
    Guid CardId,
    Guid TenantId,
    Guid LocationId,
    Guid ProgramId,
    DateTime DepletedAt
) : IntegrationEvent
{
    public override string EventType => "giftcards.card.depleted";
}

/// <summary>
/// Published when a new gift card program is created
/// </summary>
public sealed record GiftCardProgramCreated(
    Guid ProgramId,
    Guid TenantId,
    string Name,
    string CurrencyCode,
    DateTime CreatedAt
) : IntegrationEvent
{
    public override string EventType => "giftcards.program.created";
}

/// <summary>
/// Published when a gift card program is deactivated
/// </summary>
public sealed record GiftCardProgramDeactivated(
    Guid ProgramId,
    Guid TenantId,
    string Name,
    DateTime DeactivatedAt
) : IntegrationEvent
{
    public override string EventType => "giftcards.program.deactivated";
}
