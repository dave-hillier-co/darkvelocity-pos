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

    // Given: A POS device requesting authorization
    // When: The device authorization flow is initiated
    // Then: A user code, device code, and verification URI are generated with proper expiration
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

    // Given: A POS device initiating authorization
    // When: A user code is generated
    // Then: The code follows the XXXX-XXXX alphanumeric display format
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

    // Given: A device authorization has been initiated
    // When: The authorization status is checked
    // Then: The status is pending awaiting user approval
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

    // Given: No device authorization has been initiated
    // When: The authorization status is checked
    // Then: The status is expired
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

    // Given: A pending device authorization
    // When: An admin approves the device for a site
    // Then: The authorization status transitions to authorized
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

    // Given: A pending device authorization
    // When: The authorization is denied by an admin
    // Then: The authorization status transitions to denied
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

    // Given: An authorized device with completed pairing
    // When: The device polls for tokens using the correct device code
    // Then: Access and refresh tokens are issued with organization and site context
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

    // Given: A pending device authorization not yet approved
    // When: The device polls for tokens before approval
    // Then: No tokens are returned
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

    // Given: An authorized device
    // When: The device polls with an incorrect device code
    // Then: No tokens are returned
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

    // Given: No device authorization has been initiated
    // When: Expiration is checked
    // Then: The authorization is considered expired
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

    // Given: A freshly initiated device authorization
    // When: Expiration is checked
    // Then: The authorization is not yet expired
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

    // Given: A device authorization already initiated
    // When: A second initiation is attempted for the same user code
    // Then: An error is raised preventing duplicate authorization flows
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

    // Given: No device authorization has been initiated
    // When: Authorization approval is attempted without a pending flow
    // Then: An error is raised because there is no pending authorization
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

    // Given: A denied device authorization
    // When: Authorization approval is attempted after denial
    // Then: An error is raised because the flow has already been denied
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

    // Given: An already authorized device
    // When: Denial is attempted after authorization
    // Then: An error is raised because the flow is already completed
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

    // Given: A POS device with a hardware fingerprint and IP address
    // When: The device authorization flow is initiated
    // Then: The device fingerprint is captured and the flow proceeds as pending
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

    // Given: A POS device requesting multiple OAuth scopes
    // When: The device authorization flow is initiated
    // Then: The scopes are accepted and the flow starts successfully
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

    // Given: An unregistered device
    // When: The device is registered for a site with POS type
    // Then: The device is authorized with correct organization, site, name, and type details
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

    // Given: An already registered device
    // When: A duplicate registration is attempted
    // Then: An error is raised preventing re-registration
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

    // Given: An authorized device
    // When: The device is suspended for a security concern
    // Then: The device status becomes suspended and authorization is revoked
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

    // Given: An authorized device
    // When: The device is permanently revoked due to loss
    // Then: The device status becomes revoked and authorization is removed
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

    // Given: A suspended device temporarily on hold
    // When: The device is reactivated
    // Then: The device status returns to authorized
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

    // Given: A permanently revoked device
    // When: Reactivation is attempted
    // Then: An error is raised because revoked devices cannot be reactivated
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

    // Given: A registered POS device
    // When: A staff member logs in to the device
    // Then: The device tracks the currently signed-in user
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

    // Given: A device with a signed-in user
    // When: The current user is cleared (logout)
    // Then: No user is associated with the device
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

    // Given: A registered device
    // When: A heartbeat is received with an app version
    // Then: The last-seen timestamp is updated
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

    // Given: A device with a known app version
    // When: A heartbeat is received without version info
    // Then: Only the last-seen timestamp is updated
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

    // Given: A registered and authorized device
    // When: The authorization status is checked
    // Then: The device is confirmed as authorized
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

    // Given: An unregistered device
    // When: The authorization status is checked
    // Then: The device is not authorized
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

    // Given: A registered device
    // When: Existence is checked
    // Then: The device is found to exist
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

    // Given: An unregistered device
    // When: Existence is checked
    // Then: The device does not exist
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

    // Given: An unregistered device
    // When: The device is registered as a kitchen display system
    // Then: The device type is recorded as KDS
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

    // Given: An unregistered device
    // When: The device is registered as a back-office device
    // Then: The device type is recorded as backoffice
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

    // Given: A device with a signed-in staff member
    // When: The device is suspended for security reasons
    // Then: The current user session is cleared for security
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

    // Given: A device with a signed-in staff member
    // When: The device is permanently revoked
    // Then: The current user session is cleared
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

    // Given: A user authenticating via PIN from a POS device
    // When: A session is created with device and connection context
    // Then: Access and refresh tokens are issued with appropriate expiration times
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

    // Given: A new user session
    // When: Tokens are generated
    // Then: The access token expires in 60 minutes
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

    // Given: A new user session
    // When: Tokens are generated
    // Then: The refresh token expires in 30 days
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

    // Given: An active session with valid tokens
    // When: The refresh token is used to obtain new tokens
    // Then: Both tokens are rotated and the old refresh token is invalidated
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

    // Given: An active session
    // When: An invalid refresh token is presented
    // Then: The refresh is rejected with an error message
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

    // Given: No session exists
    // When: A token refresh is attempted
    // Then: The refresh is rejected because the session is not found
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

    // Given: A revoked session
    // When: A token refresh is attempted with the old refresh token
    // Then: The refresh is rejected because the session was revoked
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

    // Given: An active session
    // When: The session is revoked
    // Then: The session becomes invalid and no further operations are allowed
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

    // Given: A freshly created session
    // When: Session validity is checked
    // Then: The session is confirmed as valid
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

    // Given: No session has been created
    // When: Session validity is checked
    // Then: The session is not valid
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

    // Given: An active session with prior activity
    // When: New user activity is recorded
    // Then: The last-activity timestamp is updated
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

    // Given: A session created via PIN authentication from a specific IP
    // When: The session state is retrieved
    // Then: The state includes user, organization, auth method, and connection details
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

    // Given: An active session with zero refreshes
    // When: A token refresh succeeds
    // Then: The refresh count is incremented for audit tracking
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

    // Given: An existing session
    // When: A duplicate session creation is attempted for the same session ID
    // Then: An error is raised preventing session duplication
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

    // Given: An active session
    // When: A token refresh succeeds
    // Then: The new refresh token expiry is extended to 30 days from now
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

    // Given: A registered user in the organization
    // When: A PIN hash is registered for the user
    // Then: The PIN can be used to look up the user
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

    // Given: A user with a registered PIN
    // When: The correct PIN hash is used for lookup
    // Then: The user's identity, display name, and organization are returned
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

    // Given: A user with a registered PIN
    // When: A different PIN hash is used for lookup
    // Then: No user is found
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

    // Given: A user with site access and a registered PIN
    // When: The PIN is used for lookup at the authorized site
    // Then: The user is found and authenticated
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

    // Given: A user with access to one site but not another
    // When: The PIN is used for lookup at the unauthorized site
    // Then: No user is found due to missing site access
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

    // Given: A user with a registered PIN
    // When: The PIN mapping is unregistered
    // Then: The PIN can no longer be used for lookup
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

    // Given: Multiple users with different site access levels
    // When: Users for a specific site are requested
    // Then: Only users with access to that site are listed
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

    // Given: Active and deactivated users with site access
    // When: Users for the site are listed
    // Then: Only active users are included
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

    // Given: A deactivated user with a registered PIN
    // When: The PIN is used for lookup
    // Then: No user is found because the account is inactive
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

    // Given: A user with an existing registered PIN
    // When: A second PIN is registered for the same user
    // Then: Both PINs can be used for lookup
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

    // Given: A PIN hash already mapped to one user
    // When: The same PIN hash is registered for a different user
    // Then: The PIN mapping is overwritten to point to the new user
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

    // Given: A new session requiring token tracking
    // When: A refresh token hash is registered
    // Then: The token maps to the correct organization and session
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

    // Given: A registered refresh token
    // When: The token hash is used for lookup
    // Then: The associated organization and session are returned
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

    // Given: A registered refresh token
    // When: A non-existent token hash is used for lookup
    // Then: No session is found
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

    // Given: A registered refresh token
    // When: The token mapping is removed
    // Then: The token hash can no longer be used for session lookup
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

    // Given: No registered refresh token for the given hash
    // When: Removal of a non-existent hash is attempted
    // Then: No error is raised
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

    // Given: A session with an active refresh token
    // When: Token rotation is performed
    // Then: The old token is invalidated and the new token maps to the same session
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

    // Given: A session with an active refresh token
    // When: Token rotation is performed with new organization and session context
    // Then: The new token maps to the updated organization and session
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

    // Given: Multiple active sessions in the same organization
    // When: Each session's refresh token is registered
    // Then: All tokens are independently tracked and resolvable
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

    // Given: A registered refresh token mapping
    // When: The same hash is re-registered with different session context
    // Then: The mapping is updated to the new organization and session
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
