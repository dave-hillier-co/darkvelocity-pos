using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests.Grains.Devices;

/// <summary>
/// Comprehensive tests for PosDeviceGrain covering:
/// - Device registration (all device types)
/// - Configuration updates (partial and full)
/// - Status management (online/offline transitions)
/// - Heartbeat handling
/// - Error handling and edge cases
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PosDeviceGrainTests
{
    private readonly TestClusterFixture _fixture;

    public PosDeviceGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPosDeviceGrain GetGrain(Guid orgId, Guid deviceId)
    {
        var key = $"{orgId}:posdevice:{deviceId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IPosDeviceGrain>(key);
    }

    #region Registration Tests

    // Given: A new POS tablet device with full registration details including model, OS, and app version
    // When: The device is registered at a site location
    // Then: The device is created as active and online with all properties set and default settings enabled
    [Fact]
    public async Task RegisterAsync_WithFullDetails_ShouldRegisterDevice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPosDeviceCommand(
            LocationId: locationId,
            Name: "Main Register",
            DeviceId: "POS-MAIN-001",
            DeviceType: PosDeviceType.Tablet,
            Model: "iPad Pro 12.9 (6th Gen)",
            OsVersion: "iPadOS 17.2.1",
            AppVersion: "3.1.0"));

        // Assert
        result.PosDeviceId.Should().Be(deviceId);
        result.LocationId.Should().Be(locationId);
        result.Name.Should().Be("Main Register");
        result.DeviceId.Should().Be("POS-MAIN-001");
        result.DeviceType.Should().Be(PosDeviceType.Tablet);
        result.Model.Should().Be("iPad Pro 12.9 (6th Gen)");
        result.OsVersion.Should().Be("iPadOS 17.2.1");
        result.AppVersion.Should().Be("3.1.0");
        result.IsActive.Should().BeTrue();
        result.IsOnline.Should().BeTrue();
        result.AutoPrintReceipts.Should().BeTrue(); // Default value
        result.OpenDrawerOnCash.Should().BeTrue(); // Default value
        result.DefaultPrinterId.Should().BeNull();
        result.DefaultCashDrawerId.Should().BeNull();
        result.RegisteredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: A new POS mobile device with only required registration fields
    // When: The device is registered without optional model, OS, or app version
    // Then: The device is created as active with null optional properties
    [Fact]
    public async Task RegisterAsync_WithMinimalDetails_ShouldRegisterDevice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPosDeviceCommand(
            LocationId: Guid.NewGuid(),
            Name: "Temp Register",
            DeviceId: "TEMP-001",
            DeviceType: PosDeviceType.Mobile,
            Model: null,
            OsVersion: null,
            AppVersion: null));

        // Assert
        result.Name.Should().Be("Temp Register");
        result.Model.Should().BeNull();
        result.OsVersion.Should().BeNull();
        result.AppVersion.Should().BeNull();
        result.IsActive.Should().BeTrue();
    }

    // Given: A new Samsung tablet for tableside ordering
    // When: The device is registered as a Tablet type
    // Then: The device type is set to Tablet
    [Fact]
    public async Task RegisterAsync_TabletDevice_ShouldSetCorrectType()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await grain.RegisterAsync(new RegisterPosDeviceCommand(
            LocationId: Guid.NewGuid(),
            Name: "Table Side Tablet",
            DeviceId: "TAB-TABLE-01",
            DeviceType: PosDeviceType.Tablet,
            Model: "Samsung Galaxy Tab S9",
            OsVersion: "Android 14",
            AppVersion: "2.0.0"));

        // Assert
        result.DeviceType.Should().Be(PosDeviceType.Tablet);
    }

    // Given: A Clover checkout terminal
    // When: The device is registered as a Terminal type
    // Then: The device type is set to Terminal
    [Fact]
    public async Task RegisterAsync_TerminalDevice_ShouldSetCorrectType()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await grain.RegisterAsync(new RegisterPosDeviceCommand(
            LocationId: Guid.NewGuid(),
            Name: "Checkout Terminal",
            DeviceId: "TERM-MAIN-01",
            DeviceType: PosDeviceType.Terminal,
            Model: "Clover Station Duo",
            OsVersion: "Android 11",
            AppVersion: "4.2.0"));

        // Assert
        result.DeviceType.Should().Be(PosDeviceType.Terminal);
    }

    // Given: An iPhone used as a server handheld device
    // When: The device is registered as a Mobile type
    // Then: The device type is set to Mobile
    [Fact]
    public async Task RegisterAsync_MobileDevice_ShouldSetCorrectType()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await grain.RegisterAsync(new RegisterPosDeviceCommand(
            LocationId: Guid.NewGuid(),
            Name: "Server Handheld",
            DeviceId: "MOBILE-SERVER-01",
            DeviceType: PosDeviceType.Mobile,
            Model: "iPhone 15 Pro",
            OsVersion: "iOS 17.3",
            AppVersion: "2.5.1"));

        // Assert
        result.DeviceType.Should().Be(PosDeviceType.Mobile);
    }

    // Given: A POS device that has already been registered
    // When: A second registration is attempted on the same device grain
    // Then: An exception is thrown indicating the device is already registered
    [Fact]
    public async Task RegisterAsync_WhenAlreadyRegistered_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "First Registration", "DEV-001",
            PosDeviceType.Tablet, null, null, null));

        // Act
        var act = async () => await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Second Registration", "DEV-002",
            PosDeviceType.Terminal, null, null, null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    #endregion

    #region Update Tests

    // Given: A registered POS device
    // When: All configurable fields are updated including name, model, versions, peripherals, and settings
    // Then: All fields reflect the new values
    [Fact]
    public async Task UpdateAsync_AllFields_ShouldUpdateAllProperties()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var cashDrawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Original Name", "DEV-001",
            PosDeviceType.Tablet, "iPad", "17.0", "1.0.0"));

        // Act
        var result = await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: "Updated Name",
            Model: "iPad Pro",
            OsVersion: "iPadOS 17.3",
            AppVersion: "2.0.0",
            DefaultPrinterId: printerId,
            DefaultCashDrawerId: cashDrawerId,
            AutoPrintReceipts: false,
            OpenDrawerOnCash: false,
            IsActive: true));

        // Assert
        result.Name.Should().Be("Updated Name");
        result.Model.Should().Be("iPad Pro");
        result.OsVersion.Should().Be("iPadOS 17.3");
        result.AppVersion.Should().Be("2.0.0");
        result.DefaultPrinterId.Should().Be(printerId);
        result.DefaultCashDrawerId.Should().Be(cashDrawerId);
        result.AutoPrintReceipts.Should().BeFalse();
        result.OpenDrawerOnCash.Should().BeFalse();
    }

    // Given: A registered POS device with a specific model and OS version
    // When: Only the name is updated with all other fields set to null
    // Then: The name changes while model, OS version, and app version remain unchanged
    [Fact]
    public async Task UpdateAsync_PartialUpdate_ShouldOnlyChangeSpecifiedFields()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Original Name", "DEV-001",
            PosDeviceType.Tablet, "iPad Mini", "16.0", "1.0.0"));

        // Act
        var result = await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: "New Name",
            Model: null,
            OsVersion: null,
            AppVersion: null,
            DefaultPrinterId: null,
            DefaultCashDrawerId: null,
            AutoPrintReceipts: null,
            OpenDrawerOnCash: null,
            IsActive: null));

        // Assert
        result.Name.Should().Be("New Name");
        result.Model.Should().Be("iPad Mini"); // Unchanged
        result.OsVersion.Should().Be("16.0"); // Unchanged
        result.AppVersion.Should().Be("1.0.0"); // Unchanged
    }

    // Given: A registered POS device with no default printer assigned
    // When: A default printer ID is set via update
    // Then: The device is linked to the specified printer
    [Fact]
    public async Task UpdateAsync_DefaultPrinterOnly_ShouldLinkPrinter()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Printer Link Test", "DEV-PRT",
            PosDeviceType.Tablet, null, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: null, Model: null, OsVersion: null, AppVersion: null,
            DefaultPrinterId: printerId,
            DefaultCashDrawerId: null,
            AutoPrintReceipts: null,
            OpenDrawerOnCash: null,
            IsActive: null));

        // Assert
        result.DefaultPrinterId.Should().Be(printerId);
    }

    // Given: A registered POS terminal with no default cash drawer assigned
    // When: A default cash drawer ID is set via update
    // Then: The device is linked to the specified cash drawer
    [Fact]
    public async Task UpdateAsync_DefaultCashDrawerOnly_ShouldLinkCashDrawer()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Drawer Link Test", "DEV-DRW",
            PosDeviceType.Terminal, null, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: null, Model: null, OsVersion: null, AppVersion: null,
            DefaultPrinterId: null,
            DefaultCashDrawerId: drawerId,
            AutoPrintReceipts: null,
            OpenDrawerOnCash: null,
            IsActive: null));

        // Assert
        result.DefaultCashDrawerId.Should().Be(drawerId);
    }

    // Given: A registered POS device with auto-print receipts enabled by default
    // When: The auto-print receipts setting is disabled via update
    // Then: The auto-print receipts setting is false
    [Fact]
    public async Task UpdateAsync_DisableAutoPrintReceipts_ShouldUpdateSetting()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Auto Print Test", "DEV-AP",
            PosDeviceType.Tablet, null, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: null, Model: null, OsVersion: null, AppVersion: null,
            DefaultPrinterId: null, DefaultCashDrawerId: null,
            AutoPrintReceipts: false,
            OpenDrawerOnCash: null,
            IsActive: null));

        // Assert
        result.AutoPrintReceipts.Should().BeFalse();
    }

    // Given: A registered POS terminal with open-drawer-on-cash enabled by default
    // When: The open-drawer-on-cash setting is disabled via update
    // Then: The open-drawer-on-cash setting is false
    [Fact]
    public async Task UpdateAsync_DisableOpenDrawerOnCash_ShouldUpdateSetting()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Drawer Open Test", "DEV-DO",
            PosDeviceType.Terminal, null, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: null, Model: null, OsVersion: null, AppVersion: null,
            DefaultPrinterId: null, DefaultCashDrawerId: null,
            AutoPrintReceipts: null,
            OpenDrawerOnCash: false,
            IsActive: null));

        // Assert
        result.OpenDrawerOnCash.Should().BeFalse();
    }

    // Given: A registered active POS device
    // When: The IsActive flag is set to false via update
    // Then: The device becomes inactive
    [Fact]
    public async Task UpdateAsync_DeactivateViaUpdate_ShouldDeactivateDevice()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Deactivate Test", "DEV-DEACT",
            PosDeviceType.Tablet, null, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: null, Model: null, OsVersion: null, AppVersion: null,
            DefaultPrinterId: null, DefaultCashDrawerId: null,
            AutoPrintReceipts: null, OpenDrawerOnCash: null,
            IsActive: false));

        // Assert
        result.IsActive.Should().BeFalse();
    }

    // Given: A POS device grain that has never been registered
    // When: An update is attempted
    // Then: An exception is thrown indicating the device is not initialized
    [Fact]
    public async Task UpdateAsync_WhenNotInitialized_ShouldThrowException()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = async () => await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: "Test", Model: null, OsVersion: null, AppVersion: null,
            DefaultPrinterId: null, DefaultCashDrawerId: null,
            AutoPrintReceipts: null, OpenDrawerOnCash: null, IsActive: null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    #endregion

    #region Deactivation Tests

    // Given: A registered active POS device
    // When: The device is deactivated
    // Then: The device becomes inactive
    [Fact]
    public async Task DeactivateAsync_ShouldDeactivateDevice()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "To Deactivate", "DEV-DEL",
            PosDeviceType.Tablet, null, null, null));

        // Act
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
    }

    // Given: A registered POS device that is currently online
    // When: The device is deactivated
    // Then: The device is set to both inactive and offline
    [Fact]
    public async Task DeactivateAsync_ShouldAlsoSetOffline()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Deactivate Online", "DEV-DEACT-ON",
            PosDeviceType.Terminal, null, null, null));

        // Act
        await grain.DeactivateAsync();

        // Assert
        var isOnline = await grain.IsOnlineAsync();
        isOnline.Should().BeFalse();
    }

    // Given: A POS device grain that has never been registered
    // When: Deactivation is attempted
    // Then: An exception is thrown indicating the device is not initialized
    [Fact]
    public async Task DeactivateAsync_WhenNotInitialized_ShouldThrowException()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = async () => await grain.DeactivateAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    #endregion

    #region Snapshot Tests

    // Given: A POS device registered with full details
    // When: The device snapshot is retrieved
    // Then: The snapshot contains all registration properties including active and online status
    [Fact]
    public async Task GetSnapshotAsync_AfterRegistration_ShouldReturnCompleteSnapshot()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            LocationId: locationId,
            Name: "Snapshot Test",
            DeviceId: "DEV-SNAP",
            DeviceType: PosDeviceType.Tablet,
            Model: "Test Model",
            OsVersion: "17.0",
            AppVersion: "1.0.0"));

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.PosDeviceId.Should().Be(deviceId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.Name.Should().Be("Snapshot Test");
        snapshot.DeviceId.Should().Be("DEV-SNAP");
        snapshot.DeviceType.Should().Be(PosDeviceType.Tablet);
        snapshot.Model.Should().Be("Test Model");
        snapshot.OsVersion.Should().Be("17.0");
        snapshot.AppVersion.Should().Be("1.0.0");
        snapshot.IsActive.Should().BeTrue();
        snapshot.IsOnline.Should().BeTrue();
    }

    // Given: A registered POS device that has been updated with new name, model, OS, and app version
    // When: The device snapshot is retrieved
    // Then: The snapshot reflects the updated values
    [Fact]
    public async Task GetSnapshotAsync_AfterUpdate_ShouldReturnUpdatedSnapshot()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Pre-Update", "DEV-UPD",
            PosDeviceType.Tablet, "Old Model", "16.0", "1.0.0"));
        await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: "Post-Update", Model: "New Model", OsVersion: "17.0", AppVersion: "2.0.0",
            DefaultPrinterId: null, DefaultCashDrawerId: null,
            AutoPrintReceipts: null, OpenDrawerOnCash: null, IsActive: null));

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Name.Should().Be("Post-Update");
        snapshot.Model.Should().Be("New Model");
        snapshot.OsVersion.Should().Be("17.0");
        snapshot.AppVersion.Should().Be("2.0.0");
    }

    // Given: A POS device grain that has never been registered
    // When: A snapshot is requested
    // Then: An exception is thrown indicating the device is not initialized
    [Fact]
    public async Task GetSnapshotAsync_WhenNotInitialized_ShouldThrowException()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = async () => await grain.GetSnapshotAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    #endregion

    #region Heartbeat Tests

    // Given: A registered POS device with existing app and OS versions
    // When: A heartbeat is received with updated app and OS versions
    // Then: Both versions are updated, the device remains online, and last-seen is refreshed
    [Fact]
    public async Task RecordHeartbeatAsync_WithVersions_ShouldUpdateVersionsAndLastSeen()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Heartbeat Test", "DEV-HB",
            PosDeviceType.Tablet, "iPad", "17.0", "2.0.0"));

        // Act
        await grain.RecordHeartbeatAsync("2.1.0", "17.1");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.AppVersion.Should().Be("2.1.0");
        snapshot.OsVersion.Should().Be("17.1");
        snapshot.IsOnline.Should().BeTrue();
        snapshot.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: A registered POS device with specific app and OS versions
    // When: A heartbeat is received with null version parameters
    // Then: The existing versions are preserved and the device stays online
    [Fact]
    public async Task RecordHeartbeatAsync_WithNullVersions_ShouldPreserveExistingVersions()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Version Preserve", "DEV-VP",
            PosDeviceType.Tablet, "iPad", "17.0", "2.0.0"));

        // Act
        await grain.RecordHeartbeatAsync(null, null);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.AppVersion.Should().Be("2.0.0"); // Unchanged
        snapshot.OsVersion.Should().Be("17.0"); // Unchanged
        snapshot.IsOnline.Should().BeTrue();
    }

    // Given: A registered POS mobile device with known versions
    // When: A heartbeat is received with only the app version updated
    // Then: The app version is updated while the OS version remains unchanged
    [Fact]
    public async Task RecordHeartbeatAsync_OnlyAppVersion_ShouldUpdateOnlyAppVersion()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "App Version Only", "DEV-AVO",
            PosDeviceType.Mobile, "iPhone", "17.0", "1.0.0"));

        // Act
        await grain.RecordHeartbeatAsync("1.1.0", null);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.AppVersion.Should().Be("1.1.0");
        snapshot.OsVersion.Should().Be("17.0"); // Unchanged
    }

    // Given: A registered POS mobile device with known versions
    // When: A heartbeat is received with only the OS version updated
    // Then: The OS version is updated while the app version remains unchanged
    [Fact]
    public async Task RecordHeartbeatAsync_OnlyOsVersion_ShouldUpdateOnlyOsVersion()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "OS Version Only", "DEV-OVO",
            PosDeviceType.Mobile, "iPhone", "17.0", "1.0.0"));

        // Act
        await grain.RecordHeartbeatAsync(null, "17.2");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.AppVersion.Should().Be("1.0.0"); // Unchanged
        snapshot.OsVersion.Should().Be("17.2");
    }

    // Given: A registered POS device that has been set to offline
    // When: A heartbeat is received
    // Then: The device comes back online
    [Fact]
    public async Task RecordHeartbeatAsync_AfterSetOffline_ShouldSetBackOnline()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Reconnect Test", "DEV-RECON",
            PosDeviceType.Tablet, null, null, null));
        await grain.SetOfflineAsync();
        var offlineStatus = await grain.IsOnlineAsync();
        offlineStatus.Should().BeFalse();

        // Act
        await grain.RecordHeartbeatAsync(null, null);

        // Assert
        var isOnline = await grain.IsOnlineAsync();
        isOnline.Should().BeTrue();
    }

    // Given: A POS device grain that has never been registered
    // When: A heartbeat is recorded
    // Then: An exception is thrown indicating the device is not initialized
    [Fact]
    public async Task RecordHeartbeatAsync_WhenNotInitialized_ShouldThrowException()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = async () => await grain.RecordHeartbeatAsync("1.0.0", "17.0");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    #endregion

    #region Online/Offline Status Tests

    // Given: A registered POS device that is currently online
    // When: The device is set to offline
    // Then: The device reports as offline
    [Fact]
    public async Task SetOfflineAsync_ShouldSetDeviceOffline()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Offline Test", "DEV-OFF",
            PosDeviceType.Tablet, null, null, null));
        var initialStatus = await grain.IsOnlineAsync();
        initialStatus.Should().BeTrue();

        // Act
        await grain.SetOfflineAsync();

        // Assert
        var isOnline = await grain.IsOnlineAsync();
        isOnline.Should().BeFalse();
    }

    // Given: A POS device grain that has never been registered
    // When: Setting the device offline is attempted
    // Then: An exception is thrown indicating the device is not initialized
    [Fact]
    public async Task SetOfflineAsync_WhenNotInitialized_ShouldThrowException()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = async () => await grain.SetOfflineAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    // Given: A newly registered POS device
    // When: The online status is checked immediately after registration
    // Then: The device is online
    [Fact]
    public async Task IsOnlineAsync_AfterRegistration_ShouldReturnTrue()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Online Check", "DEV-CHK",
            PosDeviceType.Tablet, null, null, null));

        // Act
        var isOnline = await grain.IsOnlineAsync();

        // Assert
        isOnline.Should().BeTrue();
    }

    // Given: A registered POS device that has been set to offline
    // When: The online status is checked
    // Then: The device is offline
    [Fact]
    public async Task IsOnlineAsync_AfterSetOffline_ShouldReturnFalse()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Offline Check", "DEV-OFFCHK",
            PosDeviceType.Tablet, null, null, null));
        await grain.SetOfflineAsync();

        // Act
        var isOnline = await grain.IsOnlineAsync();

        // Assert
        isOnline.Should().BeFalse();
    }

    // Given: A POS device grain that has never been registered
    // When: The online status is checked
    // Then: An exception is thrown indicating the device is not initialized
    [Fact]
    public async Task IsOnlineAsync_WhenNotInitialized_ShouldThrowException()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = async () => await grain.IsOnlineAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    #endregion

    #region Edge Cases

    // Given: Two POS devices registered under the same organization and location
    // When: Each device is configured with different names and types
    // Then: Each device grain maintains independent state
    [Fact]
    public async Task RegisterAsync_MultipleDevicesSameOrg_ShouldHaveIndependentState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        var grain1 = GetGrain(orgId, device1Id);
        var grain2 = GetGrain(orgId, device2Id);

        // Act
        await grain1.RegisterAsync(new RegisterPosDeviceCommand(
            locationId, "Device 1", "DEV-001",
            PosDeviceType.Tablet, null, null, null));
        await grain2.RegisterAsync(new RegisterPosDeviceCommand(
            locationId, "Device 2", "DEV-002",
            PosDeviceType.Terminal, null, null, null));

        // Assert
        var snapshot1 = await grain1.GetSnapshotAsync();
        var snapshot2 = await grain2.GetSnapshotAsync();
        snapshot1.PosDeviceId.Should().Be(device1Id);
        snapshot1.Name.Should().Be("Device 1");
        snapshot1.DeviceType.Should().Be(PosDeviceType.Tablet);
        snapshot2.PosDeviceId.Should().Be(device2Id);
        snapshot2.Name.Should().Be("Device 2");
        snapshot2.DeviceType.Should().Be(PosDeviceType.Terminal);
    }

    // Given: A registered POS device with a default printer already assigned
    // When: The default printer is updated to Guid.Empty
    // Then: The default printer ID is set to Guid.Empty
    [Fact]
    public async Task UpdateAsync_EmptyGuidForPrinter_ShouldSetPrinterId()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        var printerId = Guid.NewGuid();
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Printer ID Test", "DEV-PID",
            PosDeviceType.Tablet, null, null, null));

        // First set a printer
        await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: null, Model: null, OsVersion: null, AppVersion: null,
            DefaultPrinterId: printerId,
            DefaultCashDrawerId: null,
            AutoPrintReceipts: null, OpenDrawerOnCash: null, IsActive: null));

        // Act - Set to a different printer (including Guid.Empty to test)
        var emptyGuid = Guid.Empty;
        var result = await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: null, Model: null, OsVersion: null, AppVersion: null,
            DefaultPrinterId: emptyGuid,
            DefaultCashDrawerId: null,
            AutoPrintReceipts: null, OpenDrawerOnCash: null, IsActive: null));

        // Assert
        result.DefaultPrinterId.Should().Be(Guid.Empty);
    }

    // Given: A registered POS mobile device
    // When: Multiple heartbeats are sent sequentially with incrementing app versions
    // Then: The last heartbeat's app version is retained and last-seen is current
    [Fact]
    public async Task SequentialHeartbeats_ShouldUpdateLastSeenEachTime()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Sequential HB", "DEV-SHB",
            PosDeviceType.Mobile, null, null, "1.0.0"));

        // Act - Multiple heartbeats
        await grain.RecordHeartbeatAsync("1.0.1", null);
        await Task.Delay(10); // Small delay to ensure timestamp difference
        await grain.RecordHeartbeatAsync("1.0.2", null);
        await Task.Delay(10);
        await grain.RecordHeartbeatAsync("1.0.3", null);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.AppVersion.Should().Be("1.0.3");
        snapshot.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: A registered POS device that has been deactivated
    // When: The device is reactivated by setting IsActive to true via update
    // Then: The device becomes active again
    [Fact]
    public async Task DeactivateAsync_ThenUpdate_ShouldAllowReactivation()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Reactivate Test", "DEV-REACT",
            PosDeviceType.Tablet, null, null, null));
        await grain.DeactivateAsync();
        var deactivatedSnapshot = await grain.GetSnapshotAsync();
        deactivatedSnapshot.IsActive.Should().BeFalse();

        // Act - Reactivate via update
        var result = await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: null, Model: null, OsVersion: null, AppVersion: null,
            DefaultPrinterId: null, DefaultCashDrawerId: null,
            AutoPrintReceipts: null, OpenDrawerOnCash: null,
            IsActive: true));

        // Assert
        result.IsActive.Should().BeTrue();
    }

    // Given: A registered POS device
    // When: The device name is updated to a 200-character string
    // Then: The long name is accepted and stored correctly
    [Fact]
    public async Task UpdateAsync_WithLongName_ShouldHandleCorrectly()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Short Name", "DEV-LONG",
            PosDeviceType.Tablet, null, null, null));
        var longName = new string('A', 200);

        // Act
        var result = await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: longName, Model: null, OsVersion: null, AppVersion: null,
            DefaultPrinterId: null, DefaultCashDrawerId: null,
            AutoPrintReceipts: null, OpenDrawerOnCash: null, IsActive: null));

        // Assert
        result.Name.Should().Be(longName);
    }

    // Given: A new POS device
    // When: The device is registered with a name containing special characters
    // Then: The special characters are preserved in the name
    [Fact]
    public async Task RegisterAsync_WithSpecialCharactersInName_ShouldHandleCorrectly()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        var specialName = "Register #1 - Main (Floor 2) [Active]";

        // Act
        var result = await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), specialName, "DEV-SPEC",
            PosDeviceType.Tablet, null, null, null));

        // Assert
        result.Name.Should().Be(specialName);
    }

    // Given: A new POS device
    // When: The device is registered with a Unicode name containing accented characters and bullet points
    // Then: The Unicode characters are preserved in the name
    [Fact]
    public async Task RegisterAsync_WithUnicodeInName_ShouldHandleCorrectly()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        var unicodeName = "Register - Caf\u00e9 \u00c9lite \u2022 Main";

        // Act
        var result = await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), unicodeName, "DEV-UNI",
            PosDeviceType.Tablet, null, null, null));

        // Assert
        result.Name.Should().Be(unicodeName);
    }

    #endregion

    #region State Persistence Tests

    // Given: A POS terminal registered with full details
    // When: A new grain reference is obtained for the same device
    // Then: The state is persisted and the snapshot matches the original registration
    [Fact]
    public async Task State_ShouldPersistAcrossGrainActivations()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            locationId, "Persistence Test", "DEV-PERSIST",
            PosDeviceType.Terminal, "Model X", "OS 1.0", "App 1.0"));

        // Act - Get a new reference to the same grain
        var grain2 = GetGrain(orgId, deviceId);
        var snapshot = await grain2.GetSnapshotAsync();

        // Assert
        snapshot.PosDeviceId.Should().Be(deviceId);
        snapshot.Name.Should().Be("Persistence Test");
        snapshot.Model.Should().Be("Model X");
    }

    // Given: A POS device that has been registered and then updated
    // When: A new grain reference is obtained for the same device
    // Then: The updated state is persisted and retrievable from the new reference
    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Before Update", "DEV-UPDT",
            PosDeviceType.Tablet, null, null, null));
        await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: "After Update", Model: null, OsVersion: null, AppVersion: null,
            DefaultPrinterId: null, DefaultCashDrawerId: null,
            AutoPrintReceipts: false, OpenDrawerOnCash: null, IsActive: null));

        // Act - Get fresh grain reference
        var grain2 = GetGrain(orgId, deviceId);
        var snapshot = await grain2.GetSnapshotAsync();

        // Assert
        snapshot.Name.Should().Be("After Update");
        snapshot.AutoPrintReceipts.Should().BeFalse();
    }

    #endregion

    #region Concurrent Operations Tests

    // Given: A registered POS device
    // When: Ten heartbeats with different app versions are sent concurrently
    // Then: The grain handles all heartbeats without errors and ends in a consistent state
    [Fact]
    public async Task ConcurrentHeartbeats_ShouldHandleGracefully()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Concurrent Test", "DEV-CONC",
            PosDeviceType.Tablet, null, null, "1.0.0"));

        // Act - Send multiple heartbeats concurrently
        var tasks = Enumerable.Range(0, 10)
            .Select(i => grain.RecordHeartbeatAsync($"1.0.{i}", null));
        await Task.WhenAll(tasks);

        // Assert - Should not throw, grain should be in consistent state
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsOnline.Should().BeTrue();
        snapshot.AppVersion.Should().NotBeNull();
    }

    // Given: A registered POS device
    // When: Five name updates are sent concurrently
    // Then: The grain serializes the updates and ends with one of the update names
    [Fact]
    public async Task ConcurrentUpdates_ShouldSerializeCorrectly()
    {
        // Arrange
        var grain = GetGrain(Guid.NewGuid(), Guid.NewGuid());
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Concurrent Update", "DEV-CUPD",
            PosDeviceType.Tablet, null, null, null));

        // Act - Multiple updates
        var tasks = Enumerable.Range(0, 5)
            .Select(i => grain.UpdateAsync(new UpdatePosDeviceCommand(
                Name: $"Update {i}", Model: null, OsVersion: null, AppVersion: null,
                DefaultPrinterId: null, DefaultCashDrawerId: null,
                AutoPrintReceipts: null, OpenDrawerOnCash: null, IsActive: null)));
        await Task.WhenAll(tasks);

        // Assert - State should be consistent (one of the updates won)
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Name.Should().StartWith("Update ");
    }

    #endregion
}
