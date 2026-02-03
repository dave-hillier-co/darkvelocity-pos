using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.TestingHost;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class EmployeeGrainTests
{
    private readonly TestCluster _cluster;

    public EmployeeGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    [Fact]
    public async Task CreateEmployee_SetsCorrectState()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        var command = new CreateEmployeeCommand(
            OrganizationId: orgId,
            UserId: userId,
            DefaultSiteId: siteId,
            EmployeeNumber: "EMP-001",
            FirstName: "John",
            LastName: "Doe",
            Email: "john.doe@example.com",
            EmploymentType: EmploymentType.FullTime);

        var result = await grain.CreateAsync(command);

        Assert.Equal(employeeId, result.Id);
        Assert.Equal("EMP-001", result.EmployeeNumber);

        var state = await grain.GetStateAsync();
        Assert.Equal("John", state.FirstName);
        Assert.Equal("Doe", state.LastName);
        Assert.Equal(EmployeeStatus.Active, state.Status);
        Assert.Contains(siteId, state.AllowedSiteIds);
    }

    [Fact]
    public async Task UpdateEmployee_UpdatesNameAndEmail()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, userId, siteId, "EMP-002", "Jane", "Doe", "jane@example.com"));

        var result = await grain.UpdateAsync(new UpdateEmployeeCommand(
            FirstName: "Janet",
            Email: "janet@example.com"));

        Assert.True(result.Version > 1);

        var state = await grain.GetStateAsync();
        Assert.Equal("Janet", state.FirstName);
        Assert.Equal("janet@example.com", state.Email);
    }

    [Fact]
    public async Task ClockIn_RecordsTimeEntry()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, userId, siteId, "EMP-003", "Bob", "Smith", "bob@example.com"));

        var clockInResult = await grain.ClockInAsync(new ClockInCommand(siteId));

        Assert.NotEqual(Guid.Empty, clockInResult.TimeEntryId);
        Assert.True(await grain.IsClockedInAsync());
    }

    [Fact]
    public async Task ClockOut_CalculatesTotalHours()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, userId, siteId, "EMP-004", "Alice", "Johnson", "alice@example.com"));

        await grain.ClockInAsync(new ClockInCommand(siteId));
        await Task.Delay(100); // Short delay to ensure clock out time > clock in

        var clockOutResult = await grain.ClockOutAsync(new ClockOutCommand("Test complete"));

        Assert.True(clockOutResult.TotalHours >= 0);
        Assert.False(await grain.IsClockedInAsync());
    }

    [Fact]
    public async Task AssignRole_AddsRoleToEmployee()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, userId, siteId, "EMP-005", "Charlie", "Brown", "charlie@example.com"));

        await grain.AssignRoleAsync(new AssignRoleCommand(
            RoleId: roleId,
            RoleName: "Server",
            Department: "Front of House",
            IsPrimary: true,
            HourlyRateOverride: 15.50m));

        var state = await grain.GetStateAsync();
        Assert.Single(state.RoleAssignments);
        Assert.Equal("Server", state.RoleAssignments[0].RoleName);
        Assert.True(state.RoleAssignments[0].IsPrimary);
    }

    [Fact]
    public async Task Terminate_SetsStatusAndPreventsReactivation()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, userId, siteId, "EMP-006", "David", "Lee", "david@example.com"));

        await grain.TerminateAsync(DateOnly.FromDateTime(DateTime.UtcNow), "Position eliminated");

        var state = await grain.GetStateAsync();
        Assert.Equal(EmployeeStatus.Terminated, state.Status);
        Assert.Equal("Position eliminated", state.TerminationReason);

        await Assert.ThrowsAsync<InvalidOperationException>(() => grain.ActivateAsync());
    }

    [Fact]
    public async Task GrantSiteAccess_AllowsClockInAtNewSite()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId1 = Guid.NewGuid();
        var siteId2 = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, userId, siteId1, "EMP-007", "Emma", "Wilson", "emma@example.com"));

        // Cannot clock in at site2 initially
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            grain.ClockInAsync(new ClockInCommand(siteId2)));

        // Grant access to site2
        await grain.GrantSiteAccessAsync(siteId2);

        // Now can clock in at site2
        var result = await grain.ClockInAsync(new ClockInCommand(siteId2));
        Assert.NotEqual(Guid.Empty, result.TimeEntryId);
    }

    [Fact]
    public async Task SyncFromUser_UpdatesEmployeeFromUserChanges()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, userId, siteId, "EMP-008", "Frank", "Miller", "frank@example.com"));

        // Simulate sync from user update
        await grain.SyncFromUserAsync("Franklin", "Miller Jr", UserStatus.Active);

        var state = await grain.GetStateAsync();
        Assert.Equal("Franklin", state.FirstName);
        Assert.Equal("Miller Jr", state.LastName);
    }

    [Fact]
    public async Task SyncFromUser_DeactivatesEmployee_WhenUserIsDeactivated()
    {
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();

        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, userId, siteId, "EMP-009", "Grace", "Hopper", "grace@example.com"));

        // Simulate sync from user deactivation
        await grain.SyncFromUserAsync(null, null, UserStatus.Inactive);

        var state = await grain.GetStateAsync();
        Assert.Equal(EmployeeStatus.Inactive, state.Status);
    }
}
