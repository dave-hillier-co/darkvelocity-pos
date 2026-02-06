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

    // Given: a new user with email, display name, and employee type
    // When: the user is created in the organization
    // Then: the user is assigned an ID, email is stored, and creation timestamp is recorded
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

    // Given: a user created as a manager in an organization
    // When: the user state is retrieved
    // Then: the state contains the user's ID, organization, email, display name, manager type, and active status
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

    // Given: an existing user in the organization
    // When: the user's display name is updated
    // Then: the state version increments and the display name reflects the change
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

    // Given: an existing user without a PIN
    // When: a four-digit PIN is set for POS login
    // Then: the user's PIN hash is stored (non-empty)
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

    // Given: a user with PIN "1234" configured for POS login
    // When: the correct PIN "1234" is submitted for verification
    // Then: verification succeeds
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

    // Given: a user with PIN "1234" configured for POS login
    // When: an incorrect PIN "5678" is submitted for verification
    // Then: verification fails with an "Invalid PIN" error
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

    // Given: a user with a valid PIN whose account has been locked
    // When: the correct PIN is submitted for verification
    // Then: verification fails with a "User account is locked" error
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

    // Given: a user in an organization with no site access
    // When: access to a specific site is granted
    // Then: the user has access to that site
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

    // Given: a user who has been granted access to a site
    // When: site access is revoked
    // Then: the user no longer has access to that site
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

    // Given: a user not assigned to any user group
    // When: the user is added to a user group
    // Then: the user's group memberships include the new group
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

    // Given: an active user in the organization
    // When: the user account is deactivated
    // Then: the user's status changes to Inactive
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

    // Given: an active user in the organization
    // When: the user account is locked for a security reason
    // Then: the user's status changes to Locked
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

    // Given: a user whose account has been locked
    // When: the user account is unlocked
    // Then: the user's status returns to Active
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

    // Given: an existing user who has not yet logged in
    // When: a login event is recorded
    // Then: the user's last login timestamp is set to approximately now
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

    // Given: a new user group with name and description for an organization
    // When: the user group is created
    // Then: the group is assigned an ID and creation timestamp is recorded
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

    // Given: a user group created with name "Managers" and description
    // When: the group state is retrieved
    // Then: the state contains the group's ID, organization, name, and description
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

    // Given: a user group with no members
    // When: a user is added as a member
    // Then: the group reports the user as a member
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

    // Given: a user group with a member
    // When: the member is removed from the group
    // Then: the group no longer reports the user as a member
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

    // Given: a user group with two members added
    // When: the member list is retrieved
    // Then: both members are returned
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
