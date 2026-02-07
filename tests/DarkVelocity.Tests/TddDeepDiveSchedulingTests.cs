using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TddDeepDiveSchedulingTests
{
    private readonly TestClusterFixture _fixture;

    public TddDeepDiveSchedulingTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================================================
    // Schedule Grain - Edge Case Tests
    // ============================================================================

    // Given: a published schedule
    // When: a shift is added where EndTime (8:00) is before StartTime (17:00),
    //       simulating a 5PM-to-8AM overnight scenario without overnight logic
    // Then: the scheduled hours are negative because the code does
    //       (EndTime - StartTime).TotalHours which yields a negative value.
    //       This is a bug: the system should either throw or handle overnight shifts.
    [Fact]
    public async Task AddShift_EndTimeBeforeStartTime_NegativeHours()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(100));
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: "overnight edge case"));

        await grain.PublishAsync(new PublishScheduleCommand(
            PublishedByUserId: Guid.NewGuid()));

        var shiftId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var shiftDate = weekStart.ToDateTime(TimeOnly.MinValue).AddDays(1);

        // Act - EndTime 8:00 is before StartTime 17:00 (overnight shift without proper handling)
        await grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: shiftId,
            EmployeeId: employeeId,
            RoleId: roleId,
            Date: shiftDate,
            StartTime: TimeSpan.FromHours(17),
            EndTime: TimeSpan.FromHours(8),
            BreakMinutes: 0,
            HourlyRate: 20.00m,
            Notes: "Overnight shift 5PM to 8AM"));

        var snapshot = await grain.GetSnapshotAsync();

        // Assert - (8 - 17).TotalHours = -9, so scheduledHours = -9.0
        // This is a bug: negative hours should not be allowed
        var shift = snapshot.Shifts.Should().ContainSingle().Subject;
        shift.ScheduledHours.Should().BeNegative("the code does (EndTime - StartTime).TotalHours without overnight handling");
        shift.ScheduledHours.Should().Be(-9.0m);
        shift.LaborCost.Should().BeNegative("negative hours times positive rate yields negative cost");
        shift.LaborCost.Should().Be(-180.00m);
        snapshot.TotalScheduledHours.Should().Be(-9.0m);
        snapshot.TotalLaborCost.Should().Be(-180.00m);
    }

    // Given: a draft schedule
    // When: two overlapping shifts for the same employee are added (9AM-5PM and 2PM-10PM)
    // Then: both shifts are added without error -- the system does not validate
    //       for overlapping shifts, which is a potential scheduling issue
    [Fact]
    public async Task AddShift_OverlappingShiftsForSameEmployee_AllowedSilently()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(101));
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: "overlap test"));

        var employeeId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var shiftDate = weekStart.ToDateTime(TimeOnly.MinValue);

        // Act - Add two overlapping shifts for the same employee
        await grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: Guid.NewGuid(),
            EmployeeId: employeeId,
            RoleId: roleId,
            Date: shiftDate,
            StartTime: TimeSpan.FromHours(9),
            EndTime: TimeSpan.FromHours(17),
            BreakMinutes: 30,
            HourlyRate: 15.00m,
            Notes: "Morning shift"));

        await grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: Guid.NewGuid(),
            EmployeeId: employeeId,
            RoleId: roleId,
            Date: shiftDate,
            StartTime: TimeSpan.FromHours(14),
            EndTime: TimeSpan.FromHours(22),
            BreakMinutes: 30,
            HourlyRate: 15.00m,
            Notes: "Evening shift - overlaps morning"));

        var snapshot = await grain.GetSnapshotAsync();

        // Assert - Both shifts are accepted without any overlap validation
        snapshot.Shifts.Should().HaveCount(2, "the system silently accepts overlapping shifts for the same employee");
        var employeeShifts = await grain.GetShiftsForEmployeeAsync(employeeId);
        employeeShifts.Should().HaveCount(2);

        // The overlapping window (2PM-5PM) means the employee is double-booked for 3 hours
        // Total scheduled hours count both shifts independently: 7.5 + 7.5 = 15.0
        snapshot.TotalScheduledHours.Should().Be(15.0m);
    }

    // Given: a schedule
    // When: a shift from 9:00 to 17:00 with 0 break minutes is added
    // Then: scheduledHours = 8.0 and laborCost = 8.0 * hourlyRate (no deduction for break)
    [Fact]
    public async Task AddShift_ZeroBreakMinutes_FullHoursCalculated()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(102));
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: "zero break test"));

        var shiftId = Guid.NewGuid();
        var hourlyRate = 25.00m;

        // Act
        await grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: shiftId,
            EmployeeId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            Date: weekStart.ToDateTime(TimeOnly.MinValue),
            StartTime: TimeSpan.FromHours(9),
            EndTime: TimeSpan.FromHours(17),
            BreakMinutes: 0,
            HourlyRate: hourlyRate,
            Notes: "Full day no break"));

        var snapshot = await grain.GetSnapshotAsync();

        // Assert - (17-9) = 8 hours, 0 break => 8.0 scheduled hours
        var shift = snapshot.Shifts.Should().ContainSingle().Subject;
        shift.ScheduledHours.Should().Be(8.0m);
        shift.LaborCost.Should().Be(8.0m * hourlyRate);
        shift.LaborCost.Should().Be(200.00m);
        snapshot.TotalScheduledHours.Should().Be(8.0m);
        snapshot.TotalLaborCost.Should().Be(200.00m);
    }

    // Given: a schedule
    // When: a shift of 2 hours (10:00-12:00) is added with 180 break minutes (3 hours)
    // Then: scheduledHours becomes negative (-1.0h) because the code does not validate
    //       that break minutes are less than the shift duration. This is a bug.
    [Fact]
    public async Task AddShift_BreakExceedsShiftDuration_NegativeHours()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(103));
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: "break exceeds shift test"));

        var shiftId = Guid.NewGuid();
        var hourlyRate = 18.00m;

        // Act - 2-hour shift with a 3-hour (180 minute) break
        await grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: shiftId,
            EmployeeId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            Date: weekStart.ToDateTime(TimeOnly.MinValue),
            StartTime: TimeSpan.FromHours(10),
            EndTime: TimeSpan.FromHours(12),
            BreakMinutes: 180,
            HourlyRate: hourlyRate,
            Notes: "Short shift with excessive break"));

        var snapshot = await grain.GetSnapshotAsync();

        // Assert - (12-10) = 2 hours, minus 180/60 = 3 hours break => -1.0 scheduled hours
        // This is a bug: break minutes should not exceed shift duration
        var shift = snapshot.Shifts.Should().ContainSingle().Subject;
        shift.ScheduledHours.Should().Be(-1.0m);
        shift.LaborCost.Should().Be(-1.0m * hourlyRate);
        shift.LaborCost.Should().Be(-18.00m);
        snapshot.TotalScheduledHours.Should().Be(-1.0m);
        snapshot.TotalLaborCost.Should().Be(-18.00m);
    }

    // ============================================================================
    // Tip Pool Grain - Edge Case Tests
    // ============================================================================

    // Given: a tip pool with $100.00 in tips and 3 participants using Equal distribution
    // When: tips are distributed
    // Then: each participant gets $33.333... (not a clean $33.33) because the code at
    //       LaborGrains.cs:633 does simple division (TotalTips / Participants.Count)
    //       without rounding, so the sum of distributions does not equal the total tips.
    //       This is a rounding loss bug in financial calculations.
    [Fact]
    public async Task TipPool_EqualDistribution_ThreeParticipants_RoundingLoss()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(200));
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ITipPoolGrain>(
            GrainKeys.TipPool(orgId, siteId, businessDate, "rounding-test"));

        await grain.CreateAsync(new CreateTipPoolCommand(
            LocationId: siteId,
            BusinessDate: businessDate.ToDateTime(TimeOnly.MinValue),
            Name: "Rounding Test Pool",
            Method: TipPoolMethod.Equal,
            EligibleRoleIds: new List<Guid> { Guid.NewGuid() }));

        var employee1 = Guid.NewGuid();
        var employee2 = Guid.NewGuid();
        var employee3 = Guid.NewGuid();

        await grain.AddParticipantAsync(employee1, hoursWorked: 8.0m, points: 0);
        await grain.AddParticipantAsync(employee2, hoursWorked: 8.0m, points: 0);
        await grain.AddParticipantAsync(employee3, hoursWorked: 8.0m, points: 0);

        await grain.AddTipsAsync(new AddTipsCommand(Amount: 100.00m, Source: "Tables"));

        // Act
        var snapshot = await grain.DistributeAsync(new DistributeTipsCommand(
            DistributedByUserId: Guid.NewGuid()));

        // Assert
        snapshot.Distributions.Should().HaveCount(3);
        snapshot.TotalTips.Should().Be(100.00m);

        // Each participant gets 100 / 3 = 33.333333333333333333333333333...
        // The per-person amount is NOT a clean cent value
        var perPerson = snapshot.Distributions[0].TipAmount;
        perPerson.Should().NotBe(33.33m, "simple division does not round to cents");
        perPerson.Should().NotBe(33.34m);

        // All three get the same amount (equal distribution)
        snapshot.Distributions.Should().OnlyContain(d => d.TipAmount == perPerson);

        // The sum of all distributions does NOT equal the original total
        // because 3 * (100/3) != 100 in decimal arithmetic
        var totalDistributed = snapshot.Distributions.Sum(d => d.TipAmount);
        totalDistributed.Should().NotBe(100.00m, "rounding loss: 3 * (100/3) != 100 in decimal");
        totalDistributed.Should().BeLessThan(100.00m);
    }

    // Given: a tip pool using ByHoursWorked method with $200 in tips
    // When: one participant has 0 hours worked and another has 8 hours
    // Then: the participant with 0 hours gets $0 share and the participant
    //       with 8 hours gets the full $200. The totalHours check at line 651
    //       only guards against ALL participants having 0 hours.
    [Fact]
    public async Task TipPool_ByHours_ZeroHoursWorked_SilentFailure()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(201));
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ITipPoolGrain>(
            GrainKeys.TipPool(orgId, siteId, businessDate, "zero-hours-test"));

        await grain.CreateAsync(new CreateTipPoolCommand(
            LocationId: siteId,
            BusinessDate: businessDate.ToDateTime(TimeOnly.MinValue),
            Name: "Zero Hours Test Pool",
            Method: TipPoolMethod.ByHoursWorked,
            EligibleRoleIds: new List<Guid> { Guid.NewGuid() }));

        var employeeWithZeroHours = Guid.NewGuid();
        var employeeWithHours = Guid.NewGuid();

        await grain.AddParticipantAsync(employeeWithZeroHours, hoursWorked: 0m, points: 0);
        await grain.AddParticipantAsync(employeeWithHours, hoursWorked: 8.0m, points: 0);

        await grain.AddTipsAsync(new AddTipsCommand(Amount: 200.00m, Source: "Tables"));

        // Act
        var snapshot = await grain.DistributeAsync(new DistributeTipsCommand(
            DistributedByUserId: Guid.NewGuid()));

        // Assert
        snapshot.Distributions.Should().HaveCount(2);

        // totalHours = 0 + 8 = 8, so the guard (totalHours <= 0) does NOT trigger
        // The 0-hours participant gets share = 0/8 = 0, tipAmount = 200 * 0 = 0
        var zeroHoursDist = snapshot.Distributions.First(d => d.EmployeeId == employeeWithZeroHours);
        zeroHoursDist.TipAmount.Should().Be(0m, "0 hours / 8 total hours = 0 share");
        zeroHoursDist.HoursWorked.Should().Be(0m);

        // The 8-hours participant gets the full pool
        var fullHoursDist = snapshot.Distributions.First(d => d.EmployeeId == employeeWithHours);
        fullHoursDist.TipAmount.Should().Be(200.00m, "8 hours / 8 total hours = 100% share");

        // Total distributed should equal total tips when hours divide cleanly
        var totalDistributed = snapshot.Distributions.Sum(d => d.TipAmount);
        totalDistributed.Should().Be(200.00m);
    }

    // ============================================================================
    // Schedule Grain - Lock / Modification Tests
    // ============================================================================

    // Given: a locked schedule
    // When: trying to add a shift
    // Then: InvalidOperationException is thrown with "Cannot modify a locked schedule"
    [Fact]
    public async Task Schedule_LockPreventsModification()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(104));
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: "lock test"));

        await grain.LockAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(ScheduleStatus.Locked);

        // Act & Assert - AddShift should throw
        var addAct = () => grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: Guid.NewGuid(),
            EmployeeId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            Date: weekStart.ToDateTime(TimeOnly.MinValue),
            StartTime: TimeSpan.FromHours(9),
            EndTime: TimeSpan.FromHours(17),
            BreakMinutes: 30,
            HourlyRate: 15.00m,
            Notes: "Should fail"));

        await addAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot modify a locked schedule");
    }

    // Given: a schedule with a shift from 9:00 to 17:00 with 30 min break at $15/hr
    // When: only EndTime is updated to 20:00 (8PM)
    // Then: scheduledHours should be recalculated to (20-9) = 11 hours - 0.5 break = 10.5 hours
    //       and laborCost should be 10.5 * $15 = $157.50
    [Fact]
    public async Task UpdateShift_OnlyEndTime_RecalculatesHours()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(105));
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: "update end time test"));

        var shiftId = Guid.NewGuid();
        var hourlyRate = 15.00m;

        await grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: shiftId,
            EmployeeId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            Date: weekStart.ToDateTime(TimeOnly.MinValue),
            StartTime: TimeSpan.FromHours(9),
            EndTime: TimeSpan.FromHours(17),
            BreakMinutes: 30,
            HourlyRate: hourlyRate,
            Notes: "Original shift"));

        // Verify initial state: (17-9) = 8h - 0.5 break = 7.5h, cost = 7.5 * 15 = 112.50
        var initialSnapshot = await grain.GetSnapshotAsync();
        initialSnapshot.Shifts[0].ScheduledHours.Should().Be(7.5m);
        initialSnapshot.Shifts[0].LaborCost.Should().Be(112.50m);

        // Act - Update only EndTime to 20:00, leaving StartTime, BreakMinutes, etc. unchanged
        await grain.UpdateShiftAsync(new UpdateShiftCommand(
            ShiftId: shiftId,
            StartTime: null,
            EndTime: TimeSpan.FromHours(20),
            BreakMinutes: null,
            EmployeeId: null,
            RoleId: null,
            Notes: null));

        var snapshot = await grain.GetSnapshotAsync();

        // Assert - (20-9) = 11h - 0.5 break = 10.5h, cost = 10.5 * 15 = 157.50
        var shift = snapshot.Shifts.Should().ContainSingle().Subject;
        shift.StartTime.Should().Be(TimeSpan.FromHours(9), "StartTime was not updated");
        shift.EndTime.Should().Be(TimeSpan.FromHours(20), "EndTime was updated to 20:00");
        shift.BreakMinutes.Should().Be(30, "BreakMinutes was not updated");
        shift.ScheduledHours.Should().Be(10.5m, "(20-9) - 30/60 = 10.5 hours");
        shift.LaborCost.Should().Be(157.50m, "10.5 * 15.00 = 157.50");
        shift.HourlyRate.Should().Be(hourlyRate, "HourlyRate was not updated");
        snapshot.TotalScheduledHours.Should().Be(10.5m);
        snapshot.TotalLaborCost.Should().Be(157.50m);
    }
}
