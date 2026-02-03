using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class UserGrainTests
{
    private readonly TestClusterFixture _fixture;

    public UserGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateUser()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

        var command = new CreateUserCommand(orgId, "test@example.com", "Test User", UserType.Employee, "Test", "User");

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Id.Should().Be(userId);
        result.Email.Should().Be("test@example.com");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetStateAsync_ShouldReturnState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

        await grain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User", UserType.Manager, "Test", "User"));

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        state.Id.Should().Be(userId);
        state.OrganizationId.Should().Be(orgId);
        state.Email.Should().Be("test@example.com");
        state.DisplayName.Should().Be("Test User");
        state.Type.Should().Be(UserType.Manager);
        state.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateUser()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

        await grain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));

        // Act
        var result = await grain.UpdateAsync(new UpdateUserCommand(DisplayName: "Updated Name"));

        // Assert
        result.Version.Should().Be(2);
        var state = await grain.GetStateAsync();
        state.DisplayName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task SetPinAsync_ShouldSetPin()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

        await grain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));

        // Act
        await grain.SetPinAsync("1234");

        // Assert
        var state = await grain.GetStateAsync();
        state.PinHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task VerifyPinAsync_WithCorrectPin_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

        await grain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));
        await grain.SetPinAsync("1234");

        // Act
        var result = await grain.VerifyPinAsync("1234");

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyPinAsync_WithIncorrectPin_ShouldFail()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

        await grain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));
        await grain.SetPinAsync("1234");

        // Act
        var result = await grain.VerifyPinAsync("5678");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Invalid PIN");
    }

    [Fact]
    public async Task VerifyPinAsync_WhenLocked_ShouldFail()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

        await grain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));
        await grain.SetPinAsync("1234");
        await grain.LockAsync("Too many failed attempts");

        // Act
        var result = await grain.VerifyPinAsync("1234");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("User account is locked");
    }

    [Fact]
    public async Task GrantSiteAccessAsync_ShouldAddSiteAccess()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

        await grain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));

        // Act
        await grain.GrantSiteAccessAsync(siteId);

        // Assert
        var hasAccess = await grain.HasSiteAccessAsync(siteId);
        hasAccess.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeSiteAccessAsync_ShouldRemoveSiteAccess()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

        await grain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));
        await grain.GrantSiteAccessAsync(siteId);

        // Act
        await grain.RevokeSiteAccessAsync(siteId);

        // Assert
        var hasAccess = await grain.HasSiteAccessAsync(siteId);
        hasAccess.Should().BeFalse();
    }

    [Fact]
    public async Task AddToGroupAsync_ShouldAddGroup()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

        await grain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));

        // Act
        await grain.AddToGroupAsync(groupId);

        // Assert
        var state = await grain.GetStateAsync();
        state.UserGroupIds.Should().Contain(groupId);
    }

    [Fact]
    public async Task DeactivateAsync_ShouldSetStatusToInactive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

        await grain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));

        // Act
        await grain.DeactivateAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(UserStatus.Inactive);
    }

    [Fact]
    public async Task LockAsync_ShouldSetStatusToLocked()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

        await grain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));

        // Act
        await grain.LockAsync("Security reason");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(UserStatus.Locked);
    }

    [Fact]
    public async Task UnlockAsync_ShouldSetStatusToActive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

        await grain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));
        await grain.LockAsync("Security reason");

        // Act
        await grain.UnlockAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task RecordLoginAsync_ShouldUpdateLastLoginAt()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(GrainKeys.User(orgId, userId));

        await grain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));

        // Act
        await grain.RecordLoginAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.LastLoginAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class UserGroupGrainTests
{
    private readonly TestClusterFixture _fixture;

    public UserGroupGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateUserGroup()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(orgId, groupId));

        var command = new CreateUserGroupCommand(orgId, "Managers", "Management team");

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Id.Should().Be(groupId);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetStateAsync_ShouldReturnState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(orgId, groupId));

        await grain.CreateAsync(new CreateUserGroupCommand(orgId, "Managers", "Management team"));

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        state.Id.Should().Be(groupId);
        state.OrganizationId.Should().Be(orgId);
        state.Name.Should().Be("Managers");
        state.Description.Should().Be("Management team");
    }

    [Fact]
    public async Task AddMemberAsync_ShouldAddMember()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(orgId, groupId));

        await grain.CreateAsync(new CreateUserGroupCommand(orgId, "Managers"));

        // Act
        await grain.AddMemberAsync(userId);

        // Assert
        var hasMember = await grain.HasMemberAsync(userId);
        hasMember.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveMemberAsync_ShouldRemoveMember()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(orgId, groupId));

        await grain.CreateAsync(new CreateUserGroupCommand(orgId, "Managers"));
        await grain.AddMemberAsync(userId);

        // Act
        await grain.RemoveMemberAsync(userId);

        // Assert
        var hasMember = await grain.HasMemberAsync(userId);
        hasMember.Should().BeFalse();
    }

    [Fact]
    public async Task GetMembersAsync_ShouldReturnAllMembers()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(orgId, groupId));

        await grain.CreateAsync(new CreateUserGroupCommand(orgId, "Managers"));
        await grain.AddMemberAsync(userId1);
        await grain.AddMemberAsync(userId2);

        // Act
        var members = await grain.GetMembersAsync();

        // Assert
        members.Should().HaveCount(2);
        members.Should().Contain(userId1);
        members.Should().Contain(userId2);
    }
}
