# Payment Processing User Stories

Stories extracted from unit test specifications covering cash payments, card authorization flows, refunds, tips, cash drawer management, gift cards, and payment intents.

## Payment Lifecycle

### Initiating Payments

**As a** cashier,
**I want to** initiate a payment against an order,
**So that** the payment is created with the correct amount.

- Given: an order with a line item totaling $100
- When: a cash payment of $100 is initiated against the order
- Then: the payment is created with Initiated status and the correct amount

### Cash Payments

**As a** cashier,
**I want to** complete a cash payment with change and tip,
**So that** the transaction is fully recorded.

- Given: an initiated cash payment of $100
- When: the cashier completes the payment with $120 tendered and a $5 tip
- Then: the payment is completed with a total of $105 and $15 change given

### Card Payments

**As a** cashier,
**I want to** process a card payment with tip,
**So that** the card transaction is recorded with processor details.

- Given: an initiated credit card payment of $100
- When: the card payment is processed with a $10 tip via Stripe
- Then: the payment is completed with a total of $110 and card details are recorded

### Card Authorization Flow

**As a** system,
**I want to** process card authorization, capture, and decline flows,
**So that** card payments follow proper payment processing protocols.

- Given: an initiated credit card payment of $150
- When: the card is authorized, the authorization is recorded, and the payment is captured
- Then: the payment progresses through Authorizing, Authorized, and Captured statuses

- Given: an initiated credit card payment awaiting authorization
- When: the card is declined due to insufficient funds
- Then: the payment status is set to Declined

## Refunds

**As a** manager,
**I want to** issue a full refund,
**So that** the customer receives their money back.

- Given: a completed cash payment of $100
- When: a full refund of $100 is issued for customer dissatisfaction
- Then: the payment is fully refunded with a zero remaining balance

**As a** manager,
**I want to** issue a partial refund,
**So that** only the disputed amount is returned.

- Given: a completed cash payment of $100
- When: a partial refund of $30 is issued
- Then: $30 is refunded and $70 remains, with the payment marked as partially refunded

## Voiding Payments

**As a** manager,
**I want to** void a payment,
**So that** the transaction is cancelled before settlement.

- Given: an initiated cash payment of $100
- When: the payment is voided because the customer cancelled
- Then: the payment status is Voided and the void reason is recorded

- Given: an authorized credit card payment of $75
- When: the payment is voided because the customer changed their mind
- Then: the payment status is Voided and the void reason is recorded

## Tips

**As a** server,
**I want to** adjust the tip amount,
**So that** the correct tip is recorded for the transaction.

- Given: a completed cash payment of $100 with a $10 tip
- When: the tip is adjusted to $15
- Then: the tip amount is updated to $15 and the total becomes $115

## Payment Status Guards

**As a** system,
**I want to** enforce valid payment status transitions,
**So that** payments cannot be in invalid states.

- Given: a payment that has already been completed
- When: completing the same payment a second time is attempted
- Then: the operation is rejected with an invalid status error

- Given: an initiated payment that has not been completed
- When: a refund is attempted before the payment is completed
- Then: the operation is rejected because only completed payments can be refunded

- Given: a payment that has already been voided
- When: a second void is attempted
- Then: the void is rejected because the payment is already voided

- Given: a completed payment that has been fully refunded
- When: a void is attempted on the refunded payment
- Then: the void is rejected because fully refunded payments cannot be voided

## Cash Drawer

### Opening and Closing

**As a** cashier,
**I want to** open a cash drawer with a starting float,
**So that** I can begin accepting cash transactions.

- Given: a new cash drawer at a site
- When: the drawer is opened with a $200 starting float
- Then: the drawer status is Open and the expected balance matches the opening float

**As a** cashier,
**I want to** close the drawer with an actual count,
**So that** variances are calculated for reconciliation.

- Given: an open cash drawer with $200 opening float and $100 cash received
- When: the drawer is closed with an actual count of $295
- Then: the drawer is closed showing a -$5 variance (short)

### Cash Transactions

**As a** system,
**I want to** track cash in and cash out transactions,
**So that** the expected drawer balance is always accurate.

- Given: an open cash drawer with a $200 balance
- When: $50 cash is received from a payment
- Then: the expected drawer balance increases to $250

- Given: an open cash drawer with a $200 balance
- When: $50 is paid out as change
- Then: the expected drawer balance decreases to $150

### Cash Drops

**As a** manager,
**I want to** record cash drops to the safe,
**So that** excess cash is secured and tracked.

- Given: an open cash drawer with a $500 balance
- When: a $300 cash drop is made to the safe
- Then: the expected balance decreases to $200 and the drop is recorded

### Reconciliation

**As a** manager,
**I want to** count the cash drawer mid-shift,
**So that** I can check for discrepancies.

- Given: an open cash drawer with $200 float and $300 in cash sales
- When: a cash count of $440 is performed
- Then: the drawer enters Counting status with the actual balance recorded

**As a** system,
**I want to** calculate drawer variances,
**So that** discrepancies are flagged for investigation.

- Given: a cash drawer opened with $100 float and $200 in cash sales received
- When: the drawer is closed with an actual count of $305 against an expected $300
- Then: a positive variance of $5.00 is recorded indicating the drawer is over

- Given: a cash drawer opened with $500 float and $1,500 in cash sales received
- When: the drawer is closed with an actual count of $1,900 against an expected $2,000
- Then: a large negative variance of -$100.00 is recorded indicating a cash shortage

### No-Sale

**As a** cashier,
**I want to** open the drawer for a no-sale transaction,
**So that** I can make change without affecting the expected balance.

- Given: an open cash drawer
- When: the drawer is opened for a no-sale transaction (e.g., making change)
- Then: a no-sale transaction is recorded in the drawer history

## Gift Cards

### Card Lifecycle

**As a** cashier,
**I want to** create and activate a gift card,
**So that** the customer can use it for purchases.

- Given: a new gift card grain with no prior state
- When: a digital gift card is created with a $50 value and 6-month expiry
- Then: the card is created in inactive status with the correct initial balance

- Given: an inactive gift card with a $100 balance
- When: the card is activated at a site with purchaser details
- Then: the card becomes active with an activation transaction recorded

### Redemption

**As a** cashier,
**I want to** redeem a gift card balance,
**So that** the customer can pay with their card.

- Given: an active gift card with a $100 balance
- When: $30 is redeemed against an order
- Then: the balance decreases to $70 and the redemption count increments

- Given: an active gift card with a $50 balance
- When: a $100 redemption is attempted, exceeding the available balance
- Then: the redemption is rejected with an insufficient balance error

- Given: an active gift card with a $50 balance
- When: the full $50 balance is redeemed
- Then: the card balance reaches zero and the status changes to depleted

### Reloading

**As a** cashier,
**I want to** reload a gift card,
**So that** the customer can add more value.

- Given: an active gift card with a $50 balance
- When: $25 is reloaded onto the card with a birthday note
- Then: the balance increases to $75 and the reload amount is tracked

- Given: a depleted gift card with a zero balance after full redemption
- When: $30 is reloaded onto the card
- Then: the card reactivates with a $30 balance and returns to active status

### Expiration and Cancellation

**As a** system,
**I want to** handle card expiration,
**So that** expired cards cannot be used.

- Given: an activated gift card with an expiry date in the past
- When: a redemption is attempted on the expired card
- Then: the redemption is rejected because the card has expired

**As a** manager,
**I want to** cancel a lost or stolen card,
**So that** it can no longer be used.

- Given: an active gift card with a $75 balance
- When: the card is cancelled due to a lost card report
- Then: the status changes to cancelled, the balance is zeroed, and a void transaction is recorded

### PIN Validation

**As a** system,
**I want to** validate gift card PINs,
**So that** only authorized users can redeem the card.

- Given: a gift card created with PIN "1234"
- When: the PIN "1234" is validated
- Then: validation succeeds

- Given: a gift card created with PIN "1234"
- When: an incorrect PIN "5678" is validated
- Then: validation fails

### Multi-Currency Support

**As a** system,
**I want to** support gift cards in multiple currencies,
**So that** international venues can issue cards in their local currency.

- Given: a new gift card being created
- When: the card is created with EUR as the currency
- Then: the card stores EUR as its currency

- Given: an activated JPY gift card with a 100,000 balance
- When: 45,678 is redeemed from the card
- Then: the currency remains JPY and the balance correctly reflects 54,322

## Payment Intents (Stripe-style)

### Creating Intents

**As a** system,
**I want to** create payment intents,
**So that** the payment flow follows the modern two-step process.

- Given: a merchant account
- When: a payment intent for $10.00 USD is created without a payment method
- Then: the intent is created with RequiresPaymentMethod status and a client secret

### Confirming Intents

**As a** system,
**I want to** confirm payment intents,
**So that** the payment is processed.

- Given: a payment intent for $30.00 with a valid card attached
- When: the payment intent is confirmed
- Then: the payment succeeds and the full amount is received

### Manual Capture

**As a** system,
**I want to** support manual capture for pre-auth flows,
**So that** the merchant can capture when ready.

- Given: a payment intent for $50.00 configured for manual capture with a valid card
- When: the payment intent is confirmed
- Then: the payment is authorized but not captured, with the full amount held as capturable

- Given: a confirmed payment intent for $50.00 awaiting manual capture
- When: only $30.00 of the authorized amount is captured
- Then: the payment succeeds with $30.00 received

### 3D Secure

**As a** system,
**I want to** handle 3D Secure authentication,
**So that** SCA-compliant payments are supported.

- Given: a payment intent with a card that requires 3D Secure authentication
- When: the payment intent is confirmed
- Then: the status is RequiresAction with a redirect URL for 3DS verification

- Given: a payment intent awaiting 3D Secure authentication
- When: the 3DS authentication is completed successfully
- Then: the next action is cleared and the payment moves to Processing status

## Payment Method Management

### Card Detection

**As a** system,
**I want to** detect card brands from card numbers,
**So that** the correct brand logo and validation rules are applied.

- Given: a valid Visa card number with cardholder details
- When: a card payment method is created
- Then: the payment method is stored with brand detection, last 4 digits, and a fingerprint

- Given: a Discover card number is provided for payment method creation
- When: the payment method is created
- Then: the card brand is detected as "discover" with the correct last four digits

### Card Validation

**As a** system,
**I want to** validate card details,
**So that** invalid cards are rejected at entry.

- Given: an American Express card with a 3-digit CVC (Amex requires 4 digits)
- When: the payment method creation is attempted
- Then: the creation is rejected because the CVC is invalid for Amex

- Given: a card with expiry month 13, which is invalid
- When: the payment method creation is attempted
- Then: the creation is rejected due to an invalid expiry month

### Duplicate Detection

**As a** system,
**I want to** detect duplicate cards via fingerprinting,
**So that** the same card is not stored twice.

- Given: two payment methods created with the same card number on different accounts
- When: both payment methods are created
- Then: both produce the same card fingerprint for duplicate detection

### Bank Accounts

**As a** system,
**I want to** support bank account payment methods,
**So that** ACH/SEPA payments can be processed.

- Given: a German IBAN bank account with individual holder details
- When: the bank account payment method is created
- Then: the last 4 digits are extracted from the IBAN along with country and currency
