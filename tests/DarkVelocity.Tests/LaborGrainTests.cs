using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;
using Orleans.TestingHost;
using Xunit;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class LaborGrainTests
{
    private readonly TestCluster _cluster;

    public LaborGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    // ============================================================================
    // RoleGrain Tests
    // ============================================================================

    [Fact]
    public async Task RoleGrain_Create_CreatesRoleSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRoleGrain>(
            GrainKeys.Role(orgId, roleId));

        var command = new CreateRoleCommand(
            Name: "Server",
            Department: Department.FrontOfHouse,
            DefaultHourlyRate: 15.00m,
            Color: "#3498db",
            SortOrder: 1,
            RequiredCertifications: new List<string> { "Food Handler" });

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.RoleId.Should().Be(roleId);
        snapshot.Name.Should().Be("Server");
        snapshot.Department.Should().Be(Department.FrontOfHouse);
        snapshot.DefaultHourlyRate.Should().Be(15.00m);
        snapshot.Color.Should().Be("#3498db");
        snapshot.SortOrder.Should().Be(1);
        snapshot.RequiredCertifications.Should().ContainSingle().Which.Should().Be("Food Handler");
        snapshot.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RoleGrain_Create_ThrowsIfAlreadyExists()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRoleGrain>(
            GrainKeys.Role(orgId, roleId));

        var command = new CreateRoleCommand(
            Name: "Bartender",
            Department: Department.FrontOfHouse,
            DefaultHourlyRate: 12.00m,
            Color: "#e74c3c",
            SortOrder: 2,
            RequiredCertifications: new List<string> { "Alcohol Service" });

        await grain.CreateAsync(command);

        // Act & Assert
        var act = () => grain.CreateAsync(command);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Role already exists");
    }

    [Fact]
    public async Task RoleGrain_Update_UpdatesRoleProperties()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRoleGrain>(
            GrainKeys.Role(orgId, roleId));

        await grain.CreateAsync(new CreateRoleCommand(
            Name: "Host",
            Department: Department.FrontOfHouse,
            DefaultHourlyRate: 11.00m,
            Color: "#9b59b6",
            SortOrder: 5,
            RequiredCertifications: new List<string>()));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateRoleCommand(
            Name: "Host/Hostess",
            Department: null,
            DefaultHourlyRate: 12.50m,
            Color: "#8e44ad",
            SortOrder: 3,
            IsActive: null));

        // Assert
        snapshot.Name.Should().Be("Host/Hostess");
        snapshot.DefaultHourlyRate.Should().Be(12.50m);
        snapshot.Color.Should().Be("#8e44ad");
        snapshot.SortOrder.Should().Be(3);
        snapshot.Department.Should().Be(Department.FrontOfHouse); // Unchanged
        snapshot.IsActive.Should().BeTrue(); // Unchanged
    }

    [Fact]
    public async Task RoleGrain_Update_CanDeactivateRole()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRoleGrain>(
            GrainKeys.Role(orgId, roleId));

        await grain.CreateAsync(new CreateRoleCommand(
            Name: "Busser",
            Department: Department.FrontOfHouse,
            DefaultHourlyRate: 10.00m,
            Color: "#27ae60",
            SortOrder: 10,
            RequiredCertifications: new List<string>()));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateRoleCommand(
            Name: null,
            Department: null,
            DefaultHourlyRate: null,
            Color: null,
            SortOrder: null,
            IsActive: false));

        // Assert
        snapshot.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task RoleGrain_GetSnapshot_ReturnsRoleState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRoleGrain>(
            GrainKeys.Role(orgId, roleId));

        await grain.CreateAsync(new CreateRoleCommand(
            Name: "Line Cook",
            Department: Department.BackOfHouse,
            DefaultHourlyRate: 16.00m,
            Color: "#f39c12",
            SortOrder: 1,
            RequiredCertifications: new List<string> { "Food Handler", "Safety" }));

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.RoleId.Should().Be(roleId);
        snapshot.Name.Should().Be("Line Cook");
        snapshot.Department.Should().Be(Department.BackOfHouse);
        snapshot.RequiredCertifications.Should().HaveCount(2);
    }

    [Fact]
    public async Task RoleGrain_GetSnapshot_ThrowsIfNotInitialized()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IRoleGrain>(
            GrainKeys.Role(orgId, roleId));

        // Act & Assert
        var act = () => grain.GetSnapshotAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Role grain not initialized");
    }

    // ============================================================================
    // ScheduleGrain Tests
    // ============================================================================

    [Fact]
    public async Task ScheduleGrain_Create_CreatesScheduleForWeek()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek));
        var grain = _cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        var command = new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: "Regular week schedule");

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.ScheduleId.Should().NotBeEmpty();
        snapshot.LocationId.Should().Be(siteId);
        snapshot.WeekStartDate.Date.Should().Be(weekStart.ToDateTime(TimeOnly.MinValue).Date);
        snapshot.Status.Should().Be(ScheduleStatus.Draft);
        snapshot.Shifts.Should().BeEmpty();
        snapshot.TotalScheduledHours.Should().Be(0);
        snapshot.TotalLaborCost.Should().Be(0);
        snapshot.Notes.Should().Be("Regular week schedule");
    }

    [Fact]
    public async Task ScheduleGrain_Publish_PublishesSchedule()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var grain = _cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));
        var publisherId = Guid.NewGuid();

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: null));

        // Act
        var snapshot = await grain.PublishAsync(new PublishScheduleCommand(
            PublishedByUserId: publisherId));

        // Assert
        snapshot.Status.Should().Be(ScheduleStatus.Published);
        snapshot.PublishedByUserId.Should().Be(publisherId);
        snapshot.PublishedAt.Should().NotBeNull();
        snapshot.PublishedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ScheduleGrain_Lock_LocksSchedule()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var grain = _cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: null));

        // Act
        await grain.LockAsync();
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Status.Should().Be(ScheduleStatus.Locked);
    }

    [Fact]
    public async Task ScheduleGrain_AddShift_AddsShiftToSchedule()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));
        var grain = _cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: null));

        var shiftId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var shiftDate = weekStart.ToDateTime(TimeOnly.MinValue).AddDays(1);

        // Act
        await grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: shiftId,
            EmployeeId: employeeId,
            RoleId: roleId,
            Date: shiftDate,
            StartTime: TimeSpan.FromHours(9),
            EndTime: TimeSpan.FromHours(17),
            BreakMinutes: 30,
            HourlyRate: 15.00m,
            Notes: "Opening shift"));

        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Shifts.Should().HaveCount(1);
        var shift = snapshot.Shifts[0];
        shift.ShiftId.Should().Be(shiftId);
        shift.EmployeeId.Should().Be(employeeId);
        shift.RoleId.Should().Be(roleId);
        shift.StartTime.Should().Be(TimeSpan.FromHours(9));
        shift.EndTime.Should().Be(TimeSpan.FromHours(17));
        shift.BreakMinutes.Should().Be(30);
        shift.Notes.Should().Be("Opening shift");
    }

    [Fact]
    public async Task ScheduleGrain_AddShift_CalculatesScheduledHoursAndLaborCost()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(21));
        var grain = _cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: null));

        // 8 hour shift with 30 min break = 7.5 working hours
        // At $20/hour = $150 labor cost
        await grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: Guid.NewGuid(),
            EmployeeId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            Date: weekStart.ToDateTime(TimeOnly.MinValue),
            StartTime: TimeSpan.FromHours(8),
            EndTime: TimeSpan.FromHours(16),
            BreakMinutes: 30,
            HourlyRate: 20.00m,
            Notes: null));

        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Shifts[0].ScheduledHours.Should().Be(7.5m);
        snapshot.Shifts[0].LaborCost.Should().Be(150.00m);
        snapshot.TotalScheduledHours.Should().Be(7.5m);
        snapshot.TotalLaborCost.Should().Be(150.00m);
    }

    [Fact]
    public async Task ScheduleGrain_UpdateShift_UpdatesShiftDetails()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(28));
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

        // Act - Change to evening shift
        await grain.UpdateShiftAsync(new UpdateShiftCommand(
            ShiftId: shiftId,
            StartTime: TimeSpan.FromHours(16),
            EndTime: TimeSpan.FromHours(23),
            BreakMinutes: 15,
            EmployeeId: null,
            RoleId: null,
            Notes: "Changed to evening"));

        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        var shift = snapshot.Shifts[0];
        shift.StartTime.Should().Be(TimeSpan.FromHours(16));
        shift.EndTime.Should().Be(TimeSpan.FromHours(23));
        shift.BreakMinutes.Should().Be(15);
        shift.Notes.Should().Be("Changed to evening");
    }

    [Fact]
    public async Task ScheduleGrain_RemoveShift_RemovesShiftFromSchedule()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(35));
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

        // Act
        await grain.RemoveShiftAsync(shiftId);
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Shifts.Should().BeEmpty();
        snapshot.TotalScheduledHours.Should().Be(0);
        snapshot.TotalLaborCost.Should().Be(0);
    }

    [Fact]
    public async Task ScheduleGrain_LockedSchedule_ThrowsOnAddShift()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14));
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
    public async Task ScheduleGrain_LockedSchedule_ThrowsOnUpdateShift()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-21));
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

        await grain.LockAsync();

        // Act & Assert
        var act = () => grain.UpdateShiftAsync(new UpdateShiftCommand(
            ShiftId: shiftId,
            StartTime: TimeSpan.FromHours(10),
            EndTime: null,
            BreakMinutes: null,
            EmployeeId: null,
            RoleId: null,
            Notes: null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot modify a locked schedule");
    }

    [Fact]
    public async Task ScheduleGrain_LockedSchedule_ThrowsOnRemoveShift()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-28));
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

        await grain.LockAsync();

        // Act & Assert
        var act = () => grain.RemoveShiftAsync(shiftId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot modify a locked schedule");
    }

    [Fact]
    public async Task ScheduleGrain_GetShiftsForEmployee_FiltersCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(42));
        var grain = _cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: null));

        var employee1 = Guid.NewGuid();
        var employee2 = Guid.NewGuid();
        var baseDate = weekStart.ToDateTime(TimeOnly.MinValue);

        // Add 3 shifts for employee1, 2 for employee2
        await grain.AddShiftAsync(CreateShiftCommand(employee1, baseDate, 9, 17));
        await grain.AddShiftAsync(CreateShiftCommand(employee1, baseDate.AddDays(1), 9, 17));
        await grain.AddShiftAsync(CreateShiftCommand(employee1, baseDate.AddDays(2), 9, 17));
        await grain.AddShiftAsync(CreateShiftCommand(employee2, baseDate, 17, 23));
        await grain.AddShiftAsync(CreateShiftCommand(employee2, baseDate.AddDays(1), 17, 23));

        // Act
        var employee1Shifts = await grain.GetShiftsForEmployeeAsync(employee1);
        var employee2Shifts = await grain.GetShiftsForEmployeeAsync(employee2);

        // Assert
        employee1Shifts.Should().HaveCount(3);
        employee2Shifts.Should().HaveCount(2);
        employee1Shifts.Should().OnlyContain(s => s.EmployeeId == employee1);
        employee2Shifts.Should().OnlyContain(s => s.EmployeeId == employee2);
    }

    [Fact]
    public async Task ScheduleGrain_GetShiftsForDate_FiltersCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(49));
        var grain = _cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: null));

        var employee1 = Guid.NewGuid();
        var baseDate = weekStart.ToDateTime(TimeOnly.MinValue);
        var targetDate = baseDate.AddDays(2);

        // Add shifts across different days
        await grain.AddShiftAsync(CreateShiftCommand(employee1, baseDate, 9, 17));
        await grain.AddShiftAsync(CreateShiftCommand(employee1, baseDate.AddDays(1), 9, 17));
        await grain.AddShiftAsync(CreateShiftCommand(Guid.NewGuid(), targetDate, 9, 14));
        await grain.AddShiftAsync(CreateShiftCommand(Guid.NewGuid(), targetDate, 14, 22));
        await grain.AddShiftAsync(CreateShiftCommand(employee1, baseDate.AddDays(3), 9, 17));

        // Act
        var shiftsOnTargetDate = await grain.GetShiftsForDateAsync(targetDate);

        // Assert
        shiftsOnTargetDate.Should().HaveCount(2);
        shiftsOnTargetDate.Should().OnlyContain(s => s.Date.Date == targetDate.Date);
    }

    [Fact]
    public async Task ScheduleGrain_GetTotalLaborCost_ReturnsCorrectTotal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var weekStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(56));
        var grain = _cluster.GrainFactory.GetGrain<IScheduleGrain>(
            GrainKeys.Schedule(orgId, siteId, weekStart));

        await grain.CreateAsync(new CreateScheduleCommand(
            LocationId: siteId,
            WeekStartDate: weekStart.ToDateTime(TimeOnly.MinValue),
            Notes: null));

        var baseDate = weekStart.ToDateTime(TimeOnly.MinValue);

        // 8 hour shift, 30 min break, $20/hr = (8 - 0.5) * 20 = $150
        await grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: Guid.NewGuid(),
            EmployeeId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            Date: baseDate,
            StartTime: TimeSpan.FromHours(8),
            EndTime: TimeSpan.FromHours(16),
            BreakMinutes: 30,
            HourlyRate: 20.00m,
            Notes: null));

        // 6 hour shift, no break, $15/hr = 6 * 15 = $90
        await grain.AddShiftAsync(new AddShiftCommand(
            ShiftId: Guid.NewGuid(),
            EmployeeId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            Date: baseDate,
            StartTime: TimeSpan.FromHours(16),
            EndTime: TimeSpan.FromHours(22),
            BreakMinutes: 0,
            HourlyRate: 15.00m,
            Notes: null));

        // Act
        var totalCost = await grain.GetTotalLaborCostAsync();

        // Assert
        totalCost.Should().Be(240.00m); // 150 + 90
    }

    // ============================================================================
    // TimeEntryGrain Tests
    // ============================================================================

    [Fact]
    public async Task TimeEntryGrain_ClockIn_CreatesTimeEntry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var timeEntryId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeEntryGrain>(
            GrainKeys.TimeEntry(orgId, timeEntryId));

        var employeeId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        // Act
        var snapshot = await grain.ClockInAsync(new TimeEntryClockInCommand(
            EmployeeId: employeeId,
            LocationId: locationId,
            RoleId: roleId,
            ShiftId: null,
            Method: ClockMethod.Pin,
            Notes: "Clocking in for shift"));

        // Assert
        snapshot.TimeEntryId.Should().Be(timeEntryId);
        snapshot.EmployeeId.Should().Be(employeeId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.RoleId.Should().Be(roleId);
        snapshot.ClockInMethod.Should().Be(ClockMethod.Pin);
        snapshot.ClockInAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        snapshot.ClockOutAt.Should().BeNull();
        snapshot.Status.Should().Be(TimeEntryStatus.Active);
        snapshot.Notes.Should().Be("Clocking in for shift");
    }

    [Fact]
    public async Task TimeEntryGrain_ClockIn_ThrowsIfAlreadyExists()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var timeEntryId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeEntryGrain>(
            GrainKeys.TimeEntry(orgId, timeEntryId));

        await grain.ClockInAsync(new TimeEntryClockInCommand(
            EmployeeId: Guid.NewGuid(),
            LocationId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            ShiftId: null,
            Method: ClockMethod.Pin,
            Notes: null));

        // Act & Assert
        var act = () => grain.ClockInAsync(new TimeEntryClockInCommand(
            EmployeeId: Guid.NewGuid(),
            LocationId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            ShiftId: null,
            Method: ClockMethod.Pin,
            Notes: null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Time entry already exists");
    }

    [Fact]
    public async Task TimeEntryGrain_ClockOut_CompletesEntry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var timeEntryId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeEntryGrain>(
            GrainKeys.TimeEntry(orgId, timeEntryId));

        await grain.ClockInAsync(new TimeEntryClockInCommand(
            EmployeeId: Guid.NewGuid(),
            LocationId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            ShiftId: null,
            Method: ClockMethod.Pin,
            Notes: null));

        // Act
        var snapshot = await grain.ClockOutAsync(new TimeEntryClockOutCommand(
            Method: ClockMethod.Pin,
            Notes: "End of shift"));

        // Assert
        snapshot.ClockOutAt.Should().NotBeNull();
        snapshot.ClockOutAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        snapshot.ClockOutMethod.Should().Be(ClockMethod.Pin);
        snapshot.Status.Should().Be(TimeEntryStatus.Completed);
    }

    [Fact]
    public async Task TimeEntryGrain_ClockOut_ThrowsIfAlreadyClockedOut()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var timeEntryId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeEntryGrain>(
            GrainKeys.TimeEntry(orgId, timeEntryId));

        await grain.ClockInAsync(new TimeEntryClockInCommand(
            EmployeeId: Guid.NewGuid(),
            LocationId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            ShiftId: null,
            Method: ClockMethod.Pin,
            Notes: null));

        await grain.ClockOutAsync(new TimeEntryClockOutCommand(
            Method: ClockMethod.Pin,
            Notes: null));

        // Act & Assert
        var act = () => grain.ClockOutAsync(new TimeEntryClockOutCommand(
            Method: ClockMethod.Pin,
            Notes: null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Already clocked out");
    }

    [Fact]
    public async Task TimeEntryGrain_AddBreak_TracksUnpaidBreak()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var timeEntryId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeEntryGrain>(
            GrainKeys.TimeEntry(orgId, timeEntryId));

        await grain.ClockInAsync(new TimeEntryClockInCommand(
            EmployeeId: Guid.NewGuid(),
            LocationId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            ShiftId: null,
            Method: ClockMethod.Pin,
            Notes: null));

        // Act - Add 30 minute unpaid break
        await grain.AddBreakAsync(new AddBreakCommand(
            BreakStart: TimeSpan.FromHours(12),
            BreakEnd: TimeSpan.FromHours(12.5),
            IsPaid: false));

        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.BreakMinutes.Should().Be(30);
    }

    [Fact]
    public async Task TimeEntryGrain_AddBreak_TracksPaidBreak()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var timeEntryId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeEntryGrain>(
            GrainKeys.TimeEntry(orgId, timeEntryId));

        await grain.ClockInAsync(new TimeEntryClockInCommand(
            EmployeeId: Guid.NewGuid(),
            LocationId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            ShiftId: null,
            Method: ClockMethod.Pin,
            Notes: null));

        // Act - Add 15 minute paid break (should not add to unpaid break minutes)
        await grain.AddBreakAsync(new AddBreakCommand(
            BreakStart: TimeSpan.FromHours(10),
            BreakEnd: TimeSpan.FromHours(10.25),
            IsPaid: true));

        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.BreakMinutes.Should().Be(0); // Paid breaks don't reduce worked hours
    }

    [Fact]
    public async Task TimeEntryGrain_Adjust_AdjustsTimesWithAuditTrail()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var timeEntryId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeEntryGrain>(
            GrainKeys.TimeEntry(orgId, timeEntryId));
        var adjustingUserId = Guid.NewGuid();

        await grain.ClockInAsync(new TimeEntryClockInCommand(
            EmployeeId: Guid.NewGuid(),
            LocationId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            ShiftId: null,
            Method: ClockMethod.Pin,
            Notes: null));

        await grain.ClockOutAsync(new TimeEntryClockOutCommand(
            Method: ClockMethod.Pin,
            Notes: null));

        var adjustedClockIn = DateTime.UtcNow.AddHours(-8);
        var adjustedClockOut = DateTime.UtcNow;

        // Act
        var snapshot = await grain.AdjustAsync(new AdjustTimeEntryCommand(
            AdjustedByUserId: adjustingUserId,
            ClockInAt: adjustedClockIn,
            ClockOutAt: adjustedClockOut,
            BreakMinutes: 30,
            Reason: "Employee forgot to clock in on time"));

        // Assert
        snapshot.ClockInAt.Should().BeCloseTo(adjustedClockIn, TimeSpan.FromSeconds(1));
        snapshot.ClockOutAt.Should().BeCloseTo(adjustedClockOut, TimeSpan.FromSeconds(1));
        snapshot.BreakMinutes.Should().Be(30);
        snapshot.AdjustedByUserId.Should().Be(adjustingUserId);
        snapshot.AdjustmentReason.Should().Be("Employee forgot to clock in on time");
        snapshot.Status.Should().Be(TimeEntryStatus.Adjusted);
    }

    [Fact]
    public async Task TimeEntryGrain_Approve_ApprovesEntry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var timeEntryId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeEntryGrain>(
            GrainKeys.TimeEntry(orgId, timeEntryId));
        var approvingUserId = Guid.NewGuid();

        await grain.ClockInAsync(new TimeEntryClockInCommand(
            EmployeeId: Guid.NewGuid(),
            LocationId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            ShiftId: null,
            Method: ClockMethod.Pin,
            Notes: null));

        await grain.ClockOutAsync(new TimeEntryClockOutCommand(
            Method: ClockMethod.Pin,
            Notes: null));

        // Act
        var snapshot = await grain.ApproveAsync(new ApproveTimeEntryCommand(
            ApprovedByUserId: approvingUserId));

        // Assert
        snapshot.ApprovedByUserId.Should().Be(approvingUserId);
        snapshot.ApprovedAt.Should().NotBeNull();
        snapshot.ApprovedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TimeEntryGrain_IsActive_ReturnsCorrectStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var timeEntryId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeEntryGrain>(
            GrainKeys.TimeEntry(orgId, timeEntryId));

        await grain.ClockInAsync(new TimeEntryClockInCommand(
            EmployeeId: Guid.NewGuid(),
            LocationId: Guid.NewGuid(),
            RoleId: Guid.NewGuid(),
            ShiftId: null,
            Method: ClockMethod.Pin,
            Notes: null));

        // Act & Assert - Before clock out
        var isActiveBefore = await grain.IsActiveAsync();
        isActiveBefore.Should().BeTrue();

        await grain.ClockOutAsync(new TimeEntryClockOutCommand(
            Method: ClockMethod.Pin,
            Notes: null));

        // Act & Assert - After clock out
        var isActiveAfter = await grain.IsActiveAsync();
        isActiveAfter.Should().BeFalse();
    }

    // ============================================================================
    // TipPoolGrain Tests
    // ============================================================================

    [Fact]
    public async Task TipPoolGrain_Create_CreatesPoolForDate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var grain = _cluster.GrainFactory.GetGrain<ITipPoolGrain>(
            GrainKeys.TipPool(orgId, siteId, businessDate, "dinner"));

        var roleId1 = Guid.NewGuid();
        var roleId2 = Guid.NewGuid();

        // Act
        var snapshot = await grain.CreateAsync(new CreateTipPoolCommand(
            LocationId: siteId,
            BusinessDate: businessDate.ToDateTime(TimeOnly.MinValue),
            Name: "Dinner Tips",
            Method: TipPoolMethod.Equal,
            EligibleRoleIds: new List<Guid> { roleId1, roleId2 }));

        // Assert
        snapshot.TipPoolId.Should().NotBeEmpty();
        snapshot.LocationId.Should().Be(siteId);
        snapshot.Name.Should().Be("Dinner Tips");
        snapshot.Method.Should().Be(TipPoolMethod.Equal);
        snapshot.TotalTips.Should().Be(0);
        snapshot.IsDistributed.Should().BeFalse();
        snapshot.Distributions.Should().BeEmpty();
    }

    [Fact]
    public async Task TipPoolGrain_AddTips_AddsTipsToPool()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var grain = _cluster.GrainFactory.GetGrain<ITipPoolGrain>(
            GrainKeys.TipPool(orgId, siteId, businessDate, "lunch"));

        await grain.CreateAsync(new CreateTipPoolCommand(
            LocationId: siteId,
            BusinessDate: businessDate.ToDateTime(TimeOnly.MinValue),
            Name: "Lunch Tips",
            Method: TipPoolMethod.Equal,
            EligibleRoleIds: new List<Guid>()));

        // Act
        await grain.AddTipsAsync(new AddTipsCommand(Amount: 100.00m, Source: "Table 5"));
        await grain.AddTipsAsync(new AddTipsCommand(Amount: 50.00m, Source: "Table 10"));
        await grain.AddTipsAsync(new AddTipsCommand(Amount: 75.00m, Source: "Bar"));

        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.TotalTips.Should().Be(225.00m);
    }

    [Fact]
    public async Task TipPoolGrain_AddParticipant_AddsParticipantWithHoursAndPoints()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var grain = _cluster.GrainFactory.GetGrain<ITipPoolGrain>(
            GrainKeys.TipPool(orgId, siteId, businessDate, "evening"));

        await grain.CreateAsync(new CreateTipPoolCommand(
            LocationId: siteId,
            BusinessDate: businessDate.ToDateTime(TimeOnly.MinValue),
            Name: "Evening Tips",
            Method: TipPoolMethod.ByHoursWorked,
            EligibleRoleIds: new List<Guid>()));

        var employee1 = Guid.NewGuid();
        var employee2 = Guid.NewGuid();

        // Act
        await grain.AddParticipantAsync(employee1, hoursWorked: 8.0m, points: 10.0m);
        await grain.AddParticipantAsync(employee2, hoursWorked: 6.0m, points: 8.0m);

        await grain.AddTipsAsync(new AddTipsCommand(Amount: 280.00m, Source: "All tables"));

        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.TotalTips.Should().Be(280.00m);
    }

    [Fact]
    public async Task TipPoolGrain_AddParticipant_AccumulatesHoursForSameEmployee()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3));
        var grain = _cluster.GrainFactory.GetGrain<ITipPoolGrain>(
            GrainKeys.TipPool(orgId, siteId, businessDate, "all-day"));

        await grain.CreateAsync(new CreateTipPoolCommand(
            LocationId: siteId,
            BusinessDate: businessDate.ToDateTime(TimeOnly.MinValue),
            Name: "All Day Tips",
            Method: TipPoolMethod.ByHoursWorked,
            EligibleRoleIds: new List<Guid>()));

        var employee = Guid.NewGuid();

        // Act - Same employee works lunch and dinner
        await grain.AddParticipantAsync(employee, hoursWorked: 4.0m, points: 0);
        await grain.AddParticipantAsync(employee, hoursWorked: 4.0m, points: 0);

        await grain.AddTipsAsync(new AddTipsCommand(Amount: 200.00m, Source: "Combined"));

        var snapshot = await grain.DistributeAsync(new DistributeTipsCommand(
            DistributedByUserId: Guid.NewGuid()));

        // Assert
        snapshot.Distributions.Should().HaveCount(1);
        snapshot.Distributions[0].HoursWorked.Should().Be(8.0m);
        snapshot.Distributions[0].TipAmount.Should().Be(200.00m);
    }

    [Fact]
    public async Task TipPoolGrain_DistributeEqual_DistributesEqually()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4));
        var grain = _cluster.GrainFactory.GetGrain<ITipPoolGrain>(
            GrainKeys.TipPool(orgId, siteId, businessDate, "equal-test"));

        await grain.CreateAsync(new CreateTipPoolCommand(
            LocationId: siteId,
            BusinessDate: businessDate.ToDateTime(TimeOnly.MinValue),
            Name: "Equal Distribution Test",
            Method: TipPoolMethod.Equal,
            EligibleRoleIds: new List<Guid>()));

        var employee1 = Guid.NewGuid();
        var employee2 = Guid.NewGuid();
        var employee3 = Guid.NewGuid();

        await grain.AddParticipantAsync(employee1, hoursWorked: 8.0m, points: 10.0m);
        await grain.AddParticipantAsync(employee2, hoursWorked: 4.0m, points: 5.0m);
        await grain.AddParticipantAsync(employee3, hoursWorked: 6.0m, points: 7.0m);

        await grain.AddTipsAsync(new AddTipsCommand(Amount: 300.00m, Source: "Pool"));

        // Act
        var snapshot = await grain.DistributeAsync(new DistributeTipsCommand(
            DistributedByUserId: Guid.NewGuid()));

        // Assert
        snapshot.IsDistributed.Should().BeTrue();
        snapshot.DistributedAt.Should().NotBeNull();
        snapshot.Distributions.Should().HaveCount(3);
        snapshot.Distributions.Should().OnlyContain(d => d.TipAmount == 100.00m);
    }

    [Fact]
    public async Task TipPoolGrain_DistributeByHoursWorked_DistributesProportionally()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        var grain = _cluster.GrainFactory.GetGrain<ITipPoolGrain>(
            GrainKeys.TipPool(orgId, siteId, businessDate, "hours-test"));

        await grain.CreateAsync(new CreateTipPoolCommand(
            LocationId: siteId,
            BusinessDate: businessDate.ToDateTime(TimeOnly.MinValue),
            Name: "Hours Distribution Test",
            Method: TipPoolMethod.ByHoursWorked,
            EligibleRoleIds: new List<Guid>()));

        var employee1 = Guid.NewGuid();
        var employee2 = Guid.NewGuid();

        // Employee1: 8 hours (80%), Employee2: 2 hours (20%)
        await grain.AddParticipantAsync(employee1, hoursWorked: 8.0m, points: 0);
        await grain.AddParticipantAsync(employee2, hoursWorked: 2.0m, points: 0);

        await grain.AddTipsAsync(new AddTipsCommand(Amount: 100.00m, Source: "Pool"));

        // Act
        var snapshot = await grain.DistributeAsync(new DistributeTipsCommand(
            DistributedByUserId: Guid.NewGuid()));

        // Assert
        var dist1 = snapshot.Distributions.First(d => d.EmployeeId == employee1);
        var dist2 = snapshot.Distributions.First(d => d.EmployeeId == employee2);

        dist1.TipAmount.Should().Be(80.00m);
        dist2.TipAmount.Should().Be(20.00m);
    }

    [Fact]
    public async Task TipPoolGrain_DistributeByPoints_DistributesProportionally()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6));
        var grain = _cluster.GrainFactory.GetGrain<ITipPoolGrain>(
            GrainKeys.TipPool(orgId, siteId, businessDate, "points-test"));

        await grain.CreateAsync(new CreateTipPoolCommand(
            LocationId: siteId,
            BusinessDate: businessDate.ToDateTime(TimeOnly.MinValue),
            Name: "Points Distribution Test",
            Method: TipPoolMethod.ByPoints,
            EligibleRoleIds: new List<Guid>()));

        var employee1 = Guid.NewGuid();
        var employee2 = Guid.NewGuid();
        var employee3 = Guid.NewGuid();

        // 10 + 5 + 5 = 20 total points
        await grain.AddParticipantAsync(employee1, hoursWorked: 8.0m, points: 10.0m); // 50%
        await grain.AddParticipantAsync(employee2, hoursWorked: 8.0m, points: 5.0m);  // 25%
        await grain.AddParticipantAsync(employee3, hoursWorked: 8.0m, points: 5.0m);  // 25%

        await grain.AddTipsAsync(new AddTipsCommand(Amount: 200.00m, Source: "Pool"));

        // Act
        var snapshot = await grain.DistributeAsync(new DistributeTipsCommand(
            DistributedByUserId: Guid.NewGuid()));

        // Assert
        var dist1 = snapshot.Distributions.First(d => d.EmployeeId == employee1);
        var dist2 = snapshot.Distributions.First(d => d.EmployeeId == employee2);
        var dist3 = snapshot.Distributions.First(d => d.EmployeeId == employee3);

        dist1.TipAmount.Should().Be(100.00m);
        dist2.TipAmount.Should().Be(50.00m);
        dist3.TipAmount.Should().Be(50.00m);
    }

    [Fact]
    public async Task TipPoolGrain_AddTips_ThrowsAfterDistribution()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));
        var grain = _cluster.GrainFactory.GetGrain<ITipPoolGrain>(
            GrainKeys.TipPool(orgId, siteId, businessDate, "closed-test"));

        await grain.CreateAsync(new CreateTipPoolCommand(
            LocationId: siteId,
            BusinessDate: businessDate.ToDateTime(TimeOnly.MinValue),
            Name: "Closed Pool Test",
            Method: TipPoolMethod.Equal,
            EligibleRoleIds: new List<Guid>()));

        await grain.AddParticipantAsync(Guid.NewGuid(), hoursWorked: 8.0m, points: 0);
        await grain.AddTipsAsync(new AddTipsCommand(Amount: 100.00m, Source: "Pool"));

        await grain.DistributeAsync(new DistributeTipsCommand(
            DistributedByUserId: Guid.NewGuid()));

        // Act & Assert
        var act = () => grain.AddTipsAsync(new AddTipsCommand(Amount: 50.00m, Source: "Late tip"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot add tips to a distributed pool");
    }

    [Fact]
    public async Task TipPoolGrain_AddParticipant_ThrowsAfterDistribution()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8));
        var grain = _cluster.GrainFactory.GetGrain<ITipPoolGrain>(
            GrainKeys.TipPool(orgId, siteId, businessDate, "locked-test"));

        await grain.CreateAsync(new CreateTipPoolCommand(
            LocationId: siteId,
            BusinessDate: businessDate.ToDateTime(TimeOnly.MinValue),
            Name: "Locked Pool Test",
            Method: TipPoolMethod.Equal,
            EligibleRoleIds: new List<Guid>()));

        await grain.AddParticipantAsync(Guid.NewGuid(), hoursWorked: 8.0m, points: 0);
        await grain.AddTipsAsync(new AddTipsCommand(Amount: 100.00m, Source: "Pool"));

        await grain.DistributeAsync(new DistributeTipsCommand(
            DistributedByUserId: Guid.NewGuid()));

        // Act & Assert
        var act = () => grain.AddParticipantAsync(Guid.NewGuid(), hoursWorked: 4.0m, points: 0);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot add participants to a distributed pool");
    }

    [Fact]
    public async Task TipPoolGrain_Distribute_ThrowsIfAlreadyDistributed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var businessDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(9));
        var grain = _cluster.GrainFactory.GetGrain<ITipPoolGrain>(
            GrainKeys.TipPool(orgId, siteId, businessDate, "double-dist-test"));

        await grain.CreateAsync(new CreateTipPoolCommand(
            LocationId: siteId,
            BusinessDate: businessDate.ToDateTime(TimeOnly.MinValue),
            Name: "Double Distribution Test",
            Method: TipPoolMethod.Equal,
            EligibleRoleIds: new List<Guid>()));

        await grain.AddParticipantAsync(Guid.NewGuid(), hoursWorked: 8.0m, points: 0);
        await grain.AddTipsAsync(new AddTipsCommand(Amount: 100.00m, Source: "Pool"));

        await grain.DistributeAsync(new DistributeTipsCommand(
            DistributedByUserId: Guid.NewGuid()));

        // Act & Assert
        var act = () => grain.DistributeAsync(new DistributeTipsCommand(
            DistributedByUserId: Guid.NewGuid()));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tips already distributed");
    }

    // ============================================================================
    // PayrollPeriodGrain Tests
    // ============================================================================

    [Fact]
    public async Task PayrollPeriodGrain_Create_CreatesPayrollPeriod()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14));
        var grain = _cluster.GrainFactory.GetGrain<IPayrollPeriodGrain>(
            GrainKeys.PayrollPeriod(orgId, siteId, periodStart));

        var periodEnd = periodStart.AddDays(13).ToDateTime(TimeOnly.MaxValue);

        // Act
        var snapshot = await grain.CreateAsync(new CreatePayrollPeriodCommand(
            LocationId: siteId,
            PeriodStart: periodStart.ToDateTime(TimeOnly.MinValue),
            PeriodEnd: periodEnd));

        // Assert
        snapshot.PayrollPeriodId.Should().NotBeEmpty();
        snapshot.LocationId.Should().Be(siteId);
        snapshot.PeriodStart.Should().BeCloseTo(periodStart.ToDateTime(TimeOnly.MinValue), TimeSpan.FromSeconds(1));
        snapshot.PeriodEnd.Should().BeCloseTo(periodEnd, TimeSpan.FromSeconds(1));
        snapshot.Status.Should().Be(PayrollStatus.Open);
        snapshot.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task PayrollPeriodGrain_Create_ThrowsIfAlreadyExists()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-28));
        var grain = _cluster.GrainFactory.GetGrain<IPayrollPeriodGrain>(
            GrainKeys.PayrollPeriod(orgId, siteId, periodStart));

        await grain.CreateAsync(new CreatePayrollPeriodCommand(
            LocationId: siteId,
            PeriodStart: periodStart.ToDateTime(TimeOnly.MinValue),
            PeriodEnd: periodStart.AddDays(13).ToDateTime(TimeOnly.MaxValue)));

        // Act & Assert
        var act = () => grain.CreateAsync(new CreatePayrollPeriodCommand(
            LocationId: siteId,
            PeriodStart: periodStart.ToDateTime(TimeOnly.MinValue),
            PeriodEnd: periodStart.AddDays(13).ToDateTime(TimeOnly.MaxValue)));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Payroll period already exists");
    }

    [Fact]
    public async Task PayrollPeriodGrain_Calculate_MovesToPendingApproval()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-42));
        var grain = _cluster.GrainFactory.GetGrain<IPayrollPeriodGrain>(
            GrainKeys.PayrollPeriod(orgId, siteId, periodStart));

        await grain.CreateAsync(new CreatePayrollPeriodCommand(
            LocationId: siteId,
            PeriodStart: periodStart.ToDateTime(TimeOnly.MinValue),
            PeriodEnd: periodStart.AddDays(13).ToDateTime(TimeOnly.MaxValue)));

        // Act
        await grain.CalculateAsync();
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Status.Should().Be(PayrollStatus.PendingApproval);
    }

    [Fact]
    public async Task PayrollPeriodGrain_Calculate_ThrowsIfNotOpen()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-56));
        var grain = _cluster.GrainFactory.GetGrain<IPayrollPeriodGrain>(
            GrainKeys.PayrollPeriod(orgId, siteId, periodStart));

        await grain.CreateAsync(new CreatePayrollPeriodCommand(
            LocationId: siteId,
            PeriodStart: periodStart.ToDateTime(TimeOnly.MinValue),
            PeriodEnd: periodStart.AddDays(13).ToDateTime(TimeOnly.MaxValue)));

        await grain.CalculateAsync(); // Now in PendingApproval

        // Act & Assert
        var act = () => grain.CalculateAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Payroll period is not open");
    }

    [Fact]
    public async Task PayrollPeriodGrain_Approve_MovesToApproved()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-70));
        var grain = _cluster.GrainFactory.GetGrain<IPayrollPeriodGrain>(
            GrainKeys.PayrollPeriod(orgId, siteId, periodStart));
        var approverId = Guid.NewGuid();

        await grain.CreateAsync(new CreatePayrollPeriodCommand(
            LocationId: siteId,
            PeriodStart: periodStart.ToDateTime(TimeOnly.MinValue),
            PeriodEnd: periodStart.AddDays(13).ToDateTime(TimeOnly.MaxValue)));

        await grain.CalculateAsync();

        // Act
        await grain.ApproveAsync(approverId);
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Status.Should().Be(PayrollStatus.Approved);
    }

    [Fact]
    public async Task PayrollPeriodGrain_Approve_ThrowsIfNotPendingApproval()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-84));
        var grain = _cluster.GrainFactory.GetGrain<IPayrollPeriodGrain>(
            GrainKeys.PayrollPeriod(orgId, siteId, periodStart));

        await grain.CreateAsync(new CreatePayrollPeriodCommand(
            LocationId: siteId,
            PeriodStart: periodStart.ToDateTime(TimeOnly.MinValue),
            PeriodEnd: periodStart.AddDays(13).ToDateTime(TimeOnly.MaxValue)));

        // Act & Assert - Try to approve without calculating first
        var act = () => grain.ApproveAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Payroll period is not pending approval");
    }

    [Fact]
    public async Task PayrollPeriodGrain_Process_MovesToProcessed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-98));
        var grain = _cluster.GrainFactory.GetGrain<IPayrollPeriodGrain>(
            GrainKeys.PayrollPeriod(orgId, siteId, periodStart));

        await grain.CreateAsync(new CreatePayrollPeriodCommand(
            LocationId: siteId,
            PeriodStart: periodStart.ToDateTime(TimeOnly.MinValue),
            PeriodEnd: periodStart.AddDays(13).ToDateTime(TimeOnly.MaxValue)));

        await grain.CalculateAsync();
        await grain.ApproveAsync(Guid.NewGuid());

        // Act
        await grain.ProcessAsync();
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Status.Should().Be(PayrollStatus.Processed);
    }

    [Fact]
    public async Task PayrollPeriodGrain_Process_ThrowsIfNotApproved()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-112));
        var grain = _cluster.GrainFactory.GetGrain<IPayrollPeriodGrain>(
            GrainKeys.PayrollPeriod(orgId, siteId, periodStart));

        await grain.CreateAsync(new CreatePayrollPeriodCommand(
            LocationId: siteId,
            PeriodStart: periodStart.ToDateTime(TimeOnly.MinValue),
            PeriodEnd: periodStart.AddDays(13).ToDateTime(TimeOnly.MaxValue)));

        await grain.CalculateAsync();

        // Act & Assert - Try to process without approving first
        var act = () => grain.ProcessAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Payroll period is not approved");
    }

    [Fact]
    public async Task PayrollPeriodGrain_StatusTransitions_FollowCorrectOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-126));
        var grain = _cluster.GrainFactory.GetGrain<IPayrollPeriodGrain>(
            GrainKeys.PayrollPeriod(orgId, siteId, periodStart));

        await grain.CreateAsync(new CreatePayrollPeriodCommand(
            LocationId: siteId,
            PeriodStart: periodStart.ToDateTime(TimeOnly.MinValue),
            PeriodEnd: periodStart.AddDays(13).ToDateTime(TimeOnly.MaxValue)));

        // Act & Assert - Open
        var snapshot1 = await grain.GetSnapshotAsync();
        snapshot1.Status.Should().Be(PayrollStatus.Open);

        // Calculate -> PendingApproval
        await grain.CalculateAsync();
        var snapshot2 = await grain.GetSnapshotAsync();
        snapshot2.Status.Should().Be(PayrollStatus.PendingApproval);

        // Approve -> Approved
        await grain.ApproveAsync(Guid.NewGuid());
        var snapshot3 = await grain.GetSnapshotAsync();
        snapshot3.Status.Should().Be(PayrollStatus.Approved);

        // Process -> Processed
        await grain.ProcessAsync();
        var snapshot4 = await grain.GetSnapshotAsync();
        snapshot4.Status.Should().Be(PayrollStatus.Processed);
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private static AddShiftCommand CreateShiftCommand(Guid employeeId, DateTime date, int startHour, int endHour)
    {
        return new AddShiftCommand(
            ShiftId: Guid.NewGuid(),
            EmployeeId: employeeId,
            RoleId: Guid.NewGuid(),
            Date: date,
            StartTime: TimeSpan.FromHours(startHour),
            EndTime: TimeSpan.FromHours(endHour),
            BreakMinutes: 0,
            HourlyRate: 15.00m,
            Notes: null);
    }
}
