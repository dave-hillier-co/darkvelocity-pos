# Organization & Site Management User Stories

Stories extracted from unit test specifications covering organizations, sites, users, user groups, and external identity providers.

---

## Organization Lifecycle

**As a** platform operator,
**I want to** create a new organization with a name and slug,
**So that** the tenant is registered and ready to onboard sites, users, and configuration.

- Given: a new organization with a name and slug
- When: the organization is created
- Then: it should return the organization ID, slug, and creation timestamp

---

**As a** system,
**I want to** reject duplicate organization creation attempts,
**So that** each tenant is uniquely registered and conflicting entries are prevented.

- Given: an organization that already exists
- When: a second creation is attempted
- Then: it should reject the duplicate with an error

---

**As a** platform operator,
**I want to** suspend an organization for non-payment,
**So that** access is restricted until the billing issue is resolved.

- Given: an active organization
- When: the organization is suspended for non-payment
- Then: its status should change to Suspended

---

**As a** platform operator,
**I want to** reactivate a suspended organization,
**So that** the tenant regains full access after resolving their billing issue.

- Given: a suspended organization
- When: the organization is reactivated
- Then: its status should return to Active

---

**As a** platform operator,
**I want to** immediately cancel an organization with a reason,
**So that** the tenant is terminated right away when circumstances require it.

- Given: an active organization
- When: immediate cancellation is initiated
- Then: the organization should be cancelled immediately with the provided reason

---

**As a** platform operator,
**I want to** schedule an end-of-period cancellation for an organization,
**So that** the tenant can continue operating until their current billing period ends.

- Given: an active organization
- When: end-of-period cancellation is initiated
- Then: the organization should enter a pending cancellation state with a future effective date

---

**As a** platform operator,
**I want to** reverse a pending cancellation,
**So that** an organization that decides to stay can return to normal active status.

- Given: an organization with a pending cancellation
- When: the cancellation is reversed
- Then: the organization should return to active status with cancellation cleared

---

**As an** organization owner,
**I want to** change my organization's slug,
**So that** the URL-friendly identifier can be updated while preserving a history of previous slugs.

- Given: an organization with a slug
- When: the slug is changed to a new value
- Then: the new slug should be active and the old slug should be recorded in history

---

## Organization Features

**As an** organization owner,
**I want to** update branding with a logo, primary color, and secondary color,
**So that** the organization's visual identity is consistently applied across all applications.

- Given: an existing organization
- When: branding is updated with a logo, primary, and secondary color
- Then: the organization state should reflect the new branding

---

**As a** platform operator,
**I want to** enable a feature flag for an organization,
**So that** specific capabilities can be rolled out or restricted on a per-tenant basis.

- Given: an existing organization
- When: a feature flag is enabled
- Then: the feature flag should be retrievable as enabled

---

**As a** system,
**I want to** default unconfigured feature flags to false,
**So that** new features are opt-in and organizations only get capabilities that are explicitly enabled.

- Given: an organization with no feature flags configured
- When: checking a feature flag that was never set
- Then: it should default to false

---

**As an** organization owner,
**I want to** configure a custom domain for my organization,
**So that** the platform can be accessed through a branded URL after domain verification.

- Given: an existing organization
- When: a custom domain is configured
- Then: the domain should be stored as unverified with a verification token

---

## Site Management

**As an** organization owner,
**I want to** create a new site with a name, code, and address,
**So that** a physical venue is registered and ready for operations.

- Given: an organization with no venues
- When: a new site is created with name, code, and address
- Then: the site is created with a unique ID, the correct code, and a creation timestamp

---

**As a** system,
**I want to** automatically register a new site with its parent organization,
**So that** the organization always has an up-to-date list of its venues.

- Given: an organization
- When: a new site is created
- Then: the site is automatically registered with the parent organization

---

**As an** organization owner,
**I want to** configure a site with timezone, currency, and address details,
**So that** the venue operates with the correct regional settings for transactions and reporting.

- Given: a site created with timezone, currency, and address details
- When: the site state is retrieved
- Then: all configured properties including timezone, currency, status, and address are returned

---

**As a** manager,
**I want to** close a site,
**So that** the venue is marked as no longer operational.

- Given: an open site
- When: the site is closed
- Then: the site status changes to Closed

---

**As a** manager,
**I want to** reopen a closed site,
**So that** the venue can resume operations after a permanent closure is reversed.

- Given: a closed site
- When: the site is reopened
- Then: the site status changes to Open

---

**As a** manager,
**I want to** temporarily close a site for maintenance,
**So that** the venue is marked as unavailable without indicating a permanent closure.

- Given: an open site
- When: the site is temporarily closed for maintenance
- Then: the site status changes to TemporarilyClosed

---

**As a** manager,
**I want to** set an active menu for a site,
**So that** the POS and ordering systems use the correct menu for the venue.

- Given: a site with no active menu
- When: a menu is set as the active menu for the site
- Then: the site settings reflect the newly active menu

---

## User Management

**As an** organization owner,
**I want to** create a new user with email, display name, and employee type,
**So that** the person is registered in the organization and can be granted access to systems.

- Given: a new user with email, display name, and employee type
- When: the user is created in the organization
- Then: the user is assigned an ID, email is stored, and creation timestamp is recorded

---

**As a** manager,
**I want to** set a four-digit PIN for a user,
**So that** the user can authenticate quickly at POS terminals without a full login flow.

- Given: an existing user without a PIN
- When: a four-digit PIN is set for POS login
- Then: the user's PIN hash is stored (non-empty)

---

**As a** user,
**I want to** log in with my correct PIN,
**So that** I can access the POS terminal quickly and securely.

- Given: a user with PIN "1234" configured for POS login
- When: the correct PIN "1234" is submitted for verification
- Then: verification succeeds

---

**As a** system,
**I want to** reject incorrect PIN submissions,
**So that** unauthorized access to POS terminals is prevented.

- Given: a user with PIN "1234" configured for POS login
- When: an incorrect PIN "5678" is submitted for verification
- Then: verification fails with an "Invalid PIN" error

---

**As a** system,
**I want to** reject PIN verification for locked accounts,
**So that** users whose access has been revoked cannot authenticate even with a valid PIN.

- Given: a user with a valid PIN whose account has been locked
- When: the correct PIN is submitted for verification
- Then: verification fails with a "User account is locked" error

---

**As a** manager,
**I want to** grant a user access to a specific site,
**So that** the user can operate at that venue's POS and back-office systems.

- Given: a user in an organization with no site access
- When: access to a specific site is granted
- Then: the user has access to that site

---

**As a** manager,
**I want to** deactivate a user account,
**So that** the user can no longer access any systems while their record is preserved.

- Given: an active user in the organization
- When: the user account is deactivated
- Then: the user's status changes to Inactive

---

**As a** security officer,
**I want to** lock a user account for a security reason,
**So that** compromised or suspicious accounts are immediately prevented from accessing the system.

- Given: an active user in the organization
- When: the user account is locked for a security reason
- Then: the user's status changes to Locked

---

**As a** manager,
**I want to** unlock a previously locked user account,
**So that** the user can resume access after the security concern has been resolved.

- Given: a user whose account has been locked
- When: the user account is unlocked
- Then: the user's status returns to Active

---

## User Groups

**As an** organization owner,
**I want to** create a user group with a name and description,
**So that** users can be organized into logical groups for permissions and role assignment.

- Given: a new user group with name and description for an organization
- When: the user group is created
- Then: the group is assigned an ID and creation timestamp is recorded

---

**As a** manager,
**I want to** add a user to a group,
**So that** the user inherits the group's permissions and policies.

- Given: a user group with no members
- When: a user is added as a member
- Then: the group reports the user as a member

---

**As a** manager,
**I want to** remove a user from a group,
**So that** the user no longer inherits the group's permissions when their role changes.

- Given: a user group with a member
- When: the member is removed from the group
- Then: the group no longer reports the user as a member

---

## External Identity Providers

**As a** user,
**I want to** link my Google OAuth identity to my account,
**So that** I can sign in using my Google credentials instead of a password or PIN.

- Given: a user account in an organization
- When: linking a Google OAuth identity to the user
- Then: the external identity mapping should be stored on the user

---

**As a** user,
**I want to** link multiple external identity providers to my account,
**So that** I can sign in using whichever provider is most convenient.

- Given: a user account in an organization
- When: linking identities from both Google and Microsoft
- Then: both external identity providers should be stored on the user

---

**As a** user,
**I want to** unlink an external identity provider from my account,
**So that** the provider can no longer be used to sign in and my account is decoupled from that service.

- Given: a user with a linked Google identity
- When: unlinking the Google identity
- Then: the Google provider should no longer appear in the user's external identities

---

**As a** system,
**I want to** prevent the same external identity from being linked to multiple user accounts,
**So that** each external credential maps to exactly one user and sign-in is unambiguous.

- Given: a Google identity already linked to one user
- When: a different user attempts to link the same Google identity
- Then: it should reject the duplicate link with an error
