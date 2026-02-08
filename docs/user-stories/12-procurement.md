# Procurement User Stories

Stories extracted from unit test specifications covering suppliers, purchase orders, deliveries, purchase document processing, partial deliveries, and delivery discrepancies.

## Supplier Management

**As a** purchasing manager,
**I want to** register a new supplier with contact details, payment terms, and lead time,
**So that** the organization has a record to place orders against.

- Given: no supplier record exists
- When: a new supplier is registered with contact details, payment terms, and lead time
- Then: the supplier is created with all provided details and defaults to active with no purchases

**As a** purchasing manager,
**I want to** update supplier details without overwriting unchanged fields,
**So that** corrections are safe and incremental.

- Given: an existing meat supplier with 14-day payment terms
- When: the supplier name, contact, and payment terms are updated
- Then: only the changed fields are updated while unchanged fields retain their original values

**As a** purchasing manager,
**I want to** see supplier performance metrics based on purchase history,
**So that** I can evaluate reliability and spend.

- Given: a supplier with no purchase history
- When: three purchases are recorded -- two on-time and one late
- Then: year-to-date spend totals $4,500 and on-time delivery rate is 66%

**As a** purchasing manager,
**I want to** deactivate a supplier that is no longer in business,
**So that** no new orders are placed with them while history is preserved.

- Given: an active supplier
- When: the supplier is updated with IsActive set to false and a note that they are no longer in business
- Then: the supplier is marked as inactive with the closing note recorded

## Supplier Catalog

**As a** purchasing manager,
**I want to** add an ingredient to a supplier's catalog with pricing and SKU,
**So that** purchase orders can reference the supplier's own product codes and costs.

- Given: a dairy farm supplier with no ingredients in their catalog
- When: whole milk is added to the supplier's ingredient catalog with pricing and SKU
- Then: the supplier catalog contains the milk entry with correct price and supplier SKU

**As a** purchasing manager,
**I want to** add multiple ingredients to a supplier's catalog,
**So that** the full range of products available from this vendor is captured.

- Given: a bakery supplies vendor with an empty catalog
- When: flour, sugar, and butter are each added to the catalog
- Then: the supplier catalog contains all three ingredients

**As a** purchasing manager,
**I want to** update an existing catalog entry with new pricing and SKU,
**So that** cost changes from the supplier are reflected without creating duplicates.

- Given: a seafood supplier with salmon fillet already in their catalog at $45/lb
- When: the same salmon fillet ingredient is re-added with a new price and SKU
- Then: the catalog still has one entry but reflects the updated price, SKU, and minimum order

**As a** purchasing manager,
**I want to** remove a discontinued product from a supplier's catalog,
**So that** it is no longer offered for selection on purchase orders.

- Given: a beverage distributor with cola syrup and orange juice in their catalog
- When: cola syrup is removed from the supplier catalog
- Then: only orange juice remains in the catalog

## Purchase Orders

**As a** purchasing manager,
**I want to** create a new purchase order for a supplier with an expected delivery date,
**So that** I can begin building the order before submitting it.

- Given: no purchase order exists
- When: a new purchase order is created for a supplier with an expected delivery date
- Then: the order is in draft status with a generated PO number, no lines, and zero total

**As a** purchasing manager,
**I want to** add line items to a draft purchase order,
**So that** the order reflects what I need to procure.

- Given: a draft purchase order with no line items
- When: 50 units of tomatoes at $2.50 each are added as a line item
- Then: the order has one line totaling $125 and the order total reflects this

**As a** purchasing manager,
**I want to** submit a draft purchase order to the supplier,
**So that** the supplier knows what to deliver.

- Given: a draft purchase order with a line item for carrots
- When: the purchase order is submitted to the supplier
- Then: the order status changes to submitted with a recorded submission timestamp

**As a** system,
**I want to** prevent submission of a purchase order with no line items,
**So that** empty orders are not sent to suppliers.

- Given: a draft purchase order with no line items
- When: the empty order is submitted
- Then: the operation fails because a purchase order cannot be submitted without lines

**As a** system,
**I want to** prevent modifications to a purchase order after submission,
**So that** the supplier's copy and the system's copy stay in sync.

- Given: a purchase order that has already been submitted to the supplier
- When: a new line item is added to the submitted order
- Then: the operation fails because lines cannot be added after submission

**As a** system,
**I want to** track received quantities across multiple deliveries and close the order when complete,
**So that** the purchasing manager knows the order is fulfilled.

- Given: a submitted purchase order for 100 units of rice
- When: rice is received in three separate deliveries of 40, 35, and 25 units
- Then: the total received accumulates to 100 and the order is marked as received

**As a** purchasing manager,
**I want to** cancel a submitted purchase order with a reason,
**So that** the cancellation is documented and the order is closed.

- Given: a submitted purchase order for mushrooms
- When: the order is cancelled because the supplier is out of stock
- Then: the order status is cancelled with a timestamp and the cancellation reason recorded

**As a** system,
**I want to** prevent cancellation of a fully received purchase order,
**So that** historical receiving records remain intact.

- Given: a purchase order that has been fully received
- When: an attempt is made to cancel the received order
- Then: the operation fails because a fully received order cannot be cancelled

**As a** system,
**I want to** accept over-deliveries without blocking the receiving process,
**So that** service is not interrupted and discrepancies are tracked for reconciliation.

- Given: a submitted purchase order for 10 units of an item
- When: 15 units are received (over-delivery)
- Then: the over-delivery is accepted and tracked, reflecting the negative stock philosophy

## Receiving Deliveries

**As a** receiving clerk,
**I want to** create a delivery record linked to a supplier and purchase order,
**So that** incoming goods are formally tracked.

- Given: no delivery record exists
- When: a delivery is created linked to a supplier, purchase order, and location
- Then: the delivery is in pending status with a generated delivery number and no lines or discrepancies

**As a** receiving clerk,
**I want to** receive a walk-in delivery without a prior purchase order,
**So that** unplanned deliveries are still captured in the system.

- Given: no delivery record exists and no prior purchase order was placed
- When: a walk-in delivery is created without a linked purchase order
- Then: the delivery is created in pending status with no purchase order reference

**As a** receiving clerk,
**I want to** record received items with quantities, prices, and batch numbers,
**So that** each delivery line is traceable.

- Given: a pending delivery with no line items
- When: 5 units of fresh basil at $3.00 each are received with a batch number
- Then: the delivery has one line totaling $15.00 with batch tracking information

**As a** receiving clerk,
**I want to** accept a delivery after verifying the goods,
**So that** the delivery is closed and inventory can be updated.

- Given: a pending delivery with bread items and batch tracking
- When: the delivery is accepted by a staff member
- Then: the delivery status changes to accepted with a recorded acceptance timestamp

**As a** receiving clerk,
**I want to** reject a delivery when the product has expired,
**So that** spoiled goods are refused and the reason is documented.

- Given: a pending delivery of fish with an expiry date that has already passed
- When: the delivery is rejected because the product expired on arrival
- Then: the delivery status changes to rejected with a timestamp and the rejection reason

## Delivery Discrepancies

**As a** receiving clerk,
**I want to** record a short delivery discrepancy when fewer units arrive than expected,
**So that** the shortage is documented for follow-up with the supplier.

- Given: a delivery with 80 chicken wings received against an expected 100
- When: a short delivery discrepancy of 20 units is recorded
- Then: the delivery is flagged with a short delivery discrepancy showing expected vs actual quantities

**As a** receiving clerk,
**I want to** record a damaged goods discrepancy,
**So that** breakage is documented for supplier credit claims.

- Given: a delivery with 48 glass bottles received, 6 of which are broken
- When: a damaged goods discrepancy is recorded
- Then: the delivery is flagged as having discrepancies

**As a** receiving clerk,
**I want to** accept a delivery even when discrepancies exist,
**So that** usable goods enter inventory while the discrepancy remains on record.

- Given: a pending delivery of cheese with a recorded short delivery discrepancy of 5 units
- When: the delivery is accepted despite the discrepancy
- Then: the delivery is accepted but still flagged as having discrepancies

**As a** receiving clerk,
**I want to** record an over-delivery discrepancy when more units arrive than expected,
**So that** the surplus is documented and inventory reflects reality.

- Given: a delivery with a line for 120 potatoes received against an expected 100
- When: an over-delivery discrepancy is recorded
- Then: the discrepancy shows OverDelivery type with expected 100 and actual 120

**As a** receiving clerk,
**I want to** accept a delivery with discrepancies and preserve the discrepancy record,
**So that** the receiving process is not blocked and the issue can be resolved later.

- Given: a delivery with a recorded short delivery discrepancy on cheese (50 expected, 45 received)
- When: the delivery is accepted despite the discrepancy
- Then: the delivery status transitions to Accepted while preserving the discrepancy record

**As a** receiving clerk,
**I want to** reject an entire delivery when a quality issue makes all goods unusable,
**So that** none of the product enters inventory and the reason is documented.

- Given: a delivery of fresh fish with all product expired and a quality issue discrepancy recorded
- When: the entire delivery is rejected
- Then: the delivery status transitions to Rejected with the rejection reason noting expired product

## Partial Deliveries

**As a** system,
**I want to** track partial deliveries against a purchase order line and transition the PO status as quantities accumulate,
**So that** the purchasing manager has visibility into fulfillment progress.

- Given: a submitted purchase order with a single line of 100 units of ground beef
- When: the line is received in three deliveries (30, 40, 30 units)
- Then: the PO transitions through PartiallyReceived to Received as quantities accumulate to 100

**As a** system,
**I want to** track received quantities independently per line on a multi-line purchase order,
**So that** partial fulfillment of individual items is visible.

- Given: a submitted purchase order with three lines (chicken 50, beef 30, pork 20)
- When: chicken is fully received, beef is partially received, and pork is not yet received
- Then: each line tracks its received quantity independently and the PO remains PartiallyReceived

**As a** system,
**I want to** handle many small deliveries against a single purchase order line,
**So that** high-frequency receiving workflows are supported.

- Given: a submitted purchase order for 1,000 small parts
- When: the line is received across 10 deliveries of 100 units each
- Then: the accumulated received quantity reaches 1,000 and the PO status transitions to Received

## Purchase Document Processing

**As a** system,
**I want to** initialize a purchase document when an invoice is received via email,
**So that** the document enters the processing pipeline with correct defaults.

- Given: a new purchase document grain
- When: an invoice is received via email
- Then: the document is initialized with Received status and defaults to unpaid

**As a** system,
**I want to** initialize a purchase document when a receipt is captured via photo,
**So that** paper receipts are digitized with the assumption they are already paid.

- Given: a new purchase document grain
- When: a receipt is received via photo capture
- Then: the document is initialized with Received status and defaults to paid

**As a** system,
**I want to** apply OCR extraction results to a purchase document,
**So that** vendor details and line items are populated automatically.

- Given: an invoice in processing status
- When: OCR extraction results are applied with vendor and line item data
- Then: the document moves to Extracted status with populated vendor, totals, and line items

**As a** purchasing manager,
**I want to** manually map an extracted line item to an inventory ingredient,
**So that** unrecognized items on the invoice are linked to the correct stock record.

- Given: an extracted invoice with unmapped line items
- When: a line item is manually mapped to an inventory ingredient
- Then: the line records the ingredient mapping with manual source and full confidence

**As a** purchasing manager,
**I want to** confirm an extracted purchase document after review,
**So that** it is approved for posting to inventory and accounts payable.

- Given: an extracted invoice with reviewed line items
- When: a user confirms the document
- Then: the document moves to Confirmed status with a confirmation timestamp

**As a** purchasing manager,
**I want to** reject a purchase document that is a duplicate or invalid,
**So that** it does not affect inventory or financial records.

- Given: a received purchase document
- When: a user rejects the document as a duplicate
- Then: the document moves to Rejected status with the rejection reason and user recorded

**As a** system,
**I want to** handle OCR extraction failures gracefully,
**So that** documents with poor image quality are flagged for manual processing.

- Given: a receipt photo in processing status
- When: OCR extraction fails due to poor document quality
- Then: the document moves to Failed status with the error details recorded

**As a** purchasing manager,
**I want to** override extracted values when confirming a purchase document,
**So that** OCR errors are corrected before the document is posted.

- Given: an extracted invoice with auto-detected vendor information
- When: the document is confirmed with vendor name, date, and currency overrides
- Then: the confirmed document uses the overridden values instead of the extracted ones
