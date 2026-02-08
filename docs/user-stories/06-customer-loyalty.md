# Customer & Loyalty User Stories

Stories extracted from unit test specifications covering customer profiles, loyalty programs, points, tiers, rewards, referrals, visit history, dietary preferences, house accounts, and spend projections.

---

## Customer Profiles

**As a** staff member,
**I want to** register a new customer with their name and email,
**So that** the venue can build a guest database for personalized service.

- Given: no existing customer profile
- When: a new customer is registered with name and email
- Then: the customer profile is created with the correct display name and contact info

---

**As a** staff member,
**I want to** update a customer's name,
**So that** the profile stays accurate when guests correct or change their details.

- Given: an existing customer profile
- When: the customer's first and last name are updated
- Then: the profile reflects the new name and display name

---

**As a** staff member,
**I want to** tag a customer with labels like "VIP" and "Regular",
**So that** the venue can segment guests and provide differentiated service.

- Given: a customer with no tags
- When: "VIP" and "Regular" tags are added
- Then: both tags appear on the customer profile

---

**As a** staff member,
**I want to** add notes to a customer profile such as seating preferences,
**So that** the team can deliver a consistently personalized experience.

- Given: a customer with no notes
- When: a staff member adds a seating preference note
- Then: the note is recorded on the customer profile

---

**As a** staff member,
**I want to** assign a referral code to a customer,
**So that** the customer can share it with friends and earn referral rewards.

- Given: a customer without a referral code
- When: the referral code "JOHN2024" is assigned
- Then: the referral code is stored on the customer profile

---

**As a** manager,
**I want to** delete a customer profile,
**So that** customers who are no longer active are removed from operational views.

- Given: an active customer profile
- When: the customer is deleted
- Then: the customer status is set to inactive

---

## Dietary Preferences & Allergens

**As a** staff member,
**I want to** record a customer's full dining preferences including dietary restrictions, allergens, seating preference, and notes,
**So that** every visit can accommodate the guest's needs without re-asking.

- Given: a customer with no dining preferences
- When: dietary restrictions, allergens, seating preference, and notes are set
- Then: all preferences are stored on the customer profile

---

**As a** staff member,
**I want to** add dietary restrictions such as "Vegan" and "Halal" to a customer profile,
**So that** the kitchen and service staff are aware of the guest's requirements.

- Given: a customer with no dietary restrictions
- When: "Vegan" and "Halal" restrictions are added
- Then: both restrictions appear in the customer preferences

---

**As a** staff member,
**I want to** remove a specific dietary restriction from a customer profile,
**So that** the profile stays current when a guest's diet changes.

- Given: a customer with "Vegan" and "Kosher" dietary restrictions
- When: the "Vegan" restriction is removed
- Then: only "Kosher" remains in the restrictions list

---

**As a** staff member,
**I want to** record allergens such as "Peanuts", "Tree Nuts", and "Dairy" for a customer,
**So that** the kitchen can take precautions to prevent allergic reactions.

- Given: a customer with no recorded allergens
- When: "Peanuts", "Tree Nuts", and "Dairy" allergens are added
- Then: all three allergens are recorded in the customer preferences

---

**As a** staff member,
**I want to** remove a specific allergen from a customer profile,
**So that** outdated allergen information does not unnecessarily restrict menu recommendations.

- Given: a customer with "Peanuts" and "Shellfish" allergens
- When: the "Peanuts" allergen is removed
- Then: only "Shellfish" remains in the allergen list

---

## Visit History

**As a** manager,
**I want to** track a customer's visit count, total spend, and average check,
**So that** the venue can identify high-value guests and tailor outreach accordingly.

- Given: a customer with no prior visits
- When: two visits are recorded totaling $100
- Then: visit count is 2, total spend is $100, and average check is $50

---

**As a** host,
**I want to** view a customer's visit history with the most recent visits first,
**So that** I can quickly reference the guest's last experience.

- Given: a customer with five recorded visits
- When: the visit history is retrieved
- Then: visits are returned most recent first

---

**As a** system,
**I want to** cap the visit history to 50 entries,
**So that** the system maintains performance while retaining sufficient history.

- Given: a customer with 60 recorded visits
- When: the full visit history is retrieved
- Then: the result is capped at 50 visits

---

**As a** manager,
**I want to** filter a customer's visit history by site,
**So that** I can see how often a guest visits a specific location in a multi-site organization.

- Given: a customer with visits at two different sites
- When: visit history is filtered by each site
- Then: only the visits for the requested site are returned

---

**As a** system,
**I want to** return null when requesting the last visit for a customer with no history,
**So that** the caller can handle the absence of data gracefully.

- Given: a customer with no visit history
- When: the last visit is requested
- Then: null is returned

---

## Loyalty Enrollment & Points

**As a** staff member,
**I want to** enroll a customer in the loyalty program with a member number and tier,
**So that** the customer can begin earning and redeeming rewards.

- Given: a customer who is not enrolled in the loyalty program
- When: the customer is enrolled with a member number and tier
- Then: the customer becomes a loyalty member with the assigned member number and tier

---

**As a** loyalty member,
**I want to** earn points from a purchase,
**So that** my spending translates into future rewards.

- Given: a loyalty member with zero points
- When: 100 points are earned from a purchase
- Then: the points balance and lifetime points both reflect 100

---

**As a** loyalty member,
**I want to** redeem points for a reward,
**So that** I can receive tangible value from my accumulated loyalty.

- Given: a loyalty member with 100 points
- When: 30 points are redeemed for a reward
- Then: the balance drops to 70 but lifetime points remain at 100

---

**As a** system,
**I want to** reject a redemption when the member has insufficient points,
**So that** loyalty balances cannot go negative through redemptions.

- Given: a loyalty member with 50 points
- When: attempting to redeem 100 points
- Then: the redemption is rejected due to insufficient points

---

**As a** manager,
**I want to** apply a goodwill points adjustment to a loyalty member's account,
**So that** service recovery or promotional bonuses are reflected in the member's balance.

- Given: a loyalty member with 100 points
- When: a positive goodwill adjustment of 50 points is applied
- Then: the balance increases to 150 and lifetime points also increases

---

**As a** system,
**I want to** expire points from a loyalty member's balance,
**So that** points do not accumulate indefinitely and the program remains financially sustainable.

- Given: a loyalty member with 100 points
- When: 30 points are expired
- Then: the points balance drops to 70

---

**As a** system,
**I want to** reject points-earning attempts from customers who are not loyalty members,
**So that** only enrolled members participate in the program.

- Given: a customer who is not enrolled in the loyalty program
- When: attempting to earn points
- Then: the operation is rejected because the customer is not a loyalty member

---

## Rewards

**As a** loyalty member,
**I want to** receive a reward such as "Free Coffee" in exchange for points,
**So that** I can enjoy concrete benefits from my loyalty.

- Given: a loyalty member with 100 points
- When: a "Free Coffee" reward costing 50 points is issued
- Then: the reward appears in available rewards and the points balance drops to 50

---

**As a** loyalty member,
**I want to** redeem an available reward against an order,
**So that** the reward value is applied to my purchase.

- Given: a loyalty member with an available "Free Coffee" reward
- When: the reward is redeemed against an order
- Then: the reward is no longer available

---

**As a** system,
**I want to** reject redemption of an expired reward,
**So that** the venue is not obligated to honor rewards past their validity period.

- Given: a loyalty member with an issued reward that expired yesterday
- When: attempting to redeem the expired reward
- Then: the redemption is rejected because the reward has expired

---

**As a** system,
**I want to** reject redemption of a reward that has already been redeemed,
**So that** rewards cannot be used more than once.

- Given: a loyalty member whose reward has already been redeemed
- When: attempting to redeem the same reward a second time
- Then: the redemption is rejected because the reward is no longer available

---

**As a** system,
**I want to** automatically expire rewards that have passed their expiry date while keeping valid rewards intact,
**So that** the rewards catalog stays accurate without manual cleanup.

- Given: a loyalty customer with five issued rewards (three past expiry, two still valid)
- When: the reward expiration process runs
- Then: only the three expired rewards are removed and the two valid rewards remain available

---

## Tier Management

**As a** system,
**I want to** promote a loyalty member from one tier to a higher tier,
**So that** members who reach spending thresholds receive enhanced benefits.

- Given: a loyalty member at the Bronze tier
- When: the member is promoted to Silver
- Then: the tier ID and name reflect the Silver tier

---

**As a** system,
**I want to** support multi-level tier progression through sequential promotions,
**So that** members can advance through all tiers as their loyalty grows.

- Given: a loyalty customer enrolled at Bronze tier
- When: they are promoted through Silver, Gold, and Platinum tiers sequentially
- Then: their current tier reflects the final Platinum promotion

---

**As a** system,
**I want to** demote a loyalty member to a lower tier,
**So that** members who no longer meet tier maintenance requirements are moved to the appropriate level.

- Given: a loyalty customer enrolled at Gold tier
- When: their tier is demoted to Silver
- Then: the customer's loyalty tier reflects the Silver downgrade

---

**As a** system,
**I want to** reject tier promotions for customers who are not loyalty members,
**So that** only enrolled members participate in the tier system.

- Given: a customer who is not enrolled in the loyalty program
- When: a tier promotion is attempted
- Then: the operation is rejected because the customer is not a loyalty member

---

## Referral Program

**As a** loyalty member,
**I want to** have my successful referrals tracked,
**So that** I can see how many friends I have brought to the venue.

- Given: a customer with zero successful referrals
- When: the referral count is incremented three times
- Then: the successful referrals count is 3

---

**As a** loyalty member,
**I want to** earn bonus points when I complete a referral up to the program cap,
**So that** I am rewarded for bringing new customers to the venue.

- Given: a loyalty customer with a referral code who has completed 9 of 10 allowed referrals
- When: the 10th referral is completed
- Then: the referral cap is marked as reached and bonus points are still awarded

---

**As a** system,
**I want to** reject referral completions once the member has reached the referral cap,
**So that** the referral program has a defined limit to control costs.

- Given: a loyalty customer who has already reached the referral cap of 10
- When: an 11th referral completion is attempted
- Then: the referral is rejected and no bonus points are awarded

---

## Customer Anonymization & Merging

**As a** system,
**I want to** anonymize a customer profile by redacting all personal data,
**So that** the venue complies with data deletion requests while preserving transactional history.

- Given: an active customer with personal data
- When: the customer profile is anonymized
- Then: all personal fields are redacted and the status is set to inactive

---

**As a** staff member,
**I want to** merge duplicate customer profiles into a primary profile,
**So that** guest history and loyalty data are consolidated into a single record.

- Given: a primary customer profile
- When: two duplicate customer profiles are merged into it
- Then: both source customer IDs are tracked in the merge history

---

**As a** staff member,
**I want to** merge multiple duplicate profiles at once,
**So that** large-scale deduplication can be performed efficiently.

- Given: a primary customer profile
- When: five duplicate customer profiles are merged into it
- Then: all five source customer IDs are recorded in the merge history

---

## Loyalty Program Configuration

**As a** manager,
**I want to** create a new loyalty program for the organization,
**So that** the venue can define and customize its own rewards program.

- Given: an organization without a loyalty program
- When: a new loyalty program named "VIP Rewards" is created
- Then: the program is created in Draft status with the specified name and description

---

**As a** manager,
**I want to** add a per-dollar earning rule to the loyalty program,
**So that** members earn points proportional to their spending.

- Given: an existing loyalty program
- When: a per-dollar earning rule is added with 10 points per dollar and $5 minimum spend
- Then: the earning rule is stored with the specified configuration

---

**As a** manager,
**I want to** add a tier with benefits, a multiplier, and maintenance requirements,
**So that** high-value guests receive differentiated rewards and incentives to maintain their status.

- Given: an existing loyalty program
- When: a Gold tier is added with 1000 points required, benefits, 2x multiplier, and maintenance requirements
- Then: the tier is stored with all specified benefits and configuration

---

**As a** manager,
**I want to** add a reward to the loyalty program with points cost and redemption limits,
**So that** members have attractive options for spending their accumulated points.

- Given: an existing loyalty program
- When: a "Free Coffee" reward is added requiring 100 points with tier and usage limits
- Then: the reward is stored with the specified points cost, type, and redemption limits

---

**As a** manager,
**I want to** activate a fully configured loyalty program,
**So that** customers can begin enrolling, earning, and redeeming.

- Given: a fully configured loyalty program with earning rules and tiers in Draft status
- When: the program is activated
- Then: the program status changes to Active with an activation timestamp

---

**As a** system,
**I want to** prevent activation of a loyalty program that has no earning rules,
**So that** members are guaranteed a way to earn points before the program goes live.

- Given: a loyalty program with tiers but no earning rules
- When: activation is attempted
- Then: the operation is rejected because at least one earning rule is required

---

**As a** system,
**I want to** calculate earned points using the base earning rate multiplied by the member's tier multiplier,
**So that** higher-tier members earn points at an accelerated rate.

- Given: an active loyalty program with 10 points per dollar and a Gold tier (2x multiplier)
- When: points are calculated for a $50 purchase at Gold tier
- Then: 1000 total points are earned (500 base * 2.0 Gold multiplier)

---

**As a** manager,
**I want to** configure a referral program with points for both referrer and referee,
**So that** existing members are incentivized to bring in new customers.

- Given: an existing loyalty program
- When: a referral program is configured with 100 referrer points, 50 referee points, and $25 qualifying spend
- Then: the referral program settings are stored and enabled

---

## House Accounts

**As a** manager,
**I want to** open a house account for a customer with a credit limit and payment terms,
**So that** trusted guests can charge meals and settle periodically.

- Given: a new customer with no existing house account
- When: a house account is opened with a $500 credit limit and 30-day payment terms
- Then: the account is created in Active status with zero balance and the specified credit terms

---

**As a** staff member,
**I want to** post a charge to a customer's house account,
**So that** the guest can dine without paying at the time of service.

- Given: an active customer house account with a $500 credit limit and zero balance
- When: a $100 dinner charge is posted to the account
- Then: the balance increases to $100 and available credit decreases to $400

---

**As a** system,
**I want to** reject charges that would exceed a house account's credit limit,
**So that** the venue's financial exposure is controlled.

- Given: an active customer house account with a $100 credit limit
- When: a $150 charge is attempted that would exceed the credit limit
- Then: the charge is rejected to prevent exceeding the customer's approved credit

---

**As a** staff member,
**I want to** apply a payment to a customer's house account,
**So that** the outstanding balance is reduced when the customer settles their tab.

- Given: a customer house account with a $200 outstanding balance
- When: a $150 credit card payment is applied to the account
- Then: the balance decreases to $50 and total payments reflect the $150 received

---

**As a** manager,
**I want to** suspend a house account due to overdue payments,
**So that** no further charges accumulate while the balance is in arrears.

- Given: an active customer house account
- When: the account is suspended due to overdue payments
- Then: the account status changes to Suspended with the reason recorded

---

**As a** system,
**I want to** reject charges against a suspended house account,
**So that** accounts with payment issues cannot accumulate additional debt.

- Given: a customer house account that has been suspended for overdue payments
- When: a new charge is attempted against the suspended account
- Then: the charge is rejected since the account is not active

---

**As a** system,
**I want to** prevent closing a house account that has an outstanding balance,
**So that** all debts are settled before the account lifecycle ends.

- Given: a customer house account with a $100 outstanding balance
- When: an attempt is made to close the account
- Then: closure is rejected because the account has an outstanding balance

---

**As a** manager,
**I want to** close a house account with zero balance at the customer's request,
**So that** the account lifecycle is cleanly terminated.

- Given: a customer house account with zero balance
- When: the account is closed at the customer's request
- Then: the account status changes to Closed

---

## Spend Projections & Tier Promotion

**As a** system,
**I want to** promote a customer to a higher tier when their spend reaches the tier threshold,
**So that** tier progression is driven automatically by real purchasing behavior.

- Given: a customer at Bronze tier with no prior spend
- When: a transaction of exactly $500 is recorded (the Silver tier threshold)
- Then: the customer is promoted to Silver tier

---

**As a** system,
**I want to** skip intermediate tiers when a single transaction exceeds multiple thresholds,
**So that** high-spending customers are immediately placed at the correct tier.

- Given: a customer at Bronze tier with no prior spend
- When: a single $2000 transaction is recorded (skipping Silver, landing in the Gold range)
- Then: the customer is promoted directly to Gold tier

---

**As a** system,
**I want to** demote a customer when a refund drops their lifetime spend below the current tier threshold,
**So that** tier status accurately reflects actual net spending.

- Given: a customer at Gold tier with $2000 in lifetime spend
- When: an $1800 refund drops lifetime spend to $200
- Then: the customer is demoted from Gold back to Bronze tier

---

**As a** system,
**I want to** apply the correct tier multiplier when earning points after a promotion,
**So that** the member immediately benefits from their new tier status.

- Given: a customer recently promoted to Silver tier (1.25x multiplier)
- When: a new $100 transaction is recorded at the Silver tier
- Then: points are earned using the Silver multiplier (125 points)

---

**As a** system,
**I want to** floor lifetime spend at zero when a refund exceeds the recorded amount,
**So that** spend data remains logically consistent and never goes negative.

- Given: a customer with $100 in lifetime spend
- When: a $500 refund is processed (exceeding lifetime spend)
- Then: lifetime spend floors at $0 rather than going negative
