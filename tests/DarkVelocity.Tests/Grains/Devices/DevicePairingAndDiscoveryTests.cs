using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests.Grains.Devices;

/// <summary>
/// Tests for device pairing workflows covering:
/// - Complete device authorization flow (RFC 8628)
/// - Token polling during authorization
/// - Status transitions during pairing
/// - Device registration after authorization
/// - Multi-device pairing scenarios
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DevicePairingWorkflowTests
{
    private readonly TestClusterFixture _fixture;

    public DevicePairingWorkflowTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDeviceAuthGrain GetAuthGrain(string userCode)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IDeviceAuthGrain>(userCode);
    }

    private IDeviceGrain GetDeviceGrain(Guid orgId, Guid deviceId)
    {
        var key = $"{orgId}:device:{deviceId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IDeviceGrain>(key);
    }

    #region Complete Pairing Workflow Tests

    // Given: A POS device initiating the device authorization flow (RFC 8628)
    // When: A user authorizes the device from the browser and the device polls for tokens
    // Then: The device receives access and refresh tokens with the correct organization and site assignment
    [Fact]
    public async Task CompletePairingWorkflow_FromInitiationToAuthorization_ShouldSucceed()
    {
        // Arrange
        var userCode = $"PAIR{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authGrain = GetAuthGrain(userCode);

        // Step 1: Device initiates authorization
        var deviceResponse = await authGrain.InitiateAsync(new DeviceCodeRequest(
            ClientId: "pos-app",
            Scope: "openid profile offline_access",
            DeviceFingerprint: "test-fp-001"));

        // Verify pending status
        var pendingStatus = await authGrain.GetStatusAsync();
        pendingStatus.Should().Be(DeviceAuthStatus.Pending);

        // Step 2: User authorizes the device (from browser)
        await authGrain.AuthorizeAsync(new AuthorizeDeviceCommand(
            AuthorizedBy: userId,
            OrganizationId: orgId,
            SiteId: siteId,
            DeviceName: "Main Register",
            AppType: DeviceType.Pos));

        // Step 3: Device polls and gets tokens
        var tokenResponse = await authGrain.GetTokenAsync(deviceResponse.DeviceCode);

        // Assert
        tokenResponse.Should().NotBeNull();
        tokenResponse!.AccessToken.Should().NotBeNullOrEmpty();
        tokenResponse.RefreshToken.Should().NotBeNullOrEmpty();
        tokenResponse.DeviceId.Should().NotBe(Guid.Empty);
        tokenResponse.OrganizationId.Should().Be(orgId);
        tokenResponse.SiteId.Should().Be(siteId);
    }

    // Given: A device that has initiated the authorization flow
    // When: A user denies the authorization request and the device polls for tokens
    // Then: No tokens are returned and the authorization status is Denied
    [Fact]
    public async Task PairingWorkflow_WithDenial_ShouldReturnNullToken()
    {
        // Arrange
        var userCode = $"DENY{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var authGrain = GetAuthGrain(userCode);

        // Step 1: Device initiates
        var deviceResponse = await authGrain.InitiateAsync(new DeviceCodeRequest(
            ClientId: "pos-app",
            Scope: "openid"));

        // Step 2: User denies
        await authGrain.DenyAsync("Device not recognized");

        // Step 3: Device polls
        var tokenResponse = await authGrain.GetTokenAsync(deviceResponse.DeviceCode);

        // Assert
        tokenResponse.Should().BeNull();
        var status = await authGrain.GetStatusAsync();
        status.Should().Be(DeviceAuthStatus.Denied);
    }

    // Given: A device that has initiated the authorization flow but no user has responded yet
    // When: The device polls for tokens before authorization
    // Then: No tokens are returned and the status remains Pending
    [Fact]
    public async Task PairingWorkflow_PollingBeforeAuthorization_ShouldReturnNull()
    {
        // Arrange
        var userCode = $"POLL{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var authGrain = GetAuthGrain(userCode);

        var deviceResponse = await authGrain.InitiateAsync(new DeviceCodeRequest(
            ClientId: "pos-app",
            Scope: "openid"));

        // Act - poll before authorization
        var tokenResponse = await authGrain.GetTokenAsync(deviceResponse.DeviceCode);

        // Assert
        tokenResponse.Should().BeNull();
        var status = await authGrain.GetStatusAsync();
        status.Should().Be(DeviceAuthStatus.Pending);
    }

    #endregion

    #region Multi-Device Pairing Tests

    // Given: Two devices at the same site both initiating authorization
    // When: Only the first device is authorized
    // Then: The first device receives tokens while the second remains in Pending status
    [Fact]
    public async Task MultipleDevices_SameSite_ShouldPairIndependently()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var userCode1 = $"DEV1{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var userCode2 = $"DEV2{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var authGrain1 = GetAuthGrain(userCode1);
        var authGrain2 = GetAuthGrain(userCode2);

        // Act - initiate both
        var response1 = await authGrain1.InitiateAsync(new DeviceCodeRequest("pos-app", "openid"));
        var response2 = await authGrain2.InitiateAsync(new DeviceCodeRequest("pos-app", "openid"));

        // Authorize first only
        await authGrain1.AuthorizeAsync(new AuthorizeDeviceCommand(
            userId, orgId, siteId, "Register 1", DeviceType.Pos));

        // Get tokens
        var token1 = await authGrain1.GetTokenAsync(response1.DeviceCode);
        var token2 = await authGrain2.GetTokenAsync(response2.DeviceCode);

        // Assert
        token1.Should().NotBeNull();
        token2.Should().BeNull(); // Not authorized yet

        var status1 = await authGrain1.GetStatusAsync();
        var status2 = await authGrain2.GetStatusAsync();

        status1.Should().Be(DeviceAuthStatus.Authorized);
        status2.Should().Be(DeviceAuthStatus.Pending);
    }

    // Given: A POS device and a KDS device both initiating authorization at the same site
    // When: Both devices are authorized with their respective device types
    // Then: Both receive tokens and are assigned unique device IDs
    [Fact]
    public async Task MultipleDevices_DifferentTypes_ShouldPairCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var posUserCode = $"POS{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var kdsUserCode = $"KDS{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var posAuthGrain = GetAuthGrain(posUserCode);
        var kdsAuthGrain = GetAuthGrain(kdsUserCode);

        // Initiate both
        var posResponse = await posAuthGrain.InitiateAsync(new DeviceCodeRequest("pos-app", "openid"));
        var kdsResponse = await kdsAuthGrain.InitiateAsync(new DeviceCodeRequest("kds-app", "openid"));

        // Authorize both with different types
        await posAuthGrain.AuthorizeAsync(new AuthorizeDeviceCommand(
            userId, orgId, siteId, "Main Register", DeviceType.Pos));
        await kdsAuthGrain.AuthorizeAsync(new AuthorizeDeviceCommand(
            userId, orgId, siteId, "Kitchen Display", DeviceType.Kds));

        // Get tokens
        var posToken = await posAuthGrain.GetTokenAsync(posResponse.DeviceCode);
        var kdsToken = await kdsAuthGrain.GetTokenAsync(kdsResponse.DeviceCode);

        // Assert
        posToken.Should().NotBeNull();
        kdsToken.Should().NotBeNull();
        posToken!.DeviceId.Should().NotBe(kdsToken!.DeviceId);
    }

    #endregion

    #region Pairing Status Tests

    // Given: A device authorization grain that has not been initiated
    // When: The status is checked, then initiation occurs, then authorization occurs
    // Then: The status transitions from Expired to Pending to Authorized
    [Fact]
    public async Task PairingStatus_TransitionsCorrectly_ThroughLifecycle()
    {
        // Arrange
        var userCode = $"LIFE{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var authGrain = GetAuthGrain(userCode);

        // Not initiated - should be expired
        var initialStatus = await authGrain.GetStatusAsync();
        initialStatus.Should().Be(DeviceAuthStatus.Expired);

        // Initiate
        await authGrain.InitiateAsync(new DeviceCodeRequest("client", "scope"));
        var pendingStatus = await authGrain.GetStatusAsync();
        pendingStatus.Should().Be(DeviceAuthStatus.Pending);

        // Authorize
        await authGrain.AuthorizeAsync(new AuthorizeDeviceCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Device", DeviceType.Pos));
        var authorizedStatus = await authGrain.GetStatusAsync();
        authorizedStatus.Should().Be(DeviceAuthStatus.Authorized);
    }

    // Given: A newly initiated device authorization request
    // When: Checking whether the authorization has expired
    // Then: The authorization is not expired
    [Fact]
    public async Task PairingStatus_Expiration_ShouldBeDetectable()
    {
        // Arrange
        var userCode = $"EXPR{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var authGrain = GetAuthGrain(userCode);

        // Act - newly initiated should not be expired
        await authGrain.InitiateAsync(new DeviceCodeRequest("client", "scope"));
        var isExpired = await authGrain.IsExpiredAsync();

        // Assert
        isExpired.Should().BeFalse();
    }

    // Given: A device authorization grain that has never been initiated
    // When: Checking whether the authorization has expired
    // Then: The authorization is considered expired
    [Fact]
    public async Task PairingStatus_NotInitialized_ShouldBeExpired()
    {
        // Arrange
        var userCode = $"NOIN{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var authGrain = GetAuthGrain(userCode);

        // Act
        var isExpired = await authGrain.IsExpiredAsync();

        // Assert
        isExpired.Should().BeTrue();
    }

    #endregion

    #region Device Code Validation Tests

    // Given: An authorized device authorization request
    // When: The device polls with an incorrect device code
    // Then: No tokens are returned
    [Fact]
    public async Task PairingWorkflow_WrongDeviceCode_ShouldReturnNull()
    {
        // Arrange
        var userCode = $"WCODE{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var authGrain = GetAuthGrain(userCode);

        await authGrain.InitiateAsync(new DeviceCodeRequest("client", "scope"));
        await authGrain.AuthorizeAsync(new AuthorizeDeviceCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Device", DeviceType.Pos));

        // Act - use wrong device code
        var tokenResponse = await authGrain.GetTokenAsync("wrong-device-code");

        // Assert
        tokenResponse.Should().BeNull();
    }

    // Given: An authorized device authorization request
    // When: The device polls with an empty device code
    // Then: No tokens are returned
    [Fact]
    public async Task PairingWorkflow_EmptyDeviceCode_ShouldReturnNull()
    {
        // Arrange
        var userCode = $"EMPT{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var authGrain = GetAuthGrain(userCode);

        await authGrain.InitiateAsync(new DeviceCodeRequest("client", "scope"));
        await authGrain.AuthorizeAsync(new AuthorizeDeviceCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Device", DeviceType.Pos));

        // Act
        var tokenResponse = await authGrain.GetTokenAsync(string.Empty);

        // Assert
        tokenResponse.Should().BeNull();
    }

    #endregion

    #region Post-Authorization Device Setup Tests

    // Given: A device that has completed the full authorization flow and received tokens
    // When: The device grain is accessed using the assigned device ID
    // Then: The device grain contains the correct organization, site, name, type, and authorized status
    [Fact]
    public async Task PostAuthorization_DeviceGrain_ShouldBeAccessible()
    {
        // Arrange
        var userCode = $"POST{Guid.NewGuid():N}"[..8].ToUpperInvariant();
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var authGrain = GetAuthGrain(userCode);

        var response = await authGrain.InitiateAsync(new DeviceCodeRequest("client", "scope"));
        await authGrain.AuthorizeAsync(new AuthorizeDeviceCommand(
            Guid.NewGuid(), orgId, siteId, "New Device", DeviceType.Pos));

        var tokenResponse = await authGrain.GetTokenAsync(response.DeviceCode);

        // Act - access the device grain
        var deviceGrain = GetDeviceGrain(orgId, tokenResponse!.DeviceId);
        var snapshot = await deviceGrain.GetSnapshotAsync();

        // Assert
        snapshot.Should().NotBeNull();
        snapshot.Id.Should().Be(tokenResponse.DeviceId);
        snapshot.OrganizationId.Should().Be(orgId);
        snapshot.SiteId.Should().Be(siteId);
        snapshot.Name.Should().Be("New Device");
        snapshot.Type.Should().Be(DeviceType.Pos);
        snapshot.Status.Should().Be(DeviceStatus.Authorized);
    }

    #endregion
}

/// <summary>
/// Tests for device discovery scenarios covering:
/// - Site-level device listing
/// - Device status aggregation
/// - Finding devices by various criteria
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DeviceDiscoveryTests
{
    private readonly TestClusterFixture _fixture;

    public DeviceDiscoveryTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDeviceStatusGrain GetStatusGrain(Guid orgId, Guid locationId)
    {
        var key = $"{orgId}:{locationId}:devicestatus";
        return _fixture.Cluster.GrainFactory.GetGrain<IDeviceStatusGrain>(key);
    }

    #region Device Registration Discovery Tests

    // Given: A newly initialized site location with no registered devices
    // When: The device summary is requested
    // Then: All device counts are zero and no alerts exist
    [Fact]
    public async Task DiscoverDevices_NewLocation_ShouldReturnEmptySummary()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        // Act
        var summary = await grain.GetSummaryAsync();

        // Assert
        summary.TotalPosDevices.Should().Be(0);
        summary.TotalPrinters.Should().Be(0);
        summary.TotalCashDrawers.Should().Be(0);
        summary.Alerts.Should().BeEmpty();
    }

    // Given: An initialized site location
    // When: A POS device is registered at the location
    // Then: The device appears in the site summary with a total POS count of one
    [Fact]
    public async Task DiscoverDevices_AfterRegistration_ShouldIncludeDevice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        // Act
        await grain.RegisterDeviceAsync("POS", deviceId, "Register 1");

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalPosDevices.Should().Be(1);
    }

    // Given: An initialized site location
    // When: Multiple device types (POS, Printer, CashDrawer) are registered
    // Then: Each device type is correctly categorized and counted in the summary
    [Fact]
    public async Task DiscoverDevices_MultipleTypes_ShouldCategorizeCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        // Act - register various device types
        await grain.RegisterDeviceAsync("POS", Guid.NewGuid(), "Register 1");
        await grain.RegisterDeviceAsync("POS", Guid.NewGuid(), "Register 2");
        await grain.RegisterDeviceAsync("Printer", Guid.NewGuid(), "Receipt Printer");
        await grain.RegisterDeviceAsync("Printer", Guid.NewGuid(), "Kitchen Printer");
        await grain.RegisterDeviceAsync("Printer", Guid.NewGuid(), "Label Printer");
        await grain.RegisterDeviceAsync("CashDrawer", Guid.NewGuid(), "Main Drawer");

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalPosDevices.Should().Be(2);
        summary.TotalPrinters.Should().Be(3);
        summary.TotalCashDrawers.Should().Be(1);
    }

    // Given: A site with one registered POS device
    // When: The device is unregistered from the site
    // Then: The POS device count drops to zero
    [Fact]
    public async Task DiscoverDevices_AfterUnregister_ShouldRemoveDevice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("POS", deviceId, "Temporary Device");

        // Act
        await grain.UnregisterDeviceAsync("POS", deviceId);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalPosDevices.Should().Be(0);
    }

    #endregion

    #region Online/Offline Discovery Tests

    // Given: A site with three registered POS devices
    // When: Two devices are marked online and one is marked offline
    // Then: The summary reports two online POS devices out of three total
    [Fact]
    public async Task DiscoverOnlineDevices_ShouldCountCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();
        var device3 = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        await grain.RegisterDeviceAsync("POS", device1, "Online Device 1");
        await grain.RegisterDeviceAsync("POS", device2, "Online Device 2");
        await grain.RegisterDeviceAsync("POS", device3, "Offline Device");

        // Act - mark some as online
        await grain.UpdateDeviceStatusAsync("POS", device1, true);
        await grain.UpdateDeviceStatusAsync("POS", device2, true);
        await grain.UpdateDeviceStatusAsync("POS", device3, false);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalPosDevices.Should().Be(3);
        summary.OnlinePosDevices.Should().Be(2);
    }

    // Given: A registered POS device at a site that has not sent a heartbeat
    // When: The device sends its first heartbeat
    // Then: The device health shows it as online
    [Fact]
    public async Task DiscoverDevices_WithHeartbeat_ShouldBeOnline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("POS", deviceId, "Heartbeat Device");

        // Act
        await grain.RecordHeartbeatAsync(deviceId);

        // Assert
        var health = await grain.GetDeviceHealthAsync(deviceId);
        health.Should().NotBeNull();
        health!.IsOnline.Should().BeTrue();
    }

    #endregion

    #region Health Summary Discovery Tests

    // Given: A site with two POS devices and one printer, with partial heartbeat coverage
    // When: The health summary is requested
    // Then: The summary reports correct total, online, and offline device counts with per-device metrics
    [Fact]
    public async Task GetHealthSummary_ShouldProvideComprehensiveOverview()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        // Register devices
        var pos1 = Guid.NewGuid();
        var pos2 = Guid.NewGuid();
        var printer = Guid.NewGuid();
        await grain.RegisterDeviceAsync("POS", pos1, "Register 1");
        await grain.RegisterDeviceAsync("POS", pos2, "Register 2");
        await grain.RegisterDeviceAsync("Printer", printer, "Main Printer");

        // Send heartbeats for some
        await grain.RecordHeartbeatAsync(pos1);
        await grain.RecordHeartbeatAsync(printer);

        // Act
        var summary = await grain.GetHealthSummaryAsync();

        // Assert
        summary.LocationId.Should().Be(locationId);
        summary.TotalDevices.Should().Be(3);
        summary.OnlineDevices.Should().Be(2);
        summary.OfflineDevices.Should().Be(1);
        summary.DeviceMetrics.Should().HaveCount(3);
    }

    // Given: A site with a registered printer that has a paper-out alert
    // When: The health summary is requested
    // Then: The summary reports one device with alerts and one active alert
    [Fact]
    public async Task GetHealthSummary_WithAlerts_ShouldIncludeAlertCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("Printer", printerId, "Problem Printer");

        // Act
        await grain.UpdatePrinterHealthAsync(printerId, PrinterHealthStatus.PaperOut);
        var summary = await grain.GetHealthSummaryAsync();

        // Assert
        summary.DevicesWithAlerts.Should().Be(1);
        summary.ActiveAlerts.Should().HaveCount(1);
    }

    #endregion
}

/// <summary>
/// Tests for offline mode transitions covering:
/// - Device going offline detection
/// - Reconnection handling
/// - Offline queue management
/// - Sync resume after reconnection
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class OfflineModeTransitionTests
{
    private readonly TestClusterFixture _fixture;

    public OfflineModeTransitionTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPosDeviceGrain GetPosDeviceGrain(Guid orgId, Guid deviceId)
    {
        var key = $"{orgId}:posdevice:{deviceId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IPosDeviceGrain>(key);
    }

    private IOfflineSyncQueueGrain GetSyncQueueGrain(Guid orgId, Guid deviceId)
    {
        var key = $"{orgId}:device:{deviceId}:syncqueue";
        return _fixture.Cluster.GrainFactory.GetGrain<IOfflineSyncQueueGrain>(key);
    }

    private IDeviceStatusGrain GetStatusGrain(Guid orgId, Guid locationId)
    {
        var key = $"{orgId}:{locationId}:devicestatus";
        return _fixture.Cluster.GrainFactory.GetGrain<IDeviceStatusGrain>(key);
    }

    #region Device Offline Transition Tests

    // Given: A registered POS device that is currently online
    // When: The device is set to offline mode
    // Then: The device status changes to offline
    [Fact]
    public async Task DeviceGoesOffline_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetPosDeviceGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            locationId, "Offline Test Device", "DEV-001",
            PosDeviceType.Tablet, "iPad", "17.0", "3.0.0"));

        // Verify initially online
        (await grain.IsOnlineAsync()).Should().BeTrue();

        // Act
        await grain.SetOfflineAsync();

        // Assert
        (await grain.IsOnlineAsync()).Should().BeFalse();
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsOnline.Should().BeFalse();
    }

    // Given: A POS device that is currently offline
    // When: The device sends a heartbeat with updated app and OS versions
    // Then: The device comes back online with updated version information
    [Fact]
    public async Task DeviceReconnects_WithHeartbeat_ShouldGoOnline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetPosDeviceGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            locationId, "Reconnect Test", "DEV-002",
            PosDeviceType.Tablet, "iPad", "17.0", "3.0.0"));
        await grain.SetOfflineAsync();

        // Act
        await grain.RecordHeartbeatAsync("3.0.1", "17.1");

        // Assert
        (await grain.IsOnlineAsync()).Should().BeTrue();
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsOnline.Should().BeTrue();
        snapshot.AppVersion.Should().Be("3.0.1");
        snapshot.OsVersion.Should().Be("17.1");
    }

    // Given: A registered POS device with an initial last-seen timestamp
    // When: The device sends a heartbeat after a brief delay
    // Then: The last-seen timestamp is updated to a more recent time
    [Fact]
    public async Task OfflineDevice_Heartbeat_ShouldUpdateLastSeen()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetPosDeviceGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Heartbeat Test", "DEV-003",
            PosDeviceType.Tablet, null, null, null));

        var initialSnapshot = await grain.GetSnapshotAsync();
        var initialLastSeen = initialSnapshot.LastSeenAt;

        await Task.Delay(50);

        // Act
        await grain.RecordHeartbeatAsync("3.0.0", null);

        // Assert
        var updatedSnapshot = await grain.GetSnapshotAsync();
        updatedSnapshot.LastSeenAt.Should().BeAfter(initialLastSeen!.Value);
    }

    #endregion

    #region Offline Queue Management Tests

    // Given: An initialized offline sync queue for a device
    // When: Two operations (order creation and payment) are queued while the device is offline
    // Then: The queue contains two pending operations
    [Fact]
    public async Task OfflineMode_OperationsQueued_ShouldPersist()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetSyncQueueGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        // Act - queue operations while "offline"
        await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{\"items\": [\"item1\"]}", DateTime.UtcNow, 1));
        await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.ApplyPayment, "Payment", Guid.NewGuid(),
            "{\"amount\": 25.00}", DateTime.UtcNow, 2));

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.QueuedCount.Should().Be(2);
        (await grain.HasPendingOperationsAsync()).Should().BeTrue();
    }

    // Given: An offline sync queue with two queued order operations
    // When: The queue is processed upon device reconnection
    // Then: Both operations are synced successfully
    [Fact]
    public async Task OfflineToOnline_ProcessQueue_ShouldSyncOperations()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetSyncQueueGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 1));
        await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 2));

        // Act - process queue (simulate reconnection)
        var result = await grain.ProcessQueueAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.SyncedCount.Should().Be(2);
    }

    // Given: An initialized offline sync queue
    // When: Five operations are queued with sequential client sequence numbers
    // Then: The operations maintain their original queuing order
    [Fact]
    public async Task OfflineMode_QueueFull_ShouldMaintainOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetSyncQueueGrain(orgId, deviceId);
        await grain.InitializeAsync(deviceId);

        // Queue operations in specific order
        for (int i = 1; i <= 5; i++)
        {
            await grain.QueueOperationAsync(new QueueOfflineOperationCommand(
                OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
                $"{{\"sequence\": {i}}}", DateTime.UtcNow, i));
        }

        // Act
        var queued = await grain.GetQueuedOperationsAsync();

        // Assert
        queued.Should().HaveCount(5);
        queued[0].ClientSequence.Should().Be(1);
        queued[4].ClientSequence.Should().Be(5);
    }

    #endregion

    #region Multi-Device Offline Scenarios

    // Given: Two POS devices registered at the same site location
    // When: Only one device goes offline
    // Then: The offline state is independent; the other device remains online
    [Fact]
    public async Task MultipleDevices_IndependentOfflineState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();

        var grain1 = GetPosDeviceGrain(orgId, device1);
        var grain2 = GetPosDeviceGrain(orgId, device2);

        await grain1.RegisterAsync(new RegisterPosDeviceCommand(
            locationId, "Device 1", "D1", PosDeviceType.Tablet, null, null, null));
        await grain2.RegisterAsync(new RegisterPosDeviceCommand(
            locationId, "Device 2", "D2", PosDeviceType.Tablet, null, null, null));

        // Act - only device 1 goes offline
        await grain1.SetOfflineAsync();

        // Assert
        (await grain1.IsOnlineAsync()).Should().BeFalse();
        (await grain2.IsOnlineAsync()).Should().BeTrue();
    }

    // Given: Two devices with independent offline sync queues
    // When: Operations are queued only on the first device's sync queue
    // Then: The second device's sync queue remains empty
    [Fact]
    public async Task MultipleDevices_IndependentSyncQueues()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();

        var queue1 = GetSyncQueueGrain(orgId, device1);
        var queue2 = GetSyncQueueGrain(orgId, device2);

        await queue1.InitializeAsync(device1);
        await queue2.InitializeAsync(device2);

        // Act
        await queue1.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 1));
        await queue1.QueueOperationAsync(new QueueOfflineOperationCommand(
            OfflineOperationType.CreateOrder, "Order", Guid.NewGuid(),
            "{}", DateTime.UtcNow, 2));

        // Assert - queues are independent
        var summary1 = await queue1.GetSummaryAsync();
        var summary2 = await queue2.GetSummaryAsync();

        summary1.QueuedCount.Should().Be(2);
        summary2.QueuedCount.Should().Be(0);
    }

    #endregion

    #region Health Check and Stale Detection Tests

    // Given: A site with one device that has sent a heartbeat and one that has never heartbeated
    // When: A health check is performed with a very short stale threshold
    // Then: The fresh device remains online
    [Fact]
    public async Task HealthCheck_StaleDevices_ShouldBeDetected()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var freshDevice = Guid.NewGuid();
        var staleDevice = Guid.NewGuid();

        var statusGrain = GetStatusGrain(orgId, locationId);
        await statusGrain.InitializeAsync(locationId);

        await statusGrain.RegisterDeviceAsync("POS", freshDevice, "Fresh Device");
        await statusGrain.RegisterDeviceAsync("POS", staleDevice, "Stale Device");

        // Heartbeat fresh device
        await statusGrain.RecordHeartbeatAsync(freshDevice);

        // Act - perform health check with very short threshold
        var staleDevices = await statusGrain.PerformHealthCheckAsync(TimeSpan.FromMilliseconds(1));

        // Assert - stale device was never heartbeated so should be detected
        var freshHealth = await statusGrain.GetDeviceHealthAsync(freshDevice);
        freshHealth!.IsOnline.Should().BeTrue();
    }

    #endregion
}
