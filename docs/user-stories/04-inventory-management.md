# Inventory Management User Stories

Stories extracted from unit test specifications covering inventory tracking, batch receiving, FIFO costing, negative stock handling, waste, stock takes, vendor item mapping, and inter-site transfers.

## Inventory Tracking

**As a** kitchen manager,
**I want to** register a new ingredient with reorder and par level thresholds,
**So that** the system can monitor stock levels and trigger alerts.

- Given: a new ingredient (Ground Beef) being tracked for the first time
- When: the inventory record is initialized with reorder point of 10 lbs and par level of 50 lbs
- Then: the ingredient is registered with its SKU, unit, and stock thresholds

**As a** kitchen manager,
**I want to** check whether I have enough stock for a production run,
**So that** I can plan prep work without interruption.

- Given: 50 lbs of Ground Beef in stock
- When: checking stock sufficiency for 30, 50, and 51 lbs
- Then: sufficient for 30 and 50 lbs, insufficient for 51 lbs

**As a** system,
**I want to** accurately track inventory through a sequence of mixed operations,
**So that** the running total always reflects reality.

- Given: an empty ingredient inventory
- When: multiple receipts, consumptions, and waste events occur in sequence
- Then: the final stock of 80 units accurately reflects all operations

## Batch Receiving & FIFO

**As a** receiving clerk,
**I want to** receive a batch of ingredients into inventory,
**So that** stock on hand and cost per unit are updated.

- Given: an empty Ground Beef inventory
- When: a batch of 100 lbs at $5.00/lb is received
- Then: stock on hand is 100 lbs and cost is $5.00/lb with one active batch

**As a** system,
**I want to** calculate weighted average cost when multiple batches are received,
**So that** the cost of goods sold is accurate.

- Given: an empty Ground Beef inventory
- When: 100 lbs at $5.00/lb and 100 lbs at $7.00/lb are received
- Then: stock on hand is 200 lbs with a weighted average cost of $6.00/lb

**As a** system,
**I want to** consume stock using FIFO order,
**So that** the oldest inventory is used first and costing is accurate.

- Given: 50 lbs of Ground Beef at $5.00/lb and 50 lbs at $7.00/lb received in order
- When: 60 lbs are consumed for production
- Then: FIFO depletes all 50 lbs from the first batch and 10 lbs from the second, costing $320

**As a** system,
**I want to** reset the weighted average cost when stock is fully depleted and a new batch arrives,
**So that** stale cost data does not distort margins.

- Given: Ground Beef stock fully depleted after receiving 50 lbs at $5.00/lb and consuming all
- When: a new batch of 30 lbs at $8.00/lb is received
- Then: weighted average cost resets to the new batch cost of $8.00/lb

**As a** kitchen manager,
**I want to** write off expired batches,
**So that** only usable stock is counted as available inventory.

- Given: 50 lbs in an expired batch and 50 lbs in a valid batch
- When: expired batches are written off
- Then: only the 50 lbs from the valid batch remain in stock

## Negative Stock Philosophy

**As a** system,
**I want to** allow stock to go negative when consumption exceeds recorded inventory,
**So that** service is never interrupted and discrepancies are flagged for reconciliation.

- Given: 50 lbs of Ground Beef in stock
- When: 100 lbs are consumed for production
- Then: stock goes to -50 lbs, flagging a discrepancy for reconciliation

**As a** system,
**I want to** track negative stock caused by unrecorded transfers or deliveries,
**So that** discrepancies surface naturally without blocking sales.

- Given: 5 portions recorded in system
- When: Kitchen sells 8 (had unrecorded stock from transfer)
- Then: System shows -3, flagging discrepancy

**As a** system,
**I want to** cover negative stock deficits when new deliveries arrive,
**So that** the running total stays accurate without manual intervention.

- Given: Stock at -5 from overselling
- When: New delivery of 30 arrives
- Then: Stock is 25 (deficit covered + new stock)

**As a** kitchen manager,
**I want to** correct large discrepancies discovered after busy periods through physical counts,
**So that** the system reflects actual inventory on hand.

- Given: System shows -20 after busy weekend
- When: Physical count finds 10 remaining (unrecorded transfer of 30 happened)
- Then: Stock corrected to actual

**As a** system,
**I want to** correctly report sufficiency when stock is negative,
**So that** reorder decisions are based on real availability.

- Given: 50 units in stock
- When: 80 units are consumed during high demand
- Then: stock goes to -30 units and sufficiency checks correctly report no available stock

**As a** system,
**I want to** maintain correct stock level classification even with partial replenishment of negative stock,
**So that** managers know stock is still unavailable.

- Given: stock at -80 units after a large oversell
- When: a partial replenishment of 50 units arrives
- Then: stock remains at -30 units and is still classified as OutOfStock

## Waste & Adjustments

**As a** kitchen manager,
**I want to** record waste due to spoilage,
**So that** stock on hand reflects usable inventory and waste is tracked.

- Given: 100 lbs of Ground Beef in stock
- When: 10 lbs are recorded as waste due to spoilage
- Then: stock on hand decreases to 90 lbs

**As a** kitchen manager,
**I want to** adjust inventory based on a physical count,
**So that** the system matches what is actually on the shelf.

- Given: 100 lbs of Ground Beef in stock
- When: a physical count adjustment sets the quantity to 120 lbs
- Then: stock on hand increases to 120 lbs to reflect the extra stock found

**As a** system,
**I want to** reverse inventory consumption when an order is voided,
**So that** stock levels accurately reflect what was actually used.

- Given: 100 units in stock with 30 consumed for an order
- When: the order consumption is reversed (voided)
- Then: stock returns to 100 units and the ledger accurately reflects the reversal

**As a** kitchen manager,
**I want to** transfer stock out to another site,
**So that** sister locations can share inventory when needed.

- Given: 100 lbs of Ground Beef in stock
- When: 30 lbs are transferred out to another site
- Then: stock on hand decreases to 70 lbs

## Stock Level Monitoring

**As a** kitchen manager,
**I want to** see stock level classifications change as deliveries arrive,
**So that** I know at a glance whether to reorder.

- Given: Ground Beef inventory with reorder point of 10 lbs and par level of 50 lbs
- When: stock moves from 0 to 5 to 30 to 60 lbs through successive deliveries
- Then: stock level transitions through OutOfStock, Low, Normal, and AbovePar

## Stock Takes

**As a** manager,
**I want to** start a stock take for an ingredient at a site,
**So that** I can compare physical counts against theoretical inventory.

- Given: An ingredient with 100 units on hand at a site
- When: A monthly stock take is started for that ingredient with blind count disabled
- Then: The stock take is initialized as InProgress with the theoretical quantity of 100

**As a** manager,
**I want to** record a physical count and see the variance,
**So that** I understand how far actual stock deviates from the books.

- Given: An in-progress stock take for an ingredient with 100 units at $5.00 each
- When: A physical count of 95 units is recorded
- Then: The variance is calculated as -5 units worth -$25 with medium severity

**As a** manager,
**I want to** run stock takes in blind count mode,
**So that** counters are not biased by knowing the expected quantity.

- Given: A stock take started in blind count mode for an ingredient with 100 units
- When: Line items are retrieved without revealing theoretical quantities
- Then: The theoretical quantity is hidden (shown as 0) to prevent counting bias

**As a** manager,
**I want to** finalize a stock take and apply the adjustments to inventory,
**So that** the books are corrected to match reality.

- Given: A submitted stock take showing a physical count of 85 against a theoretical 100
- When: The stock take is finalized with adjustments applied
- Then: The stock take status transitions to Finalized and inventory is adjusted to 85 units

**As a** manager,
**I want to** generate a variance report for a multi-item stock take,
**So that** I can review total overages and shortages in one view.

- Given: A stock take with two ingredients counted: Item 1 at 90/100 and Item 2 at 55/50
- When: The variance report is generated
- Then: The report shows 2 items with variance, $100 positive variance, and $100 negative variance

**As a** manager,
**I want to** cancel an in-progress stock take,
**So that** incomplete counts do not affect inventory records.

- Given: An in-progress stock take for an ingredient at a site
- When: The stock take is cancelled
- Then: The status transitions to Cancelled and subsequent count recordings are rejected

## Vendor Item Mapping

**As a** purchasing manager,
**I want to** manually map a vendor product description to an inventory ingredient,
**So that** future invoices from that vendor are matched automatically.

- Given: an initialized vendor mapping for a supplier
- When: a vendor product description is manually mapped to an inventory ingredient
- Then: the mapping is created with manual source and full confidence

**As a** system,
**I want to** look up ingredient mappings by exact vendor product description,
**So that** known items on invoices are resolved instantly.

- Given: a vendor mapping with an exact product description registered
- When: the same description is looked up
- Then: the mapping is found via exact description match

**As a** system,
**I want to** look up ingredient mappings by supplier product code,
**So that** items are matched even when descriptions vary.

- Given: a vendor mapping with a supplier product code assigned
- When: looking up by product code with a different description
- Then: the mapping is found via product code match

**As a** system,
**I want to** learn mappings from confirmed purchase documents,
**So that** the system gets smarter with each invoice processed.

- Given: an initialized vendor mapping with no prior learned patterns
- When: a mapping is learned from a confirmed purchase document
- Then: the exact mapping is created and fuzzy matching patterns are generated

**As a** system,
**I want to** suggest ingredient matches for unknown vendor descriptions,
**So that** the purchasing manager can confirm mappings quickly.

- Given: a vendor mapping with learned patterns for chicken breast and ground beef
- When: suggestions are requested for a similar chicken description
- Then: the chicken ingredient is suggested as the top match

**As a** system,
**I want to** match vendor descriptions via fuzzy pattern matching,
**So that** minor variations in packaging or wording do not break automatic matching.

- Given: a learned vendor mapping for "Chicken Breast Boneless Skinless 10LB"
- When: a similar description differing only in package size is looked up
- Then: the mapping is found via fuzzy pattern matching above the similarity threshold

## Inter-Site Transfers

**As a** manager,
**I want to** request an inventory transfer between sites,
**So that** stock can be redistributed across locations.

- Given: A new inter-site inventory transfer with one line item for ground beef
- When: The transfer is requested with source site, destination site, and 25 lb of ground beef
- Then: The transfer is created with status Requested and the line item records the requested quantity

**As a** manager,
**I want to** approve a pending transfer,
**So that** the source site can begin preparing the shipment.

- Given: A pending inventory transfer between two sites
- When: A manager approves the transfer
- Then: The transfer status transitions to Approved with the approver recorded

**As a** manager,
**I want to** reject a transfer when stock is insufficient,
**So that** the source site does not commit inventory it cannot spare.

- Given: A pending inventory transfer between two sites
- When: The transfer is rejected due to insufficient stock at the source site
- Then: The transfer status transitions to Rejected with the rejection reason recorded

**As a** warehouse staff,
**I want to** ship an approved transfer and deduct inventory from the source,
**So that** stock on hand at the source reflects what was sent.

- Given: An approved transfer of 25 lb ground beef from a source site with 100 units on hand
- When: The transfer is shipped with a tracking number
- Then: The transfer status transitions to Shipped and source inventory is deducted to 75 units

**As a** receiving clerk,
**I want to** finalize receipt of a shipped transfer,
**So that** the destination site's inventory is credited.

- Given: A shipped transfer of 25 lb ground beef with destination inventory initialized
- When: The transfer receipt is finalized at the destination site
- Then: The transfer status transitions to Received and destination inventory is credited with 25 units

**As a** receiving clerk,
**I want to** record a short receipt when items are damaged in transit,
**So that** the variance is captured for investigation.

- Given: A shipped transfer of 25 lb ground beef between two sites
- When: The destination receives only 23 units due to transit damage
- Then: A negative variance of -2 units is recorded for the transfer line

**As a** manager,
**I want to** cancel a shipped transfer and return stock to the source,
**So that** inventory is restored when a transfer is aborted.

- Given: A shipped transfer that deducted 25 units from source inventory (100 down to 75)
- When: The transfer is cancelled with stock return to source enabled
- Then: The transfer status transitions to Cancelled and source inventory is restored to 100 units

## Transfer Variances

**As a** receiving clerk,
**I want to** record a negative variance when fewer units arrive than shipped,
**So that** transit losses are tracked and can be investigated.

- Given: A shipped inter-site transfer of 50 units of ground beef
- When: The destination site receives only 45 units (5 damaged in transit)
- Then: A negative variance of -5 is recorded against the shipped quantity

**As a** receiving clerk,
**I want to** record a positive variance when more units arrive than shipped,
**So that** overages are captured for reconciliation.

- Given: A shipped inter-site transfer of 60 units of chicken wings
- When: The destination site receives 65 units (extra units included)
- Then: A positive variance of +5 is recorded against the shipped quantity

**As a** system,
**I want to** track variances per item on a multi-item transfer,
**So that** shortages and overages are attributable to specific products.

- Given: A shipped transfer containing three items (A: 30, B: 20, C: 25 units)
- When: Items are received with mixed results (A: 28 short, B: 20 exact, C: 27 over)
- Then: Variances are tracked per item: A at -2, B at 0, C at +2

**As a** system,
**I want to** calculate variance values using each item's unit cost,
**So that** the financial impact of transit losses is visible.

- Given: A shipped transfer with an expensive item ($50/unit) and a cheap item ($5/unit)
- When: Both items are received 2 units short
- Then: Variance values reflect unit cost: -$100 for the expensive item and -$10 for the cheap item
