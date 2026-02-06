using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests.Grains.Devices;

/// <summary>
/// Tests for device group management at the location/site level covering:
/// - Site-level device aggregation
/// - Device registration/unregistration tracking
/// - Status monitoring for device groups
/// - Alert management for device groups
/// - Cross-device operations at site level
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DeviceGroupManagementTests
{
    private readonly TestClusterFixture _fixture;

    public DeviceGroupManagementTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDeviceStatusGrain GetStatusGrain(Guid orgId, Guid locationId)
    {
        var key = $"{orgId}:{locationId}:devicestatus";
        return _fixture.Cluster.GrainFactory.GetGrain<IDeviceStatusGrain>(key);
    }

    #region Site-Level Device Management Tests

    // Given: An initialized site location
    // When: POS terminals, receipt printers, kitchen printers, and cash drawers are registered
    // Then: The site summary correctly counts devices by type
    [Fact]
    public async Task SiteDevices_RegisterMultipleTypes_ShouldTrackAllTypes()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        // Act - register a complete site setup
        var pos1 = Guid.NewGuid();
        var pos2 = Guid.NewGuid();
        var receiptPrinter = Guid.NewGuid();
        var kitchenPrinter = Guid.NewGuid();
        var drawer1 = Guid.NewGuid();
        var drawer2 = Guid.NewGuid();

        await grain.RegisterDeviceAsync("POS", pos1, "Main Register");
        await grain.RegisterDeviceAsync("POS", pos2, "Bar Register");
        await grain.RegisterDeviceAsync("Printer", receiptPrinter, "Receipt Printer");
        await grain.RegisterDeviceAsync("Printer", kitchenPrinter, "Kitchen Printer");
        await grain.RegisterDeviceAsync("CashDrawer", drawer1, "Main Drawer");
        await grain.RegisterDeviceAsync("CashDrawer", drawer2, "Bar Drawer");

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalPosDevices.Should().Be(2);
        summary.TotalPrinters.Should().Be(2);
        summary.TotalCashDrawers.Should().Be(2);
    }

    // Given: A site with five registered POS devices
    // When: Three devices report as online and two report as offline
    // Then: The site summary shows three online POS devices out of five total
    [Fact]
    public async Task SiteDevices_UpdateStatus_ShouldAggregateOnlineCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        var devices = Enumerable.Range(1, 5).Select(_ => Guid.NewGuid()).ToList();
        foreach (var (device, i) in devices.Select((d, i) => (d, i)))
        {
            await grain.RegisterDeviceAsync("POS", device, $"Register {i + 1}");
        }

        // Act - mark 3 devices online
        await grain.UpdateDeviceStatusAsync("POS", devices[0], true);
        await grain.UpdateDeviceStatusAsync("POS", devices[1], true);
        await grain.UpdateDeviceStatusAsync("POS", devices[2], true);
        await grain.UpdateDeviceStatusAsync("POS", devices[3], false);
        await grain.UpdateDeviceStatusAsync("POS", devices[4], false);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalPosDevices.Should().Be(5);
        summary.OnlinePosDevices.Should().Be(3);
    }

    // Given: A site with three registered POS devices
    // When: One device is unregistered from the site
    // Then: The site total POS device count decreases to two
    [Fact]
    public async Task SiteDevices_UnregisterDevice_ShouldUpdateCounts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();
        var device3 = Guid.NewGuid();

        await grain.RegisterDeviceAsync("POS", device1, "Temp 1");
        await grain.RegisterDeviceAsync("POS", device2, "Temp 2");
        await grain.RegisterDeviceAsync("POS", device3, "Temp 3");

        // Act
        await grain.UnregisterDeviceAsync("POS", device2);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalPosDevices.Should().Be(2);
    }

    #endregion

    #region Alert Management Tests

    // Given: A registered printer at an initialized site
    // When: An offline alert is raised for the printer
    // Then: The site summary includes the alert with the correct device and alert type
    [Fact]
    public async Task SiteAlerts_AddAlert_ShouldBeTracked()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("Printer", deviceId, "Problem Printer");

        // Act
        await grain.AddAlertAsync(new DeviceAlert(
            DeviceId: deviceId,
            DeviceType: "Printer",
            DeviceName: "Problem Printer",
            AlertType: "Offline",
            Message: "Printer has been offline for 10 minutes",
            Timestamp: DateTime.UtcNow));

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.Alerts.Should().HaveCount(1);
        summary.Alerts[0].DeviceId.Should().Be(deviceId);
        summary.Alerts[0].AlertType.Should().Be("Offline");
    }

    // Given: A printer with an active paper-out alert at a site
    // When: The alert is cleared for the printer
    // Then: The site summary has no remaining alerts
    [Fact]
    public async Task SiteAlerts_ClearAlert_ShouldRemoveAlert()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("Printer", deviceId, "Alert Printer");

        await grain.AddAlertAsync(new DeviceAlert(
            deviceId, "Printer", "Alert Printer", "PaperOut",
            "Paper tray empty", DateTime.UtcNow));

        // Verify alert exists
        var beforeSummary = await grain.GetSummaryAsync();
        beforeSummary.Alerts.Should().HaveCount(1);

        // Act
        await grain.ClearAlertAsync(deviceId);

        // Assert
        var afterSummary = await grain.GetSummaryAsync();
        afterSummary.Alerts.Should().BeEmpty();
    }

    // Given: A site with two printers and one POS terminal registered
    // When: Alerts are raised for paper-low, offline, and low-battery across the three devices
    // Then: The site summary tracks all three alerts
    [Fact]
    public async Task SiteAlerts_MultipleAlerts_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        var printer1 = Guid.NewGuid();
        var printer2 = Guid.NewGuid();
        var pos = Guid.NewGuid();

        await grain.RegisterDeviceAsync("Printer", printer1, "Printer 1");
        await grain.RegisterDeviceAsync("Printer", printer2, "Printer 2");
        await grain.RegisterDeviceAsync("POS", pos, "POS Terminal");

        // Act - add multiple alerts
        await grain.AddAlertAsync(new DeviceAlert(
            printer1, "Printer", "Printer 1", "PaperLow",
            "Paper level below 20%", DateTime.UtcNow));
        await grain.AddAlertAsync(new DeviceAlert(
            printer2, "Printer", "Printer 2", "Offline",
            "Printer offline", DateTime.UtcNow));
        await grain.AddAlertAsync(new DeviceAlert(
            pos, "POS", "POS Terminal", "LowBattery",
            "Battery below 15%", DateTime.UtcNow));

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.Alerts.Should().HaveCount(3);
    }

    #endregion

    #region Printer Health Management Tests

    // Given: A registered receipt printer at a site
    // When: The printer reports a paper-low status with 15% paper level
    // Then: A PaperLow alert is created for the printer
    [Fact]
    public async Task PrinterHealth_StatusUpdates_ShouldCreateAppropriateAlerts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("Printer", printerId, "Receipt Printer");

        // Act - update printer health to problematic status
        await grain.UpdatePrinterHealthAsync(printerId, PrinterHealthStatus.PaperLow, paperLevel: 15);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.Alerts.Should().ContainSingle(a =>
            a.DeviceId == printerId &&
            a.AlertType == "PaperLow");
    }

    // Given: A printer with an active paper-out alert at a site
    // When: The printer reports a Ready status
    // Then: All alerts for the printer are cleared
    [Fact]
    public async Task PrinterHealth_ReadyStatus_ShouldClearAlerts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("Printer", printerId, "Receipt Printer");

        // Create an alert
        await grain.UpdatePrinterHealthAsync(printerId, PrinterHealthStatus.PaperOut);
        var beforeSummary = await grain.GetSummaryAsync();
        beforeSummary.Alerts.Should().NotBeEmpty();

        // Act - printer is now ready
        await grain.UpdatePrinterHealthAsync(printerId, PrinterHealthStatus.Ready);

        // Assert
        var afterSummary = await grain.GetSummaryAsync();
        afterSummary.Alerts.Should().BeEmpty();
    }

    // Given: A site with three printers (receipt, kitchen, and label)
    // When: Each printer reports a different health status (Ready, PaperOut, Error)
    // Then: Each printer's health status is tracked independently
    [Fact]
    public async Task PrinterHealth_MultiplePrinterStatuses_ShouldTrackIndependently()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        var printer1 = Guid.NewGuid();
        var printer2 = Guid.NewGuid();
        var printer3 = Guid.NewGuid();

        await grain.RegisterDeviceAsync("Printer", printer1, "Receipt Printer");
        await grain.RegisterDeviceAsync("Printer", printer2, "Kitchen Printer");
        await grain.RegisterDeviceAsync("Printer", printer3, "Label Printer");

        // Act - set different statuses
        await grain.UpdatePrinterHealthAsync(printer1, PrinterHealthStatus.Ready);
        await grain.UpdatePrinterHealthAsync(printer2, PrinterHealthStatus.PaperOut);
        await grain.UpdatePrinterHealthAsync(printer3, PrinterHealthStatus.Error);

        // Assert
        var health1 = await grain.GetDeviceHealthAsync(printer1);
        var health2 = await grain.GetDeviceHealthAsync(printer2);
        var health3 = await grain.GetDeviceHealthAsync(printer3);

        health1!.PrinterStatus.Should().Be(PrinterHealthStatus.Ready);
        health2!.PrinterStatus.Should().Be(PrinterHealthStatus.PaperOut);
        health3!.PrinterStatus.Should().Be(PrinterHealthStatus.Error);
    }

    #endregion

    #region Cross-Location Isolation Tests

    // Given: Two different site locations within the same organization
    // When: Devices are registered at each location independently
    // Then: Each location's device counts are isolated from the other
    [Fact]
    public async Task MultipleLocations_ShouldBeIsolated()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var location1 = Guid.NewGuid();
        var location2 = Guid.NewGuid();

        var grain1 = GetStatusGrain(orgId, location1);
        var grain2 = GetStatusGrain(orgId, location2);

        await grain1.InitializeAsync(location1);
        await grain2.InitializeAsync(location2);

        // Act - register devices in each location
        await grain1.RegisterDeviceAsync("POS", Guid.NewGuid(), "L1 Register 1");
        await grain1.RegisterDeviceAsync("POS", Guid.NewGuid(), "L1 Register 2");

        await grain2.RegisterDeviceAsync("POS", Guid.NewGuid(), "L2 Register 1");
        await grain2.RegisterDeviceAsync("POS", Guid.NewGuid(), "L2 Register 2");
        await grain2.RegisterDeviceAsync("POS", Guid.NewGuid(), "L2 Register 3");

        // Assert
        var summary1 = await grain1.GetSummaryAsync();
        var summary2 = await grain2.GetSummaryAsync();

        summary1.TotalPosDevices.Should().Be(2);
        summary2.TotalPosDevices.Should().Be(3);
    }

    // Given: Two different organizations with the same location ID
    // When: Devices are registered under each organization
    // Then: Each organization's device counts are isolated by tenant boundary
    [Fact]
    public async Task MultipleOrganizations_ShouldBeIsolated()
    {
        // Arrange
        var org1 = Guid.NewGuid();
        var org2 = Guid.NewGuid();
        var locationId = Guid.NewGuid(); // Same location ID, different orgs

        var grain1 = GetStatusGrain(org1, locationId);
        var grain2 = GetStatusGrain(org2, locationId);

        await grain1.InitializeAsync(locationId);
        await grain2.InitializeAsync(locationId);

        // Act
        await grain1.RegisterDeviceAsync("POS", Guid.NewGuid(), "Org1 Device");
        await grain2.RegisterDeviceAsync("POS", Guid.NewGuid(), "Org2 Device 1");
        await grain2.RegisterDeviceAsync("POS", Guid.NewGuid(), "Org2 Device 2");

        // Assert
        var summary1 = await grain1.GetSummaryAsync();
        var summary2 = await grain2.GetSummaryAsync();

        summary1.TotalPosDevices.Should().Be(1);
        summary2.TotalPosDevices.Should().Be(2);
    }

    #endregion

    #region Connection Quality Tests

    // Given: A registered POS device at a site with excellent signal and very low latency
    // When: The device sends a heartbeat with those metrics
    // Then: The device connection quality is rated as Excellent
    [Fact]
    public async Task ConnectionQuality_CalculatedCorrectly_ExcellentNetwork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("POS", deviceId, "High Quality Device");

        // Act - report excellent metrics
        await grain.RecordHeartbeatAsync(deviceId, new UpdateDeviceHealthCommand(
            DeviceId: deviceId,
            SignalStrength: -40, // Excellent
            LatencyMs: 15));     // Very low

        // Assert
        var health = await grain.GetDeviceHealthAsync(deviceId);
        health.Should().NotBeNull();
        health!.ConnectionQuality.Should().Be(ConnectionQuality.Excellent);
    }

    // Given: A registered POS device at a site with weak signal and high latency
    // When: The device sends a heartbeat with those poor metrics
    // Then: The device connection quality is rated as Poor
    [Fact]
    public async Task ConnectionQuality_CalculatedCorrectly_PoorNetwork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("POS", deviceId, "Poor Network Device");

        // Act - report poor metrics
        await grain.RecordHeartbeatAsync(deviceId, new UpdateDeviceHealthCommand(
            DeviceId: deviceId,
            SignalStrength: -80, // Poor
            LatencyMs: 800));    // High

        // Assert
        var health = await grain.GetDeviceHealthAsync(deviceId);
        health.Should().NotBeNull();
        health!.ConnectionQuality.Should().Be(ConnectionQuality.Poor);
    }

    // Given: A site with five registered POS devices all sending heartbeats with good connection metrics
    // When: The site health summary is requested
    // Then: The overall site connection quality is rated as Excellent
    [Fact]
    public async Task OverallConnectionQuality_ReflectsSiteHealth()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        // Register multiple devices
        var devices = Enumerable.Range(1, 5).Select(_ => Guid.NewGuid()).ToList();
        foreach (var (device, i) in devices.Select((d, i) => (d, i)))
        {
            await grain.RegisterDeviceAsync("POS", device, $"Device {i}");
        }

        // Act - all devices online with good connection
        foreach (var device in devices)
        {
            await grain.RecordHeartbeatAsync(device, new UpdateDeviceHealthCommand(
                DeviceId: device,
                SignalStrength: -45,
                LatencyMs: 25));
        }

        // Assert
        var summary = await grain.GetHealthSummaryAsync();
        summary.OverallConnectionQuality.Should().Be(ConnectionQuality.Excellent);
    }

    #endregion

    #region Heartbeat Aggregation Tests

    // Given: A site with three registered POS devices
    // When: All three devices send heartbeats
    // Then: The health summary reports all three devices as online with zero offline
    [Fact]
    public async Task SiteHeartbeats_AllDevicesReporting_ShouldShowAllOnline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        var devices = Enumerable.Range(1, 3).Select(_ => Guid.NewGuid()).ToList();
        foreach (var (device, i) in devices.Select((d, i) => (d, i)))
        {
            await grain.RegisterDeviceAsync("POS", device, $"Register {i}");
        }

        // Act - all devices send heartbeats
        foreach (var device in devices)
        {
            await grain.RecordHeartbeatAsync(device);
        }

        // Assert
        var summary = await grain.GetHealthSummaryAsync();
        summary.OnlineDevices.Should().Be(3);
        summary.OfflineDevices.Should().Be(0);
    }

    // Given: A site with two registered POS devices
    // When: Only one device sends a heartbeat while the other remains silent
    // Then: The health summary reports one device online and one device offline
    [Fact]
    public async Task SiteHeartbeats_PartialReporting_ShouldShowMixedStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetStatusGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        var onlineDevice = Guid.NewGuid();
        var offlineDevice = Guid.NewGuid();

        await grain.RegisterDeviceAsync("POS", onlineDevice, "Online Register");
        await grain.RegisterDeviceAsync("POS", offlineDevice, "Offline Register");

        // Act - only one device sends heartbeat
        await grain.RecordHeartbeatAsync(onlineDevice);
        // offlineDevice never sends heartbeat

        // Assert
        var summary = await grain.GetHealthSummaryAsync();
        summary.OnlineDevices.Should().Be(1);
        summary.OfflineDevices.Should().Be(1);
    }

    #endregion
}

/// <summary>
/// Additional edge case tests for hardware grain operations
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class HardwareGrainEdgeCaseTests
{
    private readonly TestClusterFixture _fixture;

    public HardwareGrainEdgeCaseTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPosDeviceGrain GetPosDeviceGrain(Guid orgId, Guid deviceId)
    {
        var key = $"{orgId}:posdevice:{deviceId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IPosDeviceGrain>(key);
    }

    private IPrinterGrain GetPrinterGrain(Guid orgId, Guid printerId)
    {
        var key = $"{orgId}:printer:{printerId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IPrinterGrain>(key);
    }

    private ICashDrawerHardwareGrain GetCashDrawerGrain(Guid orgId, Guid drawerId)
    {
        var key = $"{orgId}:cashdrawerhw:{drawerId}";
        return _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerHardwareGrain>(key);
    }

    #region POS Device Edge Cases

    // Given: A registered POS device with a configured default printer and cash drawer
    // When: The device is deactivated and then reactivated via update
    // Then: The device retains its printer and cash drawer configuration after reactivation
    [Fact]
    public async Task PosDevice_DeactivateReactivate_ShouldMaintainConfig()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetPosDeviceGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Config Test", "CFG-001",
            PosDeviceType.Tablet, "iPad", "17.0", "3.0.0"));

        await grain.UpdateAsync(new UpdatePosDeviceCommand(
            null, null, null, null,
            DefaultPrinterId: printerId,
            DefaultCashDrawerId: drawerId,
            AutoPrintReceipts: false,
            OpenDrawerOnCash: false,
            IsActive: null));

        // Act
        await grain.DeactivateAsync();
        var deactivatedSnapshot = await grain.GetSnapshotAsync();

        // Simulate reactivation by updating
        await grain.UpdateAsync(new UpdatePosDeviceCommand(
            null, null, null, null, null, null, null, null,
            IsActive: true));
        var reactivatedSnapshot = await grain.GetSnapshotAsync();

        // Assert
        deactivatedSnapshot.IsActive.Should().BeFalse();
        reactivatedSnapshot.IsActive.Should().BeTrue();
        reactivatedSnapshot.DefaultPrinterId.Should().Be(printerId);
        reactivatedSnapshot.DefaultCashDrawerId.Should().Be(drawerId);
        reactivatedSnapshot.AutoPrintReceipts.Should().BeFalse();
        reactivatedSnapshot.OpenDrawerOnCash.Should().BeFalse();
    }

    // Given: A registered POS device with a specific model and OS version
    // When: Only the device name is updated via a partial update
    // Then: The name changes while model and OS version remain unchanged
    [Fact]
    public async Task PosDevice_PartialUpdate_ShouldOnlyChangeSpecifiedFields()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetPosDeviceGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Partial Update Test", "PART-001",
            PosDeviceType.Tablet, "iPad Pro", "17.0", "3.0.0"));

        // Act - update only name
        await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: "New Name",
            Model: null, OsVersion: null, AppVersion: null,
            DefaultPrinterId: null, DefaultCashDrawerId: null,
            AutoPrintReceipts: null, OpenDrawerOnCash: null, IsActive: null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Name.Should().Be("New Name");
        snapshot.Model.Should().Be("iPad Pro"); // Unchanged
        snapshot.OsVersion.Should().Be("17.0");  // Unchanged
    }

    #endregion

    #region Printer Edge Cases

    // Given: A registered network receipt printer that has never printed
    // When: A print is recorded on the printer
    // Then: The last-print timestamp is set and the printer is marked as online
    [Fact]
    public async Task Printer_RecordPrint_ShouldUpdateTimestampAndStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetPrinterGrain(orgId, printerId);

        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Print Test Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        var beforeSnapshot = await grain.GetSnapshotAsync();
        beforeSnapshot.LastPrintAt.Should().BeNull();

        // Act
        await grain.RecordPrintAsync();

        // Assert
        var afterSnapshot = await grain.GetSnapshotAsync();
        afterSnapshot.LastPrintAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        afterSnapshot.IsOnline.Should().BeTrue();
    }

    // Given: A registered kitchen printer
    // When: The online status is toggled between online and offline multiple times
    // Then: The printer correctly tracks each status change
    [Fact]
    public async Task Printer_SetOnlineStatus_ShouldToggleCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetPrinterGrain(orgId, printerId);

        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Online Toggle Printer", PrinterType.Kitchen,
            PrinterConnectionType.Network, "192.168.1.101", 9100,
            null, null, null, 80, false));

        // Act & Assert - toggle online
        await grain.SetOnlineAsync(true);
        (await grain.IsOnlineAsync()).Should().BeTrue();

        await grain.SetOnlineAsync(false);
        (await grain.IsOnlineAsync()).Should().BeFalse();

        await grain.SetOnlineAsync(true);
        (await grain.IsOnlineAsync()).Should().BeTrue();
    }

    // Given: A new Bluetooth receipt printer with a MAC address
    // When: The printer is registered with Bluetooth connection type
    // Then: The MAC address is stored and network properties are null
    [Fact]
    public async Task Printer_BluetoothConnection_ShouldStoreCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetPrinterGrain(orgId, printerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Bluetooth Printer", PrinterType.Receipt,
            PrinterConnectionType.Bluetooth, null, null,
            MacAddress: "AA:BB:CC:DD:EE:FF",
            null, null, 58, false));

        // Assert
        result.ConnectionType.Should().Be(PrinterConnectionType.Bluetooth);
        result.MacAddress.Should().Be("AA:BB:CC:DD:EE:FF");
        result.IpAddress.Should().BeNull();
        result.Port.Should().BeNull();
    }

    #endregion

    #region Cash Drawer Edge Cases

    // Given: A registered USB cash drawer
    // When: Custom kick pulse settings are configured (pin 2, 300ms on, 200ms off)
    // Then: The custom kick pulse settings are persisted correctly
    [Fact]
    public async Task CashDrawer_CustomKickPulseSettings_ShouldPersist()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetCashDrawerGrain(orgId, drawerId);

        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Custom Settings Drawer",
            null, CashDrawerConnectionType.Usb, null, null));

        // Act
        await grain.UpdateAsync(new UpdateCashDrawerCommand(
            null, null, null, null, null,
            KickPulsePin: 2,
            KickPulseOnTime: 300,
            KickPulseOffTime: 200));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.KickPulsePin.Should().Be(2);
        snapshot.KickPulseOnTime.Should().Be(300);
        snapshot.KickPulseOffTime.Should().Be(200);
    }

    // Given: A registered USB cash drawer that has never been opened
    // When: A drawer open event is recorded
    // Then: The last-opened timestamp is set to the current time
    [Fact]
    public async Task CashDrawer_RecordOpen_ShouldTrackTimestamp()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetCashDrawerGrain(orgId, drawerId);

        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Tracking Drawer",
            null, CashDrawerConnectionType.Usb, null, null));

        // Act
        await grain.RecordOpenAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastOpenedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: A registered USB cash drawer
    // When: The kick command bytes are requested
    // Then: A non-empty kick command is returned
    [Fact]
    public async Task CashDrawer_GetKickCommand_ShouldReturnCommand()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetCashDrawerGrain(orgId, drawerId);

        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Kick Command Drawer",
            null, CashDrawerConnectionType.Usb, null, null));

        // Act
        var kickCommand = await grain.GetKickCommandAsync();

        // Assert
        kickCommand.Should().NotBeNullOrEmpty();
    }

    // Given: A cash drawer connected via a receipt printer
    // When: The drawer is registered with Printer connection type and a printer ID
    // Then: The drawer stores the linked printer ID and Printer connection type
    [Fact]
    public async Task CashDrawer_PrinterConnected_ShouldStorePrinterId()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetCashDrawerGrain(orgId, drawerId);

        // Act
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Printer-Connected Drawer",
            PrinterId: printerId,
            CashDrawerConnectionType.Printer,
            null, null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PrinterId.Should().Be(printerId);
        snapshot.ConnectionType.Should().Be(CashDrawerConnectionType.Printer);
    }

    #endregion
}
