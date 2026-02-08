# Staff & Labor Management User Stories

Stories extracted from unit test specifications covering employees, roles, scheduling, time tracking, tip pools, payroll, availability, shift swaps, time off, certifications, labor law compliance, and tax calculation.

---

## Employee Management

**As a** manager,
**I want to** create an employee record with a name, employee number, and site assignment,
**So that** the new hire is registered in the system and can begin working at their assigned venue.

- Given: a new hire with employee number EMP-001 assigned to a site
- When: the employee record is created
- Then: the employee is active with correct name, number, and site access

---

**As a** manager,
**I want to** update an employee's name and contact information,
**So that** the employee record stays accurate as personal details change.

- Given: an existing employee named Jane Doe
- When: her name is changed to Janet and her email is updated
- Then: the employee record reflects the new name and email with an incremented version

---

**As a** manager,
**I want to** terminate an employee with a documented reason,
**So that** the separation is recorded and the employee can no longer access the system.

- Given: an active employee
- When: the employee is terminated with reason "Position eliminated"
- Then: the employee status is Terminated and reactivation is rejected

---

**As a** manager,
**I want to** grant an employee access to an additional site,
**So that** the employee can work across multiple venues within the organization.

- Given: an employee assigned only to site 1 who cannot clock in at site 2
- When: the employee is granted access to site 2
- Then: the employee can successfully clock in at the newly authorized site

---

**As a** system,
**I want to** automatically deactivate an employee when their linked user account is deactivated,
**So that** user account changes propagate to the employee record without manual intervention.

- Given: an active employee linked to a user account
- When: the linked user account is deactivated
- Then: the employee status is automatically set to Inactive

---

## Roles

**As a** manager,
**I want to** define a new role with department, pay rate, color, and required certifications,
**So that** positions are standardized and compliance requirements are clearly documented.

- Given: a new Front of House role definition for "Server" at $15/hr requiring Food Handler certification
- When: the role is created
- Then: the role is active with the correct department, rate, color, and required certifications

---

**As a** manager,
**I want to** update a role's name, pay rate, and color,
**So that** role definitions evolve with the business without recreating them from scratch.

- Given: an existing Host role at $11/hr
- When: the role is renamed to "Host/Hostess" with an updated rate and color
- Then: the modified properties are updated while unchanged properties remain the same

---

**As a** manager,
**I want to** deactivate a role that is no longer needed,
**So that** obsolete positions cannot be assigned to employees or used in scheduling.

- Given: an active Busser role
- When: the role is deactivated
- Then: the role is marked as inactive and can no longer be assigned

---

## Schedule Management

**As a** manager,
**I want to** create a new weekly schedule for a site,
**So that** I can begin planning staff coverage for the upcoming period.

- Given: a site that needs a weekly staff schedule
- When: a new schedule is created for the current week
- Then: the schedule is in Draft status with no shifts, zero hours, and zero labor cost

---

**As a** manager,
**I want to** publish a draft schedule so that employees can see their shifts,
**So that** the team knows when and where they are expected to work.

- Given: a draft schedule for next week
- When: a manager publishes the schedule
- Then: the schedule status changes to Published with the publisher and timestamp recorded

---

**As a** manager,
**I want to** lock a schedule after the work period has ended,
**So that** payroll can process final hours without further modifications.

- Given: a schedule for a past week
- When: the schedule is locked for payroll processing
- Then: the schedule status changes to Locked

---

**As a** manager,
**I want to** add a shift with start time, end time, break duration, and role to a draft schedule,
**So that** each employee knows exactly when they work and what position they fill.

- Given: a draft schedule for an upcoming week
- When: a 9 AM to 5 PM opening shift with a 30-minute break is added for an employee
- Then: the shift appears on the schedule with correct times, role, and notes

---

**As a** manager,
**I want to** see scheduled hours and labor cost calculated automatically when adding shifts,
**So that** I can manage labor budgets while building the schedule.

- Given: a draft schedule for an upcoming week
- When: an 8-hour shift with a 30-minute break at $20/hr is added
- Then: scheduled hours are 7.5 and labor cost is $150

---

**As a** system,
**I want to** reject modifications to a locked schedule,
**So that** payroll-finalized hours are protected from accidental changes.

- Given: a schedule that has been locked for payroll
- When: a manager attempts to add a new shift
- Then: the system rejects the change because locked schedules cannot be modified

---

**As a** manager,
**I want to** see total weekly labor cost aggregated across all shifts,
**So that** I can evaluate whether the schedule stays within the labor budget.

- Given: a schedule with two shifts -- one at $20/hr and one at $15/hr
- When: the total weekly labor cost is calculated
- Then: the cost equals $240 ($150 + $90) accounting for breaks

---

## Time Tracking

**As an** employee,
**I want to** clock in using my PIN at the start of my shift,
**So that** my attendance is recorded with the correct time, location, and role.

- Given: an employee arriving for their shift
- When: the employee clocks in using their PIN
- Then: an active time entry is created with the correct location, role, and clock-in time

---

**As a** system,
**I want to** reject a duplicate clock-in for an employee who already has an active time entry,
**So that** overlapping entries do not corrupt payroll calculations.

- Given: an employee who already has an active time entry
- When: a second clock-in is attempted for the same entry
- Then: the system rejects the duplicate clock-in

---

**As an** employee,
**I want to** clock out at the end of my shift,
**So that** my time entry is completed with an accurate departure time and method.

- Given: an employee who is currently clocked in
- When: the employee clocks out at the end of their shift
- Then: the time entry is completed with a clock-out timestamp and method recorded

---

**As a** manager,
**I want to** adjust a time entry's clock-in time with a reason and audit trail,
**So that** forgotten or incorrect punches can be corrected while maintaining accountability.

- Given: a completed time entry for an employee who forgot to clock in on time
- When: a manager adjusts the clock-in time back 8 hours with a 30-minute break
- Then: the corrected times are saved with the adjusting manager and reason as an audit trail

---

**As a** manager,
**I want to** approve a completed time entry,
**So that** reviewed hours are confirmed as accurate before payroll processing.

- Given: a completed time entry awaiting manager review
- When: a manager approves the time entry
- Then: the approval is recorded with the approver and approval timestamp

---

**As a** system,
**I want to** reject clock-in attempts from inactive or terminated employees,
**So that** only active employees can record working time.

- Given: an employee whose account has been deactivated (e.g., terminated)
- When: a clock-in is attempted
- Then: the system should reject the clock-in since only active employees can clock in

---

## Break Tracking

**As an** employee,
**I want to** start an unpaid meal break while clocked in,
**So that** my break time is properly tracked and deducted from paid hours.

- Given: a clocked-in employee at a site
- When: the employee starts an unpaid meal break
- Then: the break should be recorded with the correct type and the employee should be on break

---

**As an** employee,
**I want to** end my meal break and return to work,
**So that** my break duration is calculated and I resume accruing paid hours.

- Given: a clocked-in employee who is currently on a meal break
- When: the employee ends their break
- Then: the break duration should be calculated and the employee should no longer be on break

---

**As a** manager,
**I want to** view a summary of all breaks taken during a shift,
**So that** I can verify that paid and unpaid break time is accurately tracked.

- Given: a clocked-in employee who took a paid rest break and an unpaid meal break during the shift
- When: the break summary is requested
- Then: the summary should show 2 breaks with tracked paid and unpaid break minutes

---

**As a** system,
**I want to** reject a second break when an employee is already on break,
**So that** overlapping breaks do not corrupt time tracking calculations.

- Given: a clocked-in employee who is already on a meal break
- When: a second break is attempted
- Then: the system should reject the duplicate break since the employee is already on break

---

## Tip Pools

**As a** manager,
**I want to** create a tip pool for a service period with a specified distribution method,
**So that** tips can be collected and fairly distributed among eligible staff.

- Given: a dinner service that needs a tip pool for tonight
- When: an equal-distribution tip pool is created for eligible roles
- Then: the pool is created with zero tips and no distributions yet

---

**As a** staff member,
**I want to** add tips from individual tables to the tip pool,
**So that** all gratuities are aggregated for fair distribution at the end of the shift.

- Given: an open lunch tip pool
- When: tips from Table 5 ($100), Table 10 ($50), and the Bar ($75) are added
- Then: the pool total equals $225

---

**As a** manager,
**I want to** distribute pooled tips equally among all participating staff,
**So that** every team member receives the same share regardless of station assignment.

- Given: a $300 tip pool with three staff members using equal distribution
- When: tips are distributed
- Then: each staff member receives exactly $100 regardless of hours worked

---

**As a** manager,
**I want to** distribute pooled tips proportionally by hours worked,
**So that** staff who worked longer shifts receive a proportionally larger share.

- Given: a $100 tip pool with one employee working 8 hours (80%) and another 2 hours (20%)
- When: tips are distributed by hours worked
- Then: the 8-hour employee gets $80 and the 2-hour employee gets $20

---

**As a** manager,
**I want to** distribute pooled tips by assigned points,
**So that** seniority or role-based weighting is reflected in tip shares.

- Given: a $200 tip pool with staff earning 10, 5, and 5 tip points (50%, 25%, 25%)
- When: tips are distributed by points
- Then: the employees receive $100, $50, and $50 respectively

---

**As a** system,
**I want to** reject tip additions to a pool that has already been distributed,
**So that** finalized distributions are not invalidated by late entries.

- Given: a tip pool that has already been distributed to staff
- When: a late tip is added after distribution
- Then: the system rejects the addition because the pool is closed

---

## Employee Availability

**As an** employee,
**I want to** set my availability for a specific day and time range,
**So that** managers can schedule me during times I am able to work.

- Given: an initialized employee availability grain
- When: availability is set for Monday 9 AM to 5 PM as a preferred shift
- Then: the entry should reflect the correct day, time range, availability, and preference

---

**As a** manager,
**I want to** check whether an employee is available at a specific day and time,
**So that** I do not schedule staff outside their stated availability.

- Given: an employee available on Monday from 9 AM to 5 PM
- When: availability is checked at 10 AM Monday, 8 AM Monday, and 10 AM Tuesday
- Then: only the 10 AM Monday check should return available

---

**As an** employee,
**I want to** mark a day as unavailable,
**So that** I am not scheduled to work on days I cannot come in.

- Given: an employee who has marked Sunday as unavailable (day off)
- When: availability is checked for Sunday at noon
- Then: the employee should not be available

---

## Shift Swaps

**As an** employee,
**I want to** request a shift swap with another employee and provide a reason,
**So that** I can arrange coverage when I cannot work my assigned shift.

- Given: a shift swap grain ready to receive a new request
- When: a swap request is created between two employees citing a doctor's appointment
- Then: the request should be in pending status with the swap type and reason recorded

---

**As a** manager,
**I want to** approve a pending shift swap request with notes,
**So that** the shift change is authorized and both employees are notified.

- Given: a pending shift drop request from an employee
- When: a manager approves the shift swap request with notes
- Then: the request status should change to approved with the response timestamp and notes recorded

---

**As an** employee,
**I want to** cancel a pending shift swap request,
**So that** I can withdraw a request that is no longer needed before it is acted upon.

- Given: a pending shift pickup request
- When: the requesting employee cancels the request
- Then: the request status should change to cancelled

---

**As a** system,
**I want to** reject cancellation of an already-approved shift swap,
**So that** finalized schedule changes are not unilaterally reversed.

- Given: a shift drop request that has already been approved
- When: the employee attempts to cancel the approved request
- Then: the system should reject the cancellation since the swap has been finalized

---

## Time Off

**As an** employee,
**I want to** request paid vacation time for a date range,
**So that** my planned absence is submitted for manager approval.

- Given: a time off grain ready to receive a new request
- When: a 7-day vacation request is created starting next week
- Then: the request should be pending, calculated as 8 total days (inclusive), and marked as paid leave

---

**As an** employee,
**I want to** request unpaid leave for personal reasons,
**So that** I can take time away from work when I do not have paid leave available.

- Given: a time off grain ready to receive a new request
- When: an unpaid leave request is created for a personal matter
- Then: the request should be marked as unpaid leave

---

**As a** manager,
**I want to** approve a time off request with optional notes,
**So that** the employee's absence is authorized and recorded in the schedule.

- Given: a pending sick leave request from an employee
- When: a manager approves the time off request with well-wishes
- Then: the request status should change to approved with the review timestamp and notes recorded

---

**As a** manager,
**I want to** reject a time off request with a reason,
**So that** the employee understands why the request was denied and can make alternative plans.

- Given: a pending personal day request from an employee
- When: a manager rejects the time off request citing insufficient notice
- Then: the request status should change to rejected with the rejection reason recorded

---

**As a** system,
**I want to** reject cancellation of an already-approved time off request,
**So that** approved absences follow a formal modification process rather than ad-hoc cancellations.

- Given: a vacation request that has already been approved by a manager
- When: the employee attempts to cancel the approved request
- Then: the system should reject the cancellation since the request has been finalized

---

## Certification Tracking

**As a** manager,
**I want to** add a certification to an employee's record with type, number, and expiration date,
**So that** the venue maintains proof of required professional credentials.

- Given: an active employee with no certifications
- When: a ServSafe Food Handler certification is added with a valid expiration date
- Then: the certification should be recorded with valid status, type, name, number, and days until expiration

---

**As a** system,
**I want to** automatically flag certifications that have already expired,
**So that** managers are immediately aware of lapsed credentials.

- Given: an active employee
- When: a TIPS alcohol service certification is added with an expiration date 30 days in the past
- Then: the certification should be automatically marked as expired with negative days until expiration

---

**As a** manager,
**I want to** check an employee's certification compliance against required types,
**So that** the venue can verify all staff meet regulatory and company requirements.

- Given: an employee with valid food handler and alcohol service certifications
- When: certification compliance is checked against both required types
- Then: the employee should be fully compliant with no missing or expired certifications

---

**As a** manager,
**I want to** identify missing certifications when an employee lacks a required credential,
**So that** the gap can be addressed before it causes a compliance issue.

- Given: an employee with only a food handler certification (missing alcohol service)
- When: certification compliance is checked against both food handler and alcohol service requirements
- Then: the employee should be non-compliant with alcohol service listed as missing

---

**As a** manager,
**I want to** renew an expiring certification with a new expiration date,
**So that** the employee's credential record stays current without creating duplicates.

- Given: an employee with a food handler certification expiring in 30 days
- When: the certification is renewed with a new expiration date 2 years out
- Then: the updated certification should show the new expiration date, valid status, and over 700 days remaining

---

**As a** system,
**I want to** alert managers when certifications are approaching expiration,
**So that** renewals can be arranged proactively before credentials lapse.

- Given: an employee with one certification expiring in 5 days and another valid for a year
- When: certification expirations are checked with a 30-day warning and 7-day critical threshold
- Then: only the soon-to-expire food handler certification should trigger a critical alert

---

## Labor Law Compliance

**As a** system,
**I want to** calculate daily overtime for California employees after 8 hours,
**So that** the venue complies with California's daily overtime threshold.

- Given: a California employee who worked a 10-hour shift in one day
- When: overtime is calculated under California daily overtime rules
- Then: 8 hours should be regular and 2 hours should be overtime (over the 8-hour daily threshold)

---

**As a** system,
**I want to** calculate double overtime for California employees after 12 hours in a single day,
**So that** extended shifts are compensated at the legally required double-time rate.

- Given: a California employee who worked a 14-hour shift in one day
- When: overtime is calculated under California daily overtime rules
- Then: 8 hours should be regular, 4 hours overtime (8-12), and 2 hours double overtime (over 12)

---

**As a** system,
**I want to** calculate weekly overtime for employees under federal jurisdiction after 40 hours,
**So that** the venue complies with the federal Fair Labor Standards Act.

- Given: an employee under federal jurisdiction who worked 5 days of 10 hours each (50 total hours)
- When: overtime is calculated under federal weekly overtime rules
- Then: 40 hours should be regular and 10 hours should be weekly overtime

---

**As a** system,
**I want to** flag a violation when a California employee works without a required meal break,
**So that** the venue is alerted to break compliance failures that could result in penalties.

- Given: a California employee who worked an 8-hour shift with no meal break taken
- When: break compliance is checked under California labor law
- Then: the result should flag a violation for insufficient breaks

---

**As a** system,
**I want to** validate New York break requirements for shifts over 6 hours,
**So that** the venue complies with New York State labor law.

- Given: a New York employee who worked a 7-hour shift and took a 30-minute meal break
- When: break compliance is checked under New York labor law
- Then: the employee should be compliant since New York requires a 30-minute meal break for shifts over 6 hours

---

**As a** system,
**I want to** recognize that Texas has no mandatory break requirements for short shifts,
**So that** managers are not falsely alerted about break compliance in jurisdictions without such rules.

- Given: a Texas employee who worked a 3-hour shift with no breaks taken
- When: break compliance is checked under Texas labor law
- Then: the employee should be compliant since Texas has no mandatory break requirements for short shifts

---

## Tax Calculation

**As a** system,
**I want to** calculate federal tax withholdings including income tax, Social Security, and Medicare,
**So that** payroll deductions meet federal tax obligations.

- Given: an employee with $1,000 gross pay under federal tax jurisdiction
- When: tax withholding is calculated
- Then: federal (22%), Social Security (6.2%), and Medicare (1.45%) should total $296.50 with no state/local tax

---

**As a** system,
**I want to** calculate state income tax withholdings for California employees,
**So that** payroll includes the correct state-level deductions.

- Given: an employee with $1,000 gross pay under California tax jurisdiction
- When: tax withholding is calculated
- Then: state withholding should be $72.50 (7.25%) in addition to federal taxes, totaling over $350

---

**As a** system,
**I want to** correctly withhold zero state tax for Texas employees,
**So that** employees in states without income tax are not over-deducted.

- Given: an employee with $1,000 gross pay under Texas tax jurisdiction
- When: tax withholding is calculated
- Then: state and local withholding should both be zero since Texas has no state income tax

---

**As a** system,
**I want to** cap Social Security withholdings at the annual wage base limit,
**So that** employees are not over-taxed once their year-to-date earnings exceed the cap.

- Given: an employee with $165,000 YTD gross pay (near the Social Security wage cap) earning $10,000 this period
- When: tax withholding is calculated
- Then: Social Security withholding should be less than the full 6.2% since only the remaining amount under the cap is taxable

---

**As a** system,
**I want to** apply the Additional Medicare Tax surtax on earnings above the $200,000 threshold,
**So that** high earners are taxed at the correct rate as required by law.

- Given: an employee with $195,000 YTD gross pay earning $10,000 this period (crossing the $200K Medicare threshold)
- When: tax withholding is calculated
- Then: Medicare withholding should include the additional 0.9% surtax on the $5,000 above the threshold

---

## Payroll Export

**As a** payroll administrator,
**I want to** generate a CSV payroll export with tax detail columns,
**So that** payroll data can be imported into general-purpose accounting systems.

- Given: a payroll export entry for a server with 40 regular hours, 5 OT hours, tips, and tax withholdings
- When: a CSV payroll export is generated with tax details included
- Then: the CSV should contain employee info, hours, and federal tax columns

---

**As a** payroll administrator,
**I want to** generate an ADP-format payroll export,
**So that** payroll data can be imported directly into ADP payroll processing.

- Given: a payroll export entry for a server with regular hours, overtime, and tips
- When: an ADP-format payroll export is generated
- Then: the output should contain ADP header, employee number, and earnings codes (REG, OT, TIPS)

---

**As a** payroll administrator,
**I want to** generate a Gusto-format payroll export,
**So that** payroll data can be imported directly into Gusto payroll processing.

- Given: a payroll export entry with regular, overtime, and double overtime hours
- When: a Gusto-format payroll export is generated
- Then: the output should contain Gusto column headers and the correct hour values for all pay categories

---

**As a** manager,
**I want to** preview payroll totals before exporting,
**So that** I can verify aggregate figures for the pay period before committing to a payroll run.

- Given: payroll export entries for 2 employees with combined 75 regular hours, 5 OT hours, and $1,760 gross pay
- When: a payroll preview is generated for the pay period
- Then: the preview should aggregate totals for employee count, hours, gross pay, withholdings, and net pay
