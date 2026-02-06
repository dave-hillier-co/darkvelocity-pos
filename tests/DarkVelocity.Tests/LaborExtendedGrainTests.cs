using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;
using Orleans.TestingHost;
using Xunit;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class LaborExtendedGrainTests
{
    private readonly TestCluster _cluster;

    public LaborExtendedGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    // ============================================================================
    // Employee Availability Grain Tests
    // ============================================================================

    // Given: a new employee availability grain that has not been initialized
    // When: the grain is initialized for an employee
    // Then: the availability snapshot should show the employee with no availability entries
    [Fact]
    public async Task EmployeeAvailabilityGrain_Initialize_CreatesEmptyAvailability()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeAvailabilityGrain>(
            GrainKeys.EmployeeAvailability(orgId, employeeId));

        // Act
        await grain.InitializeAsync(employeeId);
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.EmployeeId.Should().Be(employeeId);
        snapshot.Availabilities.Should().BeEmpty();
    }

    // Given: an initialized employee availability grain
    // When: availability is set for Monday 9 AM to 5 PM as a preferred shift
    // Then: the entry should reflect the correct day, time range, availability, and preference
    [Fact]
    public async Task EmployeeAvailabilityGrain_SetAvailability_AddsEntry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeAvailabilityGrain>(
            GrainKeys.EmployeeAvailability(orgId, employeeId));

        await grain.InitializeAsync(employeeId);

        // Act
        var entry = await grain.SetAvailabilityAsync(new SetAvailabilityCommand(
            DayOfWeek: 1, // Monday
            StartTime: TimeSpan.FromHours(9),
            EndTime: TimeSpan.FromHours(17),
            IsAvailable: true,
            IsPreferred: true,
            EffectiveFrom: null,
            EffectiveTo: null,
            Notes: "Regular shift"));

        // Assert
        entry.DayOfWeek.Should().Be(1);
        entry.DayOfWeekName.Should().Be("Monday");
        entry.StartTime.Should().Be(TimeSpan.FromHours(9));
        entry.EndTime.Should().Be(TimeSpan.FromHours(17));
        entry.IsAvailable.Should().BeTrue();
        entry.IsPreferred.Should().BeTrue();
    }

    // Given: an employee available on Monday from 9 AM to 5 PM
    // When: availability is checked at 10 AM Monday, 8 AM Monday, and 10 AM Tuesday
    // Then: only the 10 AM Monday check should return available
    [Fact]
    public async Task EmployeeAvailabilityGrain_IsAvailableOn_ReturnsCorrectValue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeAvailabilityGrain>(
            GrainKeys.EmployeeAvailability(orgId, employeeId));

        await grain.InitializeAsync(employeeId);
        await grain.SetAvailabilityAsync(new SetAvailabilityCommand(
            DayOfWeek: 1,
            StartTime: TimeSpan.FromHours(9),
            EndTime: TimeSpan.FromHours(17),
            IsAvailable: true,
            IsPreferred: false,
            EffectiveFrom: null,
            EffectiveTo: null,
            Notes: null));

        // Act
        var available10am = await grain.IsAvailableOnAsync(1, TimeSpan.FromHours(10));
        var available8am = await grain.IsAvailableOnAsync(1, TimeSpan.FromHours(8));
        var availableTuesday = await grain.IsAvailableOnAsync(2, TimeSpan.FromHours(10));

        // Assert
        available10am.Should().BeTrue();
        available8am.Should().BeFalse();
        availableTuesday.Should().BeFalse();
    }

    // ============================================================================
    // Shift Swap Grain Tests
    // ============================================================================

    // Given: a shift swap grain ready to receive a new request
    // When: a swap request is created between two employees citing a doctor's appointment
    // Then: the request should be in pending status with the swap type and reason recorded
    [Fact]
    public async Task ShiftSwapGrain_Create_CreatesRequestSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IShiftSwapGrain>(
            GrainKeys.ShiftSwapRequest(orgId, requestId));

        var command = new CreateShiftSwapCommand(
            RequestingEmployeeId: Guid.NewGuid(),
            RequestingShiftId: Guid.NewGuid(),
            TargetEmployeeId: Guid.NewGuid(),
            TargetShiftId: Guid.NewGuid(),
            Type: ShiftSwapType.Swap,
            Reason: "Need to attend a doctor's appointment");

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.SwapRequestId.Should().Be(requestId);
        snapshot.Type.Should().Be(ShiftSwapType.Swap);
        snapshot.Status.Should().Be(ShiftSwapStatus.Pending);
        snapshot.Reason.Should().Be("Need to attend a doctor's appointment");
    }

    // Given: a pending shift drop request from an employee
    // When: a manager approves the shift swap request with notes
    // Then: the request status should change to approved with the response timestamp and notes recorded
    [Fact]
    public async Task ShiftSwapGrain_Approve_UpdatesStatusToApproved()
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

        // Act
        var snapshot = await grain.ApproveAsync(new RespondToShiftSwapCommand(
            RespondingUserId: Guid.NewGuid(),
            Notes: "Approved - shift covered"));

        // Assert
        snapshot.Status.Should().Be(ShiftSwapStatus.Approved);
        snapshot.Notes.Should().Be("Approved - shift covered");
        snapshot.RespondedAt.Should().NotBeNull();
    }

    // Given: a pending shift pickup request
    // When: the requesting employee cancels the request
    // Then: the request status should change to cancelled
    [Fact]
    public async Task ShiftSwapGrain_Cancel_AllowsCancellationWhenPending()
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

        // Act
        var snapshot = await grain.CancelAsync();

        // Assert
        snapshot.Status.Should().Be(ShiftSwapStatus.Cancelled);
    }

    // ============================================================================
    // Time Off Grain Tests
    // ============================================================================

    // Given: a time off grain ready to receive a new request
    // When: a 7-day vacation request is created starting next week
    // Then: the request should be pending, calculated as 8 total days (inclusive), and marked as paid leave
    [Fact]
    public async Task TimeOffGrain_Create_CreatesRequestSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeOffGrain>(
            GrainKeys.TimeOffRequest(orgId, requestId));

        var command = new CreateTimeOffCommand(
            EmployeeId: Guid.NewGuid(),
            Type: TimeOffType.Vacation,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)),
            Reason: "Family vacation");

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.TimeOffRequestId.Should().Be(requestId);
        snapshot.Type.Should().Be(TimeOffType.Vacation);
        snapshot.Status.Should().Be(TimeOffStatus.Pending);
        snapshot.TotalDays.Should().Be(8);
        snapshot.IsPaid.Should().BeTrue(); // Vacation is paid
    }

    // Given: a time off grain ready to receive a new request
    // When: an unpaid leave request is created for a personal matter
    // Then: the request should be marked as unpaid leave
    [Fact]
    public async Task TimeOffGrain_CreateUnpaidLeave_MarksAsUnpaid()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeOffGrain>(
            GrainKeys.TimeOffRequest(orgId, requestId));

        var command = new CreateTimeOffCommand(
            EmployeeId: Guid.NewGuid(),
            Type: TimeOffType.Unpaid,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            Reason: "Personal matter");

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.IsPaid.Should().BeFalse();
    }

    // Given: a pending sick leave request from an employee
    // When: a manager approves the time off request with well-wishes
    // Then: the request status should change to approved with the review timestamp and notes recorded
    [Fact]
    public async Task TimeOffGrain_Approve_UpdatesStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeOffGrain>(
            GrainKeys.TimeOffRequest(orgId, requestId));

        await grain.CreateAsync(new CreateTimeOffCommand(
            EmployeeId: Guid.NewGuid(),
            Type: TimeOffType.Sick,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            Reason: "Flu"));

        // Act
        var snapshot = await grain.ApproveAsync(new RespondToTimeOffCommand(
            ReviewedByUserId: Guid.NewGuid(),
            Notes: "Get well soon!"));

        // Assert
        snapshot.Status.Should().Be(TimeOffStatus.Approved);
        snapshot.ReviewedAt.Should().NotBeNull();
        snapshot.Notes.Should().Be("Get well soon!");
    }

    // Given: a pending personal day request from an employee
    // When: a manager rejects the time off request citing insufficient notice
    // Then: the request status should change to rejected with the rejection reason recorded
    [Fact]
    public async Task TimeOffGrain_Reject_UpdatesStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ITimeOffGrain>(
            GrainKeys.TimeOffRequest(orgId, requestId));

        await grain.CreateAsync(new CreateTimeOffCommand(
            EmployeeId: Guid.NewGuid(),
            Type: TimeOffType.Personal,
            StartDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            EndDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Reason: null));

        // Act
        var snapshot = await grain.RejectAsync(new RespondToTimeOffCommand(
            ReviewedByUserId: Guid.NewGuid(),
            Notes: "Insufficient notice period"));

        // Assert
        snapshot.Status.Should().Be(TimeOffStatus.Rejected);
        snapshot.Notes.Should().Be("Insufficient notice period");
    }
}
