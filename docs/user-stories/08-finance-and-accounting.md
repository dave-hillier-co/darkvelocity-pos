# Finance & Accounting User Stories

Stories extracted from unit test specifications covering chart of accounts, journal entries, accounting periods, bank reconciliation, expenses, ledger operations, and financial reporting.

---

## Chart of Accounts

**As a** bookkeeper,
**I want to** create an asset account with a code and name,
**So that** the account is registered in the chart of accounts and ready to receive postings.

- Given: a new, uninitialized account
- When: the account is created as an asset account with a code and name
- Then: the account is active with zero balance and the specified details

---

**As a** bookkeeper,
**I want to** create an account with an opening balance,
**So that** the starting position is recorded and an opening journal entry is automatically generated.

- Given: a new, uninitialized account
- When: the account is created with an opening balance of 1000
- Then: the balance reflects the opening amount and an opening journal entry is recorded

---

**As a** system,
**I want to** reject duplicate account creation attempts,
**So that** the chart of accounts remains free of conflicting entries.

- Given: an account that has already been created
- When: a second creation attempt is made for the same account
- Then: the operation is rejected because the account already exists

---

**As a** bookkeeper,
**I want to** initialize a chart of accounts with default accounts and a reporting currency,
**So that** the organization starts with a standard account structure ready for transactions.

- Given: a new, uninitialized chart of accounts for an organization
- When: the chart is initialized with default accounts and USD currency
- Then: the chart contains the standard default accounts with USD as the reporting currency

---

**As a** bookkeeper,
**I want to** add a custom expense account under an existing parent account,
**So that** the chart of accounts can be extended to match the organization's specific tracking needs.

- Given: an initialized chart of accounts with default accounts
- When: a custom expense account (7000) is added under the expenses parent (6000)
- Then: the new account appears in the chart with the correct type, name, and active status

---

**As a** bookkeeper,
**I want to** view the account hierarchy with child accounts nested under their parents,
**So that** I can understand the organizational structure of the chart of accounts.

- Given: an initialized chart of accounts with parent and child accounts
- When: the account hierarchy is retrieved
- Then: the Assets account (1000) has child accounts nested under it

---

**As a** bookkeeper,
**I want to** deactivate an account that is no longer in use,
**So that** no further postings can be made to it while preserving its historical data.

- Given: an active asset account
- When: the account is deactivated
- Then: the account is marked inactive and no further postings can be made

---

**As a** system,
**I want to** prevent deactivation of system-designated accounts,
**So that** essential accounts like cash are always available for core operations.

- Given: a system-designated cash account that cannot be disabled
- When: deactivation is attempted on the system account
- Then: the operation is rejected because system accounts are protected from deactivation

---

## Double-Entry Posting

**As a** bookkeeper,
**I want to** post a debit to an asset account,
**So that** the balance increases to reflect cash received or value added.

- Given: an asset account with a balance of 1000
- When: a debit of 500 is posted (cash received)
- Then: the balance increases to 1500 because debits increase asset accounts

---

**As a** bookkeeper,
**I want to** post a credit to an asset account,
**So that** the balance decreases to reflect cash paid out or value removed.

- Given: an asset account with a balance of 1000
- When: a credit of 300 is posted (cash paid out)
- Then: the balance decreases to 700 because credits decrease asset accounts

---

**As a** bookkeeper,
**I want to** post a debit to a revenue account,
**So that** the balance decreases to reflect a sales return or adjustment.

- Given: a revenue account with 1000 in recorded sales
- When: a debit of 200 is posted (sales return)
- Then: the balance decreases to 800 because debits decrease revenue accounts

---

**As a** bookkeeper,
**I want to** post a credit to a revenue account,
**So that** the balance increases to reflect sales revenue earned.

- Given: a revenue account with zero balance
- When: a credit of 1500 is posted (sales revenue earned)
- Then: the balance increases to 1500 because credits increase revenue accounts

---

**As a** system,
**I want to** reject zero-amount postings,
**So that** all journal entries represent meaningful financial activity.

- Given: an active asset account
- When: a debit of zero is posted
- Then: the operation is rejected because posting amounts must be positive

---

**As a** system,
**I want to** reject postings to inactive accounts,
**So that** deactivated accounts cannot accumulate new transactions.

- Given: an asset account that has been deactivated
- When: a debit is posted to the inactive account
- Then: the operation is rejected because inactive accounts cannot receive postings

---

## Journal Entry Reversals

**As a** bookkeeper,
**I want to** reverse an erroneous journal entry,
**So that** the account balance is corrected and a clear audit trail links the reversal to the original entry.

- Given: an asset account with a 500 debit entry posted against a 1000 opening balance
- When: the debit entry is reversed due to an error
- Then: the balance returns to 1000, the original entry is marked reversed, and a reversal entry is created

---

**As a** system,
**I want to** prevent an entry from being reversed more than once,
**So that** the ledger does not accumulate duplicate corrections.

- Given: a debit entry that has already been reversed once
- When: a second reversal is attempted on the same entry
- Then: the operation is rejected because entries can only be reversed once

---

**As a** system,
**I want to** prevent reversal entries from being reversed,
**So that** the correction chain remains simple and auditable.

- Given: a reversal entry created from reversing a debit
- When: a reversal is attempted on the reversal entry itself
- Then: the operation is rejected because reversal entries cannot be reversed

---

## Journal Entries

**As a** bookkeeper,
**I want to** create a balanced journal entry with debit and credit lines,
**So that** a financial transaction is recorded in draft status for review before posting.

- Given: an initialized chart of accounts with cash (1110) and food sales (4100) accounts
- When: a balanced journal entry is created debiting cash $100 and crediting food sales $100
- Then: the entry is created in Draft status with equal debits and credits

---

**As a** system,
**I want to** reject unbalanced journal entries,
**So that** the fundamental accounting equation is always maintained.

- Given: an initialized chart of accounts
- When: an unbalanced journal entry is created with $100 debit and only $50 credit
- Then: an error is thrown because debits must equal credits

---

**As a** system,
**I want to** reject journal entry lines that specify both a debit and a credit,
**So that** each line clearly represents one side of the transaction.

- Given: an initialized chart of accounts
- When: a journal entry line has both a $100 debit and $50 credit on the same line
- Then: an error is thrown because a single line cannot have both debit and credit amounts

---

**As a** reviewer,
**I want to** approve a draft journal entry,
**So that** the entry is authorized for posting with the approver and timestamp on record.

- Given: a balanced journal entry in Draft status
- When: the entry is approved by a reviewer
- Then: the status changes to Approved with the approval timestamp and approver recorded

---

**As a** reviewer,
**I want to** reject a draft journal entry with a reason,
**So that** the preparer knows why the entry was not accepted and can correct it.

- Given: a balanced journal entry in Draft status
- When: the entry is rejected with reason "Incorrect amount"
- Then: the status changes to Rejected

---

**As a** bookkeeper,
**I want to** void a draft journal entry that was made in error,
**So that** the erroneous entry is removed from the workflow without affecting posted balances.

- Given: a balanced journal entry in Draft status that has not been posted
- When: the entry is voided with reason "Entry made in error"
- Then: the entry status changes to Voided

---

## Accounting Periods

**As a** controller,
**I want to** initialize a fiscal year with monthly periods,
**So that** the organization has 12 discrete periods for reporting and period-end close.

- Given: a new accounting period grain for a fiscal year
- When: the fiscal year is initialized with monthly period frequency
- Then: 12 monthly periods are created for the year

---

**As a** controller,
**I want to** initialize a fiscal year with quarterly periods,
**So that** the organization can use a simplified four-period reporting cycle.

- Given: a new accounting period grain for a fiscal year
- When: the fiscal year is initialized with quarterly period frequency
- Then: 4 quarterly periods are created for the year

---

**As a** system,
**I want to** enforce sequential period opening,
**So that** periods are opened in chronological order and no gaps appear in the fiscal year.

- Given: an initialized fiscal year with period 1 still not opened
- When: period 2 is opened without first opening period 1
- Then: an error is thrown enforcing sequential period opening

---

**As a** controller,
**I want to** close an open accounting period,
**So that** the period's transactions are finalized and reporting for that period is locked down.

- Given: an open accounting period 1 (January)
- When: the period is closed
- Then: the period status changes to Closed with a closing timestamp

---

**As a** controller,
**I want to** permanently lock a closed accounting period,
**So that** no further modifications can be made, satisfying audit and compliance requirements.

- Given: a closed accounting period 1
- When: the period is permanently locked
- Then: the period status changes to Locked, preventing further modifications

---

**As a** system,
**I want to** prevent reopening of locked accounting periods,
**So that** the integrity of finalized financial records is preserved.

- Given: a permanently locked accounting period 1
- When: reopening the locked period is attempted
- Then: an error is thrown because locked periods cannot be reopened

---

## Bank Reconciliation

**As a** bookkeeper,
**I want to** start a bank reconciliation for a specific account and statement balance,
**So that** I can begin matching bank transactions against the general ledger.

- Given: a new bank reconciliation grain
- When: a reconciliation is started for checking account 1234-5678 with a $10,000 statement ending balance
- Then: the reconciliation is created in InProgress status with the bank account and balance details

---

**As a** bookkeeper,
**I want to** import bank transactions from a CSV file,
**So that** the statement entries are loaded into the reconciliation for matching.

- Given: an in-progress bank reconciliation
- When: three bank transactions (deposit, check, fee) are imported from CSV
- Then: all three transactions are added as unmatched entries

---

**As a** bookkeeper,
**I want to** match an imported bank transaction to a journal entry,
**So that** the transaction is reconciled and the unmatched count decreases.

- Given: a reconciliation with one imported unmatched bank transaction
- When: the transaction is matched to a journal entry
- Then: the matched count increases to 1 and unmatched count drops to 0

---

**As a** system,
**I want to** prevent completing a reconciliation that has unresolved discrepancies,
**So that** the bookkeeper must address all differences before finalizing.

- Given: an in-progress reconciliation with unmatched transactions creating a discrepancy
- When: completion is attempted without the force flag
- Then: an error is thrown because the reconciliation is not balanced

---

**As a** bookkeeper,
**I want to** force-complete a reconciliation with an accepted discrepancy note,
**So that** minor unexplained differences are documented and the reconciliation can proceed.

- Given: an in-progress reconciliation with unresolved discrepancies
- When: completion is forced with an accepted discrepancy note
- Then: the reconciliation completes with CompletedWithDiscrepancies status

---

## Expense Tracking

**As a** manager,
**I want to** record a utilities expense with category, description, and vendor,
**So that** the cost is captured for tracking, approval, and reporting.

- Given: a new expense grain for a site
- When: a $250 utilities expense for a monthly electricity bill is recorded
- Then: the expense is created with pending status and the correct category, description, and vendor

---

**As a** manager,
**I want to** approve a pending expense,
**So that** the expense is authorized for payment with a record of who approved it and when.

- Given: a pending marketing expense of $500 for social media ads
- When: the expense is approved by a manager
- Then: the status changes to Approved with the approver ID and approval timestamp recorded

---

**As a** manager,
**I want to** reject a pending expense with a reason,
**So that** the submitter understands why the expense was not approved.

- Given: a pending travel expense of $800 for a conference flight
- When: the expense is rejected with a cancellation reason
- Then: the status changes to Rejected and the rejection reason is recorded in notes

---

**As a** bookkeeper,
**I want to** mark an approved expense as paid with a payment reference,
**So that** the disbursement is recorded and the expense lifecycle is complete.

- Given: an approved insurance expense of $2,000
- When: the expense is marked as paid with a check reference number
- Then: the status changes to Paid with the check number and payment method recorded

---

**As a** bookkeeper,
**I want to** void a pending expense that was entered in error,
**So that** duplicate or incorrect entries are removed from the workflow with a documented reason.

- Given: a pending maintenance expense of $450 for HVAC repair
- When: the expense is voided as a duplicate entry
- Then: the status changes to Voided with the void reason recorded in notes

---

**As a** manager,
**I want to** attach a receipt document to an expense,
**So that** supporting documentation is stored alongside the expense record for audit purposes.

- Given: a recorded supplies expense for cleaning supplies
- When: a receipt PDF document is attached to the expense
- Then: the document URL and filename are stored on the expense

---

**As a** bookkeeper,
**I want to** record an expense with tax amount and deductibility information,
**So that** tax obligations and deductions are tracked for accurate financial reporting.

- Given: a new expense grain for equipment purchase
- When: a $5,000 commercial oven expense is recorded with $400 tax amount and tax-deductible flag
- Then: the tax amount and deductibility flag are correctly stored

---

**As a** manager,
**I want to** set a recurrence pattern on an expense,
**So that** recurring costs like monthly rent are flagged for automatic re-creation.

- Given: a recorded monthly rent expense of $5,000
- When: a monthly recurrence pattern with day-of-month 1 is set
- Then: the expense is flagged as recurring

---

## Expense Index & Reporting

**As a** manager,
**I want to** filter expenses by category,
**So that** I can review spending within a specific cost type.

- Given: an index with rent, two utilities, and supplies expenses
- When: the index is queried filtering by the Utilities category
- Then: only the two utilities expenses are returned totaling $550

---

**As a** manager,
**I want to** filter expenses by status,
**So that** I can see which expenses still need approval or payment.

- Given: an index with expenses in Pending, Approved, and Paid statuses
- When: the index is queried filtering by Pending status
- Then: only the two pending expenses are returned totaling $500

---

**As a** manager,
**I want to** search expenses by vendor name regardless of casing,
**So that** vendor-based lookups are flexible and case-insensitive.

- Given: an index with expenses from "STAPLES" and "staples office" (mixed case)
- When: the index is queried with vendor filter "staples" in lowercase
- Then: both expenses are returned regardless of vendor name casing

---

**As a** manager,
**I want to** filter expenses by amount range,
**So that** I can identify expenses within a specific spending threshold.

- Given: an index with expenses of $50, $150, $250, and $500
- When: the index is queried for amounts between $100 and $300
- Then: only the $150 and $250 expenses are returned

---

**As a** manager,
**I want to** paginate through expense results,
**So that** large expense lists are returned in manageable pages without losing the total count.

- Given: an index with 10 expenses registered on consecutive dates
- When: the index is queried with skip 3 and take 3 for pagination
- Then: 3 expenses are returned and the total count remains 10

---

**As a** controller,
**I want to** view category totals for a given month,
**So that** I can see how spending breaks down across expense categories.

- Given: an index with expenses across rent, utilities, and supplies categories in January
- When: category totals are requested for January
- Then: totals are grouped by category with correct amounts and counts

---

**As a** controller,
**I want to** view the total expenses for a date range excluding voided and rejected entries,
**So that** the reported total reflects only valid expenditures.

- Given: an index with paid ($5,000), approved ($300), and pending ($150) expenses in January
- When: the total is requested for the January date range
- Then: all non-voided, non-rejected expenses are summed to $5,450

---

## Ledger Operations

**As a** system,
**I want to** initialize a ledger with a zero balance,
**So that** the ledger is ready to receive credits and debits from the start.

- Given: a new, uninitialized ledger grain for a gift card
- When: the ledger is initialized for an organization
- Then: the ledger balance starts at zero

---

**As a** cashier,
**I want to** credit a cash drawer ledger with an opening float,
**So that** the drawer starts the shift with the correct amount of cash.

- Given: an initialized cash drawer ledger with zero balance
- When: a $500 opening float credit is applied
- Then: the balance increases to $500 and the transaction records before/after balances

---

**As a** cashier,
**I want to** record a cash withdrawal from the drawer,
**So that** the ledger reflects cash removed and the debit is tracked.

- Given: a cash drawer ledger with a $1,000 opening float
- When: a $200 cash withdrawal debit is applied
- Then: the balance decreases to $800 and the debit is recorded as a negative amount

---

**As a** system,
**I want to** reject redemptions that exceed the available gift card balance,
**So that** customers cannot spend more than the card holds.

- Given: a gift card ledger with only $50 balance
- When: a $100 redemption debit is attempted
- Then: the operation fails with an insufficient balance error showing available vs. requested amounts

---

**As a** system,
**I want to** allow inventory ledgers to go negative when the allowNegative flag is set,
**So that** service continues even when recorded stock is exhausted and discrepancies are flagged for reconciliation.

- Given: an inventory ledger with 10 units of stock
- When: 15 units are consumed with the allowNegative flag set
- Then: the debit succeeds and the balance goes to -5, flagging an inventory discrepancy

---

**As a** manager,
**I want to** adjust a cash drawer ledger balance after a physical count,
**So that** the ledger matches the actual cash on hand and the variance is recorded.

- Given: a cash drawer ledger with a $100 balance
- When: the balance is adjusted to $150 after a physical count
- Then: the balance is set to $150 and the $50 adjustment amount is recorded

---

**As a** system,
**I want to** maintain independent balances for ledgers of different types under the same owner,
**So that** a gift card ledger and a cash drawer ledger do not interfere with each other.

- Given: two ledgers with the same owner ID but different types (gift card vs. cash drawer)
- When: each ledger is credited with different amounts ($100 and $500 respectively)
- Then: each ledger maintains its own independent balance

---

**As a** system,
**I want to** cap ledger transaction history at 100 entries,
**So that** memory usage stays bounded while retaining the most recent activity.

- Given: a cash drawer ledger that has accumulated 110 transactions
- When: the full transaction history is retrieved
- Then: only the most recent 100 transactions are retained due to the history limit

---

## Financial Reports

**As a** controller,
**I want to** generate a trial balance report as of a given date,
**So that** I can verify that total debits equal total credits across all accounts.

- Given: an initialized chart of accounts for the organization
- When: a trial balance report is generated as of today
- Then: the report shows balanced debits and credits for the organization

---

**As a** controller,
**I want to** generate an income statement for a date range,
**So that** I can review the organization's revenue and expenses for the period.

- Given: an initialized chart of accounts for the organization
- When: an income statement is generated for the past month
- Then: the report includes revenue and operating expense sections for the date range

---

**As a** controller,
**I want to** generate a balance sheet as of a given date,
**So that** I can see the organization's financial position including assets, liabilities, and equity.

- Given: an initialized chart of accounts for the organization
- When: a balance sheet is generated as of today
- Then: the report includes assets, liabilities, and equity sections

---

**As a** controller,
**I want to** generate a cash flow statement for a date range,
**So that** I can understand how cash moved through operating, investing, and financing activities.

- Given: a financial reports grain for an organization
- When: a cash flow statement is generated for the past month
- Then: the report includes operating, investing, and financing activity sections
