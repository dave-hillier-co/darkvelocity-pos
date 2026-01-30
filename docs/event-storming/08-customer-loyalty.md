# Event Storming: Customer & Loyalty Domain

## Overview

The Customer & Loyalty domain manages customer profiles, loyalty programs, points earning and redemption, tier progression, rewards, and referral systems. This domain helps build lasting customer relationships and encourages repeat business through personalized experiences and rewards.

---

## Domain Purpose

- **Customer Profiles**: Maintain customer information and preferences
- **Loyalty Programs**: Configure and manage loyalty program rules
- **Points Management**: Track earning, redemption, expiration of points
- **Tier System**: Manage customer tiers with benefits
- **Rewards**: Issue and track reward redemptions
- **Referrals**: Handle customer referral programs
- **Analytics**: Provide customer insights and segmentation

---

## Actors

| Actor | Description | Typical Actions |
|-------|-------------|-----------------|
| **Customer** | End customer | Enroll, earn points, redeem rewards |
| **Server/Cashier** | Front-line staff | Look up customers, apply points |
| **Manager** | Venue management | Adjust points, issue rewards |
| **Marketing** | Marketing team | Configure programs, analyze data |
| **System** | Automated processes | Expire points, promote tiers |

---

## Aggregates

### Customer Aggregate

Represents a customer profile.

```
Customer
├── Id: Guid
├── OrgId: Guid
├── Status: CustomerStatus
├── Profile: CustomerProfile
├── ContactInfo: ContactInfo
├── Preferences: CustomerPreferences
├── Tags: List<string>
├── Source: CustomerSource
├── Loyalty: LoyaltyStatus?
├── Stats: CustomerStats
├── Notes: List<CustomerNote>
├── CreatedAt: DateTime
├── LastVisitAt?: DateTime
├── MergedFrom: List<Guid>
└── Metadata: Dictionary<string, string>
```

### CustomerProfile Value Object

```
CustomerProfile
├── FirstName: string
├── LastName: string
├── DisplayName: string
├── DateOfBirth?: DateOnly
├── Anniversary?: DateOnly
├── Gender?: Gender
├── Avatar?: string
└── ExternalIds: Dictionary<string, string>
```

### LoyaltyStatus Entity

```
LoyaltyStatus
├── EnrolledAt: DateTime
├── ProgramId: Guid
├── MemberNumber: string
├── TierId: Guid
├── TierName: string
├── PointsBalance: int
├── LifetimePoints: int
├── YtdPoints: int
├── PointsToNextTier: int
├── TierExpiresAt?: DateTime
├── PointsExpiring: int
├── PointsExpiringAt?: DateTime
└── AvailableRewards: List<AvailableReward>
```

### CustomerStats Value Object

```
CustomerStats
├── TotalVisits: int
├── TotalSpend: decimal
├── AverageCheck: decimal
├── LastVisitSiteId?: Guid
├── FavoriteSiteId?: Guid
├── FavoriteItems: List<FavoriteItem>
├── DaysSinceLastVisit: int
└── Segment: CustomerSegment
```

### LoyaltyProgram Aggregate

Defines the loyalty program rules for an organization.

```
LoyaltyProgram
├── Id: Guid
├── OrgId: Guid
├── Name: string
├── Status: ProgramStatus
├── EarningRules: List<EarningRule>
├── Tiers: List<LoyaltyTier>
├── Rewards: List<RewardDefinition>
├── PointsExpiry: PointsExpiryConfig?
├── ReferralProgram: ReferralConfig?
├── TermsAndConditions: string
├── CreatedAt: DateTime
└── ModifiedAt: DateTime
```

### EarningRule Value Object

```
EarningRule
├── Id: Guid
├── Name: string
├── Type: EarningType
├── PointsPerDollar?: decimal
├── PointsPerVisit?: int
├── BonusMultiplier?: decimal
├── ApplicableDays: List<DayOfWeek>?
├── ApplicableTimes: TimeRange?
├── ApplicableSites: List<Guid>?
├── MinimumSpend?: decimal
└── IsActive: bool
```

### LoyaltyTier Value Object

```
LoyaltyTier
├── Id: Guid
├── Name: string
├── Level: int
├── PointsRequired: int
├── Benefits: List<TierBenefit>
├── EarningMultiplier: decimal
├── MaintainancePoints?: int
├── GracePeriodDays?: int
└── Color: string
```

### RewardDefinition Value Object

```
RewardDefinition
├── Id: Guid
├── Name: string
├── Description: string
├── Type: RewardType
├── PointsCost: int
├── DiscountValue?: decimal
├── DiscountType?: DiscountType
├── FreeItemId?: Guid
├── MinimumTierLevel?: int
├── LimitPerCustomer?: int
├── LimitPeriod?: LimitPeriod
├── ValidDays?: int
├── ImageUrl?: string
└── IsActive: bool
```

### CustomerReward Entity

Issued reward for a customer.

```
CustomerReward
├── Id: Guid
├── CustomerId: Guid
├── RewardDefinitionId: Guid
├── Name: string
├── Status: RewardStatus
├── PointsSpent: int
├── IssuedAt: DateTime
├── ExpiresAt: DateTime
├── RedeemedAt?: DateTime
├── RedemptionOrderId?: Guid
└── RedemptionSiteId?: Guid
```

---

## Tier State Machine

```
┌─────────────┐
│   Bronze    │ (Entry tier)
└──────┬──────┘
       │
       │ Earn points >= Silver threshold
       ▼
┌─────────────┐
│   Silver    │
└──────┬──────┘
       │
       │ Earn points >= Gold threshold
       ▼
┌─────────────┐
│    Gold     │
└──────┬──────┘
       │
       │ Earn points >= Platinum threshold
       ▼
┌─────────────┐
│  Platinum   │
└─────────────┘

Demotion (after qualification period):
If YTD points < tier maintenance threshold:
  → Demote to lower tier (with grace period notification)
```

---

## Commands

### Customer Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `CreateCustomer` | Create new profile | Email/phone unique | Staff, Customer |
| `UpdateCustomer` | Modify profile | Customer exists | Customer, Staff |
| `MergeCustomers` | Combine duplicates | Both exist | Manager |
| `AddCustomerTag` | Tag customer | Customer exists | Staff |
| `RemoveCustomerTag` | Remove tag | Tag exists | Staff |
| `AddCustomerNote` | Add note | Customer exists | Staff |
| `UpdatePreferences` | Change preferences | Customer exists | Customer |
| `DeleteCustomer` | Remove customer (GDPR) | Customer exists | Manager |
| `AnonymizeCustomer` | Anonymize data | Customer exists | System |

### Loyalty Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `EnrollInLoyalty` | Join program | Customer exists, not enrolled | Customer, Staff |
| `EarnPoints` | Credit points | Customer enrolled | System |
| `RedeemPoints` | Spend points | Sufficient balance | Customer |
| `AdjustPoints` | Manual adjustment | Manager approval | Manager |
| `TransferPoints` | Move between customers | Both enrolled | Manager |
| `ExpirePoints` | Remove expired points | Points past expiry | System |
| `PromoteTier` | Upgrade tier | Points threshold met | System |
| `DemoteTier` | Downgrade tier | Maintenance not met | System |
| `IssueReward` | Grant reward | Customer eligible | Staff, System |
| `RedeemReward` | Use reward | Reward available | Customer |
| `ExtendRewardExpiry` | Extend validity | Reward exists | Manager |

### Referral Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `GenerateReferralCode` | Create unique code | Customer enrolled | Customer |
| `ApplyReferralCode` | Use during signup | Code valid | New Customer |
| `CompleteReferral` | Mark referral complete | Referee qualifies | System |
| `AwardReferralBonus` | Credit referrer | Referral complete | System |

### Program Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `CreateProgram` | Set up loyalty program | Org exists | Marketing |
| `UpdateProgram` | Modify rules | Program exists | Marketing |
| `AddEarningRule` | Add earning rule | Program exists | Marketing |
| `UpdateEarningRule` | Modify rule | Rule exists | Marketing |
| `AddTier` | Add tier level | Program exists | Marketing |
| `AddReward` | Add reward option | Program exists | Marketing |
| `ActivateProgram` | Enable program | Program configured | Marketing |
| `DeactivateProgram` | Disable program | Program active | Marketing |

---

## Domain Events

### Customer Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `CustomerCreated` | Profile created | CustomerId, Profile, Source | CreateCustomer |
| `CustomerUpdated` | Profile modified | CustomerId, Changes | UpdateCustomer |
| `CustomersMerged` | Profiles combined | PrimaryId, MergedId | MergeCustomers |
| `CustomerTagAdded` | Tag applied | CustomerId, Tag | AddCustomerTag |
| `CustomerTagRemoved` | Tag removed | CustomerId, Tag | RemoveCustomerTag |
| `CustomerNoteAdded` | Note recorded | CustomerId, Note | AddCustomerNote |
| `CustomerPreferencesUpdated` | Preferences changed | CustomerId, Preferences | UpdatePreferences |
| `CustomerDeleted` | Profile removed | CustomerId, Reason | DeleteCustomer |
| `CustomerAnonymized` | Data anonymized | CustomerId | AnonymizeCustomer |
| `VisitRecorded` | Visit tracked | CustomerId, SiteId, OrderId, Spend | Order settlement |

### Loyalty Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `LoyaltyEnrolled` | Joined program | CustomerId, ProgramId, MemberNumber | EnrollInLoyalty |
| `PointsEarned` | Points credited | CustomerId, Points, OrderId, Reason | EarnPoints |
| `PointsRedeemed` | Points spent | CustomerId, Points, OrderId | RedeemPoints |
| `PointsAdjusted` | Manual change | CustomerId, Points, Reason, AdjustedBy | AdjustPoints |
| `PointsTransferred` | Moved points | FromCustomerId, ToCustomerId, Points | TransferPoints |
| `PointsExpired` | Points removed | CustomerId, Points, ExpiryDate | ExpirePoints |
| `TierPromoted` | Upgraded tier | CustomerId, OldTier, NewTier | PromoteTier |
| `TierDemoted` | Downgraded tier | CustomerId, OldTier, NewTier | DemoteTier |
| `RewardIssued` | Reward granted | CustomerId, RewardId, PointsSpent | IssueReward |
| `RewardRedeemed` | Reward used | CustomerId, RewardId, OrderId | RedeemReward |
| `RewardExpired` | Reward lapsed | CustomerId, RewardId | System |
| `RewardExpiryExtended` | Validity extended | CustomerId, RewardId, NewExpiry | ExtendRewardExpiry |

### Referral Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `ReferralCodeGenerated` | Code created | CustomerId, Code, ExpiresAt | GenerateReferralCode |
| `ReferralCodeApplied` | Code used | ReferrerId, RefereeId, Code | ApplyReferralCode |
| `ReferralCompleted` | Referral qualified | ReferrerId, RefereeId, QualifyingOrderId | CompleteReferral |
| `ReferralBonusAwarded` | Referrer credited | CustomerId, Points, RefereeId | AwardReferralBonus |

### Program Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `ProgramCreated` | Program set up | ProgramId, OrgId, Name | CreateProgram |
| `ProgramUpdated` | Rules modified | ProgramId, Changes | UpdateProgram |
| `EarningRuleAdded` | Rule added | ProgramId, RuleId, Details | AddEarningRule |
| `TierAdded` | Tier created | ProgramId, TierId, Name, Threshold | AddTier |
| `RewardAdded` | Reward created | ProgramId, RewardId, Name, Cost | AddReward |
| `ProgramActivated` | Program enabled | ProgramId | ActivateProgram |
| `ProgramDeactivated` | Program disabled | ProgramId | DeactivateProgram |

---

## Event Details

### PointsEarned

```csharp
public record PointsEarned : DomainEvent
{
    public override string EventType => "customers.loyalty.points_earned";

    public required Guid CustomerId { get; init; }
    public required Guid ProgramId { get; init; }
    public required int PointsEarned { get; init; }
    public required int NewBalance { get; init; }
    public required string Reason { get; init; }
    public required EarningSource Source { get; init; }
    public Guid? OrderId { get; init; }
    public Guid? SiteId { get; init; }
    public decimal? SpendAmount { get; init; }
    public decimal? Multiplier { get; init; }
    public required DateTime EarnedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

public enum EarningSource
{
    Purchase,
    Visit,
    BonusPromotion,
    SignupBonus,
    BirthdayBonus,
    ReferralBonus,
    ManualAward,
    TierBonus,
    CampaignReward
}
```

### TierPromoted

```csharp
public record TierPromoted : DomainEvent
{
    public override string EventType => "customers.loyalty.tier_promoted";

    public required Guid CustomerId { get; init; }
    public required Guid ProgramId { get; init; }
    public required Guid OldTierId { get; init; }
    public required string OldTierName { get; init; }
    public required Guid NewTierId { get; init; }
    public required string NewTierName { get; init; }
    public required int LifetimePoints { get; init; }
    public required IReadOnlyList<TierBenefit> NewBenefits { get; init; }
    public required DateTime PromotedAt { get; init; }
}

public record TierBenefit
{
    public string Name { get; init; }
    public string Description { get; init; }
    public BenefitType Type { get; init; }
    public decimal? Value { get; init; }
}

public enum BenefitType
{
    PointsMultiplier,
    PercentDiscount,
    FreeItem,
    PriorityBooking,
    FreeDelivery,
    ExclusiveAccess,
    BirthdayReward,
    Custom
}
```

### RewardRedeemed

```csharp
public record RewardRedeemed : DomainEvent
{
    public override string EventType => "customers.loyalty.reward_redeemed";

    public required Guid CustomerId { get; init; }
    public required Guid RewardId { get; init; }
    public required string RewardName { get; init; }
    public required RewardType Type { get; init; }
    public required Guid OrderId { get; init; }
    public required Guid SiteId { get; init; }
    public decimal? DiscountApplied { get; init; }
    public Guid? FreeItemId { get; init; }
    public required DateTime RedeemedAt { get; init; }
}

public enum RewardType
{
    PercentDiscount,
    FixedDiscount,
    FreeItem,
    BuyOneGetOne,
    FreeUpgrade,
    FreeDelivery,
    Custom
}
```

---

## Policies (Event Reactions)

### When PointsEarned

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Check Tier Promotion | Evaluate tier threshold | Loyalty |
| Update Customer Stats | Increment lifetime points | Customer |
| Send Notification | Points earned confirmation | Notifications |
| Track for Analytics | Record earning pattern | Reporting |

### When TierPromoted

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Send Congratulations | Tier upgrade notification | Notifications |
| Issue Tier Reward | Grant tier-up reward if configured | Rewards |
| Update Benefits | Apply new earning multiplier | Loyalty |
| Log Achievement | Record for analytics | Reporting |

### When RewardRedeemed

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Apply to Order | Discount the order | Orders |
| Mark Reward Used | Update reward status | Loyalty |
| Track Redemption | Record for analytics | Reporting |
| Check for Reissue | If limited, update count | Loyalty |

### When OrderSettled (from Order domain)

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Calculate Points | Apply earning rules | Loyalty |
| Award Points | Credit to customer | Loyalty |
| Update Visit Count | Increment stats | Customer |
| Check Referral | If new customer, complete referral | Referrals |

### When PointsExpired

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Notify Customer | Expiration notice | Notifications |
| Update Balance | Deduct expired points | Loyalty |
| Log Expiration | Record for reporting | Reporting |

---

## Read Models / Projections

### CustomerProfileView

```csharp
public record CustomerProfileView
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; }
    public string FirstName { get; init; }
    public string LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? AvatarUrl { get; init; }
    public CustomerStatus Status { get; init; }
    public IReadOnlyList<string> Tags { get; init; }

    // Loyalty Summary
    public bool IsEnrolled { get; init; }
    public string? MemberNumber { get; init; }
    public string? TierName { get; init; }
    public string? TierColor { get; init; }
    public int PointsBalance { get; init; }
    public int LifetimePoints { get; init; }
    public int AvailableRewardsCount { get; init; }

    // Visit Summary
    public int TotalVisits { get; init; }
    public decimal TotalSpend { get; init; }
    public decimal AverageCheck { get; init; }
    public DateTime? LastVisitAt { get; init; }
    public int DaysSinceLastVisit { get; init; }
    public CustomerSegment Segment { get; init; }
}
```

### LoyaltyStatusView

```csharp
public record LoyaltyStatusView
{
    public Guid CustomerId { get; init; }
    public string MemberNumber { get; init; }
    public DateTime EnrolledAt { get; init; }

    // Current Tier
    public string TierName { get; init; }
    public int TierLevel { get; init; }
    public string TierColor { get; init; }
    public decimal EarningMultiplier { get; init; }
    public IReadOnlyList<TierBenefit> Benefits { get; init; }

    // Points
    public int PointsBalance { get; init; }
    public int LifetimePoints { get; init; }
    public int YtdPoints { get; init; }
    public int PointsToNextTier { get; init; }
    public string? NextTierName { get; init; }
    public int PointsExpiringSoon { get; init; }
    public DateTime? NextExpiryDate { get; init; }

    // Available Rewards
    public IReadOnlyList<AvailableRewardView> AvailableRewards { get; init; }

    // Referral
    public string? ReferralCode { get; init; }
    public int SuccessfulReferrals { get; init; }
}

public record AvailableRewardView
{
    public Guid RewardId { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }
    public RewardType Type { get; init; }
    public int PointsCost { get; init; }
    public bool CanAfford { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public string? ImageUrl { get; init; }
}
```

### CustomerSearchResult

```csharp
public record CustomerSearchResult
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public bool IsLoyaltyMember { get; init; }
    public string? TierName { get; init; }
    public int? PointsBalance { get; init; }
    public DateTime? LastVisitAt { get; init; }
    public decimal TotalSpend { get; init; }
    public IReadOnlyList<string> Tags { get; init; }
}
```

### CustomerSegmentationView

```csharp
public record CustomerSegmentationView
{
    public Guid OrgId { get; init; }
    public DateTime AsOf { get; init; }
    public int TotalCustomers { get; init; }
    public int LoyaltyMembers { get; init; }

    // By Segment
    public IReadOnlyDictionary<CustomerSegment, int> BySegment { get; init; }

    // By Tier
    public IReadOnlyDictionary<string, int> ByTier { get; init; }

    // By Recency
    public int ActiveLast30Days { get; init; }
    public int ActiveLast90Days { get; init; }
    public int Lapsed { get; init; }
    public int AtRisk { get; init; }

    // Value Distribution
    public decimal AverageLifetimeValue { get; init; }
    public decimal Top10PercentValue { get; init; }
    public decimal MedianValue { get; init; }
}

public enum CustomerSegment
{
    New,           // First 30 days
    Regular,       // 2+ visits, active
    Loyal,         // High frequency
    Champion,      // Top 10% spend
    AtRisk,        // Was active, declining
    Lapsed,        // No visit > 90 days
    Lost           // No visit > 180 days
}
```

### PointsLedgerView

```csharp
public record PointsLedgerView
{
    public Guid CustomerId { get; init; }
    public int CurrentBalance { get; init; }
    public IReadOnlyList<PointsTransaction> Transactions { get; init; }
}

public record PointsTransaction
{
    public Guid TransactionId { get; init; }
    public DateTime Timestamp { get; init; }
    public PointsTransactionType Type { get; init; }
    public int Points { get; init; }
    public int RunningBalance { get; init; }
    public string Description { get; init; }
    public Guid? OrderId { get; init; }
    public string? SiteName { get; init; }
    public DateTime? ExpiresAt { get; init; }
}

public enum PointsTransactionType
{
    Earned,
    Redeemed,
    Expired,
    Adjusted,
    TransferIn,
    TransferOut,
    Bonus
}
```

---

## Bounded Context Relationships

### Upstream Contexts (This domain provides data to)

| Context | Relationship | Data Provided |
|---------|--------------|---------------|
| Orders | Published Language | Customer info, discounts |
| Booking | Published Language | Customer preferences, VIP status |
| Reporting | Published Language | Customer analytics |
| Marketing | Published Language | Segment data, campaign targets |

### Downstream Contexts (This domain consumes from)

| Context | Relationship | Data Consumed |
|---------|--------------|---------------|
| Orders | Customer/Supplier | Order totals for points |
| Payments | Customer/Supplier | Payment for points earning |
| Site | Customer/Supplier | Site info for visit tracking |

---

## Process Flows

### Points Earning Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│ Order Grain │   │ Customer    │   │   Loyalty   │   │ Notification│
│  (Settled)  │   │   Grain     │   │   Program   │   │             │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ OrderSettled    │                 │                 │
       │ with CustomerId │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ Get Earning Rules                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ Rules + Multiplier                │
       │                 │<────────────────│                 │
       │                 │                 │                 │
       │                 │ Calculate Points│                 │
       │                 │────┐            │                 │
       │                 │<───┘            │                 │
       │                 │                 │                 │
       │                 │ EarnPoints      │                 │
       │                 │────┐            │                 │
       │                 │<───┘            │                 │
       │                 │                 │                 │
       │                 │ PointsEarned    │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │                 │ Check Tier      │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ (if promoted)   │                 │
       │                 │ TierPromoted    │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
```

### Reward Redemption Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│   Cashier   │   │ Customer    │   │ Order Grain │
│             │   │   Grain     │   │             │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │
       │ Customer Lookup │                 │
       │────────────────>│                 │
       │                 │                 │
       │ Available Rewards                 │
       │<────────────────│                 │
       │                 │                 │
       │ Redeem Reward   │                 │
       │────────────────>│                 │
       │                 │                 │
       │                 │ Validate Reward │
       │                 │────┐            │
       │                 │<───┘            │
       │                 │                 │
       │                 │ ApplyDiscount   │
       │                 │────────────────>│
       │                 │                 │
       │                 │ RewardRedeemed  │
       │                 │────┐            │
       │                 │<───┘            │
       │                 │                 │
       │ Discount Applied│                 │
       │<────────────────│                 │
       │                 │                 │
```

---

## Business Rules

### Points Rules

1. **Earning Rate**: Configurable points per dollar (e.g., 1 point per $1)
2. **Rounding**: Points rounded down to nearest integer
3. **Tax Exclusion**: Points earned on subtotal, not tax/tip
4. **Tier Multiplier**: Higher tiers earn more points per dollar
5. **Expiration**: Points expire after configurable period (e.g., 12 months)

### Tier Rules

1. **Qualification Period**: Based on calendar year or rolling 12 months
2. **Promotion Threshold**: Points required to reach each tier
3. **Immediate Promotion**: Promoted as soon as threshold met
4. **Maintenance Requirement**: Points needed to maintain tier
5. **Grace Period**: Time to meet maintenance before demotion

### Reward Rules

1. **Points Cost**: Fixed points required for each reward
2. **Tier Restrictions**: Some rewards only for higher tiers
3. **Usage Limits**: Max redemptions per customer per period
4. **Validity Period**: Rewards expire after issuance
5. **Non-Transferable**: Rewards cannot be transferred

---

## Event Type Registry

```csharp
public static class CustomerEventTypes
{
    // Customer Profile
    public const string CustomerCreated = "customers.customer.created";
    public const string CustomerUpdated = "customers.customer.updated";
    public const string CustomersMerged = "customers.customer.merged";
    public const string CustomerTagAdded = "customers.customer.tag_added";
    public const string CustomerTagRemoved = "customers.customer.tag_removed";
    public const string CustomerNoteAdded = "customers.customer.note_added";
    public const string CustomerPreferencesUpdated = "customers.customer.preferences_updated";
    public const string CustomerDeleted = "customers.customer.deleted";
    public const string CustomerAnonymized = "customers.customer.anonymized";
    public const string VisitRecorded = "customers.customer.visit_recorded";

    // Loyalty
    public const string LoyaltyEnrolled = "customers.loyalty.enrolled";
    public const string PointsEarned = "customers.loyalty.points_earned";
    public const string PointsRedeemed = "customers.loyalty.points_redeemed";
    public const string PointsAdjusted = "customers.loyalty.points_adjusted";
    public const string PointsTransferred = "customers.loyalty.points_transferred";
    public const string PointsExpired = "customers.loyalty.points_expired";
    public const string TierPromoted = "customers.loyalty.tier_promoted";
    public const string TierDemoted = "customers.loyalty.tier_demoted";
    public const string RewardIssued = "customers.loyalty.reward_issued";
    public const string RewardRedeemed = "customers.loyalty.reward_redeemed";
    public const string RewardExpired = "customers.loyalty.reward_expired";
    public const string RewardExpiryExtended = "customers.loyalty.reward_expiry_extended";

    // Referrals
    public const string ReferralCodeGenerated = "customers.referral.code_generated";
    public const string ReferralCodeApplied = "customers.referral.code_applied";
    public const string ReferralCompleted = "customers.referral.completed";
    public const string ReferralBonusAwarded = "customers.referral.bonus_awarded";

    // Program
    public const string ProgramCreated = "customers.program.created";
    public const string ProgramUpdated = "customers.program.updated";
    public const string EarningRuleAdded = "customers.program.earning_rule_added";
    public const string TierAdded = "customers.program.tier_added";
    public const string RewardAdded = "customers.program.reward_added";
    public const string ProgramActivated = "customers.program.activated";
    public const string ProgramDeactivated = "customers.program.deactivated";
}
```
