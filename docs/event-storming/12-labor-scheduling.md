# Event Storming: Labor & Scheduling Domain

## Overview

The Labor & Scheduling domain manages employee time tracking, shift scheduling, break management, overtime calculation, tip handling, and labor cost analysis. This domain ensures proper staffing, compliance with labor laws, and accurate payroll data.

---

## Domain Purpose

- **Time Tracking**: Record clock-in/out, breaks, and actual worked hours
- **Scheduling**: Create and manage employee shift schedules
- **Compliance**: Enforce labor law requirements (breaks, overtime, minors)
- **Tip Management**: Track tips, pool distributions, and declarations
- **Labor Costing**: Calculate labor costs as percentage of sales
- **Forecasting**: Predict staffing needs based on sales patterns

---

## Actors

| Actor | Description | Typical Actions |
|-------|-------------|-----------------|
| **Employee** | Staff member | Clock in/out, start/end breaks |
| **Manager** | Supervising | Create schedules, approve timecards |
| **Server** | Tipped employee | Declare tips, view tip pool |
| **System** | Automated | Overtime alerts, compliance checks |
| **Payroll** | Payroll processor | Export timecard data |

---

## Aggregates

### Employee Aggregate

Represents an employee's labor profile.

```
Employee
├── Id: Guid
├── UserId: Guid
├── OrgId: Guid
├── EmployeeNumber: string
├── Status: EmploymentStatus
├── Type: EmploymentType
├── PayType: PayType
├── HourlyRate?: decimal
├── OvertimeRate?: decimal
├── TipEligible: bool
├── Roles: List<EmployeeRole>
├── SiteAssignments: List<Guid>
├── MinorStatus?: MinorInfo
├── HiredAt: DateTime
├── TerminatedAt?: DateTime
└── Settings: EmployeeSettings
```

### Timecard Aggregate

Represents work hours for an employee in a pay period.

```
Timecard
├── Id: Guid
├── EmployeeId: Guid
├── SiteId: Guid
├── PayPeriodStart: DateOnly
├── PayPeriodEnd: DateOnly
├── Status: TimecardStatus
├── Shifts: List<ShiftEntry>
├── TotalRegularHours: decimal
├── TotalOvertimeHours: decimal
├── TotalDoubleTimeHours: decimal
├── TotalBreakMinutes: int
├── TotalTips: decimal
├── ApprovedAt?: DateTime
├── ApprovedBy?: Guid
└── Notes: List<TimecardNote>
```

### ShiftEntry Entity

```
ShiftEntry
├── Id: Guid
├── ScheduledShiftId?: Guid
├── Date: DateOnly
├── ClockInAt: DateTime
├── ClockOutAt?: DateTime
├── Breaks: List<BreakEntry>
├── WorkedHours: decimal
├── RegularHours: decimal
├── OvertimeHours: decimal
├── Role: string
├── TipsDeclared: decimal
├── TipPoolShare: decimal
├── Status: ShiftStatus
├── ClockInMethod: ClockMethod
├── ClockOutMethod?: ClockMethod
├── GeoLocation?: GeoLocation
└── Adjustments: List<TimeAdjustment>
```

### BreakEntry Entity

```
BreakEntry
├── Id: Guid
├── Type: BreakType
├── StartAt: DateTime
├── EndAt?: DateTime
├── Duration: int (minutes)
├── IsPaid: bool
└── WasWaived: bool
```

### Schedule Aggregate

Represents a schedule for a site.

```
Schedule
├── Id: Guid
├── SiteId: Guid
├── WeekStartDate: DateOnly
├── WeekEndDate: DateOnly
├── Status: ScheduleStatus
├── Shifts: List<ScheduledShift>
├── TotalScheduledHours: decimal
├── LaborBudget?: decimal
├── PublishedAt?: DateTime
├── PublishedBy?: Guid
└── Notes?: string
```

### ScheduledShift Entity

```
ScheduledShift
├── Id: Guid
├── EmployeeId: Guid
├── EmployeeName: string
├── Date: DateOnly
├── StartTime: TimeOnly
├── EndTime: TimeOnly
├── ScheduledHours: decimal
├── Role: string
├── Station?: string
├── Notes?: string
├── Status: ShiftStatus
├── SwapRequests: List<SwapRequest>
├── DroppedAt?: DateTime
└── CoveredBy?: Guid
```

### TipPool Aggregate

Manages tip pooling for a shift period.

```
TipPool
├── Id: Guid
├── SiteId: Guid
├── Date: DateOnly
├── ShiftPeriod: ShiftPeriod
├── TotalTips: decimal
├── CashTips: decimal
├── CreditTips: decimal
├── PoolParticipants: List<TipParticipant>
├── DistributionMethod: TipDistributionMethod
├── Status: TipPoolStatus
├── DistributedAt?: DateTime
└── ApprovedBy?: Guid
```

### TipParticipant Entity

```
TipParticipant
├── EmployeeId: Guid
├── EmployeeName: string
├── Role: string
├── HoursWorked: decimal
├── PointsEarned: decimal
├── ShareAmount: decimal
├── DeclaredTips: decimal
├── TotalTips: decimal
└── PoolContribution: decimal
```

---

## Commands

### Time Clock Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `ClockIn` | Start shift | Employee active, not clocked in | Employee |
| `ClockOut` | End shift | Employee clocked in | Employee |
| `StartBreak` | Begin break | On shift, not on break | Employee |
| `EndBreak` | Resume work | On break | Employee |
| `WaiveBreak` | Skip required break | Manager approval | Employee |
| `AdjustTime` | Correct clock time | Manager approval | Manager |
| `AddMissedPunch` | Add forgotten punch | Manager approval | Manager |
| `DeletePunch` | Remove erroneous punch | Manager approval | Manager |

### Timecard Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `SubmitTimecard` | Submit for approval | Period complete | Employee |
| `ApproveTimecard` | Manager approval | Submitted | Manager |
| `RejectTimecard` | Reject for corrections | Submitted | Manager |
| `ProcessTimecard` | Mark for payroll | Approved | Payroll |
| `AddTimecardNote` | Add note | Timecard exists | Manager |

### Scheduling Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `CreateSchedule` | New week schedule | Week not scheduled | Manager |
| `AddShift` | Add shift to schedule | Schedule exists | Manager |
| `UpdateShift` | Modify shift | Shift exists | Manager |
| `DeleteShift` | Remove shift | Shift exists | Manager |
| `PublishSchedule` | Make visible to staff | Schedule ready | Manager |
| `CopySchedule` | Copy from previous | Previous exists | Manager |

### Shift Management Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `RequestShiftSwap` | Request to swap | Both shifts exist | Employee |
| `ApproveShiftSwap` | Approve swap | Swap requested | Manager |
| `DropShift` | Release shift | Shift assigned | Employee |
| `PickupShift` | Take open shift | Shift available | Employee |
| `AssignShiftCoverage` | Assign coverage | Shift dropped | Manager |

### Tip Commands

| Command | Description | Preconditions | Actor |
|---------|-------------|---------------|-------|
| `DeclareTips` | Report cash tips | Shift worked | Employee |
| `RecordCreditTips` | Record card tips | Shift complete | System |
| `DistributeTipPool` | Calculate shares | Pool ready | Manager |
| `ApproveTipDistribution` | Approve payout | Pool distributed | Manager |
| `AdjustTips` | Correct tips | Manager approval | Manager |

---

## Domain Events

### Time Clock Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `EmployeeClockedIn` | Shift started | EmployeeId, SiteId, Role, Time, Method | ClockIn |
| `EmployeeClockedOut` | Shift ended | EmployeeId, Duration, Breaks, Tips | ClockOut |
| `BreakStarted` | Break began | EmployeeId, BreakType, Time | StartBreak |
| `BreakEnded` | Break finished | EmployeeId, Duration, WasPaid | EndBreak |
| `BreakWaived` | Break skipped | EmployeeId, BreakType, Reason, ApprovedBy | WaiveBreak |
| `TimeAdjusted` | Time corrected | ShiftId, OldTime, NewTime, Reason, AdjustedBy | AdjustTime |
| `MissedPunchAdded` | Punch added | EmployeeId, PunchType, Time, AddedBy | AddMissedPunch |
| `PunchDeleted` | Punch removed | ShiftId, PunchType, DeletedBy, Reason | DeletePunch |

### Timecard Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `TimecardSubmitted` | Submitted for review | TimecardId, EmployeeId, Hours, Tips | SubmitTimecard |
| `TimecardApproved` | Manager approved | TimecardId, ApprovedBy | ApproveTimecard |
| `TimecardRejected` | Rejected for edits | TimecardId, RejectedBy, Reason | RejectTimecard |
| `TimecardProcessed` | Ready for payroll | TimecardId, ProcessedAt | ProcessTimecard |
| `OvertimeTriggered` | OT threshold reached | EmployeeId, OvertimeHours, Type | Clock tracking |

### Schedule Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `ScheduleCreated` | New schedule | ScheduleId, SiteId, WeekStart | CreateSchedule |
| `ShiftScheduled` | Shift added | ScheduleId, ShiftId, Employee, Times | AddShift |
| `ShiftUpdated` | Shift modified | ShiftId, Changes | UpdateShift |
| `ShiftDeleted` | Shift removed | ShiftId, DeletedBy | DeleteShift |
| `SchedulePublished` | Made visible | ScheduleId, PublishedBy | PublishSchedule |
| `ScheduleCopied` | Copied from prior | NewScheduleId, SourceScheduleId | CopySchedule |

### Shift Change Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `ShiftSwapRequested` | Swap requested | ShiftId1, ShiftId2, RequestedBy | RequestShiftSwap |
| `ShiftSwapApproved` | Swap approved | SwapId, ApprovedBy | ApproveShiftSwap |
| `ShiftSwapRejected` | Swap denied | SwapId, RejectedBy, Reason | System/Manager |
| `ShiftDropped` | Shift released | ShiftId, DroppedBy | DropShift |
| `ShiftPickedUp` | Shift taken | ShiftId, PickedUpBy | PickupShift |
| `ShiftCoverageAssigned` | Coverage assigned | ShiftId, CoveredBy, AssignedBy | AssignShiftCoverage |

### Tip Events

| Event | Description | Key Data | Triggered By |
|-------|-------------|----------|--------------|
| `TipsDeclared` | Cash tips reported | EmployeeId, ShiftId, Amount | DeclareTips |
| `CreditTipsRecorded` | Card tips recorded | ShiftId, Amount | RecordCreditTips |
| `TipPoolDistributed` | Shares calculated | PoolId, Participants, Amounts | DistributeTipPool |
| `TipDistributionApproved` | Payout approved | PoolId, ApprovedBy | ApproveTipDistribution |
| `TipsAdjusted` | Tips corrected | EmployeeId, OldAmount, NewAmount, Reason | AdjustTips |

---

## Event Details

### EmployeeClockedIn

```csharp
public record EmployeeClockedIn : DomainEvent
{
    public override string EventType => "labor.timecard.clocked_in";

    public required Guid EmployeeId { get; init; }
    public required string EmployeeName { get; init; }
    public required Guid SiteId { get; init; }
    public required string SiteName { get; init; }
    public required Guid ShiftEntryId { get; init; }
    public Guid? ScheduledShiftId { get; init; }
    public required string Role { get; init; }
    public required DateTime ClockInTime { get; init; }
    public required ClockMethod Method { get; init; }
    public GeoLocation? Location { get; init; }
    public required bool IsOnTime { get; init; }
    public int? MinutesEarlyLate { get; init; }
}

public enum ClockMethod
{
    Terminal,
    Pin,
    QrCode,
    Mobile,
    Manual
}
```

### EmployeeClockedOut

```csharp
public record EmployeeClockedOut : DomainEvent
{
    public override string EventType => "labor.timecard.clocked_out";

    public required Guid EmployeeId { get; init; }
    public required Guid ShiftEntryId { get; init; }
    public required DateTime ClockOutTime { get; init; }
    public required ClockMethod Method { get; init; }

    // Hours Worked
    public required decimal TotalHours { get; init; }
    public required decimal RegularHours { get; init; }
    public required decimal OvertimeHours { get; init; }
    public decimal DoubleTimeHours { get; init; }

    // Breaks
    public required int PaidBreakMinutes { get; init; }
    public required int UnpaidBreakMinutes { get; init; }
    public required int BreakCount { get; init; }

    // Tips
    public required decimal TipsDeclared { get; init; }
    public required decimal TipPoolShare { get; init; }

    // Compliance
    public required bool MetBreakRequirements { get; init; }
    public string? ComplianceNotes { get; init; }
}
```

### TipPoolDistributed

```csharp
public record TipPoolDistributed : DomainEvent
{
    public override string EventType => "labor.tips.pool_distributed";

    public required Guid PoolId { get; init; }
    public required Guid SiteId { get; init; }
    public required DateOnly Date { get; init; }
    public required ShiftPeriod ShiftPeriod { get; init; }
    public required decimal TotalTipsToDistribute { get; init; }
    public required TipDistributionMethod Method { get; init; }
    public required IReadOnlyList<TipShareResult> Shares { get; init; }
    public required DateTime DistributedAt { get; init; }
}

public record TipShareResult
{
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; }
    public string Role { get; init; }
    public decimal HoursWorked { get; init; }
    public decimal PointsEarned { get; init; }
    public decimal SharePercentage { get; init; }
    public decimal ShareAmount { get; init; }
}

public enum TipDistributionMethod
{
    Equal,
    ByHours,
    ByPoints,
    ByRole,
    Custom
}
```

### OvertimeTriggered

```csharp
public record OvertimeTriggered : DomainEvent
{
    public override string EventType => "labor.timecard.overtime_triggered";

    public required Guid EmployeeId { get; init; }
    public required string EmployeeName { get; init; }
    public required Guid SiteId { get; init; }
    public required OvertimeType Type { get; init; }
    public required decimal TotalHoursWorked { get; init; }
    public required decimal OvertimeHours { get; init; }
    public required decimal ThresholdHours { get; init; }
    public required DateTime TriggeredAt { get; init; }
    public required DateRange Period { get; init; }
}

public enum OvertimeType
{
    Daily,      // Over 8 hours in day
    Weekly,     // Over 40 hours in week
    DoubleTime, // Over 12 hours in day
    SeventhDay  // Working 7th consecutive day
}
```

---

## Policies (Event Reactions)

### When EmployeeClockedIn

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Update Schedule Status | Mark shift as started | Schedule |
| Check Compliance | Validate not minor restriction | Compliance |
| Alert if Unscheduled | Notify manager | Notifications |
| Log Attendance | Record for tracking | Reporting |

### When EmployeeClockedOut

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Update Timecard | Add shift hours | Timecard |
| Check Break Compliance | Verify breaks taken | Compliance |
| Calculate Overtime | Update OT hours | Timecard |
| Trigger Tip Pool | Add to pool if eligible | Tips |
| Post Labor Cost | Record expense | Accounting |

### When OvertimeTriggered

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Alert Manager | Overtime notification | Notifications |
| Update Rate | Apply OT rate | Timecard |
| Log for Reporting | Track OT trends | Reporting |

### When SchedulePublished

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Notify Employees | Send schedule notification | Notifications |
| Update Availability | Show on employee app | Display |
| Forecast Labor Cost | Calculate projected cost | Reporting |

### When TipPoolDistributed

| Policy | Reaction | Target Domain |
|--------|----------|---------------|
| Update Timecards | Add tip amounts | Timecard |
| Record for Payroll | Include in wages | Payroll |
| Notify Employees | Show tip amounts | Notifications |

---

## Read Models / Projections

### TimecardView

```csharp
public record TimecardView
{
    public Guid TimecardId { get; init; }
    public string EmployeeName { get; init; }
    public string EmployeeNumber { get; init; }
    public DateRange PayPeriod { get; init; }
    public TimecardStatus Status { get; init; }

    // Hours Summary
    public decimal TotalHours { get; init; }
    public decimal RegularHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal DoubleTimeHours { get; init; }
    public int PaidBreakMinutes { get; init; }
    public int UnpaidBreakMinutes { get; init; }

    // Tips
    public decimal DeclaredTips { get; init; }
    public decimal TipPoolShare { get; init; }
    public decimal TotalTips { get; init; }

    // Shifts
    public IReadOnlyList<ShiftSummary> Shifts { get; init; }

    // Approvals
    public string? ApprovedByName { get; init; }
    public DateTime? ApprovedAt { get; init; }
}

public record ShiftSummary
{
    public DateOnly Date { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly? EndTime { get; init; }
    public decimal Hours { get; init; }
    public string Role { get; init; }
    public int BreakMinutes { get; init; }
    public decimal Tips { get; init; }
    public ShiftStatus Status { get; init; }
}
```

### ScheduleView

```csharp
public record ScheduleView
{
    public Guid ScheduleId { get; init; }
    public string SiteName { get; init; }
    public DateRange Week { get; init; }
    public ScheduleStatus Status { get; init; }
    public decimal TotalScheduledHours { get; init; }
    public decimal? LaborBudget { get; init; }
    public decimal? ProjectedLaborCost { get; init; }

    // By Day
    public IReadOnlyDictionary<DateOnly, DaySchedule> ByDay { get; init; }

    // By Employee
    public IReadOnlyList<EmployeeScheduleSummary> ByEmployee { get; init; }

    // Open Shifts
    public IReadOnlyList<OpenShift> OpenShifts { get; init; }
}

public record DaySchedule
{
    public DateOnly Date { get; init; }
    public IReadOnlyList<ScheduledShiftView> Shifts { get; init; }
    public decimal TotalHours { get; init; }
    public int EmployeeCount { get; init; }
}
```

### LaborCostReport

```csharp
public record LaborCostReport
{
    public Guid SiteId { get; init; }
    public DateRange Period { get; init; }

    // Sales
    public decimal TotalSales { get; init; }

    // Labor Summary
    public decimal TotalLaborCost { get; init; }
    public decimal LaborPercentage { get; init; }

    // Hours Breakdown
    public decimal RegularHours { get; init; }
    public decimal OvertimeHours { get; init; }
    public decimal DoubleTimeHours { get; init; }
    public decimal TotalHours { get; init; }

    // Cost Breakdown
    public decimal RegularWages { get; init; }
    public decimal OvertimeWages { get; init; }
    public decimal Tips { get; init; }
    public decimal PayrollTaxes { get; init; }
    public decimal Benefits { get; init; }

    // By Role
    public IReadOnlyList<RoleLaborCost> ByRole { get; init; }

    // By Day
    public IReadOnlyList<DailyLaborCost> ByDay { get; init; }

    // Variance
    public decimal? BudgetedLaborCost { get; init; }
    public decimal? Variance { get; init; }
}
```

### AttendanceView

```csharp
public record AttendanceView
{
    public Guid SiteId { get; init; }
    public DateOnly Date { get; init; }
    public IReadOnlyList<AttendanceRecord> Records { get; init; }
    public int ScheduledCount { get; init; }
    public int PresentCount { get; init; }
    public int AbsentCount { get; init; }
    public int LateCount { get; init; }
}

public record AttendanceRecord
{
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; }
    public TimeOnly? ScheduledStart { get; init; }
    public TimeOnly? ActualStart { get; init; }
    public int? MinutesLate { get; init; }
    public AttendanceStatus Status { get; init; }
    public bool IsCurrentlyWorking { get; init; }
}
```

---

## Compliance Rules

### Break Requirements

| Jurisdiction | Work Hours | Break Required | Paid |
|--------------|------------|----------------|------|
| California | > 5 hours | 30 min meal | No |
| California | > 6 hours | 10 min rest | Yes |
| Federal | > 6 hours | No requirement | - |

### Overtime Rules

| Type | Threshold | Rate |
|------|-----------|------|
| Daily OT | > 8 hours | 1.5x |
| Weekly OT | > 40 hours | 1.5x |
| Double Time | > 12 hours/day | 2x |
| 7th Day | 7th consecutive | 1.5x |

### Minor Restrictions

| Age | Max Hours/Day | Max Hours/Week | Restricted Hours |
|-----|---------------|----------------|------------------|
| 14-15 | 3 (school) / 8 | 18 (school) / 40 | Before 7am, After 7pm |
| 16-17 | 8 | 48 | Before 6am |

---

## Event Type Registry

```csharp
public static class LaborEventTypes
{
    // Time Clock
    public const string EmployeeClockedIn = "labor.timecard.clocked_in";
    public const string EmployeeClockedOut = "labor.timecard.clocked_out";
    public const string BreakStarted = "labor.timecard.break_started";
    public const string BreakEnded = "labor.timecard.break_ended";
    public const string BreakWaived = "labor.timecard.break_waived";
    public const string TimeAdjusted = "labor.timecard.time_adjusted";
    public const string MissedPunchAdded = "labor.timecard.missed_punch_added";
    public const string PunchDeleted = "labor.timecard.punch_deleted";

    // Timecard
    public const string TimecardSubmitted = "labor.timecard.submitted";
    public const string TimecardApproved = "labor.timecard.approved";
    public const string TimecardRejected = "labor.timecard.rejected";
    public const string TimecardProcessed = "labor.timecard.processed";
    public const string OvertimeTriggered = "labor.timecard.overtime_triggered";

    // Schedule
    public const string ScheduleCreated = "labor.schedule.created";
    public const string ShiftScheduled = "labor.schedule.shift_scheduled";
    public const string ShiftUpdated = "labor.schedule.shift_updated";
    public const string ShiftDeleted = "labor.schedule.shift_deleted";
    public const string SchedulePublished = "labor.schedule.published";
    public const string ScheduleCopied = "labor.schedule.copied";

    // Shift Changes
    public const string ShiftSwapRequested = "labor.shift.swap_requested";
    public const string ShiftSwapApproved = "labor.shift.swap_approved";
    public const string ShiftSwapRejected = "labor.shift.swap_rejected";
    public const string ShiftDropped = "labor.shift.dropped";
    public const string ShiftPickedUp = "labor.shift.picked_up";
    public const string ShiftCoverageAssigned = "labor.shift.coverage_assigned";

    // Tips
    public const string TipsDeclared = "labor.tips.declared";
    public const string CreditTipsRecorded = "labor.tips.credit_recorded";
    public const string TipPoolDistributed = "labor.tips.pool_distributed";
    public const string TipDistributionApproved = "labor.tips.distribution_approved";
    public const string TipsAdjusted = "labor.tips.adjusted";
}
```
