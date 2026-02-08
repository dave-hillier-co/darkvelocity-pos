# Kitchen Operations User Stories

Stories extracted from unit test specifications covering kitchen tickets, station management, ticket routing, coursing, and priority handling.

## Kitchen Tickets

### Creating Tickets

**As a** server,
**I want to** create a kitchen ticket for a VIP order,
**So that** the kitchen knows the priority, table, and allergy information.

- Given: no existing kitchen ticket for the given order
- When: a kitchen ticket is created for a VIP dine-in order at table T10 with allergy notes
- Then: the ticket is created with correct order details, VIP priority, and a KOT number

### Adding Items to Tickets

**As a** server,
**I want to** add items to a kitchen ticket with station assignments,
**So that** each item routes to the correct preparation station.

- Given: an existing kitchen ticket for a dine-in order
- When: a burger with modifiers and special instructions is added to the grill station
- Then: the item appears on the ticket with correct details and station assignment

### Item Preparation Flow

**As a** cook,
**I want to** mark items as preparing,
**So that** the kitchen display reflects the current status.

- Given: a kitchen ticket with a pending burger item
- When: the cook starts preparing the burger
- Then: the item status changes to preparing and the ticket becomes in-progress

**As a** cook,
**I want to** mark items as ready,
**So that** the expo knows which items are complete.

- Given: a kitchen ticket with a burger being prepared
- When: the cook completes the burger preparation
- Then: the item status changes to ready and the single-item ticket becomes ready

- Given: a kitchen ticket with a burger and fries both being prepared
- When: both items are completed
- Then: the ticket status changes to ready with completion time recorded

### Voiding Items

**As a** manager,
**I want to** void a kitchen ticket item,
**So that** the kitchen stops preparing cancelled items.

- Given: a kitchen ticket with a pending burger item
- When: the item is voided because the customer changed their order
- Then: the item status changes to voided

### Bumping/Serving Tickets

**As an** expo,
**I want to** bump a completed ticket,
**So that** the ticket is marked as served and cleared from the display.

- Given: a kitchen ticket with all items completed and ready for service
- When: the expo bumps the ticket to mark it as served
- Then: the ticket status changes to served with bump timestamp recorded

### Voiding Entire Tickets

**As a** manager,
**I want to** void an entire kitchen ticket,
**So that** cancelled orders are removed from the kitchen queue.

- Given: an existing kitchen ticket
- When: the entire ticket is voided due to order cancellation
- Then: the ticket status changes to voided with the void reason noted

### Priority Management

**As a** manager,
**I want to** escalate ticket priority,
**So that** urgent orders are prepared first.

- Given: an existing kitchen ticket with normal priority
- When: the priority is escalated to rush
- Then: the ticket priority updates to rush

**As a** manager,
**I want to** fire all items simultaneously,
**So that** the entire table's food comes out together.

- Given: an existing kitchen ticket
- When: fire-all is triggered to expedite all items
- Then: the ticket is marked as fire-all with AllDay priority

### Ticket Timing

**As a** manager,
**I want to** track kitchen ticket timings,
**So that** I can monitor preparation speed.

- Given: a kitchen ticket with one item that has been started and completed
- When: ticket timings are queried
- Then: wait time, prep time, and completion timestamp are all recorded

## Kitchen Stations

### Station Setup

**As a** manager,
**I want to** create kitchen stations,
**So that** items route to the correct preparation area.

- Given: a new kitchen station grain
- When: the station is opened as a grill station
- Then: the station is active with correct name, type, and open status

**As a** manager,
**I want to** assign menu categories and items to stations,
**So that** the system knows which station prepares each item.

- Given: an open kitchen station
- When: menu categories and specific menu items are assigned to the station
- Then: the station tracks both category and item assignments for routing

### Station Peripherals

**As a** manager,
**I want to** assign a printer to a station,
**So that** kitchen tickets print at the correct station.

- Given: an open kitchen station
- When: a printer is assigned to the station
- Then: the station records the printer ID for ticket printing

**As a** manager,
**I want to** assign a display screen to a station,
**So that** tickets appear on the correct KDS.

- Given: an open kitchen station
- When: a kitchen display screen is assigned to the station
- Then: the station records the display ID for KDS routing

### Station Queue Management

**As a** system,
**I want to** track active tickets at each station,
**So that** cooks see their current workload.

- Given: an open kitchen station with no active tickets
- When: a kitchen ticket is routed to the station
- Then: the station tracks the ticket in its active queue

- Given: an open kitchen station with one active ticket
- When: the ticket is completed at the station
- Then: the ticket is removed from the station's active queue

### Station Lifecycle

**As a** manager,
**I want to** pause a station during slow periods,
**So that** tickets stop routing to it temporarily.

- Given: an open kitchen station
- When: the station is paused
- Then: the station status changes to paused

- Given: a paused kitchen station
- When: the station is resumed
- Then: the station status returns to open

**As a** manager,
**I want to** close a station at end of shift,
**So that** it no longer accepts new tickets.

- Given: an open kitchen station with one active ticket
- When: the station is closed at end of shift
- Then: the station is closed with timestamp and active tickets are cleared

## Ticket Routing

### Multi-Station Routing

**As a** system,
**I want to** route ticket items to multiple stations,
**So that** each item reaches the correct preparation area.

- Given: a kitchen ticket for a dine-in order
- When: items are added targeting grill, fry, and salad stations
- Then: the ticket tracks all three assigned station IDs

### Coursing

**As a** server,
**I want to** assign course numbers to items,
**So that** the kitchen fires courses in the correct sequence.

- Given: a kitchen ticket for a multi-course dine-in order
- When: items are added for courses 1 (soup), 2 (steak), and 3 (dessert)
- Then: each item tracks its assigned course number

### Station Types

**As a** manager,
**I want to** create multiple station types,
**So that** each area of the kitchen is represented.

- Given: a site requiring multiple kitchen station types
- When: grill, salad, prep, and expo stations are created
- Then: each station records its correct station type
