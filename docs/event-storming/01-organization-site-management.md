# Event Storming: Organization & Site Management Domain

## Overview

The Organization & Site Management domain is the foundational domain for the DarkVelocity POS platform. It handles multi-tenancy, venue management, and the hierarchical structure that governs all other domains. This domain establishes the tenant isolation boundaries and provides the configuration context for all business operations.

---

## Domain Purpose

- **Tenant Isolation**: Ensure complete data and operational separation between organizations
- **Hierarchical Management**: Model the Platform → Organization → Site → Floor → Table structure
- **Configuration Governance**: Manage settings that cascade down to operational domains
- **Lifecycle Management**: Handle the full lifecycle from onboarding to decommissioning

---

## Actors

| Actor | Description | Typical Actions |
|-------|-------------|-----------------|
| **Platform Admin** | DarkVelocity staff managing the SaaS platform | Create organizations, suspend accounts, manage billing |
| **Organization Owner** | Business owner/primary account holder | Configure org settings, add sites, manage billing |
| **Organization Admin** | Delegated administrator for the organization | Manage sites, users, settings |
| **Site Manager** | Manager of a specific venue/location | Configure site settings, manage operations |
| **Billing System** | Automated billing/payment processor | Trigger suspension, reactivation events |
| **Compliance System** | Automated compliance monitoring | Trigger suspension for violations |
| **Scheduler** | Time-based automation | Open/close sites, trigger reminders |

---

## Aggregates

### Organization Aggregate

The root aggregate for tenant management. Owns all organization-level configuration and lifecycle state.

```
Organization
├── Id: Guid
├── Name: string
├── Slug: string (URL-friendly identifier)
├── Status: OrganizationStatus
├── Billing: BillingInfo
├── Settings: OrganizationSettings
├── CreatedAt: DateTime
└── SuspensionInfo?: SuspensionInfo
```

**Invariants:**
- Slug must be unique across the platform
- Slug format: lowercase alphanumeric with hyphens
- At least one owner must exist
- Cannot be deleted while sites exist
- Suspended organizations cannot create new resources

### Site Aggregate

Represents a physical venue or location where business operations occur.

```
Site
├── Id: Guid
├── OrganizationId: Guid
├── Name: string
├── Code: string (short identifier like "DT01")
├── Address: Address
├── Timezone: string (IANA)
├── Currency: string (ISO 4217)
├── Locale: string (BCP 47)
├── TaxJurisdiction: TaxJurisdiction
├── OperatingHours: OperatingHours
├── Status: SiteStatus
├── Settings: SiteSettings
├── Floors: List<Floor>
└── Devices: List<DeviceInfo>
```

**Invariants:**
- Code must be unique within organization
- Timezone must be valid IANA timezone
- Currency must be valid ISO 4217 code
- Cannot be deleted while active orders exist
- Must belong to an active organization

### Floor Aggregate

Represents a physical floor or section within a site.

```
Floor
├── Id: Guid
├── SiteId: Guid
├── Name: string
├── DisplayOrder: int
├── Tables: List<Table>
└── Sections: List<Section>
```

### Table Entity

Represents a physical table within a floor.

```
Table
├── Id: Guid
├── FloorId: Guid
├── Number: string
├── Capacity: int
├── Status: TableStatus
├── Position: TablePosition (x, y coordinates for floor plan)
├── Shape: TableShape
└── AssignedServerId?: Guid
```

---

## Commands

### Organization Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `CreateOrganization` | Onboard a new tenant | Valid slug, owner email | Platform Admin |
| `UpdateOrganization` | Modify org settings | Org exists, not cancelled | Org Owner/Admin |
| `UpdateOrganizationSettings` | Change configuration | Org active | Org Owner/Admin |
| `SuspendOrganization` | Temporarily disable | Org active | Platform Admin, Billing System |
| `ReactivateOrganization` | Restore from suspension | Org suspended | Platform Admin |
| `CancelOrganization` | Permanently close account | No active sites | Org Owner, Platform Admin |
| `TransferOrganizationOwnership` | Change primary owner | Valid new owner | Org Owner |
| `UpdateBillingInfo` | Change billing details | Org exists | Org Owner |
| `UpgradePlan` | Change subscription tier | Org active | Org Owner |
| `DowngradePlan` | Reduce subscription tier | Org active, no feature conflicts | Org Owner |

### Site Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `CreateSite` | Add new venue | Org active, within plan limits | Org Admin |
| `UpdateSite` | Modify site details | Site exists | Site Manager |
| `UpdateSiteSettings` | Change site configuration | Site exists | Site Manager |
| `SetSiteStatus` | Change operational status | Site exists | Site Manager |
| `OpenSite` | Begin daily operations | Site exists, not permanently closed | Site Manager |
| `CloseSite` | End daily operations | Site open | Site Manager |
| `TemporarilyCloseSite` | Close for maintenance/holiday | Site not permanently closed | Site Manager |
| `PermanentlyCloseSite` | Decommission site | No active orders | Org Admin |
| `ActivateMenu` | Set active menu for site | Menu exists, site active | Site Manager |
| `SetDefaultPriceList` | Assign price list | Price list exists | Site Manager |
| `ConfigureOperatingHours` | Set business hours | Site exists | Site Manager |
| `ConfigureTaxJurisdiction` | Set tax rules | Site exists | Site Manager |

### Floor & Table Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `CreateFloor` | Add floor to site | Site exists | Site Manager |
| `UpdateFloor` | Modify floor details | Floor exists | Site Manager |
| `DeleteFloor` | Remove floor | No tables on floor | Site Manager |
| `CreateTable` | Add table to floor | Floor exists | Site Manager |
| `UpdateTable` | Modify table details | Table exists | Site Manager |
| `DeleteTable` | Remove table | Table not occupied | Site Manager |
| `MoveTable` | Change table position | Table exists | Site Manager |
| `AssignServerToTable` | Set server for table | Table exists, server valid | Site Manager |
| `UnassignServer` | Remove server from table | Table exists | Site Manager |
| `CreateSection` | Define table grouping | Floor exists | Site Manager |
| `AssignTablesToSection` | Add tables to section | Tables and section exist | Site Manager |

---

## Domain Events

### Organization Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `OrganizationCreated` | New tenant onboarded | OrgId, Name, Slug, OwnerEmail, Plan | CreateOrganization |
| `OrganizationUpdated` | Basic info changed | OrgId, Name, ChangedFields | UpdateOrganization |
| `OrganizationSettingsUpdated` | Configuration changed | OrgId, OldSettings, NewSettings | UpdateOrganizationSettings |
| `OrganizationSuspended` | Account temporarily disabled | OrgId, Reason, SuspendedBy, SuspendedAt | SuspendOrganization |
| `OrganizationReactivated` | Suspension lifted | OrgId, ReactivatedBy, ReactivatedAt | ReactivateOrganization |
| `OrganizationCancelled` | Account permanently closed | OrgId, Reason, CancelledAt, DataRetentionDate | CancelOrganization |
| `OrganizationOwnershipTransferred` | Primary owner changed | OrgId, OldOwnerId, NewOwnerId | TransferOrganizationOwnership |
| `BillingInfoUpdated` | Billing details changed | OrgId, OldBilling (masked), NewBilling (masked) | UpdateBillingInfo |
| `PlanUpgraded` | Subscription tier increased | OrgId, OldPlan, NewPlan, EffectiveDate | UpgradePlan |
| `PlanDowngraded` | Subscription tier decreased | OrgId, OldPlan, NewPlan, EffectiveDate | DowngradePlan |

### Site Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `SiteCreated` | New venue added | SiteId, OrgId, Name, Code, Address, Timezone | CreateSite |
| `SiteUpdated` | Site details changed | SiteId, ChangedFields | UpdateSite |
| `SiteSettingsUpdated` | Configuration changed | SiteId, OldSettings, NewSettings | UpdateSiteSettings |
| `SiteOpened` | Daily operations started | SiteId, OpenedBy, OpenedAt | OpenSite |
| `SiteClosed` | Daily operations ended | SiteId, ClosedBy, ClosedAt | CloseSite |
| `SiteTemporarilyClosed` | Temporary closure | SiteId, Reason, ExpectedReopenDate | TemporarilyCloseSite |
| `SitePermanentlyClosed` | Decommissioned | SiteId, Reason, ClosedAt, DataArchiveDate | PermanentlyCloseSite |
| `SiteStatusChanged` | Operational status updated | SiteId, OldStatus, NewStatus | SetSiteStatus |
| `MenuActivated` | Active menu set | SiteId, MenuId, ActivatedBy | ActivateMenu |
| `DefaultPriceListSet` | Price list assigned | SiteId, PriceListId | SetDefaultPriceList |
| `OperatingHoursConfigured` | Business hours set | SiteId, OldHours, NewHours | ConfigureOperatingHours |
| `TaxJurisdictionConfigured` | Tax rules set | SiteId, TaxJurisdiction | ConfigureTaxJurisdiction |

### Floor & Table Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `FloorCreated` | Floor added to site | FloorId, SiteId, Name | CreateFloor |
| `FloorUpdated` | Floor details changed | FloorId, ChangedFields | UpdateFloor |
| `FloorDeleted` | Floor removed | FloorId, SiteId | DeleteFloor |
| `TableCreated` | Table added | TableId, FloorId, Number, Capacity | CreateTable |
| `TableUpdated` | Table details changed | TableId, ChangedFields | UpdateTable |
| `TableDeleted` | Table removed | TableId, FloorId | DeleteTable |
| `TableMoved` | Position changed | TableId, OldPosition, NewPosition | MoveTable |
| `ServerAssignedToTable` | Server set | TableId, ServerId | AssignServerToTable |
| `ServerUnassignedFromTable` | Server removed | TableId, OldServerId | UnassignServer |
| `SectionCreated` | Table grouping created | SectionId, FloorId, Name | CreateSection |
| `TablesAssignedToSection` | Tables added to section | SectionId, TableIds | AssignTablesToSection |

---

## Event Details

### OrganizationCreated

```csharp
public record OrganizationCreated : DomainEvent
{
    public override string EventType => "org.organization.created";

    public required Guid OrganizationId { get; init; }
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public required string OwnerEmail { get; init; }
    public required string OwnerId { get; init; }
    public required string Plan { get; init; }
    public required string DefaultCurrency { get; init; }
    public required string DefaultTimezone { get; init; }
    public required string DefaultLocale { get; init; }
    public required OrganizationSettings Settings { get; init; }
}
```

### OrganizationSuspended

```csharp
public record OrganizationSuspended : DomainEvent
{
    public override string EventType => "org.organization.suspended";

    public required Guid OrganizationId { get; init; }
    public required SuspensionReason Reason { get; init; }
    public required string ReasonDetails { get; init; }
    public required Guid SuspendedBy { get; init; }
    public required DateTime SuspendedAt { get; init; }
    public DateTime? AutoReactivationDate { get; init; }
}

public enum SuspensionReason
{
    PaymentFailure,
    TermsViolation,
    SecurityConcern,
    ComplianceIssue,
    CustomerRequest,
    Other
}
```

### SiteCreated

```csharp
public record SiteCreated : DomainEvent
{
    public override string EventType => "org.site.created";

    public required Guid SiteId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required string Name { get; init; }
    public required string Code { get; init; }
    public required Address Address { get; init; }
    public required string Timezone { get; init; }
    public required string Currency { get; init; }
    public required string Locale { get; init; }
    public required TaxJurisdiction TaxJurisdiction { get; init; }
    public required Guid CreatedBy { get; init; }
}
```

### SiteOpened

```csharp
public record SiteOpened : DomainEvent
{
    public override string EventType => "org.site.opened";

    public required Guid SiteId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required Guid OpenedBy { get; init; }
    public required DateTime OpenedAt { get; init; }
    public required Guid SalesPeriodId { get; init; }
    public decimal? OpeningCashFloat { get; init; }
}
```

---

## Policies (Event Reactions)

### When OrganizationCreated

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Create Owner User | Create user record for owner email | Identity |
| Assign Owner Role | Grant owner permissions in SpiceDB | Authorization |
| Initialize Billing | Set up subscription in billing system | Billing |
| Send Welcome Email | Notify owner of successful signup | Notifications |
| Create Audit Trail | Log organization creation | Audit |

### When OrganizationSuspended

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Disable All Users | Prevent logins for org users | Identity |
| Pause Active Operations | Close all open orders (with warning) | Orders |
| Block New Transactions | Prevent new orders, payments | Orders, Payments |
| Notify Administrators | Alert org admins of suspension | Notifications |
| Schedule Data Freeze | Prepare for potential cancellation | Platform |

### When OrganizationReactivated

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Re-enable Users | Allow logins again | Identity |
| Resume Operations | Allow new transactions | Orders, Payments |
| Notify Administrators | Alert of reactivation | Notifications |
| Update Billing Status | Clear billing holds | Billing |

### When OrganizationCancelled

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Deactivate All Users | Permanently disable user accounts | Identity |
| Close All Sites | Mark all sites as permanently closed | Sites |
| Schedule Data Deletion | Plan data removal per retention policy | Platform |
| Export Customer Data | Prepare data export if requested | Data |
| Final Invoice | Generate final billing statement | Billing |

### When SiteCreated

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Create Default Floor | Add "Main Floor" with default tables | Floors |
| Assign Site Relationships | Set up SpiceDB relationships | Authorization |
| Initialize Inventory | Create inventory tracking for site | Inventory |
| Initialize Cash Drawers | Set up default drawer | Payments |
| Notify Org Admins | Alert of new site | Notifications |

### When SiteOpened

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Create Sales Period | Initialize daily sales tracking | Sales |
| Reset Table States | Mark all tables as available | Tables |
| Enable Ordering | Allow new orders for site | Orders |
| Start Inventory Tracking | Begin daily consumption tracking | Inventory |
| Clock In Auto-Scheduled | Start shifts for scheduled staff | Labor |

### When SiteClosed

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Close Sales Period | Finalize daily sales | Sales |
| Settle Open Orders | Warn or auto-close open orders | Orders |
| Generate EOD Report | Create end-of-day summary | Reporting |
| Reconcile Cash Drawers | Require till count | Payments |
| Clock Out Active Staff | End active shifts | Labor |

---

## Read Models / Projections

### OrganizationSummary

```csharp
public record OrganizationSummary
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string Slug { get; init; }
    public OrganizationStatus Status { get; init; }
    public string Plan { get; init; }
    public int SiteCount { get; init; }
    public int ActiveUserCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? SuspendedAt { get; init; }
}
```

### SiteOperationalView

```csharp
public record SiteOperationalView
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string Code { get; init; }
    public SiteStatus Status { get; init; }
    public bool IsOpen { get; init; }
    public Guid? ActiveSalesPeriodId { get; init; }
    public Guid? ActiveMenuId { get; init; }
    public int OpenOrderCount { get; init; }
    public int OccupiedTableCount { get; init; }
    public int TotalTableCount { get; init; }
    public int ActiveStaffCount { get; init; }
    public string LocalTime { get; init; }
}
```

### FloorPlanView

```csharp
public record FloorPlanView
{
    public Guid FloorId { get; init; }
    public string Name { get; init; }
    public int DisplayOrder { get; init; }
    public IReadOnlyList<TableView> Tables { get; init; }
    public IReadOnlyList<SectionView> Sections { get; init; }
}

public record TableView
{
    public Guid Id { get; init; }
    public string Number { get; init; }
    public int Capacity { get; init; }
    public TableStatus Status { get; init; }
    public TablePosition Position { get; init; }
    public Guid? CurrentOrderId { get; init; }
    public Guid? AssignedServerId { get; init; }
    public string? AssignedServerName { get; init; }
    public int? CurrentGuestCount { get; init; }
    public TimeSpan? SeatedDuration { get; init; }
}
```

### OrganizationDashboard

```csharp
public record OrganizationDashboard
{
    public Guid OrganizationId { get; init; }
    public IReadOnlyList<SiteSummary> Sites { get; init; }
    public decimal TodayRevenue { get; init; }
    public decimal ThisMonthRevenue { get; init; }
    public int TodayOrderCount { get; init; }
    public int ActiveStaffAcrossAllSites { get; init; }
    public IReadOnlyList<AlertSummary> Alerts { get; init; }
}
```

---

## Bounded Context Relationships

### Upstream Contexts (This domain provides data to)

| Context | Relationship | Data Provided |
|---------|--------------|---------------|
| Identity & Access | Published Language | Organization ID, Site ID for user scoping |
| Orders | Published Language | Site configuration, timezone, currency |
| Payments | Published Language | Site currency, tax jurisdiction |
| Inventory | Published Language | Site ID for stock location |
| Booking | Published Language | Site capacity, operating hours |
| Reporting | Published Language | Org/Site hierarchy for aggregation |
| Accounting | Published Language | Site as cost center |

### Downstream Contexts (This domain consumes from)

| Context | Relationship | Data Consumed |
|---------|--------------|---------------|
| Billing (External) | Conformist | Plan limits, payment status |
| Authentication (External) | Conformist | User identity validation |

### Anti-Corruption Layer

```csharp
// Translate external billing events to domain events
public class BillingEventTranslator
{
    public async Task HandlePaymentFailed(ExternalPaymentFailedEvent external)
    {
        // Translate to domain command
        await _organizationGrain.SuspendAsync(
            SuspensionReason.PaymentFailure,
            $"Payment failed: {external.FailureCode}");
    }

    public async Task HandlePaymentSucceeded(ExternalPaymentSucceededEvent external)
    {
        if (await _organizationGrain.IsSuspendedAsync())
        {
            await _organizationGrain.ReactivateAsync();
        }
    }
}
```

---

## Process Flows

### Organization Onboarding Flow

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│    Platform     │     │  Organization   │     │     Identity    │
│     Admin       │     │     Grain       │     │     Domain      │
└────────┬────────┘     └────────┬────────┘     └────────┬────────┘
         │                       │                       │
         │ CreateOrganization    │                       │
         │──────────────────────>│                       │
         │                       │                       │
         │                       │ OrganizationCreated   │
         │                       │──────────────────────>│
         │                       │                       │
         │                       │     Create Owner      │
         │                       │<──────────────────────│
         │                       │                       │
         │                       │ UserCreated           │
         │                       │──────────────────────>│
         │                       │                       │
         │   OrgId + Credentials │                       │
         │<──────────────────────│                       │
         │                       │                       │
```

### Site Daily Operations Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│    Site     │   │   Sales     │   │   Tables    │   │   Orders    │
│   Manager   │   │   Period    │   │             │   │             │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ OpenSite        │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ SalesPeriodOpened                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ Reset Tables    │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ Enable Orders   │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │                         ... Day Operations ...      │
       │                 │                 │                 │
       │ CloseSite       │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ SalesPeriodClosed                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ Warn Open Orders│                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
```

---

## Business Rules

### Organization Rules

1. **Slug Uniqueness**: Organization slugs must be globally unique and follow the pattern `^[a-z0-9][a-z0-9-]*[a-z0-9]$`
2. **Owner Requirement**: Every organization must have at least one owner at all times
3. **Suspension Cascade**: Suspended organizations cannot create new resources in any domain
4. **Cancellation Prerequisites**: Organizations can only be cancelled when all sites are closed and no outstanding invoices exist
5. **Plan Limits**: Number of sites, users, and features are constrained by subscription plan

### Site Rules

1. **Code Uniqueness**: Site codes must be unique within an organization
2. **Timezone Validity**: Timezone must be a valid IANA timezone identifier
3. **Currency Immutability**: Site currency cannot be changed once orders have been created
4. **Operating Hours Validation**: Opening time must be before closing time (or span midnight)
5. **Closure Prerequisites**: Sites can only be permanently closed when no active orders exist

### Table Rules

1. **Number Uniqueness**: Table numbers must be unique within a floor
2. **Capacity Limits**: Table capacity must be between 1 and 99
3. **Deletion Prerequisites**: Tables can only be deleted when not occupied
4. **Server Assignment**: Only active staff members can be assigned to tables

---

## Hotspots & Risks

### High Complexity Areas

| Area | Complexity | Mitigation |
|------|------------|------------|
| **Timezone Handling** | DateTime operations across timezones | Use NodaTime, always store UTC, convert at display |
| **Suspension Cascade** | Blocking operations across all domains | Use event-driven coordination, clear state machine |
| **Plan Limit Enforcement** | Checking limits before resource creation | Implement at command handler level, not grain level |
| **Data Retention** | GDPR/compliance requirements | Clear data lifecycle policies, automated deletion |

### Performance Considerations

| Concern | Impact | Strategy |
|---------|--------|----------|
| **Floor Plan Rendering** | Frequent reads during operations | Cache in frontend, subscribe to changes via SignalR |
| **Table Status Updates** | High frequency during peak hours | Use Orleans streams for real-time updates |
| **Site Configuration Reads** | Every order/payment needs site config | Cache site settings in memory, invalidate on update |

### Security Considerations

| Concern | Risk | Mitigation |
|---------|------|------------|
| **Cross-Tenant Access** | Data leakage between orgs | Grain key prefixing, SpiceDB validation |
| **Suspension Bypass** | Accessing suspended org data | Check org status in middleware |
| **Setting Tampering** | Unauthorized config changes | Audit trail, permission checks |

---

## Event Type Registry

```csharp
public static class OrganizationEventTypes
{
    // Organization Lifecycle
    public const string OrganizationCreated = "org.organization.created";
    public const string OrganizationUpdated = "org.organization.updated";
    public const string OrganizationSettingsUpdated = "org.organization.settings_updated";
    public const string OrganizationSuspended = "org.organization.suspended";
    public const string OrganizationReactivated = "org.organization.reactivated";
    public const string OrganizationCancelled = "org.organization.cancelled";
    public const string OrganizationOwnershipTransferred = "org.organization.ownership_transferred";

    // Billing
    public const string BillingInfoUpdated = "org.billing.updated";
    public const string PlanUpgraded = "org.billing.plan_upgraded";
    public const string PlanDowngraded = "org.billing.plan_downgraded";

    // Site Lifecycle
    public const string SiteCreated = "org.site.created";
    public const string SiteUpdated = "org.site.updated";
    public const string SiteSettingsUpdated = "org.site.settings_updated";
    public const string SiteOpened = "org.site.opened";
    public const string SiteClosed = "org.site.closed";
    public const string SiteTemporarilyClosed = "org.site.temporarily_closed";
    public const string SitePermanentlyClosed = "org.site.permanently_closed";
    public const string SiteStatusChanged = "org.site.status_changed";

    // Site Configuration
    public const string MenuActivated = "org.site.menu_activated";
    public const string DefaultPriceListSet = "org.site.default_price_list_set";
    public const string OperatingHoursConfigured = "org.site.operating_hours_configured";
    public const string TaxJurisdictionConfigured = "org.site.tax_jurisdiction_configured";

    // Floor & Table
    public const string FloorCreated = "org.floor.created";
    public const string FloorUpdated = "org.floor.updated";
    public const string FloorDeleted = "org.floor.deleted";
    public const string TableCreated = "org.table.created";
    public const string TableUpdated = "org.table.updated";
    public const string TableDeleted = "org.table.deleted";
    public const string TableMoved = "org.table.moved";
    public const string ServerAssignedToTable = "org.table.server_assigned";
    public const string ServerUnassignedFromTable = "org.table.server_unassigned";
    public const string SectionCreated = "org.section.created";
    public const string TablesAssignedToSection = "org.section.tables_assigned";
}
```

---

## Integration Points

### SpiceDB Relationships Created

```zed
// When OrganizationCreated
organization:{orgId}#owner@user:{ownerId}
organization:{orgId}#member@user:{ownerId}

// When SiteCreated
site:{siteId}#organization@organization:{orgId}

// When UserAssignedToSite
site:{siteId}#staff@user:{userId}

// When TableCreated
table:{tableId}#site@site:{siteId}

// When ServerAssignedToTable
table:{tableId}#assigned_server@user:{serverId}
```

### External System Integration

| System | Direction | Events/Data |
|--------|-----------|-------------|
| **Stripe/Billing** | Inbound | Payment success/failure → Suspension/Reactivation |
| **Auth0/Identity** | Outbound | Organization creation → Tenant provisioning |
| **SendGrid/Email** | Outbound | Welcome emails, suspension notices |
| **Segment/Analytics** | Outbound | Organization/Site events for analytics |
