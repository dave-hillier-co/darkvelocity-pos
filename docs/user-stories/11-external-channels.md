# External Channels & Delivery Platforms User Stories

Stories extracted from unit test specifications covering delivery platform integration, external orders, menu sync, status mapping, webhook handling, channel registry, and platform payouts.

---

## Channel Connection

**As a** venue operator, **I want to** connect a delivery channel with API credentials and webhook configuration, **So that** the system can begin receiving and processing orders from that platform.
- Given: A delivery channel grain has not been connected
- When: The channel is connected as a Deliverect aggregator with API credentials and webhook secret
- Then: The channel snapshot shows active status with correct platform type, integration type, and connection timestamp

**As a** venue operator, **I want to** pause an active delivery channel during kitchen overwhelm, **So that** no new orders arrive while the kitchen catches up.
- Given: A JustEat channel is actively receiving orders
- When: The channel is paused due to kitchen overwhelm
- Then: The channel status changes to Paused

**As a** venue operator, **I want to** resume a paused delivery channel, **So that** the venue can start accepting orders again once the kitchen is ready.
- Given: A DoorDash channel has been paused
- When: The channel is resumed
- Then: The channel status returns to Active

**As a** system, **I want to** record API authentication errors on a channel, **So that** operators are alerted when a platform integration needs attention.
- Given: A Wolt channel is actively connected
- When: An API authentication error is recorded
- Then: The channel status changes to Error with the error message preserved

**As a** venue operator, **I want to** disconnect a delivery channel, **So that** the platform integration is cleanly terminated when no longer needed.
- Given: A Deliverect aggregator channel is connected
- When: The channel is disconnected
- Then: The channel status changes to Disconnected

**As a** venue operator, **I want to** track daily order volume and revenue per channel, **So that** I can monitor each platform's contribution to business performance.
- Given: A self-service kiosk channel is connected
- When: Two orders are recorded with different amounts
- Then: The daily order count and revenue totals are accumulated correctly

---

## Status Mapping

**As a** venue operator, **I want to** configure status mappings between a delivery platform and internal order statuses, **So that** external order updates are translated into consistent internal workflows.
- Given: A Deliverect platform status mapping is not yet configured
- When: The status mappings are configured with four platform-to-internal status entries
- Then: The mapping snapshot contains all four entries with the correct platform type and timestamp

**As a** system, **I want to** resolve an external platform status code to the corresponding internal order status, **So that** incoming webhook events drive the correct order state transitions.
- Given: An UberEats channel has status mappings configured for "accepted" and "picked_up"
- When: The internal status for external code "accepted" is requested
- Then: The mapped internal order status Accepted is returned

**As a** system, **I want to** handle unmapped external status codes gracefully, **So that** unknown platform statuses do not cause processing errors.
- Given: A Deliveroo channel has a single status mapping configured
- When: The internal status for an unmapped external code "UNKNOWN_STATUS" is requested
- Then: Null is returned indicating no mapping exists for that code

**As a** venue operator, **I want to** add new status mappings to an existing configuration, **So that** additional platform statuses can be handled as the integration evolves.
- Given: A DoorDash channel has one initial status mapping configured
- When: A new mapping for the "ready" status is added with a courier notification trigger
- Then: The snapshot contains two mappings including the newly added one

---

## Channel Registry

**As a** system, **I want to** register a delivery channel in the organization's channel registry, **So that** all connected platforms are discoverable from a single place.
- Given: An organization's channel registry is empty
- When: A Deliverect aggregator channel is registered
- Then: The registry contains one channel with the correct platform type and integration type

**As a** venue operator, **I want to** filter registered channels by integration type, **So that** I can quickly find directly integrated, aggregator, or internal channels.
- Given: An organization has channels registered with Direct, Aggregator, and Internal integration types
- When: Channels are filtered by Direct integration type
- Then: Only the two directly integrated channels are returned

**As a** system, **I want to** reflect channel status changes in the registry, **So that** the registry always shows the current state of each connected platform.
- Given: A JustEat channel is registered in the organization's channel registry
- When: The channel status is updated to Paused
- Then: The registry reflects the paused status for that channel

---

## External Order Lifecycle

**As a** system, **I want to** ingest a delivery order received from an external platform, **So that** the venue can view and act on incoming third-party orders.
- Given: A new delivery order received from an external platform
- When: The order is ingested into the system
- Then: The external order is created with pending status and all customer and item details preserved

**As a** venue operator, **I want to** accept an incoming external order with an estimated pickup time, **So that** the platform and customer know the order has been acknowledged and when it will be ready.
- Given: A pending external delivery order
- When: The restaurant accepts the order with an estimated pickup time
- Then: The order status transitions to accepted with the pickup time recorded

**As a** venue operator, **I want to** reject an incoming external order with a reason, **So that** the platform is informed why the order cannot be fulfilled.
- Given: A pending external delivery order
- When: The restaurant rejects the order with a reason
- Then: The order status transitions to rejected with the rejection reason stored

**As a** venue operator, **I want to** progress an accepted order through preparing, ready, picked up, and delivered stages, **So that** the delivery workflow is tracked end-to-end.
- Given: An accepted external delivery order
- When: The order progresses through preparing, ready, picked up, and delivered stages
- Then: Each status transition is recorded in sequence following the delivery workflow

**As a** venue operator, **I want to** cancel an accepted external order with a reason, **So that** the cancellation is recorded and communicated to the platform.
- Given: An accepted external delivery order
- When: The order is cancelled with a reason
- Then: The order status transitions to cancelled with the cancellation reason stored

**As a** system, **I want to** link an external order to an internal POS order, **So that** revenue, items, and payments are reconciled across systems.
- Given: A received external delivery order
- When: The external order is linked to an internal POS order
- Then: The internal order ID is stored on the external order for cross-referencing

**As a** system, **I want to** store courier assignment details on an external order, **So that** the venue can track who is picking up the delivery.
- Given: A received external delivery order
- When: The delivery platform sends courier assignment details
- Then: The courier information is stored on the order for tracking

**As a** system, **I want to** prevent invalid state transitions on external orders, **So that** the order lifecycle remains consistent and auditable.
- Given: An external delivery order that has already been rejected
- When: An attempt is made to accept the rejected order
- Then: The operation is rejected because only pending orders can be accepted

---

## Menu Synchronization

**As a** venue operator, **I want to** start a menu sync operation to a delivery platform, **So that** the platform's menu reflects the venue's current offerings.
- Given: A delivery platform channel ready for menu synchronization
- When: A menu sync operation is started
- Then: The sync is initialized in progress with zero items synced or failed

**As a** system, **I want to** track successfully synced menu items with their platform PLU mappings, **So that** the venue knows which items are live on the platform.
- Given: An in-progress menu sync to a delivery platform
- When: Menu items are successfully synced with their platform PLU mappings
- Then: The synced item count is incremented for each successful item

**As a** system, **I want to** track menu items that fail to sync due to validation errors, **So that** operators can resolve issues and retry.
- Given: An in-progress menu sync to a delivery platform
- When: Menu items fail to sync due to validation errors
- Then: The failed item count and error messages are tracked

**As a** system, **I want to** mark a menu sync operation as completed, **So that** the sync history is recorded with final counts and a completion timestamp.
- Given: An in-progress menu sync with items already synced
- When: The sync operation is completed
- Then: The sync status transitions to completed with a completion timestamp

---

## Webhook Handling

**As a** system, **I want to** deserialize a raw Deliverect webhook payload into a structured order, **So that** external order data is correctly mapped to the internal domain model.
- Given: A raw Deliverect webhook JSON payload for a new order
- When: The payload is deserialized
- Then: All order fields including customer, items, and pricing are correctly mapped

**As a** system, **I want to** validate webhook payload signatures using HMAC-SHA256, **So that** only authentic payloads from trusted platforms are processed.
- Given: A webhook payload with a valid HMAC-SHA256 signature
- When: The signature is validated against the webhook secret
- Then: The validation succeeds confirming the payload is authentic

**As a** system, **I want to** reject webhook payloads with invalid signatures, **So that** tampered or forged requests are discarded.
- Given: A webhook payload with a tampered or invalid signature
- When: The signature is validated against the webhook secret
- Then: The validation fails indicating the payload may have been tampered with

**As a** system, **I want to** map Deliverect numeric order type codes to internal order types, **So that** delivery, pickup, and dine-in orders are categorized correctly.
- Given: Deliverect numeric order type codes (1=Delivery, 2=Pickup, 3=DineIn)
- When: Each code is mapped to the internal order type
- Then: Known codes map correctly and unknown codes default to Delivery

---

## Delivery Platform Integration

**As a** venue operator, **I want to** establish a delivery platform connection with merchant credentials, **So that** the venue can transact through that platform.
- Given: UberEats platform credentials and configuration
- When: The delivery platform connection is established
- Then: The platform is created with active status, merchant details, and zero daily counters

**As a** venue operator, **I want to** map an internal site to an external platform store ID, **So that** orders and menus are routed to the correct venue location.
- Given: A connected Deliverect aggregator platform
- When: A venue is mapped to a platform store ID
- Then: The location mapping is stored linking the internal site to the external store

**As a** system, **I want to** accumulate daily order counts and revenue for a delivery platform, **So that** operators can monitor platform performance throughout the day.
- Given: An active Postmates delivery platform
- When: Multiple orders are received throughout the day
- Then: Daily order count and revenue totals accumulate correctly

**As a** venue operator, **I want to** map multiple venue locations to their respective platform store IDs, **So that** a multi-location restaurant group can manage all sites through a single platform integration.
- Given: A Deliverect aggregator platform for a multi-location restaurant group
- When: Three venue locations are mapped to their platform store IDs
- Then: All location mappings are tracked including active and inactive sites

---

## Platform Payouts

**As a** venue operator, **I want to** receive a delivery platform payout with gross amount, fees, and net amount, **So that** the financial settlement from the platform is recorded for reconciliation.
- Given: A platform payout grain has not received any payout data
- When: A delivery platform payout is received with gross amount, fees, and net amount
- Then: The payout is created in Pending status with all financial details preserved

**As a** venue operator, **I want to** record the settlement period for a platform payout, **So that** payouts can be matched to the correct business period for accounting.
- Given: A platform payout grain has not received any payout data
- When: A payout is received for a specific weekly settlement period
- Then: The payout period start and end dates are stored correctly

**As a** system, **I want to** transition a payout to Processing status, **So that** the payout lifecycle reflects when settlement is underway.
- Given: A platform payout has been received and is in Pending status
- When: The payout is marked as processing
- Then: The payout status transitions to Processing

**As a** system, **I want to** mark a payout as completed with a settlement timestamp, **So that** the venue knows exactly when funds were settled.
- Given: A platform payout is currently in Processing status
- When: The payout is marked as completed with a settlement timestamp
- Then: The payout status transitions to Completed with the processed-at time recorded

**As a** system, **I want to** record payout failures, **So that** bank rejections and other settlement issues are tracked for resolution.
- Given: A platform payout is currently in Processing status
- When: The payout fails due to a bank rejection
- Then: The payout status transitions to Failed

**As a** venue operator, **I want to** track a platform payout through its full lifecycle from receipt to settlement, **So that** the complete financial journey is auditable.
- Given: A delivery platform payout is received with gross sales, commission, and settlement reference
- When: The payout progresses through the full lifecycle from Pending to Processing to Completed
- Then: The final state shows Completed status with all original financial details and settlement timestamp preserved

**As a** system, **I want to** maintain independent state for each platform payout, **So that** concurrent payouts do not interfere with each other.
- Given: Two separate platform payouts are received within the same organization
- When: One payout is completed while the other remains pending
- Then: Each payout grain maintains independent status and financial data

**As a** venue operator, **I want to** reconcile a platform-specific weekly payout with commission details, **So that** I can verify the platform's settlement matches expected revenue.
- Given: An UberEats weekly payout is received with 30% platform commission for a specific location
- When: The payout progresses through the full settlement lifecycle with a 2-day processing delay
- Then: The payout is completed with the correct gross, fee, and net amounts and settlement timestamp
