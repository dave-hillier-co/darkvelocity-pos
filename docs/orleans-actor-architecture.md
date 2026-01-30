# Orleans Actor Architecture Design

This document outlines a proposed redesign of DarkVelocity POS using Microsoft Orleans virtual actor framework. The goal is to leverage Orleans' automatic distribution, single-writer consistency, and event-driven patterns while maintaining the rich domain model.

## Table of Contents

1. [Design Principles](#design-principles)
2. [Multi-Tenancy & Core Domain Model](#multi-tenancy--core-domain-model)
3. [Authorization Model (SpiceDB)](#authorization-model-spicedb)
4. [Event Storming](#event-storming)
5. [Grain Architecture](#grain-architecture)
6. [Grain Interface Definitions](#grain-interface-definitions)
7. [Event Flows & Sagas](#event-flows--sagas)
8. [HTTP API Layer](#http-api-layer)
9. [Deployment & Operations](#deployment--operations)
10. [Migration Strategy](#migration-strategy)

---

## Design Principles

### Core Tenets

1. **Grains as Aggregates**: Each grain represents a domain aggregate with its own identity and lifecycle
2. **Event Sourcing**: Grains persist events as the source of truth, rebuilding state from event streams
3. **Single Writer Principle**: One grain owns writes for its aggregate - no distributed transactions needed
4. **Eventual Consistency**: Cross-aggregate coordination happens via events, not locks
5. **Location Transparency**: Orleans handles grain placement; code doesn't know/care where grains live
6. **Tenant Isolation**: Strong boundaries between organizations with no data leakage

### Why Orleans?

| Concern | Current Microservices | Orleans Actors |
|---------|----------------------|----------------|
| **Scaling** | Manual service scaling, load balancers | Automatic grain distribution across silos |
| **State Management** | Database round-trip per request | In-memory with async persistence |
| **Consistency** | Distributed transactions or eventual | Single-writer guarantees per aggregate |
| **Communication** | HTTP + Kafka between 18 services | Direct grain-to-grain method calls |
| **Deployment** | 18 services to deploy/monitor | Single silo cluster |
| **Real-time** | SignalR + polling + Kafka | Native Orleans Streams |
| **Failure Handling** | Circuit breakers, retries | Automatic grain reactivation |
| **Hot Data** | Cache layers (Redis) | Grains ARE the cache |

---

## Multi-Tenancy & Core Domain Model

### Hierarchy

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                              PLATFORM                                        │
│  (DarkVelocity SaaS - manages all organizations)                            │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┼───────────────┐
                    ▼               ▼               ▼
┌─────────────────────────┐ ┌─────────────────────────┐ ┌─────────────────────────┐
│     ORGANIZATION        │ │     ORGANIZATION        │ │     ORGANIZATION        │
│  (Tenant / Account)     │ │  "Burger Palace Inc"    │ │  "Fine Dining Group"    │
│                         │ │                         │ │                         │
│  • Billing entity       │ │                         │ │                         │
│  • Contract holder      │ │                         │ │                         │
│  • Data isolation unit  │ │                         │ │                         │
└─────────────────────────┘ └─────────────────────────┘ └─────────────────────────┘
            │                           │
            ▼                           ▼
┌─────────────────────────┐ ┌─────────────────────────┐
│         SITE            │ │         SITE            │
│  (Venue / Location)     │ │  "Downtown Branch"      │
│                         │ │                         │
│  • Physical location    │ │  • Address, timezone    │
│  • Operational unit     │ │  • Tax jurisdiction     │
│  • Menu assignments     │ │  • Operating hours      │
│  • Staff assignments    │ │  • Hardware config      │
└─────────────────────────┘ └─────────────────────────┘
            │
            ├─────────────────┬─────────────────┐
            ▼                 ▼                 ▼
    ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
    │    FLOOR     │  │   STATION    │  │   DEVICE     │
    │  (Area)      │  │  (Kitchen)   │  │  (Terminal)  │
    └──────────────┘  └──────────────┘  └──────────────┘
            │
            ▼
    ┌──────────────┐
    │    TABLE     │
    └──────────────┘
```

### Core Entities

#### Organization (Tenant)

The **Organization** is the top-level tenant boundary. All data is isolated at this level.

```csharp
public record Organization
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string Slug { get; init; }              // URL-friendly identifier
    public OrganizationStatus Status { get; init; } // Active, Suspended, Cancelled
    public BillingInfo Billing { get; init; }
    public OrganizationSettings Settings { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record OrganizationSettings
{
    public string DefaultCurrency { get; init; }
    public string DefaultTimezone { get; init; }
    public string DefaultLocale { get; init; }
    public bool RequirePinForVoids { get; init; }
    public bool RequireManagerApprovalForDiscounts { get; init; }
    public RetentionPolicy DataRetention { get; init; }
}
```

#### Site (Venue/Location)

A **Site** represents a physical venue where business operations occur.

```csharp
public record Site
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public string Name { get; init; }
    public string Code { get; init; }              // Short code like "DT01"
    public Address Address { get; init; }
    public string Timezone { get; init; }          // IANA timezone
    public string Currency { get; init; }          // ISO 4217
    public string Locale { get; init; }            // BCP 47
    public TaxJurisdiction TaxJurisdiction { get; init; }
    public OperatingHours OperatingHours { get; init; }
    public SiteStatus Status { get; init; }        // Open, Closed, Temporarily Closed
    public SiteSettings Settings { get; init; }
}

public record SiteSettings
{
    public Guid? ActiveMenuId { get; init; }
    public Guid? DefaultPriceListId { get; init; }
    public int DefaultGuestCount { get; init; }
    public bool AutoPrintKitchenTickets { get; init; }
    public bool AutoPrintReceipts { get; init; }
    public TimeSpan OrderTimeout { get; init; }
    public BookingSettings BookingSettings { get; init; }
}
```

#### User

A **User** is a person who can authenticate and perform actions in the system.

```csharp
public record User
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public string Email { get; init; }
    public string DisplayName { get; init; }
    public string? Pin { get; init; }              // Hashed 4-6 digit PIN
    public string? QrToken { get; init; }          // For mobile clock-in
    public UserStatus Status { get; init; }        // Active, Inactive, Locked
    public UserType Type { get; init; }            // Employee, Manager, Admin, Owner
    public IReadOnlyList<Guid> SiteAccess { get; init; }  // Sites user can access
    public IReadOnlyList<Guid> UserGroupIds { get; init; }
    public UserPreferences Preferences { get; init; }
}
```

#### User Group

**User Groups** define collections of users for permission assignment and scheduling.

```csharp
public record UserGroup
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public string Name { get; init; }             // "Servers", "Kitchen Staff", "Managers"
    public string? Description { get; init; }
    public IReadOnlyList<Guid> MemberIds { get; init; }
    public bool IsSystemGroup { get; init; }      // Built-in groups can't be deleted
}
```

### Tenant Isolation Strategies

#### Option A: Grain Key Prefixing (Recommended)

All grain keys include the organization ID as a prefix:

```csharp
// Grain key format: "orgId:entityId" or "orgId:siteId:entityId"
public interface IOrderGrain : IGrainWithStringKey { }

// Usage
var orderGrain = grainFactory.GetGrain<IOrderGrain>($"{orgId}:{orderId}");
var inventoryGrain = grainFactory.GetGrain<IInventoryGrain>($"{orgId}:{siteId}:{ingredientId}");
```

**Pros:**
- Simple, explicit isolation
- Easy to reason about
- Works with any persistence provider

**Cons:**
- Key parsing required
- Slightly longer keys

#### Option B: Grain Placement by Tenant

Use Orleans placement strategies to co-locate grains by tenant:

```csharp
[PreferLocalPlacement]
[GrainType("order")]
public class OrderGrain : Grain, IOrderGrain
{
    // Grains for same org tend to land on same silo
}
```

#### Option C: Separate Silos per Tenant (Enterprise)

For large tenants or compliance requirements, run dedicated silo clusters:

```
┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐
│  Shared Cluster │  │  Enterprise A   │  │  Enterprise B   │
│  (Small tenants)│  │  (Dedicated)    │  │  (Dedicated)    │
└─────────────────┘  └─────────────────┘  └─────────────────┘
```

### Cross-Tenant Resources

Some resources may be shared across organizations:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         PLATFORM-LEVEL RESOURCES                            │
├─────────────────────────────────────────────────────────────────────────────┤
│  • Global menu item catalog (templates)                                     │
│  • Ingredient master data                                                   │
│  • Tax rate databases                                                       │
│  • Payment gateway configurations                                           │
│  • Integration connectors (Uber Eats, etc.)                                │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Authorization Model (SpiceDB)

### Why SpiceDB?

SpiceDB (based on Google Zanzibar) provides:

1. **Relationship-based access control (ReBAC)** - permissions derived from relationships
2. **Centralized policy** - single source of truth for authorization
3. **Consistent evaluation** - same answer regardless of which service asks
4. **Audit trail** - all permission checks logged
5. **Performance** - designed for millions of checks per second

### Schema Definition

```zed
// =============================================================================
// CORE TYPES
// =============================================================================

definition organization {
    relation owner: user
    relation admin: user | user_group#member
    relation member: user | user_group#member

    // Permissions
    permission manage = owner + admin
    permission view = manage + member
    permission delete = owner
}

definition user_group {
    relation organization: organization
    relation member: user
    relation manager: user

    permission manage = manager + organization->admin
    permission view = manage + member
}

definition site {
    relation organization: organization
    relation manager: user | user_group#member
    relation staff: user | user_group#member

    // Inherit org permissions
    permission admin = organization->admin
    permission manage = admin + manager
    permission access = manage + staff
    permission view = access
}

// =============================================================================
// POS OPERATIONS
// =============================================================================

definition order {
    relation site: site
    relation server: user
    relation table: table

    // Permissions flow from site
    permission view = site->access
    permission modify = server + site->manage
    permission void = site->manage
    permission discount = site->manage
    permission transfer = site->access
}

definition payment {
    relation order: order
    relation cashier: user

    permission process = cashier + order->site->access
    permission void = order->site->manage
    permission refund = order->site->manage
}

definition table {
    relation site: site
    relation section: section
    relation assigned_server: user

    permission view = site->access
    permission manage = assigned_server + section->manager + site->manage
}

definition section {
    relation site: site
    relation manager: user
    relation server: user

    permission manage = manager + site->manage
    permission access = manage + server
}

// =============================================================================
// INVENTORY & PROCUREMENT
// =============================================================================

definition inventory {
    relation site: site

    permission view = site->access
    permission adjust = site->manage
    permission receive = site->access  // Receiving staff
    permission waste = site->access
}

definition purchase_order {
    relation site: site
    relation creator: user
    relation approver: user

    permission view = site->access
    permission edit = creator + site->manage
    permission submit = creator + site->manage
    permission approve = approver + site->admin
}

// =============================================================================
// MENU & PRICING
// =============================================================================

definition menu {
    relation organization: organization
    relation editor: user | user_group#member

    permission view = organization->member
    permission edit = editor + organization->admin
    permission publish = organization->admin
}

definition price_list {
    relation organization: organization
    relation site: site

    permission view = organization->member
    permission edit = organization->admin
    permission assign_to_site = site->manage
}

// =============================================================================
// CUSTOMERS & LOYALTY
// =============================================================================

definition customer {
    relation organization: organization

    permission view = organization->member
    permission edit = organization->member
    permission delete = organization->admin
    permission view_loyalty = organization->member
    permission adjust_points = organization->admin
}

definition loyalty_program {
    relation organization: organization

    permission view = organization->member
    permission manage = organization->admin
}

// =============================================================================
// REPORTING & ANALYTICS
// =============================================================================

definition report {
    relation organization: organization
    relation site: site | none

    // Org-level reports
    permission view_org_reports = organization->admin
    // Site-level reports
    permission view_site_reports = site->manage + organization->admin
}

// =============================================================================
// LABOR & SCHEDULING
// =============================================================================

definition schedule {
    relation site: site
    relation manager: user

    permission view = site->access
    permission edit = manager + site->manage
    permission publish = site->manage
}

definition timecard {
    relation employee: user
    relation site: site

    permission view = employee + site->manage
    permission edit = site->manage
    permission approve = site->manage
}
```

### Permission Check Examples

```csharp
// Check if user can void an order
var canVoid = await spiceDbClient.CheckPermissionAsync(new CheckPermissionRequest
{
    Resource = new ObjectReference { Type = "order", Id = orderId.ToString() },
    Permission = "void",
    Subject = new SubjectReference { Type = "user", Id = userId.ToString() }
});

// Check if user can access a site
var canAccess = await spiceDbClient.CheckPermissionAsync(new CheckPermissionRequest
{
    Resource = new ObjectReference { Type = "site", Id = siteId.ToString() },
    Permission = "access",
    Subject = new SubjectReference { Type = "user", Id = userId.ToString() }
});
```

### Relationship Management

When entities are created, relationships are written to SpiceDB:

```csharp
// When a user is assigned to a site
await spiceDbClient.WriteRelationshipsAsync(new WriteRelationshipsRequest
{
    Updates =
    {
        new RelationshipUpdate
        {
            Operation = RelationshipUpdate.Types.Operation.Touch,
            Relationship = new Relationship
            {
                Resource = new ObjectReference { Type = "site", Id = siteId },
                Relation = "staff",
                Subject = new SubjectReference { Type = "user", Id = userId }
            }
        }
    }
});

// When an order is created
await spiceDbClient.WriteRelationshipsAsync(new WriteRelationshipsRequest
{
    Updates =
    {
        new RelationshipUpdate
        {
            Operation = RelationshipUpdate.Types.Operation.Touch,
            Relationship = new Relationship
            {
                Resource = new ObjectReference { Type = "order", Id = orderId },
                Relation = "site",
                Subject = new SubjectReference { Type = "site", Id = siteId }
            }
        },
        new RelationshipUpdate
        {
            Operation = RelationshipUpdate.Types.Operation.Touch,
            Relationship = new Relationship
            {
                Resource = new ObjectReference { Type = "order", Id = orderId },
                Relation = "server",
                Subject = new SubjectReference { Type = "user", Id visibleId }
            }
        }
    }
});
```

### Authorization Middleware

```csharp
public class SpiceDbAuthorizationMiddleware
{
    public async Task InvokeAsync(HttpContext context, ISpiceDbClient spiceDb)
    {
        var endpoint = context.GetEndpoint();
        var authAttribute = endpoint?.Metadata.GetMetadata<RequirePermissionAttribute>();

        if (authAttribute != null)
        {
            var userId = context.User.GetUserId();
            var resourceId = context.GetRouteValue(authAttribute.ResourceIdRoute)?.ToString();

            var allowed = await spiceDb.CheckPermissionAsync(new CheckPermissionRequest
            {
                Resource = new ObjectReference
                {
                    Type = authAttribute.ResourceType,
                    Id = resourceId
                },
                Permission = authAttribute.Permission,
                Subject = new SubjectReference { Type = "user", Id = userId }
            });

            if (!allowed.Permissionship.HasPermission)
            {
                context.Response.StatusCode = 403;
                return;
            }
        }

        await _next(context);
    }
}

// Usage on endpoints
[HttpPost("{orderId}/void")]
[RequirePermission("order", "void", ResourceIdRoute = "orderId")]
public async Task<IActionResult> VoidOrder(Guid orderId) { }
```

---

## Event Storming

Event storming identifies **facts that happen** in the system. These events are:
- Past tense (they already happened)
- Immutable (cannot be changed)
- Domain-significant (business cares about them)

### Organization & Site Events

| Event | Description | Triggered By |
|-------|-------------|--------------|
| `OrganizationCreated` | New tenant onboarded | Platform signup |
| `OrganizationUpdated` | Settings changed | Admin action |
| `OrganizationSuspended` | Account suspended | Billing/compliance |
| `OrganizationReactivated` | Suspension lifted | Payment/resolution |
| `OrganizationCancelled` | Account closed | Churn |
| `SiteCreated` | New venue added | Admin action |
| `SiteUpdated` | Site settings changed | Manager action |
| `SiteOpened` | Site began operations | Opening day |
| `SiteClosed` | Site shut down | Closure |
| `SiteTemporarilyClosed` | Temporary closure | Holiday/emergency |

### User & Access Events

| Event | Description | Triggered By |
|-------|-------------|--------------|
| `UserCreated` | New user added | Admin action |
| `UserUpdated` | Profile changed | User/admin |
| `UserDeactivated` | User disabled | Termination |
| `UserReactivated` | User re-enabled | Rehire |
| `UserLoggedIn` | Session started | Login |
| `UserLoggedOut` | Session ended | Logout |
| `UserLoginFailed` | Bad credentials | Login attempt |
| `UserLockedOut` | Too many failures | Security |
| `PinChanged` | PIN updated | User action |
| `SiteAccessGranted` | User given site access | Admin action |
| `SiteAccessRevoked` | User lost site access | Admin action |
| `UserGroupCreated` | Group created | Admin action |
| `UserAddedToGroup` | Membership added | Admin action |
| `UserRemovedFromGroup` | Membership removed | Admin action |

### Order Events

| Event | Description | Triggered By |
|-------|-------------|--------------|
| `OrderOpened` | New order started | Server action |
| `OrderLineAdded` | Item added | Server action |
| `OrderLineQuantityChanged` | Quantity modified | Server action |
| `OrderLineVoided` | Item cancelled | Manager approval |
| `OrderLineDiscountApplied` | Line discount | Promo/manager |
| `OrderDiscountApplied` | Order-level discount | Coupon/loyalty |
| `OrderSentToKitchen` | Fired to kitchen | Server action |
| `OrderCourseFired` | Course sent | Timing |
| `OrderTransferredToTable` | Moved to table | Guest moved |
| `OrderTransferredToServer` | Reassigned | Shift change |
| `OrderSplit` | Check divided | Guest request |
| `OrdersMerged` | Orders combined | Table merge |
| `PaymentApplied` | Partial/full payment | Payment |
| `OrderSettled` | Fully paid, closed | Final payment |
| `OrderVoided` | Order cancelled | Manager action |
| `OrderReopened` | Closed order reopened | Adjustment |
| `TipRecorded` | Tip captured | Payment |

### Payment Events

| Event | Description | Triggered By |
|-------|-------------|--------------|
| `CashPaymentReceived` | Cash tendered | Cashier |
| `CardPaymentInitiated` | Card started | Terminal |
| `CardPaymentAuthorized` | Auth approved | Gateway |
| `CardPaymentCaptured` | Settled | Batch close |
| `CardPaymentDeclined` | Auth failed | Gateway |
| `GiftCardPaymentApplied` | Gift card used | POS |
| `LoyaltyPointsRedeemed` | Points used | Customer |
| `SplitPaymentRecorded` | Partial payment | Multi-payer |
| `RefundIssued` | Money returned | Manager |
| `PaymentVoided` | Payment cancelled | Correction |
| `CashDrawerOpened` | Drawer opened | Various |
| `CashDropRecorded` | Cash to safe | Security |
| `TillCounted` | Drawer reconciled | Shift end |

### Inventory Events

| Event | Description | Triggered By |
|-------|-------------|--------------|
| `StockBatchReceived` | Inventory arrived | Delivery |
| `StockConsumed` | Ingredients used | Order completion |
| `StockWasted` | Product discarded | Waste recording |
| `StockTransferred` | Moved locations | Transfer |
| `StockAdjusted` | Count correction | Physical count |
| `StockBatchExhausted` | Batch depleted | Consumption |
| `LowStockAlertTriggered` | Below threshold | Automatic |
| `StockExpired` | Past use-by | Expiry check |
| `IngredientPriceChanged` | Cost updated | Price change |
| `RecipeCostRecalculated` | Menu cost changed | Recipe/price |

### Procurement Events

| Event | Description | Triggered By |
|-------|-------------|--------------|
| `PurchaseOrderDrafted` | PO created | Manager |
| `PurchaseOrderSubmitted` | Sent to supplier | Approval |
| `PurchaseOrderConfirmed` | Supplier ACK | Supplier |
| `DeliveryScheduled` | ETA set | Supplier |
| `DeliveryReceived` | Goods arrived | Receiving |
| `DeliveryLineInspected` | Item checked | QC |
| `DeliveryDiscrepancyRecorded` | Mismatch found | Inspection |
| `DeliveryAccepted` | Delivery OK | Manager |
| `DeliveryRejected` | Delivery refused | Quality |
| `InvoiceReceived` | Invoice arrived | AP |
| `InvoiceMatched` | 3-way match | AP |

### Booking Events

| Event | Description | Triggered By |
|-------|-------------|--------------|
| `BookingRequested` | Reservation made | Guest |
| `BookingConfirmed` | Accepted | Host |
| `BookingDepositPaid` | Deposit collected | Payment |
| `BookingModified` | Changed | Guest |
| `BookingCancelled` | Cancelled | Guest/policy |
| `GuestArrived` | Checked in | Host |
| `GuestSeated` | At table | Host |
| `GuestDeparted` | Table cleared | Server |
| `BookingNoShow` | Didn't arrive | Timeout |
| `DepositForfeited` | Kept deposit | Policy |
| `DepositRefunded` | Returned deposit | Cancellation |
| `WaitlistEntryAdded` | Walk-in waiting | Host |
| `WaitlistEntrySeated` | Got table | Host |
| `BookingLinkedToOrder` | Connected to POS | Server |

### Customer & Loyalty Events

| Event | Description | Triggered By |
|-------|-------------|--------------|
| `CustomerCreated` | New customer | Registration |
| `CustomerUpdated` | Profile changed | Update |
| `CustomerMerged` | Duplicates combined | Cleanup |
| `LoyaltyEnrolled` | Joined program | Opt-in |
| `PointsEarned` | Points added | Purchase |
| `PointsRedeemed` | Points spent | Redemption |
| `PointsExpired` | Points lost | Policy |
| `PointsAdjusted` | Manual change | Service |
| `TierPromoted` | Upgraded | Threshold |
| `TierDemoted` | Downgraded | Lapse |
| `RewardIssued` | Reward unlocked | Milestone |
| `RewardRedeemed` | Reward used | Customer |
| `ReferralCompleted` | Friend joined | Referral |
| `ReferralBonusAwarded` | Bonus given | Success |

### Gift Card Events

| Event | Description | Triggered By |
|-------|-------------|--------------|
| `GiftCardIssued` | Card created | Purchase |
| `GiftCardActivated` | Enabled | Activation |
| `GiftCardRedeemed` | Balance used | Payment |
| `GiftCardReloaded` | Balance added | Top-up |
| `GiftCardTransferred` | Owner changed | Gift |
| `GiftCardSuspended` | Frozen | Fraud |
| `GiftCardResumed` | Unfrozen | Resolution |
| `GiftCardExpired` | Past validity | Time |
| `GiftCardDepleted` | Zero balance | Redemption |

### Accounting Events

| Event | Description | Triggered By |
|-------|-------------|--------------|
| `JournalEntryPosted` | Entry created | Transaction |
| `JournalEntryReversed` | Entry negated | Correction |
| `AccountingPeriodOpened` | Period started | Calendar |
| `AccountingPeriodClosed` | Period locked | Month-end |
| `ReconciliationCompleted` | Matched | Process |
| `RevenueRecognized` | Income recorded | Settlement |
| `ExpenseRecorded` | Cost recorded | Purchase |
| `COGSCalculated` | COGS computed | Sale |

### Labor Events

| Event | Description | Triggered By |
|-------|-------------|--------------|
| `EmployeeHired` | Added | HR |
| `EmployeeTerminated` | Ended | HR |
| `ShiftScheduled` | Assigned | Manager |
| `EmployeeClockedIn` | Started | Clock |
| `EmployeeClockedOut` | Ended | Clock |
| `BreakStarted` | On break | Employee |
| `BreakEnded` | Off break | Employee |
| `OvertimeTriggered` | OT threshold | Automatic |
| `TipDeclared` | Tips reported | Employee |
| `TipPoolDistributed` | Tips shared | Shift end |

### Device Events

| Event | Description | Triggered By |
|-------|-------------|--------------|
| `DeviceRegistered` | Terminal added | Setup |
| `DeviceHeartbeat` | Still alive | Periodic |
| `DeviceOffline` | Connection lost | Network |
| `PrintJobQueued` | Print requested | Various |
| `PrintJobCompleted` | Print done | Printer |
| `PrinterError` | Print failed | Hardware |
| `KitchenDisplayUpdated` | KDS refreshed | Order change |

### Sales Period Events

| Event | Description | Triggered By |
|-------|-------------|--------------|
| `SalesPeriodOpened` | Day started | Manager |
| `SalesPeriodClosed` | Day ended | Manager |
| `EndOfDayReportGenerated` | EOD report | Close process |

---

## Grain Architecture

### Grain Hierarchy

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         PLATFORM SINGLETON GRAINS                           │
├─────────────────────────────────────────────────────────────────────────────┤
│  PlatformConfigGrain          - Global platform settings                    │
│  OrganizationRegistryGrain    - Tenant directory                            │
│  EventStreamCoordinatorGrain  - Manages cross-tenant streams                │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                       ORGANIZATION-SCOPED GRAINS                            │
│                       Key format: "org:{orgId}"                             │
├─────────────────────────────────────────────────────────────────────────────┤
│  OrganizationGrain[OrgId]           - Org settings, billing state           │
│  SiteRegistryGrain[OrgId]           - Sites for this org                    │
│  UserRegistryGrain[OrgId]           - User directory                        │
│  UserGroupRegistryGrain[OrgId]      - Group management                      │
│  CustomerRegistryGrain[OrgId]       - Customer search/lookup                │
│  GiftCardRegistryGrain[OrgId]       - Card number lookup                    │
│  SupplierRegistryGrain[OrgId]       - Supplier directory                    │
│  LoyaltyProgramGrain[OrgId]         - Program rules, tiers                  │
│  MenuCatalogGrain[OrgId]            - Menu templates                        │
│  IngredientCatalogGrain[OrgId]      - Master ingredient list                │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                          SITE-SCOPED GRAINS                                 │
│                       Key format: "org:{orgId}:site:{siteId}"               │
├─────────────────────────────────────────────────────────────────────────────┤
│  SiteGrain[OrgId, SiteId]                - Site config, status              │
│  SalesPeriodGrain[OrgId, SiteId, Date]   - Daily business period            │
│  FloorPlanGrain[OrgId, SiteId]           - Tables, sections                 │
│  TableGrain[OrgId, SiteId, TableId]      - Individual table state           │
│  KitchenGrain[OrgId, SiteId]             - Kitchen queue/routing            │
│  ActiveMenuGrain[OrgId, SiteId]          - Currently active menu            │
│  InventoryGrain[OrgId, SiteId, IngId]    - Per-ingredient stock             │
│  CashDrawerGrain[OrgId, SiteId, DrawerId]- Drawer state                     │
│  DeviceGrain[OrgId, SiteId, DeviceId]    - Terminal state                   │
│  BookingCalendarGrain[OrgId, SiteId]     - Availability calendar            │
│  WaitlistGrain[OrgId, SiteId]            - Walk-in queue                    │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                         ENTITY GRAINS (by ID)                               │
│                    Key format: "org:{orgId}:{type}:{entityId}"              │
├─────────────────────────────────────────────────────────────────────────────┤
│  UserGrain[OrgId, UserId]          - User profile, sessions                 │
│  UserGroupGrain[OrgId, GroupId]    - Group membership                       │
│  OrderGrain[OrgId, OrderId]        - Order lifecycle                        │
│  PaymentGrain[OrgId, PaymentId]    - Payment processing                     │
│  BookingGrain[OrgId, BookingId]    - Reservation lifecycle                  │
│  CustomerGrain[OrgId, CustomerId]  - Customer + loyalty                     │
│  GiftCardGrain[OrgId, CardId]      - Gift card lifecycle                    │
│  PurchaseOrderGrain[OrgId, POId]   - PO lifecycle                           │
│  DeliveryGrain[OrgId, DeliveryId]  - Delivery processing                    │
│  EmployeeGrain[OrgId, EmployeeId]  - Employee + time tracking               │
│  MenuGrain[OrgId, MenuId]          - Menu definition                        │
│  RecipeGrain[OrgId, RecipeId]      - Recipe with ingredients                │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                      PROJECTION / READ MODEL GRAINS                         │
├─────────────────────────────────────────────────────────────────────────────┤
│  OrderSearchGrain[OrgId, SiteId]         - Order queries by site            │
│  DailyReportGrain[OrgId, SiteId, Date]   - Daily aggregations               │
│  CustomerSearchGrain[OrgId]              - Customer lookup                  │
│  AccountingLedgerGrain[OrgId, Period]    - Period balances                  │
│  InventoryAlertGrain[OrgId, SiteId]      - Low stock monitoring             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Grain Key Utilities

```csharp
public static class GrainKeys
{
    // Organization-scoped
    public static string Org(Guid orgId) => $"org:{orgId}";
    public static string OrgEntity(Guid orgId, string type, Guid entityId)
        => $"org:{orgId}:{type}:{entityId}";

    // Site-scoped
    public static string Site(Guid orgId, Guid siteId)
        => $"org:{orgId}:site:{siteId}";
    public static string SiteEntity(Guid orgId, Guid siteId, string type, Guid entityId)
        => $"org:{orgId}:site:{siteId}:{type}:{entityId}";

    // Parsing
    public static (Guid OrgId, Guid EntityId) ParseOrgEntity(string key)
    {
        var parts = key.Split(':');
        return (Guid.Parse(parts[1]), Guid.Parse(parts[3]));
    }
}

// Usage
var orderKey = GrainKeys.OrgEntity(orgId, "order", orderId);
var orderGrain = grainFactory.GetGrain<IOrderGrain>(orderKey);

var inventoryKey = GrainKeys.SiteEntity(orgId, siteId, "inventory", ingredientId);
var inventoryGrain = grainFactory.GetGrain<IInventoryGrain>(inventoryKey);
```

---

## Grain Interface Definitions

### Base Infrastructure

```csharp
// All domain events inherit from this
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
    Guid OrgId { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public abstract string EventType { get; }
    public required Guid OrgId { get; init; }
}

// Base grain with event sourcing
public abstract class EventSourcedGrain<TState> : Grain
    where TState : class, new()
{
    protected TState State { get; private set; } = new();
    protected ILogger Logger { get; private set; } = null!;

    private IPersistentState<EventLog> _eventLog = null!;
    private IAsyncStream<IDomainEvent> _eventStream = null!;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        Logger = ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(GetType());

        // Replay events to rebuild state
        foreach (var @event in _eventLog.State.Events)
        {
            Apply(@event);
        }

        // Set up event stream
        var streamProvider = this.GetStreamProvider("DomainEvents");
        _eventStream = streamProvider.GetStream<IDomainEvent>(
            StreamId.Create("events", GetGrainKey().ToString()));
    }

    protected async Task<TEvent> RaiseEventAsync<TEvent>(TEvent @event)
        where TEvent : IDomainEvent
    {
        // Apply to state
        Apply(@event);

        // Persist
        _eventLog.State.Events.Add(@event);
        await _eventLog.WriteStateAsync();

        // Publish to stream
        await _eventStream.OnNextAsync(@event);

        return @event;
    }

    protected abstract void Apply(IDomainEvent @event);

    public Task<IReadOnlyList<IDomainEvent>> GetEventHistoryAsync()
        => Task.FromResult<IReadOnlyList<IDomainEvent>>(_eventLog.State.Events);
}
```

### Organization Grain

```csharp
public interface IOrganizationGrain : IGrainWithStringKey
{
    // Commands
    Task<OrganizationCreatedEvent> CreateAsync(CreateOrganizationCommand cmd);
    Task<OrganizationUpdatedEvent> UpdateAsync(UpdateOrganizationCommand cmd);
    Task<OrganizationSuspendedEvent> SuspendAsync(string reason);
    Task<OrganizationReactivatedEvent> ReactivateAsync();

    // Queries
    Task<OrganizationState> GetStateAsync();
    Task<bool> IsActiveAsync();
}

public record CreateOrganizationCommand(
    string Name,
    string Slug,
    string OwnerEmail,
    string DefaultCurrency,
    string DefaultTimezone);

public record OrganizationState
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
    public OrganizationStatus Status { get; init; }
    public OrganizationSettings Settings { get; init; } = new();
    public DateTime CreatedAt { get; init; }
}
```

### Site Grain

```csharp
public interface ISiteGrain : IGrainWithStringKey
{
    // Commands
    Task<SiteCreatedEvent> CreateAsync(CreateSiteCommand cmd);
    Task<SiteUpdatedEvent> UpdateAsync(UpdateSiteCommand cmd);
    Task<SiteStatusChangedEvent> SetStatusAsync(SiteStatus status);
    Task<MenuActivatedEvent> ActivateMenuAsync(Guid menuId);

    // Sales Period
    Task<SalesPeriodOpenedEvent> OpenSalesPeriodAsync(OpenSalesPeriodCommand cmd);
    Task<SalesPeriodClosedEvent> CloseSalesPeriodAsync();

    // Queries
    Task<SiteState> GetStateAsync();
    Task<SiteSettings> GetSettingsAsync();
    Task<SalesPeriodState?> GetCurrentSalesPeriodAsync();
    Task<bool> IsOpenAsync();
}

public record CreateSiteCommand(
    Guid OrgId,
    string Name,
    string Code,
    Address Address,
    string Timezone,
    string Currency);
```

### User Grain

```csharp
public interface IUserGrain : IGrainWithStringKey
{
    // Profile Commands
    Task<UserCreatedEvent> CreateAsync(CreateUserCommand cmd);
    Task<UserUpdatedEvent> UpdateAsync(UpdateUserCommand cmd);
    Task<UserDeactivatedEvent> DeactivateAsync(string reason);
    Task<UserReactivatedEvent> ReactivateAsync();

    // Authentication
    Task<UserLoggedInEvent> RecordLoginAsync(LoginInfo info);
    Task<UserLoggedOutEvent> RecordLogoutAsync();
    Task<UserLoginFailedEvent> RecordLoginFailureAsync(string reason);
    Task<PinChangedEvent> ChangePinAsync(string hashedPin);

    // Access
    Task<SiteAccessGrantedEvent> GrantSiteAccessAsync(Guid siteId);
    Task<SiteAccessRevokedEvent> RevokeSiteAccessAsync(Guid siteId);
    Task<UserAddedToGroupEvent> AddToGroupAsync(Guid groupId);
    Task<UserRemovedFromGroupEvent> RemoveFromGroupAsync(Guid groupId);

    // Queries
    Task<UserState> GetStateAsync();
    Task<bool> CanAccessSiteAsync(Guid siteId);
    Task<IReadOnlyList<Guid>> GetAccessibleSitesAsync();
}

public record CreateUserCommand(
    Guid OrgId,
    string Email,
    string DisplayName,
    UserType Type,
    IReadOnlyList<Guid>? SiteIds = null);
```

### Order Grain

```csharp
public interface IOrderGrain : IGrainWithStringKey
{
    // Commands
    Task<OrderOpenedEvent> OpenAsync(OpenOrderCommand cmd);
    Task<OrderLineAddedEvent> AddLineAsync(AddOrderLineCommand cmd);
    Task<OrderLineUpdatedEvent> UpdateLineAsync(UpdateOrderLineCommand cmd);
    Task<OrderLineVoidedEvent> VoidLineAsync(VoidOrderLineCommand cmd);
    Task<OrderDiscountAppliedEvent> ApplyDiscountAsync(ApplyDiscountCommand cmd);
    Task<OrderSentToKitchenEvent> SendToKitchenAsync();
    Task<PaymentAppliedEvent> ApplyPaymentAsync(ApplyPaymentCommand cmd);
    Task<OrderSettledEvent> SettleAsync();
    Task<OrderVoidedEvent> VoidAsync(VoidOrderCommand cmd);
    Task<OrderReopenedEvent> ReopenAsync(string reason);
    Task<OrderTransferredEvent> TransferAsync(TransferOrderCommand cmd);

    // Queries
    Task<OrderState> GetStateAsync();
    Task<IReadOnlyList<OrderLineState>> GetLinesAsync();
    Task<decimal> GetSubtotalAsync();
    Task<decimal> GetTaxAsync();
    Task<decimal> GetTotalAsync();
    Task<decimal> GetBalanceDueAsync();
    Task<OrderStatus> GetStatusAsync();
}

public record OpenOrderCommand(
    Guid OrgId,
    Guid SiteId,
    Guid ServerId,
    Guid? TableId = null,
    Guid? CustomerId = null,
    Guid? BookingId = null,
    int GuestCount = 1,
    string? Notes = null);

public record AddOrderLineCommand(
    Guid MenuItemId,
    string MenuItemName,
    int Quantity,
    decimal UnitPrice,
    decimal TaxRate,
    Guid? RecipeId = null,
    string? Notes = null,
    IReadOnlyList<ModifierSelection>? Modifiers = null);

public record OrderState
{
    public Guid Id { get; init; }
    public Guid OrgId { get; init; }
    public Guid SiteId { get; init; }
    public Guid ServerId { get; init; }
    public Guid? TableId { get; init; }
    public Guid? CustomerId { get; init; }
    public Guid? BookingId { get; init; }
    public int GuestCount { get; init; }
    public OrderStatus Status { get; init; }
    public IReadOnlyList<OrderLineState> Lines { get; init; } = [];
    public IReadOnlyList<PaymentSummary> Payments { get; init; } = [];
    public IReadOnlyList<DiscountSummary> Discounts { get; init; } = [];
    public decimal Subtotal { get; init; }
    public decimal TaxTotal { get; init; }
    public decimal DiscountTotal { get; init; }
    public decimal Total { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal BalanceDue { get; init; }
    public DateTime OpenedAt { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime? SettledAt { get; init; }
}
```

### Payment Grain

```csharp
public interface IPaymentGrain : IGrainWithStringKey
{
    // Commands
    Task<PaymentInitiatedEvent> InitiateAsync(InitiatePaymentCommand cmd);
    Task<CashPaymentCompletedEvent> CompleteCashAsync(CompleteCashPaymentCommand cmd);
    Task<CardPaymentAuthorizedEvent> RecordCardAuthAsync(CardAuthResult result);
    Task<CardPaymentCapturedEvent> CaptureCardAsync();
    Task<PaymentVoidedEvent> VoidAsync(string reason);
    Task<RefundIssuedEvent> RefundAsync(RefundCommand cmd);

    // Queries
    Task<PaymentState> GetStateAsync();
    Task<PaymentStatus> GetStatusAsync();
}

public record InitiatePaymentCommand(
    Guid OrgId,
    Guid OrderId,
    Guid SiteId,
    PaymentMethod Method,
    decimal Amount,
    decimal? TipAmount = null,
    Guid? GiftCardId = null,
    Guid? CustomerId = null);
```

### Inventory Grain

```csharp
public interface IInventoryGrain : IGrainWithStringKey // org:site:inventory:ingredientId
{
    // Commands
    Task<StockBatchReceivedEvent> ReceiveBatchAsync(ReceiveBatchCommand cmd);
    Task<StockConsumedEvent> ConsumeAsync(ConsumeStockCommand cmd);
    Task<StockWastedEvent> RecordWasteAsync(RecordWasteCommand cmd);
    Task<StockAdjustedEvent> AdjustAsync(AdjustStockCommand cmd);
    Task<StockTransferredOutEvent> TransferOutAsync(TransferStockCommand cmd);
    Task<StockTransferredInEvent> TransferInAsync(TransferStockCommand cmd);

    // Queries
    Task<decimal> GetQuantityOnHandAsync();
    Task<IReadOnlyList<StockBatchState>> GetBatchesAsync();
    Task<decimal> GetWeightedAverageCostAsync();
    Task<StockLevel> GetStockLevelAsync(); // Normal, Low, Critical, Out
    Task<InventoryState> GetStateAsync();
}

public record ReceiveBatchCommand(
    Guid DeliveryId,
    string BatchNumber,
    decimal Quantity,
    string Unit,
    decimal UnitCost,
    DateTime? ExpiryDate = null,
    string? Notes = null);

public record ConsumeStockCommand(
    Guid OrderId,
    decimal Quantity,
    ConsumeReason Reason);

public enum ConsumeReason { Sale, Waste, Transfer, Adjustment }
```

### Customer Grain

```csharp
public interface ICustomerGrain : IGrainWithStringKey
{
    // Profile
    Task<CustomerCreatedEvent> CreateAsync(CreateCustomerCommand cmd);
    Task<CustomerUpdatedEvent> UpdateAsync(UpdateCustomerCommand cmd);
    Task<CustomerDeletedEvent> DeleteAsync(string reason);

    // Loyalty
    Task<LoyaltyEnrolledEvent> EnrollInLoyaltyAsync(EnrollLoyaltyCommand cmd);
    Task<PointsEarnedEvent> EarnPointsAsync(EarnPointsCommand cmd);
    Task<PointsRedeemedEvent> RedeemPointsAsync(RedeemPointsCommand cmd);
    Task<PointsAdjustedEvent> AdjustPointsAsync(AdjustPointsCommand cmd);
    Task<TierChangedEvent> RecalculateTierAsync();
    Task<RewardIssuedEvent> IssueRewardAsync(IssueRewardCommand cmd);
    Task<RewardRedeemedEvent> RedeemRewardAsync(Guid rewardId);

    // Visits
    Task<VisitRecordedEvent> RecordVisitAsync(RecordVisitCommand cmd);

    // Referrals
    Task<ReferralCodeGeneratedEvent> GenerateReferralCodeAsync();
    Task<ReferralCompletedEvent> CompleteReferralAsync(Guid referredCustomerId);

    // Queries
    Task<CustomerState> GetStateAsync();
    Task<LoyaltyState?> GetLoyaltyStateAsync();
    Task<int> GetPointsBalanceAsync();
    Task<IReadOnlyList<RewardState>> GetAvailableRewardsAsync();
    Task<CustomerStats> GetStatsAsync();
}
```

### Booking Grain

```csharp
public interface IBookingGrain : IGrainWithStringKey
{
    // Commands
    Task<BookingRequestedEvent> RequestAsync(RequestBookingCommand cmd);
    Task<BookingConfirmedEvent> ConfirmAsync(string? confirmationNotes = null);
    Task<BookingModifiedEvent> ModifyAsync(ModifyBookingCommand cmd);
    Task<BookingCancelledEvent> CancelAsync(CancelBookingCommand cmd);
    Task<DepositPaidEvent> RecordDepositAsync(RecordDepositCommand cmd);
    Task<GuestArrivedEvent> RecordArrivalAsync();
    Task<GuestSeatedEvent> SeatAsync(SeatGuestCommand cmd);
    Task<GuestDepartedEvent> RecordDepartureAsync();
    Task<BookingNoShowEvent> MarkNoShowAsync();
    Task<BookingLinkedToOrderEvent> LinkToOrderAsync(Guid orderId);

    // Queries
    Task<BookingState> GetStateAsync();
    Task<BookingStatus> GetStatusAsync();
}

public record RequestBookingCommand(
    Guid OrgId,
    Guid SiteId,
    DateTime RequestedTime,
    int PartySize,
    string GuestName,
    string? GuestPhone = null,
    string? GuestEmail = null,
    string? SpecialRequests = null,
    Guid? CustomerId = null);
```

### Kitchen Grain

```csharp
public interface IKitchenGrain : IGrainWithStringKey // org:site:kitchen
{
    // Commands
    Task<KitchenTicketCreatedEvent> CreateTicketAsync(CreateKitchenTicketCommand cmd);
    Task<KitchenItemStartedEvent> StartItemAsync(Guid ticketId, Guid lineId);
    Task<KitchenItemCompletedEvent> CompleteItemAsync(Guid ticketId, Guid lineId);
    Task<KitchenTicketBumpedEvent> BumpTicketAsync(Guid ticketId);
    Task<KitchenTicketRecalledEvent> RecallTicketAsync(Guid ticketId);
    Task<KitchenTicketPrioritizedEvent> SetPriorityAsync(Guid ticketId, int priority);
    Task<KitchenTicketVoidedEvent> VoidTicketAsync(Guid ticketId, string reason);

    // Queries
    Task<IReadOnlyList<KitchenTicketState>> GetActiveTicketsAsync();
    Task<IReadOnlyList<KitchenTicketState>> GetTicketsByStationAsync(string station);
    Task<KitchenMetrics> GetMetricsAsync();
}
```

### Gift Card Grain

```csharp
public interface IGiftCardGrain : IGrainWithStringKey
{
    // Commands
    Task<GiftCardIssuedEvent> IssueAsync(IssueGiftCardCommand cmd);
    Task<GiftCardActivatedEvent> ActivateAsync();
    Task<GiftCardRedeemedEvent> RedeemAsync(RedeemGiftCardCommand cmd);
    Task<GiftCardReloadedEvent> ReloadAsync(ReloadGiftCardCommand cmd);
    Task<GiftCardSuspendedEvent> SuspendAsync(string reason);
    Task<GiftCardResumedEvent> ResumeAsync();
    Task<GiftCardRefundedEvent> RefundAsync(RefundGiftCardCommand cmd);
    Task<GiftCardExpiredEvent> ExpireAsync();

    // Queries
    Task<GiftCardState> GetStateAsync();
    Task<decimal> GetBalanceAsync();
    Task<bool> CanRedeemAsync(decimal amount);
    Task<IReadOnlyList<GiftCardTransactionState>> GetTransactionsAsync();
}
```

### Purchase Order Grain

```csharp
public interface IPurchaseOrderGrain : IGrainWithStringKey
{
    // Commands
    Task<PODraftedEvent> DraftAsync(DraftPOCommand cmd);
    Task<POLineAddedEvent> AddLineAsync(AddPOLineCommand cmd);
    Task<POLineUpdatedEvent> UpdateLineAsync(UpdatePOLineCommand cmd);
    Task<POLineRemovedEvent> RemoveLineAsync(Guid lineId);
    Task<POSubmittedEvent> SubmitAsync();
    Task<POConfirmedEvent> RecordConfirmationAsync(POConfirmation confirmation);
    Task<POCancelledEvent> CancelAsync(string reason);
    Task<DeliveryLinkedEvent> LinkDeliveryAsync(Guid deliveryId);

    // Queries
    Task<PurchaseOrderState> GetStateAsync();
    Task<POStatus> GetStatusAsync();
}
```

### Accounting Ledger Grain

```csharp
public interface IAccountingLedgerGrain : IGrainWithStringKey // org:ledger:periodId
{
    // Commands
    Task<JournalEntryPostedEvent> PostEntryAsync(PostJournalEntryCommand cmd);
    Task<JournalEntryReversedEvent> ReverseEntryAsync(Guid entryId, string reason);
    Task<PeriodClosedEvent> ClosePeriodAsync();

    // Queries
    Task<decimal> GetAccountBalanceAsync(Guid accountId);
    Task<IReadOnlyList<JournalEntrySummary>> GetEntriesAsync(JournalEntryFilter? filter = null);
    Task<TrialBalance> GetTrialBalanceAsync();
    Task<bool> IsPeriodClosedAsync();
}

public record PostJournalEntryCommand(
    Guid OrgId,
    string Description,
    IReadOnlyList<JournalLine> Lines,
    Guid? SourceEntityId = null,
    string? SourceEntityType = null);

public record JournalLine(
    Guid AccountId,
    decimal DebitAmount,
    decimal CreditAmount,
    Guid? CostCenterId = null);
```

---

## Event Flows & Sagas

### Order Settlement Saga

When an order is settled, multiple grains must be coordinated:

```
┌─────────────┐
│ OrderGrain  │
│  Settle()   │
└──────┬──────┘
       │
       ├─────────────────────────────────────────────────────┐
       │                                                     │
       ▼                                                     ▼
┌─────────────────┐                               ┌─────────────────┐
│ InventoryGrain  │ (for each ingredient)         │ CustomerGrain   │
│  Consume()      │                               │  EarnPoints()   │
└─────────────────┘                               └─────────────────┘
       │                                                     │
       │                                                     │
       ▼                                                     ▼
┌─────────────────┐                               ┌─────────────────┐
│ AccountingGrain │                               │  TableGrain     │
│  PostEntry()    │                               │  Clear()        │
└─────────────────┘                               └─────────────────┘
```

```csharp
// In OrderGrain
public async Task<OrderSettledEvent> SettleAsync()
{
    // Validate
    if (State.Status != OrderStatus.FullyPaid)
        throw new InvalidOperationException("Order not fully paid");

    var grainFactory = GrainFactory;
    var (orgId, _) = GrainKeys.ParseOrgEntity(this.GetPrimaryKeyString());

    // 1. Consume inventory for each line with a recipe
    foreach (var line in State.Lines.Where(l => l.RecipeId.HasValue))
    {
        var recipeGrain = grainFactory.GetGrain<IRecipeGrain>(
            GrainKeys.OrgEntity(orgId, "recipe", line.RecipeId!.Value));
        var recipe = await recipeGrain.GetStateAsync();

        foreach (var ingredient in recipe.Ingredients)
        {
            var inventoryGrain = grainFactory.GetGrain<IInventoryGrain>(
                GrainKeys.SiteEntity(orgId, State.SiteId, "inventory", ingredient.IngredientId));

            await inventoryGrain.ConsumeAsync(new ConsumeStockCommand(
                State.Id,
                ingredient.Quantity * line.Quantity,
                ConsumeReason.Sale));
        }
    }

    // 2. Award loyalty points if customer attached
    if (State.CustomerId.HasValue)
    {
        var customerGrain = grainFactory.GetGrain<ICustomerGrain>(
            GrainKeys.OrgEntity(orgId, "customer", State.CustomerId.Value));

        await customerGrain.EarnPointsAsync(new EarnPointsCommand(
            State.Id,
            State.Total,
            PointsSource.OrderCompletion));
    }

    // 3. Create accounting entries
    var periodId = GetCurrentAccountingPeriod();
    var ledgerGrain = grainFactory.GetGrain<IAccountingLedgerGrain>(
        $"org:{orgId}:ledger:{periodId}");

    await ledgerGrain.PostEntryAsync(new PostJournalEntryCommand(
        orgId,
        $"Revenue from Order {State.Id}",
        CreateRevenueJournalLines(),
        State.Id,
        "Order"));

    // 4. Clear table if assigned
    if (State.TableId.HasValue)
    {
        var tableGrain = grainFactory.GetGrain<ITableGrain>(
            GrainKeys.SiteEntity(orgId, State.SiteId, "table", State.TableId.Value));
        await tableGrain.ClearAsync();
    }

    // 5. Raise settlement event
    return await RaiseEventAsync(new OrderSettledEvent
    {
        OrgId = orgId,
        OrderId = State.Id,
        SiteId = State.SiteId,
        Total = State.Total,
        CustomerId = State.CustomerId,
        ServerId = State.ServerId
    });
}
```

### Booking to Order Flow

```
Guest Arrives → Booking Confirmed → Guest Seated → Order Created → Order Linked
     │                │                  │              │              │
     ▼                ▼                  ▼              ▼              ▼
BookingGrain    BookingGrain       TableGrain     OrderGrain    BookingGrain
RecordArrival   (status update)    Occupy()       Open()        LinkToOrder()
```

### Stock Receipt Flow

```
PO Created → PO Submitted → Delivery Received → Stock Batches Created → Cost Recalculated
     │            │               │                    │                      │
     ▼            ▼               ▼                    ▼                      ▼
   POGrain      POGrain     DeliveryGrain      InventoryGrain (×N)       RecipeGrain (×N)
   Draft()      Submit()    Receive()          ReceiveBatch()            Recalculate()
```

### Stream Subscriptions

Grains can subscribe to event streams for reactive updates:

```csharp
public class DailyReportGrain : Grain, IDailyReportGrain
{
    private DailyReportState _state = new();

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        var streamProvider = this.GetStreamProvider("DomainEvents");

        // Subscribe to order events for this site
        var orderStream = streamProvider.GetStream<IDomainEvent>(
            StreamId.Create("site-events", GetSiteKey()));

        await orderStream.SubscribeAsync(async (@event, token) =>
        {
            switch (@event)
            {
                case OrderSettledEvent settled:
                    _state.TotalRevenue += settled.Total;
                    _state.OrderCount++;
                    break;

                case OrderVoidedEvent voided:
                    _state.VoidCount++;
                    _state.VoidTotal += voided.Total;
                    break;

                case PaymentAppliedEvent payment:
                    UpdatePaymentBreakdown(payment);
                    break;
            }

            await PersistStateAsync();
        });
    }
}
```

---

## HTTP API Layer

The HTTP layer becomes a thin routing layer that delegates to grains:

### Controller Pattern

```csharp
[ApiController]
[Route("api/orgs/{orgId}/sites/{siteId}/orders")]
public class OrdersController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ISpiceDbClient _spiceDb;

    public OrdersController(IGrainFactory grainFactory, ISpiceDbClient spiceDb)
    {
        _grainFactory = grainFactory;
        _spiceDb = spiceDb;
    }

    [HttpPost]
    [RequirePermission("site", "access", ResourceIdRoute = "siteId")]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        Guid orgId,
        Guid siteId,
        [FromBody] CreateOrderRequest request)
    {
        var userId = User.GetUserId();
        var orderId = Guid.NewGuid();

        var orderGrain = _grainFactory.GetGrain<IOrderGrain>(
            GrainKeys.OrgEntity(orgId, "order", orderId));

        var @event = await orderGrain.OpenAsync(new OpenOrderCommand(
            orgId,
            siteId,
            userId,
            request.TableId,
            request.CustomerId,
            request.BookingId,
            request.GuestCount ?? 1,
            request.Notes));

        // Write SpiceDB relationship
        await _spiceDb.WriteRelationshipsAsync(new WriteRelationshipsRequest
        {
            Updates =
            {
                Relationship("order", orderId, "server", "user", userId),
                Relationship("order", orderId, "site", "site", siteId)
            }
        });

        var state = await orderGrain.GetStateAsync();
        return CreatedAtAction(nameof(GetOrder),
            new { orgId, siteId, orderId },
            MapToResponse(state));
    }

    [HttpPost("{orderId}/lines")]
    [RequirePermission("order", "modify", ResourceIdRoute = "orderId")]
    public async Task<ActionResult<OrderLineResponse>> AddLine(
        Guid orgId,
        Guid siteId,
        Guid orderId,
        [FromBody] AddLineRequest request)
    {
        var orderGrain = _grainFactory.GetGrain<IOrderGrain>(
            GrainKeys.OrgEntity(orgId, "order", orderId));

        var @event = await orderGrain.AddLineAsync(new AddOrderLineCommand(
            request.MenuItemId,
            request.MenuItemName,
            request.Quantity,
            request.UnitPrice,
            request.TaxRate,
            request.RecipeId,
            request.Notes,
            request.Modifiers));

        return Ok(MapToResponse(@event));
    }

    [HttpPost("{orderId}/void")]
    [RequirePermission("order", "void", ResourceIdRoute = "orderId")]
    public async Task<ActionResult> VoidOrder(
        Guid orgId,
        Guid siteId,
        Guid orderId,
        [FromBody] VoidOrderRequest request)
    {
        var orderGrain = _grainFactory.GetGrain<IOrderGrain>(
            GrainKeys.OrgEntity(orgId, "order", orderId));

        await orderGrain.VoidAsync(new VoidOrderCommand(
            User.GetUserId(),
            request.Reason));

        return NoContent();
    }

    [HttpGet("{orderId}")]
    [RequirePermission("order", "view", ResourceIdRoute = "orderId")]
    public async Task<ActionResult<OrderResponse>> GetOrder(
        Guid orgId,
        Guid siteId,
        Guid orderId)
    {
        var orderGrain = _grainFactory.GetGrain<IOrderGrain>(
            GrainKeys.OrgEntity(orgId, "order", orderId));

        var state = await orderGrain.GetStateAsync();

        if (state.SiteId != siteId)
            return NotFound();

        return Ok(MapToResponse(state));
    }

    [HttpGet]
    [RequirePermission("site", "access", ResourceIdRoute = "siteId")]
    public async Task<ActionResult<OrderListResponse>> ListOrders(
        Guid orgId,
        Guid siteId,
        [FromQuery] OrderStatus? status,
        [FromQuery] int limit = 50)
    {
        var searchGrain = _grainFactory.GetGrain<IOrderSearchGrain>(
            GrainKeys.Site(orgId, siteId));

        var orders = await searchGrain.SearchAsync(new OrderSearchCriteria
        {
            Status = status,
            Limit = limit
        });

        return Ok(new OrderListResponse(orders.Select(MapToSummary)));
    }
}
```

### SignalR Integration

```csharp
public class PosHub : Hub
{
    private readonly IGrainFactory _grainFactory;

    public async Task SubscribeToSite(Guid orgId, Guid siteId)
    {
        // Verify access via SpiceDB
        // ...

        await Groups.AddToGroupAsync(Context.ConnectionId, $"site:{orgId}:{siteId}");
    }

    public async Task SubscribeToKitchen(Guid orgId, Guid siteId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"kitchen:{orgId}:{siteId}");
    }
}

// Bridge grain that forwards Orleans streams to SignalR
public class SignalRBridgeGrain : Grain, ISignalRBridgeGrain
{
    private IHubContext<PosHub> _hubContext = null!;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        _hubContext = ServiceProvider.GetRequiredService<IHubContext<PosHub>>();

        var (orgId, siteId) = ParseSiteKey(this.GetPrimaryKeyString());
        var streamProvider = this.GetStreamProvider("DomainEvents");

        var siteStream = streamProvider.GetStream<IDomainEvent>(
            StreamId.Create("site-events", this.GetPrimaryKeyString()));

        await siteStream.SubscribeAsync(async (@event, token) =>
        {
            var groupName = $"site:{orgId}:{siteId}";

            await @event switch
            {
                OrderOpenedEvent e => _hubContext.Clients.Group(groupName)
                    .SendAsync("OrderOpened", MapToDto(e)),

                OrderSettledEvent e => _hubContext.Clients.Group(groupName)
                    .SendAsync("OrderSettled", MapToDto(e)),

                KitchenTicketCreatedEvent e => _hubContext.Clients
                    .Group($"kitchen:{orgId}:{siteId}")
                    .SendAsync("NewTicket", MapToDto(e)),

                _ => Task.CompletedTask
            };
        });
    }
}
```

---

## Deployment & Operations

### Silo Configuration

```csharp
var builder = Host.CreateDefaultBuilder(args)
    .UseOrleans((context, siloBuilder) =>
    {
        var config = context.Configuration;

        siloBuilder
            // Clustering
            .UseKubernetesClustering(options =>
            {
                options.Namespace = "darkvelocity";
            })

            // Persistence (event store)
            .AddEventSourcedGrains(options =>
            {
                options.UsePostgres(config.GetConnectionString("EventStore"));
            })

            // Grain state persistence
            .AddPostgreSqlGrainStorage("GrainState", options =>
            {
                options.ConnectionString = config.GetConnectionString("GrainState");
            })

            // Streaming
            .AddKafkaStreams("DomainEvents", options =>
            {
                options.BrokerList = config["Kafka:Brokers"];
                options.ConsumerGroupId = "orleans-silo";
            })

            // Reminders for scheduled tasks
            .UsePostgreSqlReminderService(options =>
            {
                options.ConnectionString = config.GetConnectionString("Reminders");
            })

            // Dashboard
            .UseDashboard(options =>
            {
                options.Port = 8080;
            });
    })
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.UseStartup<Startup>();
    });
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: darkvelocity-silo
  namespace: darkvelocity
spec:
  replicas: 3
  selector:
    matchLabels:
      app: darkvelocity-silo
  template:
    metadata:
      labels:
        app: darkvelocity-silo
        orleans/serviceId: darkvelocity
        orleans/clusterId: production
    spec:
      containers:
      - name: silo
        image: darkvelocity/silo:latest
        ports:
        - containerPort: 11111  # Silo-to-silo
        - containerPort: 30000  # Gateway
        - containerPort: 8080   # HTTP API
        env:
        - name: ORLEANS_SERVICE_ID
          value: "darkvelocity"
        - name: ORLEANS_CLUSTER_ID
          value: "production"
        - name: POD_NAMESPACE
          valueFrom:
            fieldRef:
              fieldPath: metadata.namespace
        - name: POD_NAME
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
        - name: POD_IP
          valueFrom:
            fieldRef:
              fieldPath: status.podIP
        resources:
          requests:
            memory: "2Gi"
            cpu: "1000m"
          limits:
            memory: "4Gi"
            cpu: "2000m"
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 10
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 30
---
apiVersion: v1
kind: Service
metadata:
  name: darkvelocity-api
  namespace: darkvelocity
spec:
  selector:
    app: darkvelocity-silo
  ports:
  - name: http
    port: 80
    targetPort: 8080
  type: ClusterIP
---
apiVersion: v1
kind: Service
metadata:
  name: darkvelocity-silo
  namespace: darkvelocity
spec:
  selector:
    app: darkvelocity-silo
  ports:
  - name: silo
    port: 11111
  - name: gateway
    port: 30000
  clusterIP: None  # Headless for direct pod-to-pod
```

### Monitoring

```csharp
// Grain telemetry
siloBuilder.ConfigureServices(services =>
{
    services.AddOpenTelemetry()
        .WithMetrics(metrics =>
        {
            metrics
                .AddOrleansMeter()
                .AddPrometheusExporter();
        })
        .WithTracing(tracing =>
        {
            tracing
                .AddOrleansInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter();
        });
});
```

---

## Migration Strategy

### Phase 1: Foundation (Weeks 1-4)

1. Set up Orleans silo infrastructure
2. Implement core grain base classes (event sourcing)
3. Set up SpiceDB and define authorization schema
4. Create Organization, Site, User grains
5. HTTP API routing to grains

### Phase 2: Order Flow (Weeks 5-8)

1. Order grain with full lifecycle
2. Payment grain
3. Kitchen grain
4. Table grain
5. SignalR integration

### Phase 3: Inventory & Procurement (Weeks 9-12)

1. Inventory grain (FIFO logic)
2. Recipe grain
3. Purchase order grain
4. Delivery grain
5. Stock consumption integration

### Phase 4: Customer & Loyalty (Weeks 13-16)

1. Customer grain with loyalty
2. Gift card grain
3. Booking grain
4. Points/rewards integration

### Phase 5: Reporting & Accounting (Weeks 17-20)

1. Accounting ledger grain
2. Reporting/projection grains
3. Daily report grain
4. COGS calculations

### Phase 6: Migration & Cutover (Weeks 21-24)

1. Data migration scripts
2. Parallel running (old + new)
3. Feature flag cutover
4. Decommission old services

### Migration Script Pattern

```csharp
public class OrderMigrationJob
{
    private readonly IDbConnection _legacyDb;
    private readonly IGrainFactory _grainFactory;

    public async Task MigrateOrdersAsync(Guid orgId, Guid siteId, DateRange range)
    {
        var orders = await _legacyDb.QueryAsync<LegacyOrder>(
            "SELECT * FROM orders WHERE site_id = @SiteId AND created_at BETWEEN @Start AND @End",
            new { SiteId = siteId, Start = range.Start, End = range.End });

        foreach (var legacyOrder in orders)
        {
            var orderGrain = _grainFactory.GetGrain<IOrderGrain>(
                GrainKeys.OrgEntity(orgId, "order", legacyOrder.Id));

            // Replay events to rebuild state
            await orderGrain.ReplayEventsAsync(ConvertToEvents(legacyOrder));
        }
    }

    private IEnumerable<IDomainEvent> ConvertToEvents(LegacyOrder order)
    {
        yield return new OrderOpenedEvent { /* ... */ };

        foreach (var line in order.Lines)
            yield return new OrderLineAddedEvent { /* ... */ };

        foreach (var payment in order.Payments)
            yield return new PaymentAppliedEvent { /* ... */ };

        if (order.Status == "settled")
            yield return new OrderSettledEvent { /* ... */ };
    }
}
```

---

## Trade-offs & Considerations

### Benefits

| Aspect | Benefit |
|--------|---------|
| **Scaling** | Automatic grain distribution; no manual shard management |
| **Consistency** | Single-writer per aggregate eliminates distributed transactions |
| **Latency** | Hot data in memory; no database round-trip for reads |
| **Deployment** | Single artifact vs 18 microservices |
| **Real-time** | Native streaming; no polling |
| **Debugging** | Grain activation traces; centralized logging |

### Costs

| Aspect | Cost |
|--------|------|
| **Learning curve** | Team needs Orleans expertise |
| **Grain design** | Identity and granularity decisions are critical |
| **Testing** | Requires Orleans test cluster |
| **Vendor lock-in** | Tied to Orleans (though open source) |
| **Memory** | Active grains consume RAM |
| **External events** | Still need Kafka for external consumers |

### When NOT to Use Orleans

- Simple CRUD with no business logic
- Batch processing jobs
- Heavy analytics/reporting (use dedicated data warehouse)
- Extremely high write throughput (>100k writes/sec per grain)

---

## Appendix: Event Type Catalog

Full list of event types for serialization/deserialization:

```csharp
public static class EventTypes
{
    // Organization
    public const string OrganizationCreated = "org.organization.created";
    public const string OrganizationUpdated = "org.organization.updated";
    public const string OrganizationSuspended = "org.organization.suspended";
    public const string OrganizationReactivated = "org.organization.reactivated";

    // Site
    public const string SiteCreated = "org.site.created";
    public const string SiteUpdated = "org.site.updated";
    public const string SiteStatusChanged = "org.site.status_changed";
    public const string MenuActivated = "org.site.menu_activated";

    // User
    public const string UserCreated = "auth.user.created";
    public const string UserUpdated = "auth.user.updated";
    public const string UserDeactivated = "auth.user.deactivated";
    public const string UserLoggedIn = "auth.user.logged_in";
    public const string UserLoggedOut = "auth.user.logged_out";
    public const string UserLoginFailed = "auth.user.login_failed";
    public const string SiteAccessGranted = "auth.user.site_access_granted";
    public const string SiteAccessRevoked = "auth.user.site_access_revoked";

    // Order
    public const string OrderOpened = "orders.order.opened";
    public const string OrderLineAdded = "orders.order.line_added";
    public const string OrderLineUpdated = "orders.order.line_updated";
    public const string OrderLineVoided = "orders.order.line_voided";
    public const string OrderDiscountApplied = "orders.order.discount_applied";
    public const string OrderSentToKitchen = "orders.order.sent_to_kitchen";
    public const string PaymentApplied = "orders.order.payment_applied";
    public const string OrderSettled = "orders.order.settled";
    public const string OrderVoided = "orders.order.voided";
    public const string OrderReopened = "orders.order.reopened";
    public const string OrderTransferred = "orders.order.transferred";

    // Payment
    public const string PaymentInitiated = "payments.payment.initiated";
    public const string CashPaymentCompleted = "payments.payment.cash_completed";
    public const string CardPaymentAuthorized = "payments.payment.card_authorized";
    public const string CardPaymentCaptured = "payments.payment.card_captured";
    public const string CardPaymentDeclined = "payments.payment.card_declined";
    public const string PaymentVoided = "payments.payment.voided";
    public const string RefundIssued = "payments.payment.refund_issued";

    // Inventory
    public const string StockBatchReceived = "inventory.stock.batch_received";
    public const string StockConsumed = "inventory.stock.consumed";
    public const string StockWasted = "inventory.stock.wasted";
    public const string StockAdjusted = "inventory.stock.adjusted";
    public const string StockTransferredOut = "inventory.stock.transferred_out";
    public const string StockTransferredIn = "inventory.stock.transferred_in";
    public const string LowStockAlert = "inventory.stock.low_stock_alert";

    // Customer
    public const string CustomerCreated = "customers.customer.created";
    public const string CustomerUpdated = "customers.customer.updated";
    public const string CustomerDeleted = "customers.customer.deleted";
    public const string LoyaltyEnrolled = "customers.loyalty.enrolled";
    public const string PointsEarned = "customers.loyalty.points_earned";
    public const string PointsRedeemed = "customers.loyalty.points_redeemed";
    public const string PointsAdjusted = "customers.loyalty.points_adjusted";
    public const string TierChanged = "customers.loyalty.tier_changed";
    public const string RewardIssued = "customers.loyalty.reward_issued";
    public const string RewardRedeemed = "customers.loyalty.reward_redeemed";

    // Booking
    public const string BookingRequested = "bookings.booking.requested";
    public const string BookingConfirmed = "bookings.booking.confirmed";
    public const string BookingModified = "bookings.booking.modified";
    public const string BookingCancelled = "bookings.booking.cancelled";
    public const string GuestArrived = "bookings.booking.guest_arrived";
    public const string GuestSeated = "bookings.booking.guest_seated";
    public const string GuestDeparted = "bookings.booking.guest_departed";
    public const string BookingNoShow = "bookings.booking.no_show";
    public const string BookingLinkedToOrder = "bookings.booking.linked_to_order";

    // Gift Card
    public const string GiftCardIssued = "giftcards.card.issued";
    public const string GiftCardActivated = "giftcards.card.activated";
    public const string GiftCardRedeemed = "giftcards.card.redeemed";
    public const string GiftCardReloaded = "giftcards.card.reloaded";
    public const string GiftCardSuspended = "giftcards.card.suspended";
    public const string GiftCardResumed = "giftcards.card.resumed";
    public const string GiftCardExpired = "giftcards.card.expired";

    // Kitchen
    public const string KitchenTicketCreated = "kitchen.ticket.created";
    public const string KitchenItemStarted = "kitchen.ticket.item_started";
    public const string KitchenItemCompleted = "kitchen.ticket.item_completed";
    public const string KitchenTicketBumped = "kitchen.ticket.bumped";
    public const string KitchenTicketRecalled = "kitchen.ticket.recalled";

    // Accounting
    public const string JournalEntryPosted = "accounting.journal.posted";
    public const string JournalEntryReversed = "accounting.journal.reversed";
    public const string AccountingPeriodClosed = "accounting.period.closed";

    // Labor
    public const string EmployeeClockedIn = "labor.timecard.clocked_in";
    public const string EmployeeClockedOut = "labor.timecard.clocked_out";
    public const string BreakStarted = "labor.timecard.break_started";
    public const string BreakEnded = "labor.timecard.break_ended";

    // Sales Period
    public const string SalesPeriodOpened = "operations.period.opened";
    public const string SalesPeriodClosed = "operations.period.closed";
}
```

---

## References

- [Microsoft Orleans Documentation](https://learn.microsoft.com/en-us/dotnet/orleans/)
- [SpiceDB Documentation](https://authzed.com/docs)
- [Event Sourcing Pattern](https://martinfowler.com/eaaDev/EventSourcing.html)
- [Domain-Driven Design](https://www.domainlanguage.com/ddd/)
- [Google Zanzibar Paper](https://research.google/pubs/pub48190/)
