using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

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

    // Given: a new POS tablet device with full hardware details
    // When: the device is registered at a venue location
    // Then: the device is active, online, and all hardware details are captured
    [Fact]
    public async Task RegisterAsync_ShouldRegisterPosDevice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPosDeviceCommand(
            LocationId: locationId,
            Name: "Register 1",
            DeviceId: "TABLET-001",
            DeviceType: PosDeviceType.Tablet,
            Model: "iPad Pro 12.9",
            OsVersion: "iPadOS 17.2",
            AppVersion: "2.1.0"));

        // Assert
        result.PosDeviceId.Should().Be(deviceId);
        result.LocationId.Should().Be(locationId);
        result.Name.Should().Be("Register 1");
        result.DeviceId.Should().Be("TABLET-001");
        result.DeviceType.Should().Be(PosDeviceType.Tablet);
        result.Model.Should().Be("iPad Pro 12.9");
        result.OsVersion.Should().Be("iPadOS 17.2");
        result.AppVersion.Should().Be("2.1.0");
        result.IsActive.Should().BeTrue();
        result.IsOnline.Should().BeTrue();
        result.RegisteredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: a new fixed POS terminal device
    // When: the terminal is registered at a venue
    // Then: the device type is recorded as Terminal
    [Fact]
    public async Task RegisterAsync_Terminal_ShouldRegisterTerminalDevice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPosDeviceCommand(
            LocationId: Guid.NewGuid(),
            Name: "Main Terminal",
            DeviceId: "TERM-001",
            DeviceType: PosDeviceType.Terminal,
            Model: "Clover Station",
            OsVersion: "Android 11",
            AppVersion: "3.0.0"));

        // Assert
        result.DeviceType.Should().Be(PosDeviceType.Terminal);
    }

    // Given: a new handheld mobile POS device for tableside ordering
    // When: the mobile device is registered at a venue
    // Then: the device type is recorded as Mobile
    [Fact]
    public async Task RegisterAsync_Mobile_ShouldRegisterMobileDevice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPosDeviceCommand(
            LocationId: Guid.NewGuid(),
            Name: "Server Handheld",
            DeviceId: "MOBILE-001",
            DeviceType: PosDeviceType.Mobile,
            Model: "iPhone 15",
            OsVersion: "iOS 17.2",
            AppVersion: "2.1.0"));

        // Assert
        result.DeviceType.Should().Be(PosDeviceType.Mobile);
    }

    // Given: a registered POS tablet at a venue
    // When: the device configuration is updated with a new name, printer, cash drawer, and settings
    // Then: all updated properties are persisted on the device
    [Fact]
    public async Task UpdateAsync_ShouldUpdateDeviceProperties()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Register 2", "TAB-002",
            PosDeviceType.Tablet, "iPad", "17.0", "2.0.0"));

        // Act
        var result = await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: "Bar Register",
            Model: "iPad Pro",
            OsVersion: "iPadOS 17.3",
            AppVersion: "2.2.0",
            DefaultPrinterId: printerId,
            DefaultCashDrawerId: drawerId,
            AutoPrintReceipts: true,
            OpenDrawerOnCash: true,
            IsActive: null));

        // Assert
        result.Name.Should().Be("Bar Register");
        result.Model.Should().Be("iPad Pro");
        result.OsVersion.Should().Be("iPadOS 17.3");
        result.AppVersion.Should().Be("2.2.0");
        result.DefaultPrinterId.Should().Be(printerId);
        result.DefaultCashDrawerId.Should().Be(drawerId);
        result.AutoPrintReceipts.Should().BeTrue();
        result.OpenDrawerOnCash.Should().BeTrue();
    }

    // Given: a registered POS device that is no longer needed
    // When: the device is deactivated
    // Then: the device is marked inactive and goes offline
    [Fact]
    public async Task DeactivateAsync_ShouldDeactivateDevice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Old Register", "TAB-OLD",
            PosDeviceType.Tablet, null, null, null));

        // Act
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
        snapshot.IsOnline.Should().BeFalse();
    }

    // Given: a registered POS tablet running an older app version
    // When: the device sends a heartbeat with updated app and OS versions
    // Then: the device versions and last-seen timestamp are updated and the device stays online
    [Fact]
    public async Task RecordHeartbeatAsync_ShouldUpdateLastSeenAndVersions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Heartbeat Test", "TAB-HB",
            PosDeviceType.Tablet, "iPad", "17.0", "2.0.0"));

        // Act
        await grain.RecordHeartbeatAsync("2.1.0", "17.1");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsOnline.Should().BeTrue();
        snapshot.AppVersion.Should().Be("2.1.0");
        snapshot.OsVersion.Should().Be("17.1");
        snapshot.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: a registered POS tablet with known app and OS versions
    // When: the device sends a heartbeat without version information
    // Then: the existing versions are preserved and only the last-seen timestamp is updated
    [Fact]
    public async Task RecordHeartbeatAsync_WithNullVersions_ShouldOnlyUpdateLastSeen()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Version Test", "TAB-VER",
            PosDeviceType.Tablet, "iPad", "17.0", "2.0.0"));

        // Act
        await grain.RecordHeartbeatAsync(null, null);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.AppVersion.Should().Be("2.0.0"); // Unchanged
        snapshot.OsVersion.Should().Be("17.0"); // Unchanged
        snapshot.IsOnline.Should().BeTrue();
    }

    // Given: a registered and online POS tablet
    // When: the device loses network connectivity and is marked offline
    // Then: the device status shows as offline
    [Fact]
    public async Task SetOfflineAsync_ShouldMarkDeviceOffline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Offline Test", "TAB-OFF",
            PosDeviceType.Tablet, null, null, null));

        // Act
        await grain.SetOfflineAsync();

        // Assert
        var isOnline = await grain.IsOnlineAsync();
        isOnline.Should().BeFalse();
    }

    // Given: a freshly registered POS tablet
    // When: the device online status is checked immediately after registration
    // Then: the device reports as online
    [Fact]
    public async Task IsOnlineAsync_WhenOnline_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Online Test", "TAB-ON",
            PosDeviceType.Tablet, null, null, null));

        // Act
        var isOnline = await grain.IsOnlineAsync();

        // Assert
        isOnline.Should().BeTrue();
    }

    // Given: a registered POS tablet without a default printer
    // When: a receipt printer is assigned as the default with auto-print enabled
    // Then: the device is linked to the printer and auto-print receipts is turned on
    [Fact]
    public async Task UpdateAsync_SetDefaultPrinter_ShouldLinkPrinter()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);
        await grain.RegisterAsync(new RegisterPosDeviceCommand(
            Guid.NewGuid(), "Printer Link Test", "TAB-PRT",
            PosDeviceType.Tablet, null, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdatePosDeviceCommand(
            Name: null, Model: null, OsVersion: null, AppVersion: null,
            DefaultPrinterId: printerId,
            DefaultCashDrawerId: null,
            AutoPrintReceipts: true,
            OpenDrawerOnCash: null,
            IsActive: null));

        // Assert
        result.DefaultPrinterId.Should().Be(printerId);
        result.AutoPrintReceipts.Should().BeTrue();
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PrinterGrainTests
{
    private readonly TestClusterFixture _fixture;

    public PrinterGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPrinterGrain GetGrain(Guid orgId, Guid printerId)
    {
        var key = $"{orgId}:printer:{printerId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IPrinterGrain>(key);
    }

    // Given: a new network-connected receipt printer at a venue
    // When: the printer is registered with its IP address, port, and paper width
    // Then: the printer is active, starts offline, and all network details are stored
    [Fact]
    public async Task RegisterAsync_NetworkPrinter_ShouldRegisterPrinter()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPrinterCommand(
            LocationId: locationId,
            Name: "Receipt Printer 1",
            PrinterType: PrinterType.Receipt,
            ConnectionType: PrinterConnectionType.Network,
            IpAddress: "192.168.1.100",
            Port: 9100,
            MacAddress: null,
            UsbVendorId: null,
            UsbProductId: null,
            PaperWidth: 80,
            IsDefault: true));

        // Assert
        result.PrinterId.Should().Be(printerId);
        result.Name.Should().Be("Receipt Printer 1");
        result.PrinterType.Should().Be(PrinterType.Receipt);
        result.ConnectionType.Should().Be(PrinterConnectionType.Network);
        result.IpAddress.Should().Be("192.168.1.100");
        result.Port.Should().Be(9100);
        result.PaperWidth.Should().Be(80);
        result.IsDefault.Should().BeTrue();
        result.IsActive.Should().BeTrue();
        result.IsOnline.Should().BeFalse();
    }

    // Given: a new USB-connected receipt printer with vendor and product identifiers
    // When: the printer is registered via USB connection
    // Then: the USB details are stored and no network details are present
    [Fact]
    public async Task RegisterAsync_UsbPrinter_ShouldRegisterWithUsbDetails()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPrinterCommand(
            LocationId: Guid.NewGuid(),
            Name: "USB Receipt Printer",
            PrinterType: PrinterType.Receipt,
            ConnectionType: PrinterConnectionType.Usb,
            IpAddress: null,
            Port: null,
            MacAddress: null,
            UsbVendorId: "04b8",
            UsbProductId: "0202",
            PaperWidth: 58,
            IsDefault: false));

        // Assert
        result.ConnectionType.Should().Be(PrinterConnectionType.Usb);
        result.UsbVendorId.Should().Be("04b8");
        result.UsbProductId.Should().Be("0202");
    }

    // Given: a new network printer designated for the kitchen hot line
    // When: the printer is registered as a kitchen printer
    // Then: the printer type is recorded as Kitchen
    [Fact]
    public async Task RegisterAsync_KitchenPrinter_ShouldRegisterKitchenType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPrinterCommand(
            LocationId: Guid.NewGuid(),
            Name: "Hot Line Printer",
            PrinterType: PrinterType.Kitchen,
            ConnectionType: PrinterConnectionType.Network,
            IpAddress: "192.168.1.101",
            Port: 9100,
            MacAddress: null,
            UsbVendorId: null,
            UsbProductId: null,
            PaperWidth: 80,
            IsDefault: false));

        // Assert
        result.PrinterType.Should().Be(PrinterType.Kitchen);
    }

    // Given: a new USB label printer for item tagging
    // When: the printer is registered as a label printer
    // Then: the printer type is recorded as Label
    [Fact]
    public async Task RegisterAsync_LabelPrinter_ShouldRegisterLabelType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPrinterCommand(
            LocationId: Guid.NewGuid(),
            Name: "Label Printer",
            PrinterType: PrinterType.Label,
            ConnectionType: PrinterConnectionType.Usb,
            IpAddress: null,
            Port: null,
            MacAddress: null,
            UsbVendorId: "0a5f",
            UsbProductId: "0001",
            PaperWidth: 50,
            IsDefault: false));

        // Assert
        result.PrinterType.Should().Be(PrinterType.Label);
    }

    // Given: a new Bluetooth mobile receipt printer with a MAC address
    // When: the printer is registered via Bluetooth
    // Then: the Bluetooth connection type and MAC address are stored
    [Fact]
    public async Task RegisterAsync_BluetoothPrinter_ShouldRegisterWithMacAddress()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPrinterCommand(
            LocationId: Guid.NewGuid(),
            Name: "Mobile Printer",
            PrinterType: PrinterType.Receipt,
            ConnectionType: PrinterConnectionType.Bluetooth,
            IpAddress: null,
            Port: null,
            MacAddress: "AA:BB:CC:DD:EE:FF",
            UsbVendorId: null,
            UsbProductId: null,
            PaperWidth: 58,
            IsDefault: false));

        // Assert
        result.ConnectionType.Should().Be(PrinterConnectionType.Bluetooth);
        result.MacAddress.Should().Be("AA:BB:CC:DD:EE:FF");
    }

    // Given: a registered network receipt printer with an old name and IP
    // When: the printer configuration is updated with a new name, IP, port, and hardware capabilities
    // Then: all updated properties including character set and cash drawer support are persisted
    [Fact]
    public async Task UpdateAsync_ShouldUpdatePrinterProperties()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Old Name", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, false));

        // Act
        var result = await grain.UpdateAsync(new UpdatePrinterCommand(
            Name: "Main Receipt Printer",
            IpAddress: "192.168.1.200",
            Port: 9101,
            MacAddress: null,
            PaperWidth: 80,
            IsDefault: true,
            IsActive: null,
            CharacterSet: "UTF-8",
            SupportsCut: true,
            SupportsCashDrawer: true));

        // Assert
        result.Name.Should().Be("Main Receipt Printer");
        result.IpAddress.Should().Be("192.168.1.200");
        result.Port.Should().Be(9101);
        result.IsDefault.Should().BeTrue();
        result.CharacterSet.Should().Be("UTF-8");
        result.SupportsCut.Should().BeTrue();
        result.SupportsCashDrawer.Should().BeTrue();
    }

    // Given: a registered network receipt printer that is being retired
    // When: the printer is deactivated
    // Then: the printer is marked inactive and goes offline
    [Fact]
    public async Task DeactivateAsync_ShouldDeactivatePrinter()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "To Deactivate", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, false));

        // Act
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
        snapshot.IsOnline.Should().BeFalse();
    }

    // Given: a registered receipt printer that has not yet printed
    // When: a print job completes successfully on the printer
    // Then: the printer is marked online and the last print timestamp is recorded
    [Fact]
    public async Task RecordPrintAsync_ShouldUpdateLastPrintAndSetOnline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Print Test", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, false));

        // Act
        await grain.RecordPrintAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsOnline.Should().BeTrue();
        snapshot.LastPrintAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: a registered receipt printer that is currently offline
    // When: the printer connectivity status is set to online
    // Then: the printer reports as online
    [Fact]
    public async Task SetOnlineAsync_True_ShouldSetOnline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Online Test", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, false));

        // Act
        await grain.SetOnlineAsync(true);

        // Assert
        var isOnline = await grain.IsOnlineAsync();
        isOnline.Should().BeTrue();
    }

    // Given: a registered receipt printer that is currently online
    // When: the printer connectivity status is set to offline
    // Then: the printer reports as offline
    [Fact]
    public async Task SetOnlineAsync_False_ShouldSetOffline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Offline Test", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, false));
        await grain.SetOnlineAsync(true);

        // Act
        await grain.SetOnlineAsync(false);

        // Assert
        var isOnline = await grain.IsOnlineAsync();
        isOnline.Should().BeFalse();
    }

    // Given: a freshly registered receipt printer that has never been pinged or printed
    // When: the printer online status is checked
    // Then: the printer reports as offline since it starts in offline state
    [Fact]
    public async Task IsOnlineAsync_WhenOffline_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Check Online", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, false));

        // Act
        var isOnline = await grain.IsOnlineAsync();

        // Assert
        isOnline.Should().BeFalse(); // Starts offline
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class CashDrawerHardwareGrainTests
{
    private readonly TestClusterFixture _fixture;

    public CashDrawerHardwareGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ICashDrawerHardwareGrain GetGrain(Guid orgId, Guid drawerId)
    {
        var key = $"{orgId}:cashdrawerhw:{drawerId}";
        return _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerHardwareGrain>(key);
    }

    // Given: a new cash drawer connected via a receipt printer's kick port
    // When: the cash drawer is registered with its linked printer
    // Then: the drawer is active and associated with the printer
    [Fact]
    public async Task RegisterAsync_PrinterConnected_ShouldRegisterDrawer()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterCashDrawerCommand(
            LocationId: locationId,
            Name: "Main Cash Drawer",
            PrinterId: printerId,
            ConnectionType: CashDrawerConnectionType.Printer,
            IpAddress: null,
            Port: null));

        // Assert
        result.CashDrawerId.Should().Be(drawerId);
        result.LocationId.Should().Be(locationId);
        result.Name.Should().Be("Main Cash Drawer");
        result.PrinterId.Should().Be(printerId);
        result.ConnectionType.Should().Be(CashDrawerConnectionType.Printer);
        result.IsActive.Should().BeTrue();
    }

    // Given: a new network-connected cash drawer with a dedicated IP address
    // When: the cash drawer is registered with network connection details
    // Then: the network IP and port are stored and no printer is linked
    [Fact]
    public async Task RegisterAsync_NetworkConnected_ShouldRegisterWithIpAddress()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterCashDrawerCommand(
            LocationId: Guid.NewGuid(),
            Name: "Network Drawer",
            PrinterId: null,
            ConnectionType: CashDrawerConnectionType.Network,
            IpAddress: "192.168.1.150",
            Port: 4001));

        // Assert
        result.ConnectionType.Should().Be(CashDrawerConnectionType.Network);
        result.IpAddress.Should().Be("192.168.1.150");
        result.Port.Should().Be(4001);
        result.PrinterId.Should().BeNull();
    }

    // Given: a new cash drawer connected directly via USB
    // When: the cash drawer is registered with USB connection type
    // Then: the drawer is registered as USB with no network or printer details
    [Fact]
    public async Task RegisterAsync_UsbConnected_ShouldRegisterUsbDrawer()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterCashDrawerCommand(
            LocationId: Guid.NewGuid(),
            Name: "USB Drawer",
            PrinterId: null,
            ConnectionType: CashDrawerConnectionType.Usb,
            IpAddress: null,
            Port: null));

        // Assert
        result.ConnectionType.Should().Be(CashDrawerConnectionType.Usb);
    }

    // Given: a registered cash drawer linked to an old printer
    // When: the drawer configuration is updated with a new printer, pulse pin, and timing settings
    // Then: the new printer link and pulse settings are persisted
    [Fact]
    public async Task UpdateAsync_ShouldUpdateDrawerProperties()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var newPrinterId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Old Drawer", Guid.NewGuid(),
            CashDrawerConnectionType.Printer, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdateCashDrawerCommand(
            Name: "Updated Drawer",
            PrinterId: newPrinterId,
            IpAddress: null,
            Port: null,
            IsActive: null,
            KickPulsePin: 0,
            KickPulseOnTime: 200,
            KickPulseOffTime: 200));

        // Assert
        result.Name.Should().Be("Updated Drawer");
        result.PrinterId.Should().Be(newPrinterId);
        result.KickPulsePin.Should().Be(0);
        result.KickPulseOnTime.Should().Be(200);
        result.KickPulseOffTime.Should().Be(200);
    }

    // Given: a registered USB cash drawer that is being retired
    // When: the cash drawer is deactivated
    // Then: the drawer is marked as inactive
    [Fact]
    public async Task DeactivateAsync_ShouldDeactivateDrawer()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "To Deactivate", null,
            CashDrawerConnectionType.Usb, null, null));

        // Act
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
    }

    // Given: a registered printer-connected cash drawer
    // When: the drawer is physically opened during a cash transaction
    // Then: the last-opened timestamp is recorded
    [Fact]
    public async Task RecordOpenAsync_ShouldUpdateLastOpenedAt()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Open Test", Guid.NewGuid(),
            CashDrawerConnectionType.Printer, null, null));

        // Act
        await grain.RecordOpenAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastOpenedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: a registered printer-connected cash drawer with default pulse settings
    // When: the ESC/POS kick command is requested
    // Then: the command starts with the standard ESC/POS drawer kick sequence
    [Fact]
    public async Task GetKickCommandAsync_ShouldReturnEscPosCommand()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Kick Test", Guid.NewGuid(),
            CashDrawerConnectionType.Printer, null, null));

        // Act
        var kickCommand = await grain.GetKickCommandAsync();

        // Assert
        kickCommand.Should().StartWith("\\x1B\\x70"); // ESC p command
    }

    // Given: a registered printer-connected cash drawer
    // When: the pulse settings are updated to use pin 1 with custom timing
    // Then: the updated pulse pin and timing values are stored
    [Fact]
    public async Task UpdateAsync_CustomPulseSettings_ShouldUpdateTimings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Pulse Test", Guid.NewGuid(),
            CashDrawerConnectionType.Printer, null, null));

        // Act
        await grain.UpdateAsync(new UpdateCashDrawerCommand(
            Name: null, PrinterId: null, IpAddress: null, Port: null,
            IsActive: null,
            KickPulsePin: 1,
            KickPulseOnTime: 100,
            KickPulseOffTime: 100));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.KickPulsePin.Should().Be(1);
        snapshot.KickPulseOnTime.Should().Be(100);
        snapshot.KickPulseOffTime.Should().Be(100);
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DeviceStatusGrainTests
{
    private readonly TestClusterFixture _fixture;

    public DeviceStatusGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDeviceStatusGrain GetGrain(Guid orgId, Guid locationId)
    {
        var key = $"{orgId}:{locationId}:devicestatus";
        return _fixture.Cluster.GrainFactory.GetGrain<IDeviceStatusGrain>(key);
    }

    // Given: a new venue location with no devices
    // When: the device status tracker is initialized for the location
    // Then: all device counts are zero and there are no alerts
    [Fact]
    public async Task InitializeAsync_ShouldInitializeDeviceStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);

        // Act
        await grain.InitializeAsync(locationId);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalPosDevices.Should().Be(0);
        summary.TotalPrinters.Should().Be(0);
        summary.TotalCashDrawers.Should().Be(0);
        summary.Alerts.Should().BeEmpty();
    }

    // Given: an initialized venue location
    // When: a POS register is added to the location's device roster
    // Then: the POS device count increases and it starts as offline
    [Fact]
    public async Task RegisterDeviceAsync_PosDevice_ShouldRegister()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        // Act
        await grain.RegisterDeviceAsync("POS", deviceId, "Register 1");

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalPosDevices.Should().Be(1);
        summary.OnlinePosDevices.Should().Be(0);
    }

    // Given: an initialized venue location
    // When: a receipt printer is added to the location's device roster
    // Then: the printer count increases and it starts as offline
    [Fact]
    public async Task RegisterDeviceAsync_Printer_ShouldRegister()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        // Act
        await grain.RegisterDeviceAsync("Printer", printerId, "Receipt Printer");

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalPrinters.Should().Be(1);
        summary.OnlinePrinters.Should().Be(0);
    }

    // Given: an initialized venue location
    // When: a cash drawer is added to the location's device roster
    // Then: the cash drawer count increases
    [Fact]
    public async Task RegisterDeviceAsync_CashDrawer_ShouldRegister()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        // Act
        await grain.RegisterDeviceAsync("CashDrawer", drawerId, "Main Drawer");

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalCashDrawers.Should().Be(1);
    }

    // Given: an initialized venue location
    // When: multiple POS devices, printers, and a cash drawer are registered
    // Then: the summary accurately counts each device type
    [Fact]
    public async Task RegisterDeviceAsync_MultipleDevices_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        // Act
        await grain.RegisterDeviceAsync("POS", Guid.NewGuid(), "Register 1");
        await grain.RegisterDeviceAsync("POS", Guid.NewGuid(), "Register 2");
        await grain.RegisterDeviceAsync("Printer", Guid.NewGuid(), "Receipt Printer");
        await grain.RegisterDeviceAsync("Printer", Guid.NewGuid(), "Kitchen Printer");
        await grain.RegisterDeviceAsync("CashDrawer", Guid.NewGuid(), "Drawer 1");

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalPosDevices.Should().Be(2);
        summary.TotalPrinters.Should().Be(2);
        summary.TotalCashDrawers.Should().Be(1);
    }

    // Given: a venue location with a registered POS device
    // When: the POS device is removed from the location's roster
    // Then: the POS device count decreases to zero
    [Fact]
    public async Task UnregisterDeviceAsync_ShouldRemoveDevice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("POS", deviceId, "Register 1");

        // Act
        await grain.UnregisterDeviceAsync("POS", deviceId);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalPosDevices.Should().Be(0);
    }

    // Given: a venue location with a registered but offline POS device
    // When: the POS device comes online
    // Then: the online POS device count increases
    [Fact]
    public async Task UpdateDeviceStatusAsync_Online_ShouldUpdateCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("POS", deviceId, "Register 1");

        // Act
        await grain.UpdateDeviceStatusAsync("POS", deviceId, true);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.OnlinePosDevices.Should().Be(1);
    }

    // Given: a venue location with an online POS device
    // When: the POS device goes offline
    // Then: the online POS device count decreases to zero
    [Fact]
    public async Task UpdateDeviceStatusAsync_Offline_ShouldUpdateCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("POS", deviceId, "Register 1");
        await grain.UpdateDeviceStatusAsync("POS", deviceId, true);

        // Act
        await grain.UpdateDeviceStatusAsync("POS", deviceId, false);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.OnlinePosDevices.Should().Be(0);
    }

    // Given: a venue location with a registered kitchen printer
    // When: an offline alert is raised for the kitchen printer
    // Then: the alert is tracked in the location's device summary
    [Fact]
    public async Task AddAlertAsync_ShouldAddAlert()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("Printer", deviceId, "Kitchen Printer");

        // Act
        await grain.AddAlertAsync(new DeviceAlert(
            DeviceId: deviceId,
            DeviceType: "Printer",
            DeviceName: "Kitchen Printer",
            AlertType: "Offline",
            Message: "Printer not responding",
            Timestamp: DateTime.UtcNow));

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.Alerts.Should().HaveCount(1);
        summary.Alerts[0].AlertType.Should().Be("Offline");
        summary.Alerts[0].DeviceName.Should().Be("Kitchen Printer");
    }

    // Given: a venue location with a printer and a POS device, both having issues
    // When: alerts are raised for both the printer (paper low) and POS (battery low)
    // Then: both alerts are tracked in the location summary
    [Fact]
    public async Task AddAlertAsync_MultipleAlerts_ShouldTrackAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var posId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("Printer", printerId, "Kitchen Printer");
        await grain.RegisterDeviceAsync("POS", posId, "Register 1");

        // Act
        await grain.AddAlertAsync(new DeviceAlert(
            printerId, "Printer", "Kitchen Printer",
            "PaperLow", "Paper running low", DateTime.UtcNow));
        await grain.AddAlertAsync(new DeviceAlert(
            posId, "POS", "Register 1",
            "BatteryLow", "Battery at 15%", DateTime.UtcNow));

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.Alerts.Should().HaveCount(2);
    }

    // Given: a venue location with a printer that has an active offline alert
    // When: the alert is cleared for that printer
    // Then: the location has no remaining alerts
    [Fact]
    public async Task ClearAlertAsync_ShouldRemoveAlertsForDevice()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("Printer", deviceId, "Printer 1");
        await grain.AddAlertAsync(new DeviceAlert(
            deviceId, "Printer", "Printer 1",
            "Offline", "Not responding", DateTime.UtcNow));

        // Act
        await grain.ClearAlertAsync(deviceId);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.Alerts.Should().BeEmpty();
    }

    // Given: a venue location with a POS device that has an active offline alert
    // When: the POS device is unregistered from the location
    // Then: the device is removed and its associated alerts are also cleared
    [Fact]
    public async Task UnregisterDeviceAsync_ShouldAlsoClearAlerts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("POS", deviceId, "Register 1");
        await grain.AddAlertAsync(new DeviceAlert(
            deviceId, "POS", "Register 1",
            "Offline", "Device disconnected", DateTime.UtcNow));

        // Act
        await grain.UnregisterDeviceAsync("POS", deviceId);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.Alerts.Should().BeEmpty();
    }

    // Given: a venue location with two POS registers and a printer, some online and some offline
    // When: the device summary is requested
    // Then: the summary accurately reports total and online counts for each device type
    [Fact]
    public async Task GetSummaryAsync_ShouldReturnCompleteSummary()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var pos1 = Guid.NewGuid();
        var pos2 = Guid.NewGuid();
        var printer1 = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        await grain.RegisterDeviceAsync("POS", pos1, "Register 1");
        await grain.RegisterDeviceAsync("POS", pos2, "Register 2");
        await grain.RegisterDeviceAsync("Printer", printer1, "Receipt Printer");

        await grain.UpdateDeviceStatusAsync("POS", pos1, true);
        await grain.UpdateDeviceStatusAsync("Printer", printer1, true);

        // Act
        var summary = await grain.GetSummaryAsync();

        // Assert
        summary.TotalPosDevices.Should().Be(2);
        summary.OnlinePosDevices.Should().Be(1);
        summary.TotalPrinters.Should().Be(1);
        summary.OnlinePrinters.Should().Be(1);
    }
}
