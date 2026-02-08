# Reporting User Stories

Stories extracted from unit test specifications covering daily sales aggregation, gross profit calculation, site dashboard cross-grain integration, period rollups, consumption variance reconciliation, waste tracking, daypart analysis, product mix, and payment breakdown.

## Daily Sales Aggregation

### Initialization

**As a** system,
**I want to** create a new daily sales record when aggregation begins for a venue,
**So that** the day's transactions have a dedicated container for accumulation.

- Given: a venue with no daily sales record for today
- When: the daily sales aggregation is initialized
- Then: a new sales record is created for the venue and date

### Recording Sales

**As a** system,
**I want to** aggregate individual sale transactions into daily totals,
**So that** managers can see an accurate picture of the day's business.

- Given: an initialized daily sales record
- When: two dine-in sales (burger and fries) are recorded
- Then: gross sales, net sales, COGS, transaction count, and guest count are correctly aggregated

### Channel Breakdown

**As a** manager,
**I want to** see sales broken down by channel (dine-in, takeout, delivery),
**So that** I can understand which service modes are driving revenue.

- Given: an initialized daily sales record
- When: one dine-in sale and one takeout sale of the same item are recorded
- Then: net sales are tracked separately by channel (DineIn and TakeOut)

### Channel Accumulation

**As a** system,
**I want to** accumulate multiple sales on the same channel into a running total,
**So that** channel breakdowns reflect the full day's activity.

- Given: an initialized daily sales record
- When: two delivery orders are recorded on the same channel
- Then: the delivery channel total accumulates both sales ($18.40 + $11.04 = $29.44)

### Category Breakdown

**As a** manager,
**I want to** see sales broken down by menu category,
**So that** I can identify which categories contribute most to revenue.

- Given: an initialized daily sales record
- When: sales are recorded across Mains, Beverages, and Desserts categories
- Then: net sales are tracked separately by category with correct totals

### Stream-Based Aggregation

**As a** system,
**I want to** aggregate sales arriving via the event stream,
**So that** real-time order completions are captured in the daily totals.

- Given: an initialized daily sales record
- When: two orders arrive via the event stream (one dine-in, one takeout)
- Then: sales from both orders are aggregated with correct totals by channel

## Sales Metrics & Gross Profit

### Sales Metrics with Discounts and Comps

**As a** manager,
**I want to** retrieve daily sales metrics including discounts and comps,
**So that** I can understand how promotions and complimentary items affect the bottom line.

- Given: a daily sales record with one sale including discounts and comps
- When: the sales metrics are retrieved
- Then: gross sales, discounts, comps, net sales, transaction count, and covers are correct

### Gross Profit Calculation

**As a** manager,
**I want to** see actual and theoretical gross profit with variance,
**So that** I can identify cost control issues and measure kitchen efficiency.

- Given: a daily sales record with $100 net sales, $30 theoretical COGS, and $32 actual COGS
- When: gross profit metrics are calculated using FIFO costing
- Then: actual GP is $68 (68%), theoretical GP is $70 (70%), and variance is $2

### Zero-Transaction Handling

**As a** system,
**I want to** return zero for average ticket and gross profit when no transactions exist,
**So that** division-by-zero errors are avoided and reports display cleanly.

- Given: an initialized daily sales record with no transactions
- When: the snapshot is retrieved
- Then: average ticket and gross profit percentage are both zero

## Voids & Sale Facts

### Void Tracking

**As a** cashier,
**I want to** record voids against the daily sales record,
**So that** managers can see the total value of voided items for the day.

- Given: a daily sales record with one recorded sale
- When: a $10 void is recorded because the customer changed their mind
- Then: the void amount is tracked in the daily metrics

### Sale Fact Retrieval

**As a** manager,
**I want to** retrieve individual sale facts from the daily record,
**So that** I can drill down into which products were sold.

- Given: a daily sales record with two different products sold
- When: the individual sale facts are retrieved
- Then: both product sale records are returned with their respective product IDs

## Sales Period Lifecycle

### End-of-Day Finalization

**As a** manager,
**I want to** finalize the daily sales record at end of day,
**So that** the numbers are locked and available for reporting.

- Given: an initialized daily sales record
- When: the sales period is finalized at end of day
- Then: the record is marked as finalized and remains accessible

### Idempotent Initialization

**As a** system,
**I want to** ensure that re-initializing a daily sales record does not overwrite existing data,
**So that** duplicate messages or retries do not corrupt the record.

- Given: an already initialized daily sales record
- When: initialization is attempted a second time with a different name
- Then: the original initialization is preserved (idempotent behavior)

### Uninitialized Record Guard

**As a** system,
**I want to** reject queries against a daily sales record that has not been initialized,
**So that** callers receive a clear error instead of misleading empty data.

- Given: a daily sales record that has not been initialized
- When: the snapshot is requested
- Then: an error is raised indicating the sales record is not initialized

## Site Dashboard - Sales Overview

### Cross-Grain Sales Aggregation

**As a** manager,
**I want to** view today's sales on the site dashboard aggregated from the daily sales grain,
**So that** I get a single at-a-glance view of the day's revenue across all channels.

- Given: a daily sales grain has recorded dine-in and takeout transactions for today
- When: today's sales are retrieved through the site dashboard
- Then: the dashboard aggregates gross sales, net sales, transaction count, and guest count across all channels

### Extended Metrics

**As a** manager,
**I want to** see average ticket size, revenue per cover, and gross profit percent on the dashboard,
**So that** I can gauge operational efficiency in real time.

- Given: a daily sales grain has recorded a single steak sale with gross, net, tax, and COGS
- When: extended dashboard metrics are retrieved
- Then: average ticket size, revenue per cover, and gross profit percent are calculated correctly

### Day-Over-Day Comparison

**As a** manager,
**I want to** see today's sales compared to yesterday's as a percentage change,
**So that** I can quickly spot upward or downward trends.

- Given: yesterday's sales were 92 net and today's sales are 105.80 net
- When: extended dashboard metrics are retrieved with day-over-day comparison
- Then: the today-vs-yesterday percentage reflects the approximately 15% increase

## Site Dashboard - Inventory & Variances

### Inventory Overview

**As a** manager,
**I want to** see current inventory status on the dashboard including stock value and alerts,
**So that** I can act on low-stock and expiring items before they impact service.

- Given: a daily inventory snapshot has recorded flour, sugar, and near-expiry milk
- When: the current inventory is retrieved through the site dashboard
- Then: the dashboard aggregates total stock value, SKU count, and flags low-stock and expiring-soon items

### Top Cost Variances

**As a** manager,
**I want to** see the highest cost variances on the dashboard ranked by severity,
**So that** I can investigate the biggest sources of waste or over-usage first.

- Given: a daily consumption grain has recorded ingredients with 50%, 5%, and 20% cost variances
- When: the top variances are retrieved through the site dashboard
- Then: variances are ordered by absolute cost variance descending with the highest-variance ingredient first

## Site Dashboard - Hourly Sales & Product Mix

### Hourly Sales by Daypart

**As a** manager,
**I want to** see hourly sales data on the dashboard integrated from daypart analysis,
**So that** I can identify peak hours and optimize staffing levels.

- Given: a daypart analysis grain has recorded hourly sales for lunch (12, 13) and dinner (19) hours
- When: hourly sales are retrieved through the site dashboard
- Then: the dashboard returns all three hours with their respective net sales amounts

### Top Selling Items

**As a** manager,
**I want to** see top selling items ranked by revenue with profit margins,
**So that** I can understand which products drive the most value.

- Given: a product mix grain has recorded sales for burgers, fries, and craft soda with different revenue levels
- When: top selling items are retrieved through the site dashboard
- Then: items are ranked by net revenue with signature burger first, including gross profit and margin percent

## Site Dashboard - Payment Breakdown

### Payment Method Summary

**As a** manager,
**I want to** see payments grouped by method with percentage-of-total on the dashboard,
**So that** I can monitor cash versus card mix and reconcile with processor statements.

- Given: a payment reconciliation grain has recorded cash, credit card, and debit card payments
- When: the payment breakdown is retrieved through the site dashboard
- Then: payments are grouped by method (Cash vs Card) with amounts and percentage-of-total calculated

## Period Rollup & Aggregation

### Weekly Rollup

**As a** manager,
**I want to** aggregate daily data into a weekly period summary,
**So that** I can review the full week's performance in one consolidated view.

- Given: a weekly period aggregation is initialized for 7 days with incrementing daily sales and waste
- When: all 7 days of sales, inventory, consumption, and waste data are aggregated
- Then: the period summary totals gross sales, net sales, transactions, and waste across the entire week

### Stock Turn Calculation

**As a** manager,
**I want to** see stock turn rate for a period,
**So that** I can assess how efficiently inventory is being converted to sales.

- Given: a monthly period has daily data with 3000 COGS and 10000 closing stock value
- When: the period summary stock health metrics are calculated
- Then: stock turn is calculated as 0.3 (COGS / closing stock)

### Costing Method Comparison

**As a** manager,
**I want to** view gross profit under both FIFO and weighted average costing methods,
**So that** I can compare approaches and choose the most appropriate method for my business.

- Given: a weekly period has aggregated daily data with 4500 net sales and 1400 actual COGS
- When: gross profit metrics are retrieved under both FIFO and WAC costing methods
- Then: both methods report the same actual gross profit of 3100 at approximately 68.89%

### Four-Week Accounting Period

**As a** system,
**I want to** support four-week accounting periods with correct date boundaries,
**So that** businesses using 13-period calendars get accurate period definitions.

- Given: a four-week accounting period is initialized spanning Feb 26 to Mar 24, 2024
- When: the period summary is retrieved
- Then: the period type, number, start date, and end date span exactly 27 inclusive days

## Consumption & Variance Reconciliation

### Multi-Ingredient Variance

**As a** manager,
**I want to** see the total variance across all ingredients including over-usage, efficient usage, and zero variance,
**So that** I can understand the net cost impact of kitchen operations.

- Given: a daily consumption grain has ingredients with over-usage, efficient usage, and zero variance
- When: the consumption snapshot and variance breakdown are retrieved
- Then: total variance reflects the net of all ingredient variances, ordered by absolute cost variance

### Same-Ingredient Aggregation

**As a** system,
**I want to** aggregate consumption of the same ingredient across multiple orders into a single entry,
**So that** variance reports show one consolidated line per ingredient rather than fragmented entries.

- Given: a daily consumption grain has ground beef consumed across three separate orders
- When: the variance breakdown is retrieved
- Then: all entries are aggregated into a single ground beef entry with combined quantities and costs

## Waste Tracking

### Waste by Category and Reason

**As a** manager,
**I want to** see daily waste broken down by both category and reason,
**So that** I can target specific areas (e.g., produce spoilage vs. equipment breakage) for improvement.

- Given: a daily waste grain has recorded spoilage in Produce, expiry in Proteins, and breakage in Equipment
- When: the waste snapshot is retrieved
- Then: waste is aggregated by both category and reason with correct totals for each grouping

## Date Boundaries & Isolation

### Daily Grain Isolation

**As a** system,
**I want to** maintain completely separate sales records per business day,
**So that** late-night transactions and multi-day comparisons are never cross-contaminated.

- Given: two daily sales grains are initialized for consecutive business days
- When: different sales amounts are recorded on each day
- Then: each day's grain maintains isolated totals with the correct business date
