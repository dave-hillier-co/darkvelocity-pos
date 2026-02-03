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
