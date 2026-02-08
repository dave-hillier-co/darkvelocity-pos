# Order Management User Stories

Stories extracted from unit test specifications covering order lifecycle, line items, discounts, payments, bill splitting, and tax calculation.

## Order Lifecycle

### Creating Orders

**As a** server,
**I want to** create a new dine-in order,
**So that** the order is assigned a unique number and creation timestamp.

- Given: no existing order
- When: a new dine-in order is created
- Then: the order is assigned a unique number and creation timestamp

**As a** server,
**I want to** retrieve the order state,
**So that** I can see all order details including type, table, and guest count.

- Given: an open dine-in order for table T5 with 4 guests
- When: the order state is retrieved
- Then: all order details including type, table, and guest count are returned

### Adding Line Items

**As a** server,
**I want to** add items to an order,
**So that** the line total reflects the quantity and the order has line items.

- Given: an open dine-in order
- When: 2 Burgers at $12.99 each are added
- Then: the line total reflects the quantity and the order has one line item

**As a** server,
**I want to** add items with modifiers,
**So that** the line total includes the base price plus all modifier costs.

- Given: an open dine-in order
- When: a Burger with Extra Cheese and Bacon modifiers is added
- Then: the line total includes the base price plus all modifier costs

### Updating Line Items

**As a** server,
**I want to** update a line item's quantity,
**So that** the line total recalculates to reflect the new quantity.

- Given: an order with a single Burger line item
- When: the quantity is updated to 3
- Then: the line total recalculates to reflect the new quantity

### Voiding and Removing Lines

**As a** manager,
**I want to** void a line item with a reason,
**So that** the line is marked as voided and excluded from the subtotal.

- Given: an order with a Burger line item
- When: the line is voided with reason "Customer changed mind"
- Then: the line is marked as voided and excluded from the subtotal

**As a** server,
**I want to** remove a line item,
**So that** the order has no remaining line items.

- Given: an order with a Burger line item
- When: the line is removed
- Then: the order has no remaining line items

### Sending to Kitchen

**As a** server,
**I want to** send an order to the kitchen,
**So that** the order status changes to Sent and all lines are marked as sent.

- Given: an order with a Burger line item
- When: the order is sent to the kitchen
- Then: the order status changes to Sent and all lines are marked as sent

## Discounts

**As a** manager,
**I want to** apply a percentage discount to an order,
**So that** the discount is calculated correctly.

- Given: an order with a $100 item
- When: a 10% percentage discount is applied
- Then: the discount total is $10

**As a** manager,
**I want to** apply a fixed amount discount,
**So that** the discount is applied correctly.

- Given: an order with a $100 item
- When: a $5 fixed amount discount is applied
- Then: the discount total is $5

## Payments

**As a** cashier,
**I want to** record a full payment with tip,
**So that** the order is marked as paid with zero balance due.

- Given: an order with a $100 item
- When: full payment with a $10 tip is recorded
- Then: the order is marked as paid with zero balance due

**As a** cashier,
**I want to** record a partial payment,
**So that** the order is marked as partially paid with remaining balance.

- Given: an order with a $100 item
- When: a $50 partial cash payment is recorded
- Then: the order is marked as partially paid with remaining balance

## Closing and Voiding Orders

**As a** manager,
**I want to** close a fully paid order,
**So that** the order status changes to Closed with a closing timestamp.

- Given: a fully paid dine-in order
- When: the order is closed
- Then: the order status changes to Closed with a closing timestamp

**As a** system,
**I want to** prevent closing unpaid orders,
**So that** revenue is not lost.

- Given: an unpaid dine-in order with an outstanding balance
- When: closing the order is attempted
- Then: an error is raised because the balance has not been settled

**As a** manager,
**I want to** void an order,
**So that** the order status changes to Voided with the void reason recorded.

- Given: an open dine-in order with line items
- When: the order is voided because the customer left
- Then: the order status changes to Voided with the void reason recorded

## Table and Server Assignment

**As a** host,
**I want to** transfer an order to a different table,
**So that** the order reflects the new table assignment.

- Given: a dine-in order at table T1
- When: the order is transferred to table T10
- Then: the order reflects the new table assignment

**As a** manager,
**I want to** assign a server to an order,
**So that** the order records the server's ID and name.

- Given: an open dine-in order
- When: server John Smith is assigned to the order
- Then: the order records the server's ID and name

**As a** server,
**I want to** assign a customer to an order,
**So that** the order records the customer's ID and name.

- Given: an open dine-in order
- When: customer Jane Doe is assigned to the order
- Then: the order records the customer's ID and name

## Bill Splitting

### Split by Items

**As a** server,
**I want to** split items to a new order,
**So that** each guest can pay separately.

- Given: an order with Burger, Fries, and Soda at table T1
- When: Fries and Soda are split to a new order
- Then: the original order retains only the Burger and the new order contains the moved items

**As a** system,
**I want to** prevent invalid bill splits,
**So that** data integrity is maintained.

- Given: an order with a Burger
- When: a bill split is attempted with no lines specified
- Then: an error is raised requiring at least one line

- Given: an order with a single Burger
- When: all lines are selected for a split
- Then: an error is raised because at least one line must remain on the original order

- Given: a closed and fully paid dine-in order
- When: a bill split is attempted
- Then: an error is raised because closed orders cannot be split

**As a** server,
**I want to** pay split orders independently,
**So that** each guest settles their own check.

- Given: an order split into two separate checks
- When: each check is paid and closed independently
- Then: both orders reach Closed status with zero balance

### Split by People

**As a** server,
**I want to** calculate an even split among guests,
**So that** each guest pays an equal share.

- Given: an order with a $100 meal
- When: a 4-way even split is calculated
- Then: four equal guest shares summing to the balance due are returned

**As a** system,
**I want to** handle uneven splits correctly,
**So that** the total always matches the balance due.

- Given: an order with a $100 meal that does not divide evenly by 3
- When: a 3-way split is calculated
- Then: shares sum exactly to the balance due with remainder distributed

**As a** system,
**I want to** prevent invalid split requests,
**So that** errors are caught early.

- Given: an order with a $100 meal
- When: a split among zero people is requested
- Then: an error is raised because the number of people must be greater than zero

- Given: a fully paid order with zero balance due
- When: a 2-way split is calculated
- Then: the split is marked invalid with no shares returned

### Split by Custom Amounts

**As a** server,
**I want to** specify custom split amounts,
**So that** guests can pay different amounts.

- Given: an order with a $100 meal
- When: custom split amounts that sum to the balance due are provided
- Then: the split is marked valid with correct shares

- Given: an order with a $100 meal
- When: custom split amounts that do not sum to the balance due are provided
- Then: the split is marked invalid

## Per-Item Tax Rates

**As a** system,
**I want to** calculate tax per item at different rates,
**So that** tax is accurate for different product categories.

- Given: an open dine-in order
- When: items with different tax rates (food at 10%, alcohol at 20%, zero-rated gift card) are added
- Then: tax is calculated per item and order totals reflect the correct combined tax

## Bundle/Combo Meals

**As a** server,
**I want to** add combo meals with component choices,
**So that** guests can customize their bundle selections.

- Given: an open dine-in order
- When: a Combo Meal with Cheeseburger (Main), Large Fries (Side), and Large Coke (Drink) components is added
- Then: the bundle components are stored with their slot assignments

**As a** server,
**I want to** add combo meals with premium upgrades,
**So that** upgrade charges are included in the line total.

- Given: an open dine-in order
- When: a Combo Meal with Onion Rings (+$1.50 upgrade) and Milkshake (+$2.00 upgrade) is added
- Then: the line total includes the base price plus all upgrade charges ($9.99 + $1.50 + $2.00 = $13.49)

## Line Items (Standalone Grain)

**As a** system,
**I want to** manage line items as a reusable collection,
**So that** multiple domains (orders, purchase documents) can use consistent line item behavior.

- Given: an empty line items collection for an order
- When: a menu item with quantity 2 at $10 is added
- Then: the line item is created with correct extended price and totals

- Given: a line items collection with one item at quantity 1 and $10
- When: the line is updated to quantity 3 at $12
- Then: the line reflects the new quantity, price, and extended price of $36

- Given: a line items collection with one active line item
- When: the line is voided with a reason
- Then: the line is marked as voided and excluded from non-voided queries

- Given: two line items grains for the same owner ID but different owner types (order vs purchase-doc)
- When: items are added to each grain independently
- Then: each grain maintains isolated state with its own line items
