# Event Storming: Identity & Access Management Domain

## Overview

The Identity & Access Management (IAM) domain handles user authentication, authorization, session management, and access control for the DarkVelocity POS platform. This domain integrates with SpiceDB for fine-grained relationship-based access control (ReBAC) and manages user lifecycles across the multi-tenant hierarchy.

---

## Domain Purpose

- **Authentication**: Verify user identity through multiple mechanisms (email/password, PIN, QR code)
- **Authorization**: Enforce permissions based on user roles, group memberships, and resource relationships
- **Session Management**: Track active sessions, handle login/logout, enforce security policies
- **User Lifecycle**: Manage user creation, modification, deactivation, and access assignments
- **Group Management**: Organize users into groups for permission assignment and scheduling

---

## Actors

| Actor | Description | Typical Actions |
|-------|-------------|-----------------|
| **Organization Admin** | Administrator for the organization | Create users, manage groups, assign permissions |
| **Site Manager** | Manager of a specific venue | Grant site access, manage staff |
| **User** | Any authenticated person | Login, logout, change PIN, update profile |
| **Employee** | Staff member at a site | Clock in via PIN/QR, access POS |
| **System** | Automated processes | Lock accounts, expire sessions, audit |
| **Security Monitor** | Automated security system | Detect threats, lock accounts |

---

## Aggregates

### User Aggregate

The primary aggregate for user identity and access state.

```
User
├── Id: Guid
├── OrganizationId: Guid
├── Email: string
├── DisplayName: string
├── Pin?: string (hashed)
├── QrToken?: string
├── Status: UserStatus
├── Type: UserType
├── SiteAccess: List<Guid>
├── GroupMemberships: List<Guid>
├── Preferences: UserPreferences
├── SecurityInfo: SecurityInfo
└── Sessions: List<Session>
```

**Invariants:**
- Email must be unique within organization
- PIN must be 4-6 digits when set
- At least one authentication method must be active
- Cannot deactivate the last organization owner
- Cannot have more than 5 active sessions (configurable)

### UserGroup Aggregate

Groups for organizing users and assigning permissions collectively.

```
UserGroup
├── Id: Guid
├── OrganizationId: Guid
├── Name: string
├── Description?: string
├── MemberIds: List<Guid>
├── IsSystemGroup: bool
├── Permissions: List<Permission>
└── CreatedAt: DateTime
```

**Invariants:**
- Name must be unique within organization
- System groups cannot be deleted
- Cannot remove all members from a required group

### Session Aggregate

Represents an active authentication session.

```
Session
├── Id: Guid
├── UserId: Guid
├── DeviceInfo: DeviceInfo
├── IpAddress: string
├── AuthMethod: AuthMethod
├── CreatedAt: DateTime
├── LastActivityAt: DateTime
├── ExpiresAt: DateTime
└── Status: SessionStatus
```

---

## Commands

### User Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `CreateUser` | Create new user account | Org active, email unique | Org Admin |
| `UpdateUser` | Modify user profile | User exists | User, Org Admin |
| `DeactivateUser` | Disable user account | User active, not last owner | Org Admin |
| `ReactivateUser` | Re-enable user account | User deactivated | Org Admin |
| `DeleteUser` | Permanently remove user | User deactivated, retention met | Org Admin |
| `ChangePassword` | Update password | User exists | User |
| `ChangePin` | Update PIN code | User exists | User |
| `ResetPassword` | Admin password reset | User exists | Org Admin |
| `GenerateQrToken` | Create QR login code | User exists | User, Site Manager |
| `RevokeQrToken` | Invalidate QR code | QR token exists | User, Site Manager |
| `SetUserType` | Change user role | User exists | Org Admin |
| `UpdatePreferences` | Change user preferences | User exists | User |

### Session Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `Login` | Authenticate and create session | Valid credentials | User |
| `LoginWithPin` | Authenticate via PIN | Valid PIN, site access | Employee |
| `LoginWithQr` | Authenticate via QR scan | Valid QR token | Employee |
| `Logout` | End session | Active session | User |
| `LogoutAllSessions` | End all user sessions | User exists | User, Org Admin |
| `RefreshSession` | Extend session | Session active | System |
| `TerminateSession` | Force end specific session | Session exists | User, Org Admin |

### Access Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `GrantSiteAccess` | Give user site access | User and site exist | Org Admin, Site Manager |
| `RevokeSiteAccess` | Remove site access | User has access | Org Admin, Site Manager |
| `AddToGroup` | Add user to group | User and group exist | Org Admin |
| `RemoveFromGroup` | Remove from group | User in group | Org Admin |
| `TransferOwnership` | Change org owner | Current owner | Org Owner |

### Group Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `CreateUserGroup` | Create new group | Name unique | Org Admin |
| `UpdateUserGroup` | Modify group | Group exists, not system | Org Admin |
| `DeleteUserGroup` | Remove group | Not system, no dependencies | Org Admin |
| `AssignGroupToSite` | Link group to site | Group and site exist | Org Admin |
| `AddGroupPermission` | Grant permission to group | Group exists | Org Admin |
| `RemoveGroupPermission` | Revoke group permission | Permission exists | Org Admin |

### Security Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `LockAccount` | Prevent all access | User exists | System, Org Admin |
| `UnlockAccount` | Restore access | Account locked | Org Admin |
| `EnableMfa` | Activate 2FA | User exists | User |
| `DisableMfa` | Deactivate 2FA | MFA enabled | User, Org Admin |
| `VerifyMfa` | Validate 2FA code | MFA enabled | User |

---

## Domain Events

### User Lifecycle Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `UserCreated` | New user account created | UserId, OrgId, Email, Type, CreatedBy | CreateUser |
| `UserUpdated` | Profile information changed | UserId, ChangedFields | UpdateUser |
| `UserDeactivated` | Account disabled | UserId, Reason, DeactivatedBy | DeactivateUser |
| `UserReactivated` | Account re-enabled | UserId, ReactivatedBy | ReactivateUser |
| `UserDeleted` | Account permanently removed | UserId, DeletedBy | DeleteUser |
| `UserTypeChanged` | Role changed | UserId, OldType, NewType | SetUserType |
| `UserPreferencesUpdated` | Preferences changed | UserId, Preferences | UpdatePreferences |

### Authentication Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `UserLoggedIn` | Successful authentication | UserId, SessionId, Method, DeviceInfo, IpAddress | Login, LoginWithPin, LoginWithQr |
| `UserLoggedOut` | Session ended voluntarily | UserId, SessionId, Duration | Logout |
| `UserLoginFailed` | Authentication failed | Email/Pin, Reason, IpAddress, AttemptCount | Login attempt |
| `SessionTerminated` | Session forcibly ended | UserId, SessionId, Reason, TerminatedBy | TerminateSession |
| `AllSessionsTerminated` | All sessions ended | UserId, SessionCount, TerminatedBy | LogoutAllSessions |
| `SessionExpired` | Session timed out | UserId, SessionId, Duration | System |
| `SessionRefreshed` | Session extended | SessionId, NewExpiresAt | RefreshSession |

### Credential Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `PasswordChanged` | Password updated | UserId, ChangedAt | ChangePassword |
| `PasswordReset` | Password reset by admin | UserId, ResetBy | ResetPassword |
| `PinChanged` | PIN updated | UserId, ChangedAt | ChangePin |
| `QrTokenGenerated` | QR code created | UserId, TokenId, ExpiresAt | GenerateQrToken |
| `QrTokenRevoked` | QR code invalidated | UserId, TokenId, RevokedBy | RevokeQrToken |

### Access Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `SiteAccessGranted` | Site access given | UserId, SiteId, GrantedBy | GrantSiteAccess |
| `SiteAccessRevoked` | Site access removed | UserId, SiteId, RevokedBy | RevokeSiteAccess |
| `UserAddedToGroup` | Group membership added | UserId, GroupId, AddedBy | AddToGroup |
| `UserRemovedFromGroup` | Group membership removed | UserId, GroupId, RemovedBy | RemoveFromGroup |
| `OwnershipTransferred` | Org owner changed | OrgId, OldOwnerId, NewOwnerId | TransferOwnership |

### Group Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `UserGroupCreated` | New group created | GroupId, OrgId, Name, CreatedBy | CreateUserGroup |
| `UserGroupUpdated` | Group modified | GroupId, ChangedFields | UpdateUserGroup |
| `UserGroupDeleted` | Group removed | GroupId, DeletedBy | DeleteUserGroup |
| `GroupAssignedToSite` | Group linked to site | GroupId, SiteId | AssignGroupToSite |
| `GroupPermissionAdded` | Permission granted | GroupId, Permission | AddGroupPermission |
| `GroupPermissionRemoved` | Permission revoked | GroupId, Permission | RemoveGroupPermission |

### Security Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `AccountLocked` | Access prevented | UserId, Reason, LockedBy, UnlockAt | LockAccount |
| `AccountUnlocked` | Access restored | UserId, UnlockedBy | UnlockAccount |
| `MfaEnabled` | 2FA activated | UserId, Method | EnableMfa |
| `MfaDisabled` | 2FA deactivated | UserId, DisabledBy | DisableMfa |
| `MfaVerified` | 2FA code validated | UserId, SessionId | VerifyMfa |
| `SuspiciousActivityDetected` | Security anomaly | UserId, ActivityType, Details | System |
| `BruteForceDetected` | Multiple failed attempts | Email/IpAddress, AttemptCount | System |

---

## Event Details

### UserCreated

```csharp
public record UserCreated : DomainEvent
{
    public override string EventType => "auth.user.created";

    public required Guid UserId { get; init; }
    public required Guid OrganizationId { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required UserType Type { get; init; }
    public required Guid CreatedBy { get; init; }
    public IReadOnlyList<Guid>? InitialSiteAccess { get; init; }
    public IReadOnlyList<Guid>? InitialGroupMemberships { get; init; }
    public bool SendWelcomeEmail { get; init; }
}

public enum UserType
{
    Employee,
    Manager,
    Admin,
    Owner
}
```

### UserLoggedIn

```csharp
public record UserLoggedIn : DomainEvent
{
    public override string EventType => "auth.user.logged_in";

    public required Guid UserId { get; init; }
    public required Guid SessionId { get; init; }
    public required AuthMethod Method { get; init; }
    public required DeviceInfo DeviceInfo { get; init; }
    public required string IpAddress { get; init; }
    public required string UserAgent { get; init; }
    public Guid? SiteId { get; init; } // For PIN/QR logins at specific site
    public required DateTime LoginAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
}

public enum AuthMethod
{
    EmailPassword,
    Pin,
    QrCode,
    Sso,
    ApiKey
}

public record DeviceInfo
{
    public string DeviceId { get; init; }
    public string DeviceType { get; init; } // mobile, tablet, desktop, terminal
    public string Os { get; init; }
    public string Browser { get; init; }
    public string? DeviceName { get; init; }
}
```

### UserLoginFailed

```csharp
public record UserLoginFailed : DomainEvent
{
    public override string EventType => "auth.user.login_failed";

    public string? Email { get; init; }
    public string? Pin { get; init; } // Masked
    public required LoginFailureReason Reason { get; init; }
    public required string IpAddress { get; init; }
    public required string UserAgent { get; init; }
    public required int ConsecutiveFailures { get; init; }
    public bool AccountLocked { get; init; }
}

public enum LoginFailureReason
{
    InvalidCredentials,
    AccountDeactivated,
    AccountLocked,
    NoSiteAccess,
    SessionLimitExceeded,
    MfaRequired,
    MfaFailed,
    ExpiredCredentials,
    IpBlocked
}
```

### AccountLocked

```csharp
public record AccountLocked : DomainEvent
{
    public override string EventType => "auth.user.account_locked";

    public required Guid UserId { get; init; }
    public required LockReason Reason { get; init; }
    public required string ReasonDetails { get; init; }
    public Guid? LockedBy { get; init; } // null for system
    public required DateTime LockedAt { get; init; }
    public DateTime? UnlockAt { get; init; } // null for manual unlock
}

public enum LockReason
{
    TooManyFailedAttempts,
    SuspiciousActivity,
    AdminAction,
    ComplianceHold,
    SecurityIncident
}
```

---

## Policies (Event Reactions)

### When UserCreated

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Setup SpiceDB Relations | Create user relationships | Authorization |
| Send Welcome Email | Send onboarding email with temp password | Notifications |
| Create Audit Entry | Log user creation | Audit |
| Initialize Preferences | Set default user preferences | Settings |

### When UserLoggedIn

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Update Last Login | Track login timestamp | User Profile |
| Check Session Limit | Terminate oldest if limit exceeded | Sessions |
| Log Audit Entry | Record login for compliance | Audit |
| Update Device Registry | Track known devices | Security |

### When UserLoginFailed

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Increment Failure Count | Track consecutive failures | Security |
| Check Lock Threshold | Lock if threshold exceeded | Security |
| Send Alert if Suspicious | Notify on anomalous patterns | Notifications |
| Log Audit Entry | Record for security analysis | Audit |

### When AccountLocked

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Terminate All Sessions | End active sessions | Sessions |
| Send Notification | Alert user and admins | Notifications |
| Schedule Auto-Unlock | If temporary lock | Scheduler |
| Log Security Event | Record for investigation | Audit |

### When SiteAccessGranted

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Update SpiceDB | Add site staff relationship | Authorization |
| Notify User | Alert of new access | Notifications |
| Enable Scheduling | Allow shift assignment | Labor |
| Log Audit Entry | Record access grant | Audit |

### When SiteAccessRevoked

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Update SpiceDB | Remove site relationship | Authorization |
| Terminate Site Sessions | End sessions for that site | Sessions |
| Remove from Schedule | Unassign future shifts | Labor |
| Transfer Open Orders | Reassign to other server | Orders |

### When UserDeactivated

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Terminate All Sessions | End active sessions | Sessions |
| Remove SpiceDB Relations | Revoke all permissions | Authorization |
| Transfer Ownership | Reassign owned resources | Various |
| Archive Data | Prepare for retention | Archival |

---

## Read Models / Projections

### UserProfile

```csharp
public record UserProfile
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public string Email { get; init; }
    public string DisplayName { get; init; }
    public string? AvatarUrl { get; init; }
    public UserStatus Status { get; init; }
    public UserType Type { get; init; }
    public bool HasPin { get; init; }
    public bool HasQrToken { get; init; }
    public bool MfaEnabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
}
```

### UserAccessView

```csharp
public record UserAccessView
{
    public Guid UserId { get; init; }
    public IReadOnlyList<SiteAccessInfo> SiteAccess { get; init; }
    public IReadOnlyList<GroupMembershipInfo> Groups { get; init; }
    public IReadOnlyList<EffectivePermission> EffectivePermissions { get; init; }
}

public record SiteAccessInfo
{
    public Guid SiteId { get; init; }
    public string SiteName { get; init; }
    public string SiteCode { get; init; }
    public DateTime GrantedAt { get; init; }
    public Guid GrantedBy { get; init; }
}

public record GroupMembershipInfo
{
    public Guid GroupId { get; init; }
    public string GroupName { get; init; }
    public DateTime JoinedAt { get; init; }
}

public record EffectivePermission
{
    public string ResourceType { get; init; }
    public string Permission { get; init; }
    public string Source { get; init; } // "direct", "group:Managers", etc.
}
```

### ActiveSessionsView

```csharp
public record ActiveSessionsView
{
    public Guid UserId { get; init; }
    public IReadOnlyList<SessionInfo> Sessions { get; init; }
}

public record SessionInfo
{
    public Guid SessionId { get; init; }
    public DeviceInfo Device { get; init; }
    public string IpAddress { get; init; }
    public AuthMethod Method { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime LastActivityAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public bool IsCurrentSession { get; init; }
}
```

### UserSecurityView

```csharp
public record UserSecurityView
{
    public Guid UserId { get; init; }
    public UserStatus Status { get; init; }
    public bool IsLocked { get; init; }
    public LockReason? LockReason { get; init; }
    public DateTime? UnlockAt { get; init; }
    public int ConsecutiveFailedLogins { get; init; }
    public DateTime? LastFailedLoginAt { get; init; }
    public DateTime? LastPasswordChange { get; init; }
    public DateTime? LastPinChange { get; init; }
    public bool MfaEnabled { get; init; }
    public IReadOnlyList<RecentSecurityEvent> RecentEvents { get; init; }
}
```

### UserGroupView

```csharp
public record UserGroupView
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    public string? Description { get; init; }
    public bool IsSystemGroup { get; init; }
    public int MemberCount { get; init; }
    public IReadOnlyList<UserSummary> Members { get; init; }
    public IReadOnlyList<Permission> Permissions { get; init; }
    public IReadOnlyList<SiteSummary> AssignedSites { get; init; }
}
```

### OrganizationUsersView

```csharp
public record OrganizationUsersView
{
    public Guid OrganizationId { get; init; }
    public int TotalUsers { get; init; }
    public int ActiveUsers { get; init; }
    public int DeactivatedUsers { get; init; }
    public int LockedUsers { get; init; }
    public IReadOnlyList<UserSummary> Users { get; init; }
}

public record UserSummary
{
    public Guid Id { get; init; }
    public string Email { get; init; }
    public string DisplayName { get; init; }
    public UserStatus Status { get; init; }
    public UserType Type { get; init; }
    public int SiteCount { get; init; }
    public DateTime? LastLoginAt { get; init; }
}
```

---

## Bounded Context Relationships

### Upstream Contexts (This domain provides data to)

| Context | Relationship | Data Provided |
|---------|--------------|---------------|
| All Domains | Published Language | User identity, current user context |
| Orders | Published Language | Server identity for order ownership |
| Labor | Published Language | Employee identity for time tracking |
| Payments | Published Language | Cashier identity for payments |
| Audit | Published Language | User identity for audit trails |

### Downstream Contexts (This domain consumes from)

| Context | Relationship | Data Consumed |
|---------|--------------|---------------|
| Organization | Customer/Supplier | Org ID for user scoping, org status |
| Site | Customer/Supplier | Site ID for access grants |
| External Auth (Auth0/Okta) | Conformist | Identity verification, SSO |

### Anti-Corruption Layer

```csharp
// Translate external identity events
public class ExternalAuthTranslator
{
    public async Task HandleSsoLogin(SsoLoginResult result)
    {
        // Find or create user
        var user = await FindOrCreateFromSso(result);

        // Translate to internal login event
        await _userGrain.RecordLoginAsync(new LoginInfo
        {
            Method = AuthMethod.Sso,
            IpAddress = result.IpAddress,
            DeviceInfo = MapDeviceInfo(result),
            ExternalProvider = result.Provider,
            ExternalUserId = result.ExternalId
        });
    }
}
```

---

## Process Flows

### User Login Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│   Client    │   │  API Layer  │   │  UserGrain  │   │  SpiceDB    │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ POST /login     │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ Validate Creds  │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │                 │ Check Status    │
       │                 │                 │───────────────> │
       │                 │                 │                 │
       │                 │                 │<────────────────│
       │                 │                 │                 │
       │                 │ UserLoggedIn    │                 │
       │                 │<────────────────│                 │
       │                 │                 │                 │
       │                 │ Create JWT      │                 │
       │                 │                 │                 │
       │  Access Token   │                 │                 │
       │<────────────────│                 │                 │
       │                 │                 │                 │
```

### PIN Login Flow (Site-Specific)

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│  Terminal   │   │  API Layer  │   │  UserGrain  │   │  SiteGrain  │
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ PIN + SiteId    │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ Lookup by PIN   │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │                 │ Check Site Open │
       │                 │                 │───────────────> │
       │                 │                 │                 │
       │                 │                 │<────────────────│
       │                 │                 │                 │
       │                 │ Verify Access   │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │ UserLoggedIn    │                 │
       │                 │<────────────────│                 │
       │                 │                 │                 │
       │  Session Token  │                 │                 │
       │<────────────────│                 │                 │
       │                 │                 │                 │
```

### Account Lockout Flow

```
┌─────────────┐   ┌─────────────┐   ┌─────────────┐   ┌─────────────┐
│   Client    │   │  UserGrain  │   │  Security   │   │ Notifications│
└──────┬──────┘   └──────┬──────┘   └──────┬──────┘   └──────┬──────┘
       │                 │                 │                 │
       │ Bad Login x5    │                 │                 │
       │────────────────>│                 │                 │
       │                 │                 │                 │
       │                 │ LoginFailed     │                 │
       │                 │────────────────>│                 │
       │                 │                 │                 │
       │                 │                 │ Threshold Check │
       │                 │                 │────┐            │
       │                 │                 │    │            │
       │                 │                 │<───┘            │
       │                 │                 │                 │
       │                 │ LockAccount     │                 │
       │                 │<────────────────│                 │
       │                 │                 │                 │
       │                 │ AccountLocked   │                 │
       │                 │────────────────────────────────>│
       │                 │                 │                 │
       │                 │                 │    Alert Email  │
       │                 │                 │                 │
       │   Locked Error  │                 │                 │
       │<────────────────│                 │                 │
       │                 │                 │                 │
```

---

## SpiceDB Integration

### Schema Definitions

```zed
definition user {
    relation self: user
}

definition organization {
    relation owner: user
    relation admin: user | user_group#member
    relation member: user | user_group#member

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

    permission admin = organization->admin
    permission manage = admin + manager
    permission access = manage + staff
    permission view = access
}
```

### Relationship Management

```csharp
public class SpiceDbUserService
{
    private readonly ISpiceDbClient _spiceDb;

    // When user is created
    public async Task OnUserCreated(UserCreated @event)
    {
        var updates = new List<RelationshipUpdate>
        {
            // Add to organization
            new RelationshipUpdate
            {
                Operation = Operation.Touch,
                Relationship = new Relationship
                {
                    Resource = new ObjectReference { Type = "organization", Id = @event.OrganizationId.ToString() },
                    Relation = "member",
                    Subject = new SubjectReference { Type = "user", Id = @event.UserId.ToString() }
                }
            }
        };

        // If admin/owner, add those relations
        if (@event.Type == UserType.Admin || @event.Type == UserType.Owner)
        {
            updates.Add(new RelationshipUpdate
            {
                Operation = Operation.Touch,
                Relationship = new Relationship
                {
                    Resource = new ObjectReference { Type = "organization", Id = @event.OrganizationId.ToString() },
                    Relation = @event.Type == UserType.Owner ? "owner" : "admin",
                    Subject = new SubjectReference { Type = "user", Id = @event.UserId.ToString() }
                }
            });
        }

        // Add initial site access
        foreach (var siteId in @event.InitialSiteAccess ?? [])
        {
            updates.Add(new RelationshipUpdate
            {
                Operation = Operation.Touch,
                Relationship = new Relationship
                {
                    Resource = new ObjectReference { Type = "site", Id = siteId.ToString() },
                    Relation = "staff",
                    Subject = new SubjectReference { Type = "user", Id = @event.UserId.ToString() }
                }
            });
        }

        await _spiceDb.WriteRelationshipsAsync(new WriteRelationshipsRequest { Updates = { updates } });
    }

    // Permission check
    public async Task<bool> CanAccessSite(Guid userId, Guid siteId)
    {
        var result = await _spiceDb.CheckPermissionAsync(new CheckPermissionRequest
        {
            Resource = new ObjectReference { Type = "site", Id = siteId.ToString() },
            Permission = "access",
            Subject = new SubjectReference { Type = "user", Id = userId.ToString() }
        });

        return result.Permissionship == Permissionship.HasPermission;
    }
}
```

---

## Business Rules

### Authentication Rules

1. **Password Requirements**: Minimum 8 characters, at least one uppercase, one lowercase, one digit
2. **PIN Requirements**: Exactly 4-6 numeric digits, unique within organization
3. **QR Token Validity**: Tokens expire after 90 days or explicit revocation
4. **Session Duration**: Default 8 hours, configurable per organization
5. **Concurrent Sessions**: Maximum 5 active sessions per user (configurable)
6. **Lockout Threshold**: 5 consecutive failed attempts triggers 15-minute lockout

### Access Control Rules

1. **Site Access Required**: Users cannot operate at sites without explicit access
2. **Organization Scope**: Users can only access resources within their organization
3. **Owner Protection**: Cannot deactivate or remove site access from the last owner
4. **Manager Requirements**: Certain operations require manager-level permissions
5. **Cascading Deactivation**: Deactivating a user revokes all access and terminates sessions

### Group Rules

1. **System Groups**: Cannot be deleted (e.g., "All Users", "Managers")
2. **Unique Names**: Group names must be unique within organization
3. **Permission Inheritance**: Group permissions apply to all members
4. **Site Assignment**: Groups can be assigned to specific sites

---

## Security Considerations

### Threats & Mitigations

| Threat | Impact | Mitigation |
|--------|--------|------------|
| **Credential Stuffing** | Account takeover | Rate limiting, lockout, anomaly detection |
| **Session Hijacking** | Unauthorized access | Secure cookies, token rotation, IP validation |
| **Privilege Escalation** | Unauthorized actions | SpiceDB validation, audit logging |
| **PIN Brute Force** | Account compromise | Limited attempts, lockout, per-device tracking |
| **QR Code Theft** | Unauthorized access | Time-limited tokens, single-use option |

### Audit Requirements

All authentication and authorization events must be logged with:
- Timestamp (UTC)
- User ID (if known)
- IP Address
- User Agent
- Action performed
- Success/Failure
- Reason (if failure)
- Resource accessed (if applicable)

### Compliance Considerations

| Requirement | Implementation |
|-------------|----------------|
| **GDPR Right to Erasure** | User deletion with data anonymization |
| **PCI DSS** | No storage of plaintext credentials |
| **SOC 2** | Comprehensive audit logging |
| **Session Timeouts** | Configurable idle/absolute timeouts |

---

## Event Type Registry

```csharp
public static class AuthEventTypes
{
    // User Lifecycle
    public const string UserCreated = "auth.user.created";
    public const string UserUpdated = "auth.user.updated";
    public const string UserDeactivated = "auth.user.deactivated";
    public const string UserReactivated = "auth.user.reactivated";
    public const string UserDeleted = "auth.user.deleted";
    public const string UserTypeChanged = "auth.user.type_changed";
    public const string UserPreferencesUpdated = "auth.user.preferences_updated";

    // Authentication
    public const string UserLoggedIn = "auth.session.logged_in";
    public const string UserLoggedOut = "auth.session.logged_out";
    public const string UserLoginFailed = "auth.session.login_failed";
    public const string SessionTerminated = "auth.session.terminated";
    public const string AllSessionsTerminated = "auth.session.all_terminated";
    public const string SessionExpired = "auth.session.expired";
    public const string SessionRefreshed = "auth.session.refreshed";

    // Credentials
    public const string PasswordChanged = "auth.credentials.password_changed";
    public const string PasswordReset = "auth.credentials.password_reset";
    public const string PinChanged = "auth.credentials.pin_changed";
    public const string QrTokenGenerated = "auth.credentials.qr_generated";
    public const string QrTokenRevoked = "auth.credentials.qr_revoked";

    // Access
    public const string SiteAccessGranted = "auth.access.site_granted";
    public const string SiteAccessRevoked = "auth.access.site_revoked";
    public const string UserAddedToGroup = "auth.access.group_added";
    public const string UserRemovedFromGroup = "auth.access.group_removed";
    public const string OwnershipTransferred = "auth.access.ownership_transferred";

    // Groups
    public const string UserGroupCreated = "auth.group.created";
    public const string UserGroupUpdated = "auth.group.updated";
    public const string UserGroupDeleted = "auth.group.deleted";
    public const string GroupAssignedToSite = "auth.group.site_assigned";
    public const string GroupPermissionAdded = "auth.group.permission_added";
    public const string GroupPermissionRemoved = "auth.group.permission_removed";

    // Security
    public const string AccountLocked = "auth.security.account_locked";
    public const string AccountUnlocked = "auth.security.account_unlocked";
    public const string MfaEnabled = "auth.security.mfa_enabled";
    public const string MfaDisabled = "auth.security.mfa_disabled";
    public const string MfaVerified = "auth.security.mfa_verified";
    public const string SuspiciousActivityDetected = "auth.security.suspicious_activity";
    public const string BruteForceDetected = "auth.security.brute_force";
}
```

---

## Hotspots & Risks

### High Complexity Areas

| Area | Complexity | Mitigation |
|------|------------|------------|
| **Permission Calculation** | Inheritance from multiple sources | Cache effective permissions, recalculate on change |
| **Session Management** | Distributed session state | Use grain-based sessions, avoid central store |
| **Cross-Org Security** | Tenant isolation | Always include OrgId in grain keys |
| **MFA Integration** | Multiple providers | Abstract behind interface |

### Performance Considerations

| Concern | Impact | Strategy |
|---------|--------|----------|
| **Permission Checks** | Every request | Cache SpiceDB results with short TTL |
| **Session Validation** | Every request | Keep session grain hot |
| **User Lookup by PIN** | Frequent during shifts | Index by PIN within org |

### Known Edge Cases

1. **Concurrent Session Limit**: When limit reached, terminate oldest or reject new?
2. **Timezone Changes**: User preferences vs. current site timezone
3. **Group Deletion**: What happens to permissions granted via deleted group?
4. **Owner Departure**: Force transfer or prevent deactivation?
