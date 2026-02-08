# Bookings & Table Management User Stories

Stories extracted from unit test specifications covering reservations, table management, floor plans, waitlist, booking calendar, and availability.

---

## Reservation Lifecycle

**As a** guest,
**I want to** submit a reservation request for my party with special requests and occasion details,
**So that** I can secure a table and the restaurant can prepare for my visit.

- Given: a guest requesting a reservation for 6 with special requests and occasion
- When: the booking is submitted via the website
- Then: a new reservation is created with a 6-character confirmation code and all details persisted

---

**As a** host,
**I want to** confirm a reservation that is in Requested status,
**So that** the guest knows their table is guaranteed.

- Given: a reservation in Requested status
- When: the host confirms the reservation
- Then: the reservation status changes to Confirmed with a confirmation timestamp

---

**As a** guest,
**I want to** modify my reservation's party size, date, and special requests,
**So that** the restaurant has accurate information for my visit.

- Given: an existing reservation for 4 guests
- When: the guest calls to change the party size to 8 and update the date
- Then: the reservation reflects the new party size, time, and special requests

---

**As a** guest,
**I want to** cancel my reservation with a reason,
**So that** the restaurant can release the table for other guests.

- Given: an existing reservation
- When: the guest cancels the reservation with a reason
- Then: the reservation status changes to Cancelled with the cancellation reason recorded

---

**As a** host,
**I want to** record when a guest arrives for their reservation,
**So that** I can track arrival times and manage seating flow.

- Given: a confirmed reservation
- When: the guest arrives at the restaurant
- Then: the reservation status changes to Arrived with the arrival time and check-in staff recorded

---

**As a** host,
**I want to** seat an arrived guest at a specific table,
**So that** the reservation progresses and the table is marked as occupied.

- Given: a guest who has arrived for their reservation
- When: the host seats the guest at table T10
- Then: the reservation status changes to Seated with the seating time and table assignment recorded

---

**As a** host,
**I want to** mark a reservation as completed when the guest departs,
**So that** the reservation lifecycle is closed and linked to the order for reporting.

- Given: a seated guest with an active order
- When: the guest finishes dining and departs
- Then: the reservation is marked Completed with departure time and linked order

---

**As a** host,
**I want to** mark a confirmed reservation as a no-show when the guest does not arrive,
**So that** the table can be released and the no-show is recorded.

- Given: a confirmed reservation where the guest has not arrived
- When: staff marks the reservation as a no-show
- Then: the reservation status changes to NoShow

---

**As a** system,
**I want to** prevent modifications to cancelled reservations,
**So that** data integrity is maintained for closed bookings.

- Given: a cancelled reservation
- When: the host attempts to modify the reservation
- Then: modification is rejected because cancelled reservations cannot be changed

---

**As a** system,
**I want to** prevent seating a guest who has not arrived,
**So that** the reservation follows the correct lifecycle sequence.

- Given: a reservation in Requested status (guest has not arrived)
- When: the host attempts to seat the guest
- Then: seating is rejected because the guest must arrive before being seated

---

## Deposit Management

**As a** host,
**I want to** require a deposit for a reservation,
**So that** the restaurant is protected against last-minute cancellations and no-shows.

- Given: an existing reservation
- When: a deposit of $50 is required for the reservation
- Then: the reservation status changes to PendingDeposit with the deposit amount recorded

---

**As a** guest,
**I want to** pay the required deposit for my reservation,
**So that** my booking is secured and can be confirmed.

- Given: a reservation with a $50 deposit required
- When: the guest pays the deposit by credit card
- Then: the deposit is marked as Paid with the payment method and reference recorded

---

**As a** system,
**I want to** prevent confirmation of a reservation with an unpaid deposit,
**So that** the deposit policy is enforced before the booking is finalized.

- Given: a reservation with a required but unpaid deposit
- When: the host attempts to confirm the reservation
- Then: confirmation is rejected because the deposit has not been paid

---

**As a** manager,
**I want to** waive the deposit requirement for a reservation,
**So that** I can accommodate VIP guests or special circumstances.

- Given: a reservation with a $50 deposit required
- When: a manager waives the deposit requirement
- Then: the deposit status changes to Waived

---

**As a** staff member,
**I want to** process a deposit refund for a cancelled reservation,
**So that** the guest receives their money back when appropriate.

- Given: a cancelled reservation with a previously paid deposit
- When: staff processes a deposit refund after cancellation
- Then: the deposit status changes to Refunded with a refund timestamp

---

**As a** host,
**I want to** forfeit a deposit when a booking with a paid deposit is cancelled within 12 hours,
**So that** the restaurant retains compensation for the late cancellation.

- Given: a confirmed booking with a paid deposit cancelled at the last minute (within 12 hours)
- When: the booking is cancelled and the deposit is forfeited due to late cancellation
- Then: the booking should be cancelled with the deposit status set to forfeited

---

## Table Management

**As a** manager,
**I want to** create a new table with capacity range, shape, and name,
**So that** the venue's seating inventory is accurately represented in the system.

- Given: no tables exist in the venue
- When: a new corner booth table T5 is created with capacity 2-6 and rectangle shape
- Then: the table is created with all specified properties and Available status

---

**As a** host,
**I want to** seat a party at an available table with linked booking and order details,
**So that** the table is marked as occupied and all relevant information is associated.

- Given: an available table
- When: the Smith Party of 4 is seated with a linked booking and order
- Then: the table status changes to Occupied with all occupancy details recorded

---

**As a** host,
**I want to** clear a table when a walk-in guest departs,
**So that** the bussing team knows the table needs cleaning.

- Given: an occupied table with a walk-in guest
- When: the guest departs and the table is cleared
- Then: the table status changes to Dirty and the occupancy is removed

---

**As a** busser,
**I want to** mark a dirty table as clean,
**So that** the host knows the table is ready for the next guest.

- Given: a dirty table that needs bussing
- When: staff marks the table as clean
- Then: the table status changes back to Available

---

**As a** host,
**I want to** block a table for a VIP reservation,
**So that** the table is held and not assigned to other guests.

- Given: an available table
- When: the table is blocked for a VIP reservation
- Then: the table status changes to Blocked

---

**As a** host,
**I want to** combine two tables to accommodate a larger party,
**So that** groups exceeding single-table capacity can be seated together.

- Given: two separate tables
- When: the tables are combined to accommodate a larger party
- Then: the first table tracks the combination with the second table

---

**As a** system,
**I want to** prevent seating a guest at an already occupied table,
**So that** double-bookings and service conflicts are avoided.

- Given: a table currently occupied by a guest
- When: staff attempts to seat another guest at the same table
- Then: seating is rejected because the table is already occupied

---

**As a** system,
**I want to** prevent combining a table that is marked as non-combinable,
**So that** physical or operational constraints on table arrangement are respected.

- Given: a table marked as non-combinable
- When: staff attempts to combine it with another table
- Then: the combination is rejected because the table is not combinable

---

## Floor Plans

**As a** manager,
**I want to** create a floor plan with name, dimensions, and default designation,
**So that** the venue layout is represented digitally for table management.

- Given: a venue with no floor plans
- When: a new Patio floor plan is created as the default with specific dimensions
- Then: the floor plan is created with correct name, dimensions, default flag, and active status

---

**As a** manager,
**I want to** add tables to a floor plan,
**So that** the spatial arrangement of tables is captured for the host stand view.

- Given: an empty floor plan
- When: two tables are added to the floor plan
- Then: both table IDs are tracked on the floor plan

---

**As a** manager,
**I want to** define sections within a floor plan with color codes,
**So that** staff can visually distinguish areas like the bar and dining room.

- Given: an empty floor plan
- When: "Bar Area" and "Dining Room" sections are added with color codes
- Then: both sections are tracked with their names and colors

---

**As a** manager,
**I want to** deactivate a floor plan,
**So that** outdated layouts are hidden without being deleted.

- Given: an active floor plan
- When: the floor plan is deactivated
- Then: the floor plan is no longer active

---

## Booking Settings & Availability

**As a** manager,
**I want to** have default booking settings applied when a venue is first configured,
**So that** the venue can accept reservations immediately with sensible defaults.

- Given: a venue with no booking settings configured
- When: booking settings are initialized for the venue
- Then: default settings are applied (11am open, 10pm close, max party size 8)

---

**As a** system,
**I want to** mark all time slots as unavailable when the requested party size exceeds the maximum,
**So that** oversized parties are directed to contact the venue directly.

- Given: a venue with a maximum online party size of 8
- When: availability is requested for a party of 12
- Then: all time slots are returned as unavailable because the party exceeds the maximum

---

**As a** guest,
**I want to** see only time slots within the venue's operating hours when checking availability,
**So that** I can only book during times the restaurant is open.

- Given: a venue open from 11am to 10pm
- When: slot availability is checked at noon, 6pm, 10am (before open), and 11pm (after close)
- Then: slots during operating hours are available; slots outside are unavailable

---

**As a** manager,
**I want to** block a specific date from accepting reservations,
**So that** the venue can close for private events or holidays without manual intervention.

- Given: a venue accepting reservations
- When: a specific date is blocked (e.g., private event or holiday)
- Then: the date is marked as blocked and no reservations can be made for that date

---

**As a** guest,
**I want to** see available time slots generated at the configured interval,
**So that** I can choose a reservation time that fits my schedule.

- Given: a venue configured with 30-minute slot intervals from 6pm to 10pm
- When: availability is requested for a party of 2
- Then: exactly 8 time slots are generated at 30-minute intervals

---

## Booking Calendar

**As a** host,
**I want to** see confirmed reservations on the booking calendar with all relevant details,
**So that** I can plan seating and service for upcoming guests.

- Given: an empty booking calendar
- When: a confirmed reservation for the Smith Party (4 guests at 7pm) is added
- Then: the booking appears on the calendar with the correct confirmation code, time, and party details

---

**As a** manager,
**I want to** see the total cover count for a given day,
**So that** I can plan staffing and prep based on expected volume.

- Given: a booking calendar with no reservations
- When: three confirmed reservations totaling 12 covers are added
- Then: the total cover count for the day is 12

---

**As a** host,
**I want to** query bookings by time range for lunch and dinner windows,
**So that** I can focus on the reservations relevant to the current service period.

- Given: a calendar with lunch bookings and dinner bookings
- When: bookings are queried for the lunch window and dinner window
- Then: each time range returns the correct bookings

---

**As a** host,
**I want to** see all bookings sorted chronologically,
**So that** I can review the day's reservations in the order they will arrive.

- Given: three reservations added in non-chronological order (7pm, 8pm, 6pm)
- When: all bookings are retrieved from the calendar
- Then: the bookings are returned sorted by time (6pm, 7pm, 8pm)

---

## Waitlist

**As a** host,
**I want to** add a walk-in party to the waitlist with a quoted wait time,
**So that** walk-in guests are tracked and given an estimated wait.

- Given: an active waitlist for a venue
- When: a walk-in party of 4 is added with a 30-minute quoted wait
- Then: the entry is created at position 1 with the quoted wait time

---

**As a** host,
**I want to** notify a waiting party when their table is ready,
**So that** the guest knows to return to the host stand.

- Given: a party of 4 waiting on the waitlist
- When: their table becomes ready and they are notified
- Then: the entry status changes to Notified with a notification timestamp

---

**As a** host,
**I want to** seat a party from the waitlist,
**So that** the waitlist reflects that the guest has been accommodated.

- Given: a party of 4 waiting on the waitlist
- When: a table opens up and the party is seated
- Then: the entry status changes to Seated with a seating timestamp

---

**As a** host,
**I want to** record when a waiting party leaves without being seated,
**So that** the waitlist accurately reflects who is still waiting.

- Given: a party of 4 waiting on the waitlist
- When: the guest decides to leave without being seated
- Then: the entry status changes to Left

---

**As a** host,
**I want to** convert a waitlist entry into a formal reservation,
**So that** the guest's wait transitions into a confirmed booking.

- Given: a walk-in party of 4 on the waitlist
- When: the party is converted to a formal reservation
- Then: a booking is created and linked to the waitlist entry

---

**As a** host,
**I want to** view only active waitlist entries,
**So that** I can focus on guests who still need to be seated.

- Given: three waitlist entries where one is seated and one is notified
- When: the active entries are requested
- Then: only the notified and waiting entries are returned (seated entries are excluded)

---

**As a** host,
**I want to** find the next suitable party for a table that just opened,
**So that** I can seat a party whose size fits the available table.

- Given: a waitlist with parties of 6, 2, and 4 guests
- When: a 4-top table becomes available and the next suitable entry is sought
- Then: a party that fits the table capacity (between 2 and 4 guests) should be returned

---

## Turn Time Analytics

**As a** manager,
**I want to** record and aggregate table turn times across party sizes,
**So that** I can understand dining duration patterns and optimize table utilization.

- Given: a turn time analytics grain with no prior data
- When: multiple table turn times are recorded for varying party sizes
- Then: the overall stats show the correct sample count and average turn time

---

**As a** manager,
**I want to** get estimated turn times based on historical patterns for a given day and time,
**So that** I can predict table availability and quote accurate wait times.

- Given: historical Friday dinner turn times recorded over several weeks (100-120 minutes each)
- When: an estimated turn time is requested for a party of 4 on Friday at 7 PM
- Then: the estimate reflects the historical pattern

---

**As a** manager,
**I want to** identify tables that have exceeded the expected turn time,
**So that** I can proactively manage slow turns and improve table throughput.

- Given: two active seatings, one seated 30 minutes ago and another seated 3 hours ago
- When: long-running tables are queried with a 30-minute overdue threshold
- Then: only the 3-hour seating is flagged as overdue

---

## No-Show Detection

**As a** host,
**I want to** mark a booking as a no-show after the reservation time has passed,
**So that** the table can be released and the no-show is recorded for future reference.

- Given: a confirmed booking whose reservation time has passed (1 hour ago)
- When: the host marks the booking as a no-show
- Then: the booking status changes to NoShow

---

**As a** host,
**I want to** forfeit a deposit when a guest with a paid deposit does not show up,
**So that** the restaurant retains compensation for the lost table revenue.

- Given: a confirmed booking with a paid deposit that the guest never arrived for
- When: the booking is marked as a no-show and the deposit is forfeited
- Then: the booking is NoShow status with the deposit marked as forfeited

---

**As a** system,
**I want to** surface bookings that are past their reservation time and still unresolved,
**So that** the host is prompted to confirm arrival or mark as no-show.

- Given: a no-show detection grain with a booking registered that was due 30 minutes ago
- When: pending no-show checks are retrieved
- Then: the late booking appears in the pending checks list

---

**As a** system,
**I want to** remove a booking from no-show tracking when the guest arrives,
**So that** arrived guests are not incorrectly flagged as no-shows.

- Given: a booking registered for no-show tracking with one pending check
- When: the guest arrives and the booking is unregistered from no-show detection
- Then: the pending checks list is empty
