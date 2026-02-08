# Workflow and Webhooks User Stories

Stories extracted from unit test specifications covering workflow initialization, status transitions, transition history tracking, version management, transition validation, webhook subscription lifecycle, event subscriptions, payload delivery, delivery tracking, and failure handling.

## Workflow Initialization

### Creating a New Workflow

**As a** system,
**I want to** initialize a workflow with an initial status and a set of allowed statuses,
**So that** the workflow can govern the lifecycle of a domain entity such as an expense or booking.

- Given: A new expense workflow with Draft as the initial status and a defined set of allowed statuses
- When: The workflow is initialized
- Then: The workflow state reflects the organization, owner, initial status, and version 1

### Preventing Duplicate Initialization

**As a** system,
**I want to** reject initialization of an already-initialized workflow,
**So that** the workflow's original configuration is not accidentally overwritten.

- Given: A workflow that has already been initialized
- When: Initialization is attempted again
- Then: An error is raised indicating the workflow is already initialized

### Validating Initial Status

**As a** system,
**I want to** require the initial status to be in the allowed statuses list,
**So that** workflows always start in a valid, recognized state.

- Given: A new workflow grain
- When: Initialization is attempted with an initial status not in the allowed statuses list
- Then: An error is raised indicating the initial status must be in the allowed list

### Requiring Allowed Statuses

**As a** system,
**I want to** reject initialization when the allowed statuses list is empty or null,
**So that** every workflow has at least one valid state to operate within.

- Given: A new workflow grain
- When: Initialization is attempted with an empty allowed statuses list
- Then: An error is raised indicating allowed statuses cannot be empty

- Given: A new workflow grain
- When: Initialization is attempted with null allowed statuses
- Then: An error is raised indicating allowed statuses cannot be empty

### Requiring a Non-Blank Initial Status

**As a** system,
**I want to** reject initialization when the initial status is empty or whitespace,
**So that** every workflow begins with a meaningful, identifiable state.

- Given: A new workflow grain
- When: Initialization is attempted with an empty string as the initial status
- Then: An error is raised indicating the initial status is required

- Given: A new workflow grain
- When: Initialization is attempted with whitespace as the initial status
- Then: An error is raised indicating the initial status is required

## Status Transitions

### Performing a Valid Transition

**As a** manager,
**I want to** transition a workflow to a new status with a reason,
**So that** the entity progresses through its lifecycle with an auditable record.

- Given: An expense workflow initialized in Draft status
- When: The workflow is transitioned to Pending with a reason
- Then: The transition succeeds and the current status is Pending with a recorded transition ID

### Preventing Same-Status Transitions

**As a** system,
**I want to** reject transitions that target the current status,
**So that** redundant state changes do not clutter the transition history.

- Given: An expense workflow currently in Draft status
- When: A transition to the same Draft status is attempted
- Then: The transition fails with an "Already in status" message and no transition is recorded

### Rejecting Invalid Target Statuses

**As a** system,
**I want to** reject transitions to statuses not in the allowed list,
**So that** workflows only move through predefined, valid states.

- Given: An expense workflow initialized in Draft status with a defined set of allowed statuses
- When: A transition to an invalid status not in the allowed list is attempted
- Then: The transition fails and the workflow remains in Draft

### Requiring Initialization Before Transition

**As a** system,
**I want to** prevent transitions on an uninitialized workflow,
**So that** operations only occur on properly configured workflow instances.

- Given: A workflow grain that has not been initialized
- When: A status transition is attempted
- Then: An error is raised indicating the workflow is not initialized

### Capturing Transition Metadata

**As a** manager,
**I want to** have transition metadata recorded including who performed it and why,
**So that** there is a complete audit trail of every workflow change.

- Given: An expense workflow in Draft status
- When: The workflow is transitioned to Approved with a performer and reason
- Then: The transition history captures the performer, reason, timestamps, and status change

### Rejecting Empty Target Status

**As a** system,
**I want to** reject transitions with an empty target status,
**So that** every transition has a clearly defined destination state.

- Given: An expense workflow initialized in Draft status
- When: A transition with an empty target status is attempted
- Then: The transition fails with a message indicating the status is required

### Allowing Optional Transition Reason

**As a** manager,
**I want to** transition a workflow without providing a reason,
**So that** the reason field remains optional when no explanation is necessary.

- Given: An expense workflow initialized in Draft status
- When: The workflow is transitioned to Pending without providing a reason
- Then: The transition succeeds and the history records a null reason

## Querying Current Status

### Retrieving the Current Status

**As a** manager,
**I want to** query the current status of a workflow,
**So that** I can determine where an entity stands in its lifecycle.

- Given: An expense workflow initialized in Draft status
- When: The current status is queried
- Then: The status returned is Draft

### Preventing Status Query on Uninitialized Workflow

**As a** system,
**I want to** raise an error when querying status on an uninitialized workflow,
**So that** callers are informed that the workflow must be set up before use.

- Given: A workflow grain that has not been initialized
- When: The current status is queried
- Then: An error is raised indicating the workflow is not initialized

### Reflecting Status After Transition

**As a** manager,
**I want to** see the updated status after a transition,
**So that** the current state always reflects the most recent change.

- Given: An expense workflow that has been transitioned from Draft to Approved
- When: The current status is queried
- Then: The status returned is Approved

## Transition History

### Retrieving Full Transition History

**As a** manager,
**I want to** retrieve the complete transition history of a workflow,
**So that** I can review the full sequence of status changes and their reasons.

- Given: An expense workflow that has undergone three transitions (Draft to Pending to Approved to Closed)
- When: The transition history is retrieved
- Then: All three transitions are returned in chronological order with correct from/to statuses and reasons

### Preventing History Query on Uninitialized Workflow

**As a** system,
**I want to** raise an error when requesting history on an uninitialized workflow,
**So that** callers are informed that the workflow must be initialized first.

- Given: A workflow grain that has not been initialized
- When: The transition history is requested
- Then: An error is raised indicating the workflow is not initialized

### Empty History for New Workflow

**As a** system,
**I want to** return an empty history list when no transitions have occurred,
**So that** callers receive a valid response even when the workflow has only been initialized.

- Given: A workflow initialized in Draft status with no transitions performed
- When: The transition history is requested
- Then: An empty list is returned

## Transition Eligibility Checks

### Checking Valid Transition Targets

**As a** system,
**I want to** verify whether a specific status is a valid transition target,
**So that** the UI can enable or disable transition options accordingly.

- Given: An expense workflow initialized in Draft with Pending as an allowed status
- When: The workflow checks if it can transition to Pending
- Then: The check returns true

### Detecting Invalid Transition Targets

**As a** system,
**I want to** report that a transition to a disallowed status is not possible,
**So that** the UI can prevent users from selecting invalid destinations.

- Given: An expense workflow initialized in Draft status
- When: The workflow checks if it can transition to an invalid status not in the allowed list
- Then: The check returns false

### Preventing Same-Status Transition Check

**As a** system,
**I want to** report that a transition to the current status is not possible,
**So that** redundant transitions are caught before they are attempted.

- Given: An expense workflow currently in Draft status
- When: The workflow checks if it can transition to Draft (the same status)
- Then: The check returns false

### Handling Uninitialized Workflow Gracefully

**As a** system,
**I want to** return false instead of throwing when checking transitions on an uninitialized workflow,
**So that** callers can safely probe transition eligibility without error handling.

- Given: A workflow grain that has not been initialized
- When: The workflow checks if it can transition to Pending
- Then: The check returns false instead of throwing

### Rejecting Empty and Whitespace Targets

**As a** system,
**I want to** return false for empty or whitespace target statuses,
**So that** invalid inputs are handled gracefully without errors.

- Given: An expense workflow initialized in Draft status
- When: The workflow checks if it can transition to an empty string target
- Then: The check returns false

- Given: An expense workflow initialized in Draft status
- When: The workflow checks if it can transition to a whitespace-only target
- Then: The check returns false

## Workflow State

### Retrieving Complete Workflow State

**As a** manager,
**I want to** retrieve the complete state of a workflow including organization, owner, status, transitions, and version,
**So that** I have a comprehensive view of the workflow's current configuration and history.

- Given: A booking workflow initialized in Pending and transitioned to Approved
- When: The full workflow state is retrieved
- Then: The state includes the organization, owner type, current status, one transition, and version 2

### Preventing State Query on Uninitialized Workflow

**As a** system,
**I want to** raise an error when requesting state on an uninitialized workflow,
**So that** callers are informed that the workflow must be set up before use.

- Given: A workflow grain that has not been initialized
- When: The full workflow state is requested
- Then: An error is raised indicating the workflow is not initialized

## Version Tracking

### Incrementing Version on Transitions

**As a** system,
**I want to** increment the workflow version with each successful transition,
**So that** optimistic concurrency and change tracking are supported.

- Given: An expense workflow initialized in Draft status (version 1)
- When: Two successive transitions are performed (Draft to Pending, Pending to Approved)
- Then: The version increments to 2 after the first transition and 3 after the second

### Preserving Version on Failed Transitions

**As a** system,
**I want to** leave the version unchanged when a transition fails,
**So that** unsuccessful operations do not corrupt the version sequence.

- Given: An expense workflow initialized in Draft status
- When: An invalid transition to a disallowed status is attempted
- Then: The version remains unchanged from the pre-transition value

## Transition Timestamps

### Recording Last Transition Timestamp

**As a** system,
**I want to** record the timestamp of the most recent transition,
**So that** managers can see when the last status change occurred.

- Given: An expense workflow initialized in Draft with no prior transitions
- When: The workflow is transitioned to Pending
- Then: The last transition timestamp is set to approximately the current time

### Advancing Timestamp on Each Transition

**As a** system,
**I want to** update the last transition timestamp on every successful transition,
**So that** the timestamp always reflects the most recent change.

- Given: An expense workflow that has already been transitioned once (Draft to Pending)
- When: A second transition to Approved is performed
- Then: The last transition timestamp advances beyond the first transition time

## Complex Workflow Scenarios

### Full Approval Lifecycle

**As a** manager,
**I want to** support a complete approval lifecycle including submission, rejection, correction, resubmission, approval, and closure,
**So that** real-world multi-step review processes are fully tracked.

- Given: A purchase document workflow initialized in Draft status with multiple staff members
- When: Six transitions simulate a full approval lifecycle (submit, reject, correct, resubmit, approve, close)
- Then: The complete transition path is recorded with correct performers, and the final status is Closed at version 7

### Independent Workflows per Owner Type

**As a** system,
**I want to** maintain separate state for workflows on different owner types within the same organization,
**So that** an expense workflow does not interfere with a booking or purchase document workflow.

- Given: Three workflow grains for different owner types (expense, booking, purchase document) within the same organization
- When: Each workflow is initialized and transitioned independently
- Then: Each grain maintains its own status and transition history without cross-contamination

### Unique Transition Identifiers

**As a** system,
**I want to** assign a unique identifier to each transition,
**So that** individual transitions can be referenced and audited independently.

- Given: An expense workflow initialized in Draft status
- When: Three successive transitions are performed (Draft to Pending to Approved to Closed)
- Then: Each transition is assigned a unique, non-empty ID

### Custom Status Values

**As a** manager,
**I want to** define custom status values for a workflow,
**So that** workflows can be tailored to diverse business processes beyond standard approval flows.

- Given: A workflow with custom status values including "New", "In Progress", "Under Review", "On Hold", "Completed", and "Cancelled"
- When: The workflow progresses through five transitions ending in Completed
- Then: All custom statuses are accepted, the final status is Completed, and all five transitions are recorded

## Webhook Subscription Lifecycle

### Creating a Webhook Subscription

**As a** developer,
**I want to** create a webhook subscription with a name, URL, secret, custom headers, and event types,
**So that** external systems receive notifications when subscribed events occur.

- Given: A new webhook subscription with name, URL, secret, headers, and event types
- When: The webhook subscription is created
- Then: The subscription is active with all configured properties persisted

### Updating a Webhook Subscription

**As a** developer,
**I want to** update a webhook subscription's name, URL, secret, and event types,
**So that** the integration can be reconfigured without deleting and recreating it.

- Given: An active webhook subscription
- When: The subscription name, URL, secret, and event types are updated
- Then: The subscription reflects the new configuration

### Deleting a Webhook Subscription

**As a** developer,
**I want to** delete a webhook subscription,
**So that** the external system stops receiving notifications.

- Given: An active webhook subscription
- When: The webhook is deleted
- Then: The webhook status is marked as Deleted

## Webhook Event Subscriptions

### Adding Event Subscriptions

**As a** developer,
**I want to** subscribe a webhook to additional event types,
**So that** the integration can listen for new events without replacing the entire configuration.

- Given: An active webhook subscription with existing event subscriptions
- When: New event types are added to the subscription
- Then: The webhook is subscribed to both new and existing event types

### Removing Event Subscriptions

**As a** developer,
**I want to** unsubscribe a webhook from a specific event type,
**So that** unwanted notifications are no longer sent for that event.

- Given: A webhook subscribed to order.created and order.completed events
- When: The order.created event is unsubscribed
- Then: Only order.completed remains subscribed

### Checking Event Subscription Status

**As a** system,
**I want to** verify whether a webhook is subscribed to a specific event type,
**So that** delivery logic can determine if a webhook should receive a given event.

- Given: A webhook subscribed to order.created and order.completed events
- When: Subscription status is checked for various event types
- Then: Subscribed events return true and unsubscribed events return false

## Webhook Pause and Resume

### Pausing a Webhook

**As a** developer,
**I want to** pause a webhook subscription,
**So that** deliveries are temporarily halted without losing the subscription configuration.

- Given: An active webhook subscription
- When: The webhook is paused
- Then: The webhook status is Paused with a pause timestamp recorded

### Resuming a Paused Webhook

**As a** developer,
**I want to** resume a paused webhook subscription,
**So that** deliveries restart and the failure counter is cleared for a fresh start.

- Given: A paused webhook subscription
- When: The webhook is resumed
- Then: The webhook status returns to Active with failure counter and pause timestamp cleared

## Webhook Delivery

### Delivering a Payload

**As a** system,
**I want to** deliver a payload to a webhook's URL for a subscribed event,
**So that** the external system receives real-time event data.

- Given: An active webhook subscribed to order.created
- When: A payload is delivered for the order.created event
- Then: The delivery is recorded as successful with a 200 status code

### Preventing Delivery to a Paused Webhook

**As a** system,
**I want to** reject delivery attempts to a paused webhook,
**So that** payloads are not sent while the webhook is intentionally inactive.

- Given: A paused webhook subscription
- When: A delivery is attempted
- Then: An error is raised indicating the webhook is not active

### Preventing Delivery for Unsubscribed Events

**As a** system,
**I want to** reject delivery attempts for events the webhook is not subscribed to,
**So that** only relevant events trigger deliveries.

- Given: A webhook not subscribed to the customer.created event
- When: A delivery is attempted for customer.created
- Then: An error is raised indicating the webhook is not subscribed to that event

## Delivery Tracking

### Tracking Successful Deliveries

**As a** developer,
**I want to** track all successful deliveries with their timestamps and status codes,
**So that** I can monitor webhook health and confirm events are being received.

- Given: An active webhook subscription
- When: Five successful deliveries are recorded
- Then: All five deliveries are tracked and the consecutive failure counter remains at zero

### Limiting Recent Delivery History

**As a** system,
**I want to** cap the recent delivery history at 100 entries,
**So that** storage is bounded and the most relevant delivery data is retained.

- Given: A webhook with 110 recorded deliveries
- When: Recent deliveries are retrieved
- Then: Only the most recent 100 deliveries are returned

## Delivery Failure Handling

### Tracking Consecutive Failures

**As a** system,
**I want to** count consecutive delivery failures,
**So that** the system can detect persistent issues with a webhook endpoint.

- Given: An active webhook subscription
- When: Two consecutive delivery failures with 500 status codes are recorded
- Then: The consecutive failure counter is 2 and the webhook remains active

### Auto-Failing After Maximum Consecutive Failures

**As a** system,
**I want to** mark a webhook as failed after reaching the maximum number of consecutive failures,
**So that** the system stops attempting deliveries to an unresponsive endpoint.

- Given: An active webhook subscription
- When: Three consecutive delivery failures are recorded (the default maximum)
- Then: The webhook status transitions to Failed

### Resetting Failure Counter on Success

**As a** system,
**I want to** reset the consecutive failure counter when a delivery succeeds,
**So that** transient errors do not accumulate toward the failure threshold.

- Given: A webhook with two consecutive delivery failures
- When: A successful delivery is recorded
- Then: The consecutive failure counter resets to zero and the webhook remains active
