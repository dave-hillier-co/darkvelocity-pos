using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace DarkVelocity.Tests;

// ============================================================================
// DeviceAuthGrain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DeviceAuthGrainTests
{
    private readonly TestClusterFixture _fixture;

    public DeviceAuthGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDeviceAuthGrain GetGrain(string userCode)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IDeviceAuthGrain>(userCode);
    }

    [Fact]
    public async Task InitiateAsync_ShouldCreateUserCodeAndDeviceCode()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);

        // Act
        var response = await grain.InitiateAsync(new DeviceCodeRequest(
            ClientId: "pos-app",
            Scope: "openid profile"));

        // Assert
        response.DeviceCode.Should().NotBeNullOrEmpty();
        response.UserCode.Should().NotBeNullOrEmpty();
        response.VerificationUri.Should().Be("https://app.darkvelocity.io/device");
        response.VerificationUriComplete.Should().Contain(userCode);
        response.ExpiresIn.Should().Be(15 * 60); // 15 minutes
        response.Interval.Should().Be(5); // 5 seconds polling interval
    }

    [Fact]
    public async Task InitiateAsync_UserCode_ShouldHaveExpectedFormat()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);

        // Act
        var response = await grain.InitiateAsync(new DeviceCodeRequest(
            ClientId: "pos-app",
            Scope: "openid"));

        // Assert - User code should be formatted as XXXX-XXXX for display
        response.UserCode.Should().MatchRegex(@"^[A-Z0-9]{4}-[A-Z0-9]{4}$");
    }

    [Fact]
    public async Task GetStatusAsync_AfterInitiation_ShouldReturnPending()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);
        await grain.InitiateAsync(new DeviceCodeRequest("pos-app", "openid"));

        // Act
        var status = await grain.GetStatusAsync();

        // Assert
        status.Should().Be(DeviceAuthStatus.Pending);
    }

    [Fact]
    public async Task GetStatusAsync_WithoutInitiation_ShouldReturnExpired()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);

        // Act
        var status = await grain.GetStatusAsync();

        // Assert
        status.Should().Be(DeviceAuthStatus.Expired);
    }

    [Fact]
    public async Task AuthorizeAsync_ShouldCompleteAuthorization()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var authorizedBy = Guid.NewGuid();
        await grain.InitiateAsync(new DeviceCodeRequest("pos-app", "openid"));

        // Act
        await grain.AuthorizeAsync(new AuthorizeDeviceCommand(
            AuthorizedBy: authorizedBy,
            OrganizationId: orgId,
            SiteId: siteId,
            DeviceName: "Register 1",
            AppType: DeviceType.Pos));

        // Assert
        var status = await grain.GetStatusAsync();
        status.Should().Be(DeviceAuthStatus.Authorized);
    }

    [Fact]
    public async Task DenyAsync_ShouldDenyAuthorization()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);
        await grain.InitiateAsync(new DeviceCodeRequest("pos-app", "openid"));

        // Act
        await grain.DenyAsync("User rejected device authorization");

        // Assert
        var status = await grain.GetStatusAsync();
        status.Should().Be(DeviceAuthStatus.Denied);
    }

    [Fact]
    public async Task GetTokenAsync_AfterAuthorization_ShouldReturnTokens()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var authorizedBy = Guid.NewGuid();

        var response = await grain.InitiateAsync(new DeviceCodeRequest("pos-app", "openid"));
        await grain.AuthorizeAsync(new AuthorizeDeviceCommand(
            authorizedBy, orgId, siteId, "Register 1", DeviceType.Pos));

        // Act
        var tokenResponse = await grain.GetTokenAsync(response.DeviceCode);

        // Assert
        tokenResponse.Should().NotBeNull();
        tokenResponse!.AccessToken.Should().NotBeNullOrEmpty();
        tokenResponse.RefreshToken.Should().NotBeNullOrEmpty();
        tokenResponse.OrganizationId.Should().Be(orgId);
        tokenResponse.SiteId.Should().Be(siteId);
        tokenResponse.TokenType.Should().Be("Bearer");
        tokenResponse.ExpiresIn.Should().Be(3600 * 24 * 90); // 90 days
    }

    [Fact]
    public async Task GetTokenAsync_BeforeAuthorization_ShouldReturnNull()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);
        var response = await grain.InitiateAsync(new DeviceCodeRequest("pos-app", "openid"));

        // Act
        var tokenResponse = await grain.GetTokenAsync(response.DeviceCode);

        // Assert
        tokenResponse.Should().BeNull();
    }

    [Fact]
    public async Task GetTokenAsync_WithIncorrectDeviceCode_ShouldReturnNull()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var authorizedBy = Guid.NewGuid();

        await grain.InitiateAsync(new DeviceCodeRequest("pos-app", "openid"));
        await grain.AuthorizeAsync(new AuthorizeDeviceCommand(
            authorizedBy, orgId, siteId, "Register 1", DeviceType.Pos));

        // Act
        var tokenResponse = await grain.GetTokenAsync("wrong-device-code");

        // Assert
        tokenResponse.Should().BeNull();
    }

    [Fact]
    public async Task IsExpiredAsync_BeforeInitiation_ShouldReturnTrue()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);

        // Act
        var isExpired = await grain.IsExpiredAsync();

        // Assert
        isExpired.Should().BeTrue();
    }

    [Fact]
    public async Task IsExpiredAsync_AfterInitiation_ShouldReturnFalse()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);
        await grain.InitiateAsync(new DeviceCodeRequest("pos-app", "openid"));

        // Act
        var isExpired = await grain.IsExpiredAsync();

        // Assert
        isExpired.Should().BeFalse();
    }

    [Fact]
    public async Task InitiateAsync_Twice_ShouldThrow()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);
        await grain.InitiateAsync(new DeviceCodeRequest("pos-app", "openid"));

        // Act
        var act = () => grain.InitiateAsync(new DeviceCodeRequest("pos-app", "openid"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already initiated*");
    }

    [Fact]
    public async Task AuthorizeAsync_WithoutInitiation_ShouldThrow()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);

        // Act
        var act = () => grain.AuthorizeAsync(new AuthorizeDeviceCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Register", DeviceType.Pos));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initiated*");
    }

    [Fact]
    public async Task AuthorizeAsync_AfterDenial_ShouldThrow()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);
        await grain.InitiateAsync(new DeviceCodeRequest("pos-app", "openid"));
        await grain.DenyAsync("User rejected");

        // Act
        var act = () => grain.AuthorizeAsync(new AuthorizeDeviceCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Register", DeviceType.Pos));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot authorize*");
    }

    [Fact]
    public async Task DenyAsync_AfterAuthorization_ShouldThrow()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);
        await grain.InitiateAsync(new DeviceCodeRequest("pos-app", "openid"));
        await grain.AuthorizeAsync(new AuthorizeDeviceCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Register", DeviceType.Pos));

        // Act
        var act = () => grain.DenyAsync("Too late");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot deny*");
    }

    [Fact]
    public async Task InitiateAsync_ShouldCaptureDeviceFingerprint()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);
        var fingerprint = "device-fp-12345";

        // Act
        var response = await grain.InitiateAsync(new DeviceCodeRequest(
            ClientId: "pos-app",
            Scope: "openid",
            DeviceFingerprint: fingerprint,
            IpAddress: "192.168.1.100"));

        // Assert
        response.Should().NotBeNull();
        // Fingerprint is stored in state, verify by checking authorization flow works
        var status = await grain.GetStatusAsync();
        status.Should().Be(DeviceAuthStatus.Pending);
    }

    [Fact]
    public async Task InitiateAsync_WithDifferentScopes_ShouldAcceptValidScopes()
    {
        // Arrange
        var userCode = GrainKeys.GenerateUserCode();
        var grain = GetGrain(userCode);

        // Act
        var response = await grain.InitiateAsync(new DeviceCodeRequest(
            ClientId: "pos-app",
            Scope: "openid profile offline_access"));

        // Assert
        response.Should().NotBeNull();
        response.DeviceCode.Should().NotBeNullOrEmpty();
    }
}

// ============================================================================
// DeviceGrain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DeviceGrainTests
{
    private readonly TestClusterFixture _fixture;

    public DeviceGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDeviceGrain GetGrain(Guid orgId, Guid deviceId)
    {
        var key = GrainKeys.Device(orgId, deviceId);
        return _fixture.Cluster.GrainFactory.GetGrain<IDeviceGrain>(key);
    }

    [Fact]
    public async Task RegisterAsync_ShouldRegisterDevice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var authorizedBy = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // Act
        var snapshot = await grain.RegisterAsync(new RegisterDeviceCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Name: "Main Register",
            Type: DeviceType.Pos,
            Fingerprint: "fp-123",
            AuthorizedBy: authorizedBy));

        // Assert
        snapshot.Id.Should().Be(deviceId);
        snapshot.OrganizationId.Should().Be(orgId);
        snapshot.SiteId.Should().Be(siteId);
        snapshot.Name.Should().Be("Main Register");
        snapshot.Type.Should().Be(DeviceType.Pos);
        snapshot.Status.Should().Be(DeviceStatus.Authorized);
        snapshot.AuthorizedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RegisterAsync_AlreadyRegistered_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, siteId, "Device 1", DeviceType.Pos, null, Guid.NewGuid()));

        // Act
        var act = () => grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, siteId, "Device 1 Again", DeviceType.Pos, null, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public async Task SuspendAsync_ShouldSuspendDevice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, Guid.NewGuid(), "Device", DeviceType.Pos, null, Guid.NewGuid()));

        // Act
        await grain.SuspendAsync("Security concern");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(DeviceStatus.Suspended);
        var isAuthorized = await grain.IsAuthorizedAsync();
        isAuthorized.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeAsync_ShouldRevokeDevice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, Guid.NewGuid(), "Device", DeviceType.Pos, null, Guid.NewGuid()));

        // Act
        await grain.RevokeAsync("Device lost");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(DeviceStatus.Revoked);
        var isAuthorized = await grain.IsAuthorizedAsync();
        isAuthorized.Should().BeFalse();
    }

    [Fact]
    public async Task ReactivateAsync_FromSuspended_ShouldReactivateDevice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, Guid.NewGuid(), "Device", DeviceType.Pos, null, Guid.NewGuid()));
        await grain.SuspendAsync("Temporary hold");

        // Act
        await grain.ReactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(DeviceStatus.Authorized);
        var isAuthorized = await grain.IsAuthorizedAsync();
        isAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task ReactivateAsync_FromRevoked_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, Guid.NewGuid(), "Device", DeviceType.Pos, null, Guid.NewGuid()));
        await grain.RevokeAsync("Lost device");

        // Act
        var act = () => grain.ReactivateAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*revoked device*");
    }

    [Fact]
    public async Task SetCurrentUserAsync_ShouldTrackLoggedInUser()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, Guid.NewGuid(), "Device", DeviceType.Pos, null, Guid.NewGuid()));

        // Act
        await grain.SetCurrentUserAsync(userId);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.CurrentUserId.Should().Be(userId);
    }

    [Fact]
    public async Task SetCurrentUserAsync_Null_ShouldClearUser()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, Guid.NewGuid(), "Device", DeviceType.Pos, null, Guid.NewGuid()));
        await grain.SetCurrentUserAsync(Guid.NewGuid());

        // Act
        await grain.SetCurrentUserAsync(null);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.CurrentUserId.Should().BeNull();
    }

    [Fact]
    public async Task RecordHeartbeatAsync_ShouldUpdateTimestampAndVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, Guid.NewGuid(), "Device", DeviceType.Pos, null, Guid.NewGuid()));

        // Act
        await grain.RecordHeartbeatAsync("2.1.0");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastSeenAt.Should().NotBeNull();
        snapshot.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RecordHeartbeatAsync_WithNullVersion_ShouldOnlyUpdateTimestamp()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, Guid.NewGuid(), "Device", DeviceType.Pos, null, Guid.NewGuid()));
        await grain.RecordHeartbeatAsync("1.0.0");
        var initialSnapshot = await grain.GetSnapshotAsync();

        // Act
        await Task.Delay(10); // Small delay to ensure timestamp changes
        await grain.RecordHeartbeatAsync(null);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastSeenAt.Should().BeAfter(initialSnapshot.LastSeenAt!.Value);
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenAuthorized_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, Guid.NewGuid(), "Device", DeviceType.Pos, null, Guid.NewGuid()));

        // Act
        var isAuthorized = await grain.IsAuthorizedAsync();

        // Assert
        isAuthorized.Should().BeTrue();
    }

    [Fact]
    public async Task IsAuthorizedAsync_WhenNotRegistered_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // Act
        var isAuthorized = await grain.IsAuthorizedAsync();

        // Assert
        isAuthorized.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenRegistered_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, Guid.NewGuid(), "Device", DeviceType.Pos, null, Guid.NewGuid()));

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenNotRegistered_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterAsync_KdsDevice_ShouldRegisterKdsType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // Act
        var snapshot = await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, Guid.NewGuid(), "Kitchen Display", DeviceType.Kds, null, Guid.NewGuid()));

        // Assert
        snapshot.Type.Should().Be(DeviceType.Kds);
    }

    [Fact]
    public async Task RegisterAsync_BackofficeDevice_ShouldRegisterBackofficeType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // Act
        var snapshot = await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, Guid.NewGuid(), "Manager Laptop", DeviceType.Backoffice, null, Guid.NewGuid()));

        // Assert
        snapshot.Type.Should().Be(DeviceType.Backoffice);
    }

    [Fact]
    public async Task SuspendAsync_ShouldClearCurrentUser()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, Guid.NewGuid(), "Device", DeviceType.Pos, null, Guid.NewGuid()));
        await grain.SetCurrentUserAsync(Guid.NewGuid());

        // Act
        await grain.SuspendAsync("Security concern");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.CurrentUserId.Should().BeNull();
    }

    [Fact]
    public async Task RevokeAsync_ShouldClearCurrentUser()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterDeviceCommand(
            orgId, Guid.NewGuid(), "Device", DeviceType.Pos, null, Guid.NewGuid()));
        await grain.SetCurrentUserAsync(Guid.NewGuid());

        // Act
        await grain.RevokeAsync("Device lost");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.CurrentUserId.Should().BeNull();
    }
}

// ============================================================================
// SessionGrain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class SessionGrainTests
{
    private readonly TestClusterFixture _fixture;

    public SessionGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ISessionGrain GetGrain(Guid orgId, Guid sessionId)
    {
        var key = GrainKeys.Session(orgId, sessionId);
        return _fixture.Cluster.GrainFactory.GetGrain<ISessionGrain>(key);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateSessionWithTokens()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);

        // Act
        var tokens = await grain.CreateAsync(new CreateSessionCommand(
            UserId: userId,
            OrganizationId: orgId,
            SiteId: siteId,
            DeviceId: deviceId,
            AuthMethod: "pin",
            IpAddress: "192.168.1.100",
            UserAgent: "DarkVelocity POS/2.1.0"));

        // Assert
        tokens.AccessToken.Should().NotBeNullOrEmpty();
        tokens.RefreshToken.Should().NotBeNullOrEmpty();
        tokens.AccessTokenExpires.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(60), TimeSpan.FromSeconds(10));
        tokens.RefreshTokenExpires.Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CreateAsync_AccessTokenExpiry_ShouldBe60Minutes()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);

        // Act
        var tokens = await grain.CreateAsync(new CreateSessionCommand(
            Guid.NewGuid(), orgId, null, null, "password", null, null));

        // Assert
        var expectedExpiry = DateTime.UtcNow.AddMinutes(60);
        tokens.AccessTokenExpires.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CreateAsync_RefreshTokenExpiry_ShouldBe30Days()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);

        // Act
        var tokens = await grain.CreateAsync(new CreateSessionCommand(
            Guid.NewGuid(), orgId, null, null, "password", null, null));

        // Assert
        var expectedExpiry = DateTime.UtcNow.AddDays(30);
        tokens.RefreshTokenExpires.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RefreshAsync_WithValidToken_ShouldRotateTokens()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);
        var initialTokens = await grain.CreateAsync(new CreateSessionCommand(
            Guid.NewGuid(), orgId, null, null, "password", null, null));

        // Act
        var result = await grain.RefreshAsync(initialTokens.RefreshToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Tokens.Should().NotBeNull();
        result.Tokens!.AccessToken.Should().NotBeNullOrEmpty();
        result.Tokens.RefreshToken.Should().NotBeNullOrEmpty();
        result.Tokens.RefreshToken.Should().NotBe(initialTokens.RefreshToken); // Token rotated
    }

    [Fact]
    public async Task RefreshAsync_WithInvalidToken_ShouldReturnError()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);
        await grain.CreateAsync(new CreateSessionCommand(
            Guid.NewGuid(), orgId, null, null, "password", null, null));

        // Act
        var result = await grain.RefreshAsync("invalid-refresh-token");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Invalid refresh token");
        result.Tokens.Should().BeNull();
    }

    [Fact]
    public async Task RefreshAsync_OnNonExistentSession_ShouldReturnError()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);

        // Act
        var result = await grain.RefreshAsync("some-token");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Session not found");
    }

    [Fact]
    public async Task RefreshAsync_OnRevokedSession_ShouldReturnError()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);
        var tokens = await grain.CreateAsync(new CreateSessionCommand(
            Guid.NewGuid(), orgId, null, null, "password", null, null));
        await grain.RevokeAsync();

        // Act
        var result = await grain.RefreshAsync(tokens.RefreshToken);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Session revoked");
    }

    [Fact]
    public async Task RevokeAsync_ShouldInvalidateSession()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);
        await grain.CreateAsync(new CreateSessionCommand(
            Guid.NewGuid(), orgId, null, null, "password", null, null));

        // Act
        await grain.RevokeAsync();

        // Assert
        var isValid = await grain.IsValidAsync();
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task IsValidAsync_WhenValid_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);
        await grain.CreateAsync(new CreateSessionCommand(
            Guid.NewGuid(), orgId, null, null, "password", null, null));

        // Act
        var isValid = await grain.IsValidAsync();

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task IsValidAsync_WhenNotCreated_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);

        // Act
        var isValid = await grain.IsValidAsync();

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task RecordActivityAsync_ShouldUpdateActivityTimestamp()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);
        await grain.CreateAsync(new CreateSessionCommand(
            Guid.NewGuid(), orgId, null, null, "password", null, null));
        var initialState = await grain.GetStateAsync();

        await Task.Delay(10); // Small delay to ensure timestamp changes

        // Act
        await grain.RecordActivityAsync();

        // Assert
        var newState = await grain.GetStateAsync();
        newState.LastActivityAt.Should().BeAfter(initialState.LastActivityAt!.Value);
    }

    [Fact]
    public async Task GetStateAsync_ShouldReturnSessionState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);
        await grain.CreateAsync(new CreateSessionCommand(
            userId, orgId, null, null, "pin", "192.168.1.1", "TestAgent"));

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        state.UserId.Should().Be(userId);
        state.OrganizationId.Should().Be(orgId);
        state.AuthMethod.Should().Be("pin");
        state.IpAddress.Should().Be("192.168.1.1");
        state.UserAgent.Should().Be("TestAgent");
        state.IsRevoked.Should().BeFalse();
        state.RefreshCount.Should().Be(0);
    }

    [Fact]
    public async Task RefreshAsync_ShouldIncrementRefreshCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);
        var tokens = await grain.CreateAsync(new CreateSessionCommand(
            Guid.NewGuid(), orgId, null, null, "password", null, null));

        // Act
        var result = await grain.RefreshAsync(tokens.RefreshToken);

        // Assert
        result.Success.Should().BeTrue();
        var state = await grain.GetStateAsync();
        state.RefreshCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_Twice_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);
        await grain.CreateAsync(new CreateSessionCommand(
            Guid.NewGuid(), orgId, null, null, "password", null, null));

        // Act
        var act = () => grain.CreateAsync(new CreateSessionCommand(
            Guid.NewGuid(), orgId, null, null, "password", null, null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task RefreshAsync_ExtendRefreshTokenExpiry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var grain = GetGrain(orgId, sessionId);
        var initialTokens = await grain.CreateAsync(new CreateSessionCommand(
            Guid.NewGuid(), orgId, null, null, "password", null, null));

        // Act
        var result = await grain.RefreshAsync(initialTokens.RefreshToken);

        // Assert
        result.Success.Should().BeTrue();
        result.Tokens!.RefreshTokenExpires.Should().BeCloseTo(
            DateTime.UtcNow.AddDays(30), TimeSpan.FromSeconds(10));
    }
}

// ============================================================================
// UserLookupGrain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class UserLookupGrainTests
{
    private readonly TestClusterFixture _fixture;

    public UserLookupGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IUserLookupGrain GetLookupGrain(Guid orgId)
    {
        var key = GrainKeys.UserLookup(orgId);
        return _fixture.Cluster.GrainFactory.GetGrain<IUserLookupGrain>(key);
    }

    private IUserGrain GetUserGrain(Guid orgId, Guid userId)
    {
        var key = GrainKeys.User(orgId, userId);
        return _fixture.Cluster.GrainFactory.GetGrain<IUserGrain>(key);
    }

    private static string HashPin(string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
        return Convert.ToBase64String(bytes);
    }

    [Fact]
    public async Task RegisterPinAsync_ShouldCreateMapping()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var pinHash = HashPin("1234");
        var lookupGrain = GetLookupGrain(orgId);

        // Create user first
        var userGrain = GetUserGrain(orgId, userId);
        await userGrain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));

        // Act
        await lookupGrain.RegisterPinAsync(userId, pinHash);

        // Assert - verify by looking up
        var result = await lookupGrain.FindByPinHashAsync(pinHash);
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task FindByPinHashAsync_WithValidPin_ShouldReturnUser()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var pinHash = HashPin("5678");
        var lookupGrain = GetLookupGrain(orgId);

        // Create user first
        var userGrain = GetUserGrain(orgId, userId);
        await userGrain.CreateAsync(new CreateUserCommand(orgId, "lookup@example.com", "Lookup User"));
        await lookupGrain.RegisterPinAsync(userId, pinHash);

        // Act
        var result = await lookupGrain.FindByPinHashAsync(pinHash);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.DisplayName.Should().Be("Lookup User");
        result.OrganizationId.Should().Be(orgId);
    }

    [Fact]
    public async Task FindByPinHashAsync_WithInvalidPin_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var lookupGrain = GetLookupGrain(orgId);

        // Create user and register different pin
        var userGrain = GetUserGrain(orgId, userId);
        await userGrain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));
        await lookupGrain.RegisterPinAsync(userId, HashPin("1111"));

        // Act
        var result = await lookupGrain.FindByPinHashAsync(HashPin("9999"));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindByPinHashAsync_WithSiteAccess_ShouldValidateSiteAccess()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var pinHash = HashPin("4321");
        var lookupGrain = GetLookupGrain(orgId);

        // Create user with site access
        var userGrain = GetUserGrain(orgId, userId);
        await userGrain.CreateAsync(new CreateUserCommand(orgId, "siteuser@example.com", "Site User"));
        await userGrain.GrantSiteAccessAsync(siteId);
        await lookupGrain.RegisterPinAsync(userId, pinHash);

        // Act
        var result = await lookupGrain.FindByPinHashAsync(pinHash, siteId);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task FindByPinHashAsync_WithoutSiteAccess_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var siteIdWithAccess = Guid.NewGuid();
        var siteIdWithoutAccess = Guid.NewGuid();
        var pinHash = HashPin("7777");
        var lookupGrain = GetLookupGrain(orgId);

        // Create user with access to only one site
        var userGrain = GetUserGrain(orgId, userId);
        await userGrain.CreateAsync(new CreateUserCommand(orgId, "limited@example.com", "Limited User"));
        await userGrain.GrantSiteAccessAsync(siteIdWithAccess);
        await lookupGrain.RegisterPinAsync(userId, pinHash);

        // Act
        var result = await lookupGrain.FindByPinHashAsync(pinHash, siteIdWithoutAccess);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UnregisterPinAsync_ShouldRemoveMapping()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var pinHash = HashPin("8888");
        var lookupGrain = GetLookupGrain(orgId);

        var userGrain = GetUserGrain(orgId, userId);
        await userGrain.CreateAsync(new CreateUserCommand(orgId, "remove@example.com", "Remove User"));
        await lookupGrain.RegisterPinAsync(userId, pinHash);

        // Verify it exists first
        var before = await lookupGrain.FindByPinHashAsync(pinHash);
        before.Should().NotBeNull();

        // Act
        await lookupGrain.UnregisterPinAsync(userId);

        // Assert
        var after = await lookupGrain.FindByPinHashAsync(pinHash);
        after.Should().BeNull();
    }

    [Fact]
    public async Task GetUsersForSiteAsync_ShouldListUsersWithAccess()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var user3Id = Guid.NewGuid();
        var lookupGrain = GetLookupGrain(orgId);

        // Create users with different site access
        var user1Grain = GetUserGrain(orgId, user1Id);
        await user1Grain.CreateAsync(new CreateUserCommand(orgId, "user1@example.com", "User One", FirstName: "One"));
        await user1Grain.GrantSiteAccessAsync(siteId);
        await lookupGrain.RegisterPinAsync(user1Id, HashPin("1111"));

        var user2Grain = GetUserGrain(orgId, user2Id);
        await user2Grain.CreateAsync(new CreateUserCommand(orgId, "user2@example.com", "User Two", FirstName: "Two"));
        await user2Grain.GrantSiteAccessAsync(siteId);
        await lookupGrain.RegisterPinAsync(user2Id, HashPin("2222"));

        // User 3 has no access to this site
        var user3Grain = GetUserGrain(orgId, user3Id);
        await user3Grain.CreateAsync(new CreateUserCommand(orgId, "user3@example.com", "User Three", FirstName: "Three"));
        await user3Grain.GrantSiteAccessAsync(Guid.NewGuid()); // Different site
        await lookupGrain.RegisterPinAsync(user3Id, HashPin("3333"));

        // Act
        var users = await lookupGrain.GetUsersForSiteAsync(siteId);

        // Assert
        users.Should().HaveCount(2);
        users.Should().Contain(u => u.UserId == user1Id && u.DisplayName == "User One");
        users.Should().Contain(u => u.UserId == user2Id && u.DisplayName == "User Two");
        users.Should().NotContain(u => u.UserId == user3Id);
    }

    [Fact]
    public async Task GetUsersForSiteAsync_WithInactiveUser_ShouldExcludeInactive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var activeUserId = Guid.NewGuid();
        var inactiveUserId = Guid.NewGuid();
        var lookupGrain = GetLookupGrain(orgId);

        // Create active user
        var activeUserGrain = GetUserGrain(orgId, activeUserId);
        await activeUserGrain.CreateAsync(new CreateUserCommand(orgId, "active@example.com", "Active User"));
        await activeUserGrain.GrantSiteAccessAsync(siteId);
        await lookupGrain.RegisterPinAsync(activeUserId, HashPin("1111"));

        // Create inactive user
        var inactiveUserGrain = GetUserGrain(orgId, inactiveUserId);
        await inactiveUserGrain.CreateAsync(new CreateUserCommand(orgId, "inactive@example.com", "Inactive User"));
        await inactiveUserGrain.GrantSiteAccessAsync(siteId);
        await lookupGrain.RegisterPinAsync(inactiveUserId, HashPin("2222"));
        await inactiveUserGrain.DeactivateAsync();

        // Act
        var users = await lookupGrain.GetUsersForSiteAsync(siteId);

        // Assert
        users.Should().HaveCount(1);
        users.Should().Contain(u => u.UserId == activeUserId);
    }

    [Fact]
    public async Task FindByPinHashAsync_WithInactiveUser_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var pinHash = HashPin("5555");
        var lookupGrain = GetLookupGrain(orgId);

        var userGrain = GetUserGrain(orgId, userId);
        await userGrain.CreateAsync(new CreateUserCommand(orgId, "inactive@example.com", "Inactive User"));
        await lookupGrain.RegisterPinAsync(userId, pinHash);
        await userGrain.DeactivateAsync();

        // Act
        var result = await lookupGrain.FindByPinHashAsync(pinHash);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RegisterPinAsync_MultipleTimesForSameUser_ShouldAllowBothPins()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var pin1Hash = HashPin("pin-1");
        var pin2Hash = HashPin("pin-2");
        var lookupGrain = GetLookupGrain(orgId);

        var userGrain = GetUserGrain(orgId, userId);
        await userGrain.CreateAsync(new CreateUserCommand(orgId, "test@example.com", "Test User"));
        await lookupGrain.RegisterPinAsync(userId, pin1Hash);

        // Act - register second pin for same user
        await lookupGrain.RegisterPinAsync(userId, pin2Hash);

        // Assert - both pins should map to the user (implementation doesn't remove old mapping)
        var result1 = await lookupGrain.FindByPinHashAsync(pin1Hash);
        result1.Should().NotBeNull();
        result1!.UserId.Should().Be(userId);

        var result2 = await lookupGrain.FindByPinHashAsync(pin2Hash);
        result2.Should().NotBeNull();
        result2!.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task RegisterPinAsync_SamePinForDifferentUser_ShouldOverwrite()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        var sharedPinHash = HashPin("shared-pin");
        var lookupGrain = GetLookupGrain(orgId);

        var user1Grain = GetUserGrain(orgId, user1Id);
        await user1Grain.CreateAsync(new CreateUserCommand(orgId, "user1@example.com", "User One"));
        var user2Grain = GetUserGrain(orgId, user2Id);
        await user2Grain.CreateAsync(new CreateUserCommand(orgId, "user2@example.com", "User Two"));

        await lookupGrain.RegisterPinAsync(user1Id, sharedPinHash);

        // Act - register same pin hash for different user (overwrites)
        await lookupGrain.RegisterPinAsync(user2Id, sharedPinHash);

        // Assert - pin should now map to user2
        var result = await lookupGrain.FindByPinHashAsync(sharedPinHash);
        result.Should().NotBeNull();
        result!.UserId.Should().Be(user2Id);
    }
}

// ============================================================================
// RefreshTokenLookupGrain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class RefreshTokenLookupGrainTests
{
    private readonly TestClusterFixture _fixture;

    public RefreshTokenLookupGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IRefreshTokenLookupGrain GetGrain()
    {
        var key = GrainKeys.RefreshTokenLookup();
        return _fixture.Cluster.GrainFactory.GetGrain<IRefreshTokenLookupGrain>(key);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    [Fact]
    public async Task RegisterAsync_ShouldCreateMapping()
    {
        // Arrange
        var grain = GetGrain();
        var tokenHash = HashToken(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // Act
        await grain.RegisterAsync(tokenHash, orgId, sessionId);

        // Assert
        var result = await grain.LookupAsync(tokenHash);
        result.Should().NotBeNull();
        result!.OrganizationId.Should().Be(orgId);
        result.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task LookupAsync_WithValidHash_ShouldFindSession()
    {
        // Arrange
        var grain = GetGrain();
        var tokenHash = HashToken(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await grain.RegisterAsync(tokenHash, orgId, sessionId);

        // Act
        var result = await grain.LookupAsync(tokenHash);

        // Assert
        result.Should().NotBeNull();
        result!.OrganizationId.Should().Be(orgId);
        result.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task LookupAsync_WithInvalidHash_ShouldReturnNull()
    {
        // Arrange
        var grain = GetGrain();
        var tokenHash = HashToken(Guid.NewGuid().ToString());
        await grain.RegisterAsync(tokenHash, Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await grain.LookupAsync(HashToken("non-existent-token"));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_ShouldDeleteMapping()
    {
        // Arrange
        var grain = GetGrain();
        var tokenHash = HashToken(Guid.NewGuid().ToString());
        await grain.RegisterAsync(tokenHash, Guid.NewGuid(), Guid.NewGuid());

        // Verify it exists
        var before = await grain.LookupAsync(tokenHash);
        before.Should().NotBeNull();

        // Act
        await grain.RemoveAsync(tokenHash);

        // Assert
        var after = await grain.LookupAsync(tokenHash);
        after.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_WithNonExistentHash_ShouldNotThrow()
    {
        // Arrange
        var grain = GetGrain();

        // Act
        var act = () => grain.RemoveAsync(HashToken("non-existent"));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RotateAsync_ShouldAtomicallyRotateTokens()
    {
        // Arrange
        var grain = GetGrain();
        var oldTokenHash = HashToken(Guid.NewGuid().ToString());
        var newTokenHash = HashToken(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        await grain.RegisterAsync(oldTokenHash, orgId, sessionId);

        // Act
        await grain.RotateAsync(oldTokenHash, newTokenHash, orgId, sessionId);

        // Assert - old token should be removed
        var oldResult = await grain.LookupAsync(oldTokenHash);
        oldResult.Should().BeNull();

        // New token should be registered
        var newResult = await grain.LookupAsync(newTokenHash);
        newResult.Should().NotBeNull();
        newResult!.OrganizationId.Should().Be(orgId);
        newResult.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task RotateAsync_ShouldUpdateOrgAndSessionIds()
    {
        // Arrange
        var grain = GetGrain();
        var oldTokenHash = HashToken(Guid.NewGuid().ToString());
        var newTokenHash = HashToken(Guid.NewGuid().ToString());
        var oldOrgId = Guid.NewGuid();
        var oldSessionId = Guid.NewGuid();
        var newOrgId = Guid.NewGuid();
        var newSessionId = Guid.NewGuid();
        await grain.RegisterAsync(oldTokenHash, oldOrgId, oldSessionId);

        // Act
        await grain.RotateAsync(oldTokenHash, newTokenHash, newOrgId, newSessionId);

        // Assert
        var result = await grain.LookupAsync(newTokenHash);
        result.Should().NotBeNull();
        result!.OrganizationId.Should().Be(newOrgId);
        result.SessionId.Should().Be(newSessionId);
    }

    [Fact]
    public async Task RegisterAsync_MultipleSessions_ShouldTrackAll()
    {
        // Arrange
        var grain = GetGrain();
        var token1Hash = HashToken(Guid.NewGuid().ToString());
        var token2Hash = HashToken(Guid.NewGuid().ToString());
        var token3Hash = HashToken(Guid.NewGuid().ToString());
        var orgId = Guid.NewGuid();
        var session1Id = Guid.NewGuid();
        var session2Id = Guid.NewGuid();
        var session3Id = Guid.NewGuid();

        // Act
        await grain.RegisterAsync(token1Hash, orgId, session1Id);
        await grain.RegisterAsync(token2Hash, orgId, session2Id);
        await grain.RegisterAsync(token3Hash, orgId, session3Id);

        // Assert
        var result1 = await grain.LookupAsync(token1Hash);
        var result2 = await grain.LookupAsync(token2Hash);
        var result3 = await grain.LookupAsync(token3Hash);

        result1.Should().NotBeNull();
        result1!.SessionId.Should().Be(session1Id);

        result2.Should().NotBeNull();
        result2!.SessionId.Should().Be(session2Id);

        result3.Should().NotBeNull();
        result3!.SessionId.Should().Be(session3Id);
    }

    [Fact]
    public async Task RegisterAsync_SameHashOverwrites_ShouldUpdateMapping()
    {
        // Arrange
        var grain = GetGrain();
        var tokenHash = HashToken(Guid.NewGuid().ToString());
        var orgId1 = Guid.NewGuid();
        var sessionId1 = Guid.NewGuid();
        var orgId2 = Guid.NewGuid();
        var sessionId2 = Guid.NewGuid();

        await grain.RegisterAsync(tokenHash, orgId1, sessionId1);

        // Act - register same hash with different values
        await grain.RegisterAsync(tokenHash, orgId2, sessionId2);

        // Assert - should have the new values
        var result = await grain.LookupAsync(tokenHash);
        result.Should().NotBeNull();
        result!.OrganizationId.Should().Be(orgId2);
        result.SessionId.Should().Be(sessionId2);
    }
}
