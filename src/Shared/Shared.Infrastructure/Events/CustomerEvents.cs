namespace DarkVelocity.Shared.Infrastructure.Events;

public sealed record CustomerCreated(
    Guid CustomerId,
    Guid TenantId,
    string Email,
    string FirstName,
    string LastName,
    string Source
) : IntegrationEvent
{
    public override string EventType => "customers.customer.created";
}

public sealed record CustomerUpdated(
    Guid CustomerId,
    Guid TenantId,
    List<string> ChangedFields
) : IntegrationEvent
{
    public override string EventType => "customers.customer.updated";
}

public sealed record CustomerDeleted(
    Guid CustomerId,
    Guid TenantId
) : IntegrationEvent
{
    public override string EventType => "customers.customer.deleted";
}

public sealed record CustomerEnrolledInLoyalty(
    Guid CustomerId,
    Guid TenantId,
    Guid ProgramId,
    string ProgramName,
    int WelcomeBonus
) : IntegrationEvent
{
    public override string EventType => "customers.loyalty.enrolled";
}

public sealed record PointsEarned(
    Guid CustomerId,
    Guid TenantId,
    Guid ProgramId,
    int Points,
    int NewBalance,
    Guid? OrderId,
    Guid? LocationId
) : IntegrationEvent
{
    public override string EventType => "customers.points.earned";
}

public sealed record PointsRedeemed(
    Guid CustomerId,
    Guid TenantId,
    Guid ProgramId,
    int Points,
    int NewBalance,
    Guid? RewardId
) : IntegrationEvent
{
    public override string EventType => "customers.points.redeemed";
}

public sealed record TierChanged(
    Guid CustomerId,
    Guid TenantId,
    Guid ProgramId,
    string? OldTierName,
    string NewTierName,
    string Reason
) : IntegrationEvent
{
    public override string EventType => "customers.tier.changed";
}

public sealed record RewardIssued(
    Guid CustomerId,
    Guid TenantId,
    Guid RewardId,
    string RewardName,
    string Code,
    DateTime? ExpiresAt
) : IntegrationEvent
{
    public override string EventType => "customers.reward.issued";
}

public sealed record RewardRedeemed(
    Guid CustomerId,
    Guid TenantId,
    Guid CustomerRewardId,
    Guid RewardId,
    string RewardName,
    Guid? OrderId,
    Guid? LocationId
) : IntegrationEvent
{
    public override string EventType => "customers.reward.redeemed";
}

public sealed record ReferralCompleted(
    Guid ReferrerId,
    Guid RefereeId,
    Guid TenantId,
    int ReferrerBonusPoints,
    int RefereeBonusPoints
) : IntegrationEvent
{
    public override string EventType => "customers.referral.completed";
}
