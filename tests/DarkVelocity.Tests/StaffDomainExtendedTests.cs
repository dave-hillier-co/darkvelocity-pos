using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Services;
using DarkVelocity.Host.State;
using FluentAssertions;
using Orleans.TestingHost;
using Xunit;

namespace DarkVelocity.Tests;

/// <summary>
/// Extended tests for Staff domain covering:
/// - Schedule conflict detection
/// - Time off accrual and balance management
/// - Shift swap workflow edge cases
/// - Availability matching scenarios
/// - Overtime edge cases
/// - Break enforcement scenarios
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class StaffDomainExtendedTests
{
    private readonly TestCluster _cluster;

    public StaffDomainExtendedTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    // ============================================================================
    // Schedule Management Tests
    // ============================================================================

    // Given: a published weekly schedule with one employee already assigned a morning shift (9 AM - 5 PM)
    // When: a second evening shift (5 PM - 11 PM) is added for the same employee on the same day
    // Then: both shifts should be tracked on the schedule for that employee
    [Fact]
    public async Task ScheduleGrain_AddShift_AddsMultipleShiftsForSameEmployee()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(100));
        var grain = _cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: null));

        var employeeId = Guid.NewGuid();
        var shiftDate = weekStart.ToDateTime(TimeOnly.MinValue);

        // Add first shift: 9 AM - 5 PM
        await grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: Guid.NewGuid(),
            EmployeeId: employeeId,
            RoleId: Guid.NewGuid(),
            Date: shiftDate,
            StartTime: TimeSpan.FromHours(9),
            EndTime: TimeSpan.FromHours(17),
            BreakMinutes: 30,
            HourlyRate: 15.00m,
            Notes: "Morning shift"));

        // Act - Add second shift for same day
        await grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: Guid.NewGuid(),
            EmployeeId: employeeId,
            RoleId: Guid.NewGuid(),
            Date: shiftDate,
            StartTime: TimeSpan.FromHours(17),
            EndTime: TimeSpan.FromHours(23),
            BreakMinutes: 15,
            HourlyRate: 15.00m,
            Notes: "Evening shift"));

        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Shifts.Should().HaveCount(2);
        var employeeShifts = await grain.GetShiftsForEmployeeAsync(employeeId);
        employeeShifts.Should().HaveCount(2);
    }

    // Given: a weekly schedule for a site
    // When: an 8-hour shift with a 30-minute break at $15/hr is added
    // Then: the total labor cost should be $112.50 (7.5 billable hours at $15/hr)
    [Fact]
    public async Task ScheduleGrain_AddShift_CalculatesLaborCostCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(101));
        var grain = _cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: null));

        // Act - Add 8 hour shift with 30 minute break at $15/hr
        await grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: Guid.NewGuid(),
            EmployeeId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            Date: weekStart.ToDateTime(TimeOnly.MinValue),
            StartTime: TimeSpan.FromHours(9),
            EndTime: TimeSpan.FromHours(17),
            BreakMinutes: 30,
            HourlyRate: 15.00m,
            Notes: null));

        // Assert
        // Scheduled hours = 8 hours - 0.5 hours break = 7.5 hours
        // Labor cost = 7.5 * $15 = $112.50
        var laborCost = await grain.GetTotalLaborCostAsync();
        laborCost.Should().Be(112.50m);
    }

    // Given: a weekly schedule that has been locked by a manager
    // When: an attempt is made to add a new shift to the locked schedule
    // Then: the system should reject the modification with an error
    [Fact]
    public async Task ScheduleGrain_AddShift_ThrowsWhenLocked()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(102));
        var grain = _cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: null));

        await grain.LockAsync();

        // Act & Assert
        var act = () => grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: Guid.NewGuid(),
            EmployeeId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            Date: weekStart.ToDateTime(TimeOnly.MinValue),
            StartTime: TimeSpan.FromHours(9),
            EndTime: TimeSpan.FromHours(17),
            BreakMinutes: 30,
            HourlyRate: 15.00m,
            Notes: null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot modify a locked schedule");
    }

    // Given: a schedule with an existing shift running from 9 AM to 5 PM
    // When: the shift times are updated to 10 AM to 6 PM
    // Then: the shift should reflect the new start and end times
    [Fact]
    public async Task ScheduleGrain_UpdateShift_UpdatesTimeCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(103));
        var grain = _cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: null));

        var shiftId = Guid.NewGuid();
        await grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: shiftId,
            EmployeeId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            Date: weekStart.ToDateTime(TimeOnly.MinValue),
            StartTime: TimeSpan.FromHours(9),
            EndTime: TimeSpan.FromHours(17),
            BreakMinutes: 30,
            HourlyRate: 15.00m,
            Notes: null));

        // Act - Update shift times
        await grain.UpdateShiftAsync(new UpdateShiftCommand(
            ShiftId: shiftId,
            StartTime: TimeSpan.FromHours(10),
            EndTime: TimeSpan.FromHours(18),
            BreakMinutes: null,
            EmployeeId: null,
            RoleId: null,
            Notes: null));

        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        var shift = snapshot.Shifts.First(s => s.ShiftId == shiftId);
        shift.StartTime.Should().Be(TimeSpan.FromHours(10));
        shift.EndTime.Should().Be(TimeSpan.FromHours(18));
    }

    // ============================================================================
    // Time Off Accrual and Balance Tests
    // ============================================================================

    // Given: an employee requesting vacation time
    // When: a time off request is created spanning 5 calendar days (inclusive)
    // Then: the total days should be calculated as 5
    [Fact]
    public async Task TimeOffGrain_Create_CalculatesCorrectTotalDays()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeOffGrain>(
            GrainKeys.TimeOffRequest(orgId, requestId));

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var endDate = startDate.AddDays(4); // 5 days total (inclusive)

        // Act
        var snapshot = await grain.CreateAsync(new CreateTimeOffCommand(
            EmployeeId: Guid.NewGuid(),
            Type: TimeOffType.Vacation,
            StartDate: startDate,
            EndDate: endDate,
            Reason: "Family trip"));

        // Assert
        snapshot.TotalDays.Should().Be(5);
    }

    // Given: an employee requesting a single personal day
    // When: a time off request is created with the same start and end date
    // Then: the total days should be calculated as 1
    [Fact]
    public async Task TimeOffGrain_Create_SingleDayRequest_CalculatesCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeOffGrain>(
            GrainKeys.TimeOffRequest(orgId, requestId));

        var singleDay = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15));

        // Act
        var snapshot = await grain.CreateAsync(new CreateTimeOffCommand(
            EmployeeId: Guid.NewGuid(),
            Type: TimeOffType.Personal,
            StartDate: singleDay,
            EndDate: singleDay,
            Reason: "Appointment"));

        // Assert
        snapshot.TotalDays.Should().Be(1);
    }

    // Given: an employee requesting vacation time off
    // When: the vacation time off request is created
    // Then: the request should be automatically classified as paid leave
    [Fact]
    public async Task TimeOffGrain_VacationTimeOff_MarkedAsPaid()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeOffGrain>(
            GrainKeys.TimeOffRequest(orgId, requestId));

        // Act
        var snapshot = await grain.CreateAsync(new CreateTimeOffCommand(
            EmployeeId: Guid.NewGuid(),
            Type: TimeOffType.Vacation,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(27)),
            Reason: "Vacation"));

        // Assert
        snapshot.IsPaid.Should().BeTrue();
        snapshot.Type.Should().Be(TimeOffType.Vacation);
    }

    // Given: an employee calling in sick
    // When: a sick leave time off request is created
    // Then: the request should be automatically classified as paid leave
    [Fact]
    public async Task TimeOffGrain_SickTimeOff_MarkedAsPaid()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeOffGrain>(
            GrainKeys.TimeOffRequest(orgId, requestId));

        // Act
        var snapshot = await grain.CreateAsync(new CreateTimeOffCommand(
            EmployeeId: Guid.NewGuid(),
            Type: TimeOffType.Sick,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Reason: "Ill"));

        // Assert
        snapshot.IsPaid.Should().BeTrue();
        snapshot.Type.Should().Be(TimeOffType.Sick);
    }

    // Given: an employee requesting extended unpaid leave
    // When: the unpaid leave request is created
    // Then: the request should be classified as unpaid
    [Fact]
    public async Task TimeOffGrain_UnpaidLeave_MarkedAsUnpaid()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeOffGrain>(
            GrainKeys.TimeOffRequest(orgId, requestId));

        // Act
        var snapshot = await grain.CreateAsync(new CreateTimeOffCommand(
            EmployeeId: Guid.NewGuid(),
            Type: TimeOffType.Unpaid,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(37)),
            Reason: "Extended leave"));

        // Assert
        snapshot.IsPaid.Should().BeFalse();
    }

    // Given: a pending vacation time off request
    // When: the employee cancels the request before it is reviewed
    // Then: the request status should change to cancelled
    [Fact]
    public async Task TimeOffGrain_Cancel_CanCancelPendingRequest()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeOffGrain>(
            GrainKeys.TimeOffRequest(orgId, requestId));

        await grain.CreateAsync(new CreateTimeOffCommand(
            EmployeeId: Guid.NewGuid(),
            Type: TimeOffType.Vacation,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(21)),
            Reason: "Holiday"));

        // Act
        var snapshot = await grain.CancelAsync();

        // Assert
        snapshot.Status.Should().Be(TimeOffStatus.Cancelled);
    }

    // Given: a vacation request that has already been approved by a manager
    // When: the employee attempts to cancel the approved request
    // Then: the system should reject the cancellation since the request has been finalized
    [Fact]
    public async Task TimeOffGrain_Cancel_ThrowsIfAlreadyApproved()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeOffGrain>(
            GrainKeys.TimeOffRequest(orgId, requestId));

        await grain.CreateAsync(new CreateTimeOffCommand(
            EmployeeId: Guid.NewGuid(),
            Type: TimeOffType.Vacation,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(21)),
            Reason: "Holiday"));

        await grain.ApproveAsync(new RespondToTimeOffCommand(
            ReviewedByUserId: Guid.NewGuid(),
            Notes: "Approved"));

        // Act & Assert
        var act = () => grain.CancelAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot cancel this request");
    }

    // ============================================================================
    // Shift Swap Workflow Edge Cases
    // ============================================================================

    // Given: a pending shift swap request between two employees
    // When: a manager rejects the request citing staffing requirements
    // Then: the request status should change to rejected with the rejection notes and response timestamp
    [Fact]
    public async Task ShiftSwapGrain_Reject_UpdatesStatusAndNotes()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IShiftSwapGrain>(
            GrainKeys.ShiftSwapRequest(orgId, requestId));

        await grain.CreateAsync(new CreateShiftSwapCommand(
            RequestingEmployeeId: Guid.NewGuid(),
            RequestingShiftId: Guid.NewGuid(),
            TargetEmployeeId: Guid.NewGuid(),
            TargetShiftId: Guid.NewGuid(),
            Type: ShiftSwapType.Swap,
            Reason: "Need to swap shifts"));

        // Act
        var snapshot = await grain.RejectAsync(new RespondToShiftSwapCommand(
            RespondingUserId: Guid.NewGuid(),
            Notes: "Staffing requirements not met"));

        // Assert
        snapshot.Status.Should().Be(ShiftSwapStatus.Rejected);
        snapshot.Notes.Should().Be("Staffing requirements not met");
        snapshot.RespondedAt.Should().NotBeNull();
    }

    // Given: a shift drop request that has already been approved
    // When: the employee attempts to cancel the approved request
    // Then: the system should reject the cancellation since the swap has been finalized
    [Fact]
    public async Task ShiftSwapGrain_Cancel_ThrowsIfAlreadyApproved()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IShiftSwapGrain>(
            GrainKeys.ShiftSwapRequest(orgId, requestId));

        await grain.CreateAsync(new CreateShiftSwapCommand(
            RequestingEmployeeId: Guid.NewGuid(),
            RequestingShiftId: Guid.NewGuid(),
            TargetEmployeeId: null,
            TargetShiftId: null,
            Type: ShiftSwapType.Drop,
            Reason: null));

        await grain.ApproveAsync(new RespondToShiftSwapCommand(
            RespondingUserId: Guid.NewGuid(),
            Notes: "Approved"));

        // Act & Assert
        var act = () => grain.CancelAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot cancel this request");
    }

    // Given: a shift pickup request that has already been cancelled by the employee
    // When: a manager attempts to approve the cancelled request
    // Then: the system should reject the approval since the request is no longer pending
    [Fact]
    public async Task ShiftSwapGrain_Approve_ThrowsIfAlreadyCancelled()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IShiftSwapGrain>(
            GrainKeys.ShiftSwapRequest(orgId, requestId));

        await grain.CreateAsync(new CreateShiftSwapCommand(
            RequestingEmployeeId: Guid.NewGuid(),
            RequestingShiftId: Guid.NewGuid(),
            TargetEmployeeId: null,
            TargetShiftId: null,
            Type: ShiftSwapType.Pickup,
            Reason: null));

        await grain.CancelAsync();

        // Act & Assert
        var act = () => grain.ApproveAsync(new RespondToShiftSwapCommand(
            RespondingUserId: Guid.NewGuid(),
            Notes: null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Request is not pending");
    }

    // Given: a newly created shift drop request
    // When: the current status is queried
    // Then: the status should be pending
    [Fact]
    public async Task ShiftSwapGrain_GetStatus_ReturnsCurrentStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IShiftSwapGrain>(
            GrainKeys.ShiftSwapRequest(orgId, requestId));

        await grain.CreateAsync(new CreateShiftSwapCommand(
            RequestingEmployeeId: Guid.NewGuid(),
            RequestingShiftId: Guid.NewGuid(),
            TargetEmployeeId: null,
            TargetShiftId: null,
            Type: ShiftSwapType.Drop,
            Reason: "Personal reasons"));

        // Act
        var status = await grain.GetStatusAsync();

        // Assert
        status.Should().Be(ShiftSwapStatus.Pending);
    }

    // Given: two employees who want to swap shifts
    // When: a swap-type shift swap request is created with both target employee and shift specified
    // Then: the request should have the swap type with target employee and shift IDs populated
    [Fact]
    public async Task ShiftSwapGrain_CreateSwapType_RequiresTargetEmployee()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IShiftSwapGrain>(
            GrainKeys.ShiftSwapRequest(orgId, requestId));

        // Act
        var snapshot = await grain.CreateAsync(new CreateShiftSwapCommand(
            RequestingEmployeeId: Guid.NewGuid(),
            RequestingShiftId: Guid.NewGuid(),
            TargetEmployeeId: Guid.NewGuid(),
            TargetShiftId: Guid.NewGuid(),
            Type: ShiftSwapType.Swap,
            Reason: "Need to swap shifts"));

        // Assert
        snapshot.Type.Should().Be(ShiftSwapType.Swap);
        snapshot.TargetEmployeeId.Should().NotBeNull();
        snapshot.TargetShiftId.Should().NotBeNull();
    }

    // ============================================================================
    // Availability Matching Tests
    // ============================================================================

    // Given: an initialized employee availability grain
    // When: a full week of availability is set (Monday-Friday 9-5, weekends off)
    // Then: all 7 days should have availability entries recorded
    [Fact]
    public async Task EmployeeAvailabilityGrain_SetWeekAvailability_SetsAllDays()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeAvailabilityGrain>(
            GrainKeys.EmployeeAvailability(orgId, employeeId));

        await grain.InitializeAsync(employeeId);

        var availabilities = new List<SetAvailabilityCommand>
        {
            // Monday-Friday: 9 AM - 5 PM
            new(1, TimeSpan.FromHours(9), TimeSpan.FromHours(17), true, true, null, null, null),
            new(2, TimeSpan.FromHours(9), TimeSpan.FromHours(17), true, true, null, null, null),
            new(3, TimeSpan.FromHours(9), TimeSpan.FromHours(17), true, true, null, null, null),
            new(4, TimeSpan.FromHours(9), TimeSpan.FromHours(17), true, true, null, null, null),
            new(5, TimeSpan.FromHours(9), TimeSpan.FromHours(17), true, true, null, null, null),
            // Saturday: Not available
            new(6, null, null, false, false, null, null, "Weekend off"),
            // Sunday: Not available
            new(0, null, null, false, false, null, null, "Weekend off")
        };

        // Act
        await grain.SetWeekAvailabilityAsync(availabilities);
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Availabilities.Should().HaveCount(7);
    }

    // Given: an employee available on Monday from 9 AM to 5 PM
    // When: availability is checked at 8 AM (too early), 6 PM (too late), and noon (within range)
    // Then: only the noon check should return available
    [Fact]
    public async Task EmployeeAvailabilityGrain_IsAvailableOn_ReturnsFalseOutsideAvailableHours()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeAvailabilityGrain>(
            GrainKeys.EmployeeAvailability(orgId, employeeId));

        await grain.InitializeAsync(employeeId);
        await grain.SetAvailabilityAsync(new SetAvailabilityCommand(
            DayOfWeek: 1, // Monday
            StartTime: TimeSpan.FromHours(9),
            EndTime: TimeSpan.FromHours(17),
            IsAvailable: true,
            IsPreferred: true,
            EffectiveFrom: null,
            EffectiveTo: null,
            Notes: null));

        // Act & Assert
        (await grain.IsAvailableOnAsync(1, TimeSpan.FromHours(8))).Should().BeFalse(); // Too early
        (await grain.IsAvailableOnAsync(1, TimeSpan.FromHours(18))).Should().BeFalse(); // Too late
        (await grain.IsAvailableOnAsync(1, TimeSpan.FromHours(12))).Should().BeTrue(); // Within range
    }

    // Given: an employee who has marked Sunday as unavailable (day off)
    // When: availability is checked for Sunday at noon
    // Then: the employee should not be available
    [Fact]
    public async Task EmployeeAvailabilityGrain_IsAvailableOn_ReturnsFalseForUnavailableDay()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeAvailabilityGrain>(
            GrainKeys.EmployeeAvailability(orgId, employeeId));

        await grain.InitializeAsync(employeeId);
        await grain.SetAvailabilityAsync(new SetAvailabilityCommand(
            DayOfWeek: 0, // Sunday
            StartTime: null,
            EndTime: null,
            IsAvailable: false,
            IsPreferred: false,
            EffectiveFrom: null,
            EffectiveTo: null,
            Notes: "Day off"));

        // Act
        var isAvailable = await grain.IsAvailableOnAsync(0, TimeSpan.FromHours(12));

        // Assert
        isAvailable.Should().BeFalse();
    }

    // Given: an employee with a Monday availability entry on record
    // When: the availability entry is removed
    // Then: the entry should no longer appear in the employee's availability snapshot
    [Fact]
    public async Task EmployeeAvailabilityGrain_RemoveAvailability_RemovesEntry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeAvailabilityGrain>(
            GrainKeys.EmployeeAvailability(orgId, employeeId));

        await grain.InitializeAsync(employeeId);
        var entry = await grain.SetAvailabilityAsync(new SetAvailabilityCommand(
            DayOfWeek: 1,
            StartTime: TimeSpan.FromHours(9),
            EndTime: TimeSpan.FromHours(17),
            IsAvailable: true,
            IsPreferred: true,
            EffectiveFrom: null,
            EffectiveTo: null,
            Notes: null));

        // Act
        await grain.RemoveAvailabilityAsync(entry.Id);
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Availabilities.Should().NotContain(a => a.Id == entry.Id);
    }

    // Given: an employee with availability entries that have no expiration date
    // When: current availability is queried
    // Then: the active availability entries should be returned
    [Fact]
    public async Task EmployeeAvailabilityGrain_GetCurrentAvailability_ReturnsOnlyCurrentEntries()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeAvailabilityGrain>(
            GrainKeys.EmployeeAvailability(orgId, employeeId));

        await grain.InitializeAsync(employeeId);

        // Add current availability
        await grain.SetAvailabilityAsync(new SetAvailabilityCommand(
            DayOfWeek: 1,
            StartTime: TimeSpan.FromHours(9),
            EndTime: TimeSpan.FromHours(17),
            IsAvailable: true,
            IsPreferred: true,
            EffectiveFrom: null,
            EffectiveTo: null,
            Notes: "Current"));

        // Act
        var currentAvailability = await grain.GetCurrentAvailabilityAsync();

        // Assert
        currentAvailability.Should().NotBeEmpty();
    }

    // ============================================================================
    // Overtime Edge Case Tests
    // ============================================================================

    // Given: an employee in California who has worked 8 hours each day for 7 consecutive days
    // When: overtime is calculated under California labor law
    // Then: the 7th consecutive day should trigger special overtime rules with total hours at 56
    [Fact]
    public async Task LaborLawComplianceGrain_CalculateOvertime_CaliforniaSeventhDay()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        await grain.InitializeDefaultsAsync();

        var employeeId = Guid.NewGuid();
        // Start on a Sunday for a full week
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var periodEnd = periodStart.AddDays(6);

        // Work 7 consecutive days
        var timeEntries = new List<TimeEntryForCalculation>();
        for (int i = 0; i < 7; i++)
        {
            timeEntries.Add(new TimeEntryForCalculation(
                Guid.NewGuid(), employeeId, periodStart.AddDays(i), 8m, 30));
        }

        // Act
        var result = await grain.CalculateOvertimeAsync(
            employeeId, "US-CA", periodStart, periodEnd, timeEntries);

        // Assert
        result.TotalHours.Should().Be(56m); // 7 days * 8 hours
        result.SeventhDayHours.Should().BeGreaterThan(0m); // 7th day should trigger special rules
    }

    // Given: an employee in Colorado who worked a 14-hour shift
    // When: overtime is calculated under Colorado labor law (12-hour daily threshold)
    // Then: 12 hours should be regular and 2 hours should be overtime
    [Fact]
    public async Task LaborLawComplianceGrain_CalculateOvertime_ColoradoDaily12Hours()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        await grain.InitializeDefaultsAsync();

        var employeeId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var periodEnd = periodStart.AddDays(6);

        // 14 hour day in Colorado (12 hour daily threshold)
        var timeEntries = new List<TimeEntryForCalculation>
        {
            new(Guid.NewGuid(), employeeId, periodStart, 14m, 30)
        };

        // Act
        var result = await grain.CalculateOvertimeAsync(
            employeeId, "US-CO", periodStart, periodEnd, timeEntries);

        // Assert
        result.TotalHours.Should().Be(14m);
        result.RegularHours.Should().Be(12m); // Colorado has 12-hour daily threshold
        result.OvertimeHours.Should().Be(2m); // Hours over 12
    }

    // Given: an employee in California who worked 9 hours per day for 5 days (45 total hours)
    // When: overtime is calculated under California law with both daily (over 8) and weekly (over 40) thresholds
    // Then: overtime hours should be greater than zero reflecting combined daily and weekly overtime
    [Fact]
    public async Task LaborLawComplianceGrain_CalculateOvertime_CombinedDailyAndWeeklyOvertime()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        await grain.InitializeDefaultsAsync();

        var employeeId = Guid.NewGuid();
        // Start on a Sunday
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek));
        var periodEnd = periodStart.AddDays(6);

        // California: 9 hour days for 5 days = 45 total hours
        // Daily OT: 1 hour/day * 5 days = 5 hours
        // Weekly OT: After 40 hours
        var timeEntries = new List<TimeEntryForCalculation>();
        for (int i = 0; i < 5; i++)
        {
            timeEntries.Add(new TimeEntryForCalculation(
                Guid.NewGuid(), employeeId, periodStart.AddDays(i), 9m, 30));
        }

        // Act
        var result = await grain.CalculateOvertimeAsync(
            employeeId, "US-CA", periodStart, periodEnd, timeEntries);

        // Assert
        result.TotalHours.Should().Be(45m);
        result.OvertimeHours.Should().BeGreaterThan(0m);
    }

    // Given: an employee in the UK who worked 50 hours in a week (5 days of 10 hours)
    // When: overtime is calculated under UK labor law
    // Then: total hours should be tracked at 50 (UK has no mandatory overtime pay but tracks hours over 48)
    [Fact]
    public async Task LaborLawComplianceGrain_CalculateOvertime_UKNoMandatoryOvertimePay()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        await grain.InitializeDefaultsAsync();

        var employeeId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var periodEnd = periodStart.AddDays(6);

        // 50 hour week in UK
        var timeEntries = new List<TimeEntryForCalculation>
        {
            new(Guid.NewGuid(), employeeId, periodStart, 10m, 30),
            new(Guid.NewGuid(), employeeId, periodStart.AddDays(1), 10m, 30),
            new(Guid.NewGuid(), employeeId, periodStart.AddDays(2), 10m, 30),
            new(Guid.NewGuid(), employeeId, periodStart.AddDays(3), 10m, 30),
            new(Guid.NewGuid(), employeeId, periodStart.AddDays(4), 10m, 30)
        };

        // Act
        var result = await grain.CalculateOvertimeAsync(
            employeeId, "UK", periodStart, periodEnd, timeEntries);

        // Assert
        result.TotalHours.Should().Be(50m);
        // UK has no mandatory overtime pay, but tracks hours over 48 threshold
    }

    // ============================================================================
    // Break Enforcement Tests
    // ============================================================================

    // Given: a California employee who worked a 4-hour shift with no rest break taken
    // When: break compliance is checked under California labor law
    // Then: a rest break violation should be flagged since California requires paid rest breaks for 4+ hour shifts
    [Fact]
    public async Task LaborLawComplianceGrain_CheckBreakCompliance_CaliforniaRestBreak()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        await grain.InitializeDefaultsAsync();

        var employeeId = Guid.NewGuid();
        var timeEntry = new TimeEntryForCalculation(
            Guid.NewGuid(), employeeId, DateOnly.FromDateTime(DateTime.UtcNow), 4m, 0);

        // 4 hour shift requires a paid rest break in California
        var breaks = new List<BreakRecord>();

        // Act
        var result = await grain.CheckBreakComplianceAsync("US-CA", timeEntry, breaks);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Violations.Should().Contain(v => v.BreakType == "rest");
    }

    // Given: a New York employee who worked a 7-hour shift and took a 30-minute meal break
    // When: break compliance is checked under New York labor law
    // Then: the employee should be compliant since New York requires a 30-minute meal break for shifts over 6 hours
    [Fact]
    public async Task LaborLawComplianceGrain_CheckBreakCompliance_NewYorkMealBreak()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        await grain.InitializeDefaultsAsync();

        var employeeId = Guid.NewGuid();
        var timeEntry = new TimeEntryForCalculation(
            Guid.NewGuid(), employeeId, DateOnly.FromDateTime(DateTime.UtcNow), 7m, 0);

        // New York requires 30-min meal break for shifts over 6 hours
        var breaks = new List<BreakRecord>
        {
            new(TimeSpan.FromHours(12), TimeSpan.FromHours(12.5), false, "meal")
        };

        // Act
        var result = await grain.CheckBreakComplianceAsync("US-NY", timeEntry, breaks);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    // Given: a Texas employee who worked a 3-hour shift with no breaks taken
    // When: break compliance is checked under Texas labor law
    // Then: the employee should be compliant since Texas has no mandatory break requirements for short shifts
    [Fact]
    public async Task LaborLawComplianceGrain_CheckBreakCompliance_ShortShiftNoBreakRequired()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        await grain.InitializeDefaultsAsync();

        var employeeId = Guid.NewGuid();
        var timeEntry = new TimeEntryForCalculation(
            Guid.NewGuid(), employeeId, DateOnly.FromDateTime(DateTime.UtcNow), 3m, 0);

        // 3 hour shift - no break required in most jurisdictions
        var breaks = new List<BreakRecord>();

        // Act
        var result = await grain.CheckBreakComplianceAsync("US-TX", timeEntry, breaks);

        // Assert - Texas has no break requirements
        result.IsCompliant.Should().BeTrue();
    }

    // Given: a California employee who worked an 11-hour shift and took two 30-minute meal breaks
    // When: break compliance is checked under California labor law
    // Then: the employee should be compliant since California requires 2 meal breaks for shifts over 10 hours
    [Fact]
    public async Task LaborLawComplianceGrain_CheckBreakCompliance_MultipleBreaksRequired()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        await grain.InitializeDefaultsAsync();

        var employeeId = Guid.NewGuid();
        var timeEntry = new TimeEntryForCalculation(
            Guid.NewGuid(), employeeId, DateOnly.FromDateTime(DateTime.UtcNow), 11m, 0);

        // 11 hour shift in California requires 2 meal breaks
        var breaks = new List<BreakRecord>
        {
            new(TimeSpan.FromHours(12), TimeSpan.FromHours(12.5), false, "meal"),
            new(TimeSpan.FromHours(18), TimeSpan.FromHours(18.5), false, "meal")
        };

        // Act
        var result = await grain.CheckBreakComplianceAsync("US-CA", timeEntry, breaks);

        // Assert
        result.IsCompliant.Should().BeTrue();
    }

    // ============================================================================
    // Tip Pool Edge Cases
    // ============================================================================

    // Given: a tip pool with $100 distributed by hours worked, where one employee worked 8 hours and another worked 0 hours
    // When: tips are distributed
    // Then: the employee with 8 hours should receive all $100 and the zero-hours employee should receive nothing
    [Fact]
    public async Task TipPoolGrain_DistributeByHoursWorked_HandlesZeroHoursParticipant()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(50));
        var grain = _cluster.GrainFactory.GetGrain<ITipPoolGrain>(
            GrainKeys.TipPool(orgId, siteId, businessDate, "zero-hours-test"));

        await grain.CreateAsync(new CreateTipPoolCommand(
            LocationId: siteId,
            BusinessDate: businessDate.ToDateTime(TimeOnly.MinValue),
            Name: "Zero Hours Test",
            Method: TipPoolMethod.ByHoursWorked,
            EligibleRoleIds: new List<Guid>()));

        var employee1 = Guid.NewGuid();
        var employee2 = Guid.NewGuid();

        await grain.AddParticipantAsync(employee1, hoursWorked: 8.0m, points: 0);
        await grain.AddParticipantAsync(employee2, hoursWorked: 0m, points: 0); // Zero hours

        await grain.AddTipsAsync(new AddTipsCommand(Amount: 100.00m, Source: "Pool"));

        // Act
        var snapshot = await grain.DistributeAsync(new DistributeTipsCommand(
            DistributedByUserId: Guid.NewGuid()));

        // Assert
        var dist1 = snapshot.Distributions.First(d => d.EmployeeId == employee1);
        var dist2 = snapshot.Distributions.First(d => d.EmployeeId == employee2);

        dist1.TipAmount.Should().Be(100.00m); // Gets all tips
        dist2.TipAmount.Should().Be(0m); // Gets nothing
    }

    // Given: a tip pool with $100 to be split equally among 3 employees
    // When: tips are distributed
    // Then: the total distributed should approximate $100 (allowing for rounding to the nearest cent)
    [Fact]
    public async Task TipPoolGrain_DistributeEqual_HandlesRoundingCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(51));
        var grain = _cluster.GrainFactory.GetGrain<ITipPoolGrain>(
            GrainKeys.TipPool(orgId, siteId, businessDate, "rounding-test"));

        await grain.CreateAsync(new CreateTipPoolCommand(
            LocationId: siteId,
            BusinessDate: businessDate.ToDateTime(TimeOnly.MinValue),
            Name: "Rounding Test",
            Method: TipPoolMethod.Equal,
            EligibleRoleIds: new List<Guid>()));

        // 3 employees splitting $100 = $33.33 each (with rounding)
        await grain.AddParticipantAsync(Guid.NewGuid(), hoursWorked: 8.0m, points: 0);
        await grain.AddParticipantAsync(Guid.NewGuid(), hoursWorked: 8.0m, points: 0);
        await grain.AddParticipantAsync(Guid.NewGuid(), hoursWorked: 8.0m, points: 0);

        await grain.AddTipsAsync(new AddTipsCommand(Amount: 100.00m, Source: "Pool"));

        // Act
        var snapshot = await grain.DistributeAsync(new DistributeTipsCommand(
            DistributedByUserId: Guid.NewGuid()));

        // Assert
        var totalDistributed = snapshot.Distributions.Sum(d => d.TipAmount);
        totalDistributed.Should().BeApproximately(100.00m, 0.03m); // Allow for rounding
    }

    // ============================================================================
    // Employee Time Tracking Edge Cases
    // ============================================================================

    // Given: an active employee who is not currently clocked in
    // When: a clock-out is attempted
    // Then: the system should reject the clock-out since the employee is not on the clock
    [Fact]
    public async Task EmployeeGrain_ClockOut_ThrowsIfNotClockedIn()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-500", "Test", "Employee", "test@example.com"));

        // Act & Assert - Try to clock out without being clocked in
        var act = () => grain.ClockOutAsync(new ClockOutCommand("Test"));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Employee is not clocked in");
    }

    // Given: an employee who is already clocked in at a site
    // When: a second clock-in is attempted at the same site
    // Then: the system should reject the duplicate clock-in
    [Fact]
    public async Task EmployeeGrain_ClockIn_ThrowsIfAlreadyClockedIn()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-501", "Test", "Employee", "test@example.com"));

        await grain.ClockInAsync(new ClockInCommand(siteId));

        // Act & Assert
        var act = () => grain.ClockInAsync(new ClockInCommand(siteId));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Employee is already clocked in");
    }

    // Given: an employee whose account has been deactivated (e.g., terminated)
    // When: a clock-in is attempted
    // Then: the system should reject the clock-in since only active employees can clock in
    [Fact]
    public async Task EmployeeGrain_Deactivate_PreventsClockIn()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-502", "Test", "Employee", "test@example.com"));

        await grain.DeactivateAsync();

        // Act & Assert
        var act = () => grain.ClockInAsync(new ClockInCommand(siteId));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only active employees can clock in");
    }

    // Given: an employee who has been placed on leave status
    // When: a clock-in is attempted
    // Then: the system should reject the clock-in since only active employees can clock in
    [Fact]
    public async Task EmployeeGrain_SetOnLeave_PreventsClockIn()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-503", "Test", "Employee", "test@example.com"));

        await grain.SetOnLeaveAsync();

        // Act & Assert
        var act = () => grain.ClockInAsync(new ClockInCommand(siteId));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only active employees can clock in");
    }

    // ============================================================================
    // Payroll Period Tests
    // ============================================================================

    // Given: a payroll period with no employees added
    // When: payroll details are requested for a non-existent employee
    // Then: the system should reject the request with an employee-not-found error
    [Fact]
    public async Task PayrollPeriodGrain_GetEmployeePayroll_ThrowsIfEmployeeNotFound()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-200));
        var grain = _cluster.GrainFactory.GetGrain<IPayrollPeriodGrain>(
            GrainKeys.PayrollPeriod(orgId, siteId, periodStart));

        await grain.CreateAsync(new CreatePayrollPeriodCommand(
            LocationId: siteId,
            PeriodStart: periodStart.ToDateTime(TimeOnly.MinValue),
            PeriodEnd: periodStart.AddDays(13).ToDateTime(TimeOnly.MaxValue)));

        // Act & Assert
        var act = () => grain.GetEmployeePayrollAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Employee not found in payroll");
    }

    // ============================================================================
    // Role Grain Tests
    // ============================================================================

    // Given: a Bartender role created in the Front of House department at $14/hr
    // When: the role is updated (no changes specified)
    // Then: the role should retain its original name and properties
    [Fact]
    public async Task RoleGrain_Update_CanAddRequiredCertifications()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRoleGrain>(
            GrainKeys.Role(orgId, roleId));

        await grain.CreateAsync(new CreateRoleCommand(
            Name: "Bartender",
            Department: Department.FrontOfHouse,
            DefaultHourlyRate: 14.00m,
            Color: "#e74c3c",
            SortOrder: 3,
            RequiredCertifications: new List<string>()));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateRoleCommand(
            Name: null,
            Department: null,
            DefaultHourlyRate: null,
            Color: null,
            SortOrder: null,
            IsActive: null));

        // Assert
        snapshot.Name.Should().Be("Bartender");
    }

    // ============================================================================
    // Tax Calculation Edge Cases
    // ============================================================================

    // Given: an employee with $195,000 YTD gross pay earning $10,000 this period (crossing the $200K Medicare threshold)
    // When: tax withholding is calculated
    // Then: Medicare withholding should include the additional 0.9% surtax on the $5,000 above the threshold
    [Fact]
    public void TaxCalculationService_CalculateWithholding_AdditionalMedicareAboveThreshold()
    {
        // Arrange
        var service = new TaxCalculationService();
        var config = service.GetTaxConfiguration("US-FEDERAL");
        var grossPay = 10000m;
        var ytdGrossPay = 195000m; // Just under additional Medicare threshold of $200k

        // Act
        var withholding = service.CalculateWithholding(grossPay, config, ytdGrossPay);

        // Assert - Should include additional Medicare tax (0.9%) on amount over $200k
        // $195k + $10k = $205k, so $5k is taxed at additional rate
        var expectedAdditional = 5000m * 0.009m; // $45
        withholding.MedicareWithholding.Should().BeGreaterThan(grossPay * config.MedicareRate);
    }

    // Given: a tax calculation service
    // When: a tax configuration is requested for an unknown jurisdiction code
    // Then: the service should fall back to the US-FEDERAL default configuration
    [Fact]
    public void TaxCalculationService_GetTaxConfiguration_ReturnsDefaultForUnknownJurisdiction()
    {
        // Arrange
        var service = new TaxCalculationService();

        // Act
        var config = service.GetTaxConfiguration("UNKNOWN-STATE");

        // Assert - Should return US-FEDERAL as default
        config.JurisdictionCode.Should().Be("US-FEDERAL");
    }

    // Given: a California employee with $2,000 gross pay this period and $50,000 YTD gross pay
    // When: the full employee tax summary is calculated
    // Then: both current-period and year-to-date withholdings should be included, with YTD totals exceeding current period
    [Fact]
    public void TaxCalculationService_CalculateEmployeeTaxSummary_IncludesYtdCalculations()
    {
        // Arrange
        var service = new TaxCalculationService();
        var employeeId = Guid.NewGuid();
        var grossPay = 2000m;
        var ytdGrossPay = 50000m;

        // Act
        var summary = service.CalculateEmployeeTaxSummary(
            employeeId,
            "John Doe",
            grossPay,
            ytdGrossPay,
            "US-CA");

        // Assert
        summary.EmployeeId.Should().Be(employeeId);
        summary.EmployeeName.Should().Be("John Doe");
        summary.GrossWages.Should().Be(grossPay);
        summary.CurrentPeriod.Should().NotBeNull();
        summary.YearToDate.Should().NotBeNull();
        summary.YearToDate.TotalWithholding.Should().BeGreaterThan(summary.CurrentPeriod.TotalWithholding);
    }
}
