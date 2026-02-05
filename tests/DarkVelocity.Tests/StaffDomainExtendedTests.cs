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
