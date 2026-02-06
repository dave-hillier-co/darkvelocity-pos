using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests.Grains.Devices;

/// <summary>
/// Comprehensive tests for PrinterGrain covering:
/// - Printer registration (various types and connection methods)
/// - Configuration updates
/// - Status management (online/offline)
/// - Print recording
/// - Error handling and edge cases
/// </summary>
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

    #region Registration Tests

    // Given: A new network receipt printer with IP address, port, and 80mm paper width
    // When: The printer is registered as the site default
    // Then: The printer is created as active with all network properties set and starts offline
    [Fact]
    public async Task RegisterAsync_NetworkPrinter_ShouldRegisterWithCorrectProperties()
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
        result.LocationId.Should().Be(locationId);
        result.Name.Should().Be("Receipt Printer 1");
        result.PrinterType.Should().Be(PrinterType.Receipt);
        result.ConnectionType.Should().Be(PrinterConnectionType.Network);
        result.IpAddress.Should().Be("192.168.1.100");
        result.Port.Should().Be(9100);
        result.PaperWidth.Should().Be(80);
        result.IsDefault.Should().BeTrue();
        result.IsActive.Should().BeTrue();
        result.IsOnline.Should().BeFalse(); // Starts offline until first print/ping
        result.MacAddress.Should().BeNull();
        result.UsbVendorId.Should().BeNull();
        result.UsbProductId.Should().BeNull();
    }

    // Given: A new USB receipt printer with vendor and product IDs
    // When: The printer is registered with USB connection type
    // Then: The USB identifiers are stored and network properties remain null
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
        result.IpAddress.Should().BeNull();
        result.Port.Should().BeNull();
    }

    // Given: A new Bluetooth receipt printer with a MAC address
    // When: The printer is registered with Bluetooth connection type
    // Then: The MAC address is stored correctly
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
            Name: "Mobile Bluetooth Printer",
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

    // Given: A new network kitchen printer for the hot line
    // When: The printer is registered as Kitchen type
    // Then: The printer type is set to Kitchen
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
        result.Name.Should().Be("Hot Line Printer");
    }

    // Given: A new USB label printer with 50mm paper width
    // When: The printer is registered as Label type
    // Then: The printer type is set to Label with the correct paper width
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
            Name: "Item Label Printer",
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
        result.PaperWidth.Should().Be(50);
    }

    // Given: A printer that has already been registered
    // When: A second registration is attempted on the same printer grain
    // Then: An exception is thrown indicating the printer is already registered
    [Fact]
    public async Task RegisterAsync_AlreadyRegistered_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "First Registration", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        // Act
        var act = () => grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Second Registration", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.101", 9100,
            null, null, null, 80, false));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    // Given: A new receipt printer
    // When: The printer is registered with IsDefault set to false
    // Then: The printer is not marked as the site default
    [Fact]
    public async Task RegisterAsync_NonDefaultPrinter_ShouldSetIsDefaultFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPrinterCommand(
            LocationId: Guid.NewGuid(),
            Name: "Secondary Printer",
            PrinterType: PrinterType.Receipt,
            ConnectionType: PrinterConnectionType.Network,
            IpAddress: "192.168.1.102",
            Port: 9100,
            MacAddress: null,
            UsbVendorId: null,
            UsbProductId: null,
            PaperWidth: 80,
            IsDefault: false));

        // Assert
        result.IsDefault.Should().BeFalse();
    }

    // Given: Two receipt printers with different paper widths (80mm and 58mm)
    // When: Both printers are registered
    // Then: Each printer preserves its respective paper width
    [Fact]
    public async Task RegisterAsync_WithDifferentPaperWidths_ShouldPreserveWidth()
    {
        // Arrange
        var orgId = Guid.NewGuid();

        // 80mm receipt printer
        var printer80Id = Guid.NewGuid();
        var printer80 = GetGrain(orgId, printer80Id);
        var result80 = await printer80.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "80mm Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        // 58mm thermal printer
        var printer58Id = Guid.NewGuid();
        var printer58 = GetGrain(orgId, printer58Id);
        var result58 = await printer58.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "58mm Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.101", 9100,
            null, null, null, 58, false));

        // Assert
        result80.PaperWidth.Should().Be(80);
        result58.PaperWidth.Should().Be(58);
    }

    #endregion

    #region Update Tests

    // Given: A registered network receipt printer
    // When: All configurable properties are updated including name, IP, port, character set, and capabilities
    // Then: All updated properties reflect the new values
    [Fact]
    public async Task UpdateAsync_ShouldUpdateAllProperties()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Initial Name", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, false));

        // Act
        var result = await grain.UpdateAsync(new UpdatePrinterCommand(
            Name: "Updated Printer Name",
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
        result.Name.Should().Be("Updated Printer Name");
        result.IpAddress.Should().Be("192.168.1.200");
        result.Port.Should().Be(9101);
        result.IsDefault.Should().BeTrue();
        result.CharacterSet.Should().Be("UTF-8");
        result.SupportsCut.Should().BeTrue();
        result.SupportsCashDrawer.Should().BeTrue();
    }

    // Given: A registered network receipt printer with specific IP and port
    // When: Only the name is updated with all other fields set to null
    // Then: The name changes while IP, port, and default status remain unchanged
    [Fact]
    public async Task UpdateAsync_PartialUpdate_ShouldOnlyChangeSpecifiedProperties()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Original Name", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        // Act - only update name
        var result = await grain.UpdateAsync(new UpdatePrinterCommand(
            Name: "New Name",
            IpAddress: null,
            Port: null,
            MacAddress: null,
            PaperWidth: null,
            IsDefault: null,
            IsActive: null,
            CharacterSet: null,
            SupportsCut: null,
            SupportsCashDrawer: null));

        // Assert
        result.Name.Should().Be("New Name");
        result.IpAddress.Should().Be("192.168.1.100"); // Unchanged
        result.Port.Should().Be(9100); // Unchanged
        result.IsDefault.Should().BeTrue(); // Unchanged
    }

    // Given: A registered receipt printer without cash drawer support
    // When: Paper cut and cash drawer support capabilities are enabled via update
    // Then: Both capabilities are set to true
    [Fact]
    public async Task UpdateAsync_SetCashDrawerSupport_ShouldEnableCashDrawer()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Receipt Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        // Act
        var result = await grain.UpdateAsync(new UpdatePrinterCommand(
            Name: null, IpAddress: null, Port: null, MacAddress: null,
            PaperWidth: null, IsDefault: null, IsActive: null,
            CharacterSet: null,
            SupportsCut: true,
            SupportsCashDrawer: true));

        // Assert
        result.SupportsCut.Should().BeTrue();
        result.SupportsCashDrawer.Should().BeTrue();
    }

    // Given: A printer grain that has never been registered
    // When: An update is attempted
    // Then: An exception is thrown indicating the printer is not initialized
    [Fact]
    public async Task UpdateAsync_BeforeRegistration_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var act = () => grain.UpdateAsync(new UpdatePrinterCommand(
            Name: "New Name",
            IpAddress: null, Port: null, MacAddress: null,
            PaperWidth: null, IsDefault: null, IsActive: null,
            CharacterSet: null, SupportsCut: null, SupportsCashDrawer: null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    // Given: A registered receipt printer with default character set
    // When: The character set is updated to ISO-8859-1
    // Then: The character set reflects the new value
    [Fact]
    public async Task UpdateAsync_ChangeCharacterSet_ShouldUpdateCharacterSet()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "International Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        // Act
        var result = await grain.UpdateAsync(new UpdatePrinterCommand(
            Name: null, IpAddress: null, Port: null, MacAddress: null,
            PaperWidth: null, IsDefault: null, IsActive: null,
            CharacterSet: "ISO-8859-1",
            SupportsCut: null, SupportsCashDrawer: null));

        // Assert
        result.CharacterSet.Should().Be("ISO-8859-1");
    }

    // Given: A registered active printer
    // When: The IsActive flag is set to false via update
    // Then: The printer becomes inactive
    [Fact]
    public async Task UpdateAsync_Deactivate_ShouldSetInactive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Printer to Deactivate", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        // Act
        var result = await grain.UpdateAsync(new UpdatePrinterCommand(
            Name: null, IpAddress: null, Port: null, MacAddress: null,
            PaperWidth: null, IsDefault: null,
            IsActive: false,
            CharacterSet: null, SupportsCut: null, SupportsCashDrawer: null));

        // Assert
        result.IsActive.Should().BeFalse();
    }

    #endregion

    #region Deactivation Tests

    // Given: A registered printer that has been set online
    // When: The printer is deactivated
    // Then: The printer becomes both inactive and offline
    [Fact]
    public async Task DeactivateAsync_ShouldSetInactiveAndOffline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Active Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));
        await grain.SetOnlineAsync(true);

        // Act
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
        snapshot.IsOnline.Should().BeFalse();
    }

    // Given: A printer grain that has never been registered
    // When: Deactivation is attempted
    // Then: An exception is thrown indicating the printer is not initialized
    [Fact]
    public async Task DeactivateAsync_BeforeRegistration_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var act = () => grain.DeactivateAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    // Given: A registered printer that has already been deactivated
    // When: Deactivation is called a second time
    // Then: The operation is idempotent and no exception is thrown
    [Fact]
    public async Task DeactivateAsync_MultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        // Act
        await grain.DeactivateAsync();
        await grain.DeactivateAsync(); // Second call

        // Assert - should not throw
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
    }

    #endregion

    #region Print Recording Tests

    // Given: A registered receipt printer that starts offline
    // When: A print is recorded
    // Then: The last-print timestamp is set and the printer is marked online
    [Fact]
    public async Task RecordPrintAsync_ShouldUpdateLastPrintAndSetOnline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Print Test Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        // Act
        await grain.RecordPrintAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsOnline.Should().BeTrue();
        snapshot.LastPrintAt.Should().NotBeNull();
        snapshot.LastPrintAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: A registered receipt printer that has already printed once
    // When: A second print is recorded after a brief delay
    // Then: The last-print timestamp advances to the more recent print time
    [Fact]
    public async Task RecordPrintAsync_MultipleTimes_ShouldUpdateLastPrintEachTime()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Busy Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        // Act
        await grain.RecordPrintAsync();
        var firstPrintTime = (await grain.GetSnapshotAsync()).LastPrintAt;

        await Task.Delay(50); // Small delay to ensure different timestamp
        await grain.RecordPrintAsync();
        var secondPrintTime = (await grain.GetSnapshotAsync()).LastPrintAt;

        // Assert
        secondPrintTime.Should().BeAfter(firstPrintTime!.Value);
    }

    // Given: A printer grain that has never been registered
    // When: A print recording is attempted
    // Then: An exception is thrown indicating the printer is not initialized
    [Fact]
    public async Task RecordPrintAsync_BeforeRegistration_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var act = () => grain.RecordPrintAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    // Given: A registered receipt printer that is currently offline
    // When: A print is successfully recorded
    // Then: The printer is automatically set to online
    [Fact]
    public async Task RecordPrintAsync_WhenOffline_ShouldSetOnline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Offline Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        // Verify initially offline
        var isOnlineBefore = await grain.IsOnlineAsync();
        isOnlineBefore.Should().BeFalse();

        // Act
        await grain.RecordPrintAsync();

        // Assert
        var isOnlineAfter = await grain.IsOnlineAsync();
        isOnlineAfter.Should().BeTrue();
    }

    #endregion

    #region Online Status Tests

    // Given: A registered printer that starts offline
    // When: The printer is set to online
    // Then: The printer reports as online
    [Fact]
    public async Task SetOnlineAsync_True_ShouldSetOnline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Online Test Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        // Act
        await grain.SetOnlineAsync(true);

        // Assert
        var isOnline = await grain.IsOnlineAsync();
        isOnline.Should().BeTrue();
    }

    // Given: A registered printer that has been set online
    // When: The printer is set to offline
    // Then: The printer reports as offline
    [Fact]
    public async Task SetOnlineAsync_False_ShouldSetOffline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Offline Test Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));
        await grain.SetOnlineAsync(true);

        // Act
        await grain.SetOnlineAsync(false);

        // Assert
        var isOnline = await grain.IsOnlineAsync();
        isOnline.Should().BeFalse();
    }

    // Given: A registered receipt printer
    // When: The online status is toggled between online and offline multiple times
    // Then: The printer correctly tracks each status change
    [Fact]
    public async Task SetOnlineAsync_Toggle_ShouldTrackStateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Toggle Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        // Act & Assert - toggle multiple times
        await grain.SetOnlineAsync(true);
        (await grain.IsOnlineAsync()).Should().BeTrue();

        await grain.SetOnlineAsync(false);
        (await grain.IsOnlineAsync()).Should().BeFalse();

        await grain.SetOnlineAsync(true);
        (await grain.IsOnlineAsync()).Should().BeTrue();
    }

    // Given: A printer grain that has never been registered
    // When: Setting the printer online is attempted
    // Then: An exception is thrown indicating the printer is not initialized
    [Fact]
    public async Task SetOnlineAsync_BeforeRegistration_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var act = () => grain.SetOnlineAsync(true);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    // Given: A newly registered receipt printer
    // When: The online status is checked immediately after registration
    // Then: The printer is offline (printers start offline until first print or ping)
    [Fact]
    public async Task IsOnlineAsync_WhenNewlyRegistered_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "New Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        // Act
        var isOnline = await grain.IsOnlineAsync();

        // Assert
        isOnline.Should().BeFalse();
    }

    // Given: A printer grain that has never been registered
    // When: The online status is checked
    // Then: An exception is thrown indicating the printer is not initialized
    [Fact]
    public async Task IsOnlineAsync_BeforeRegistration_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var act = () => grain.IsOnlineAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    #endregion

    #region GetSnapshot Tests

    // Given: A registered kitchen printer that has been updated with capabilities and has recorded a print
    // When: The snapshot is retrieved
    // Then: The snapshot contains all registration, update, and runtime state including last-print timestamp
    [Fact]
    public async Task GetSnapshotAsync_ShouldReturnCompleteSnapshot()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            LocationId: locationId,
            Name: "Complete Printer",
            PrinterType: PrinterType.Kitchen,
            ConnectionType: PrinterConnectionType.Network,
            IpAddress: "192.168.1.100",
            Port: 9100,
            MacAddress: null,
            UsbVendorId: null,
            UsbProductId: null,
            PaperWidth: 80,
            IsDefault: true));

        await grain.UpdateAsync(new UpdatePrinterCommand(
            Name: null, IpAddress: null, Port: null, MacAddress: null,
            PaperWidth: null, IsDefault: null, IsActive: null,
            CharacterSet: "UTF-8",
            SupportsCut: true,
            SupportsCashDrawer: false));

        await grain.RecordPrintAsync();

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.PrinterId.Should().Be(printerId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.Name.Should().Be("Complete Printer");
        snapshot.PrinterType.Should().Be(PrinterType.Kitchen);
        snapshot.ConnectionType.Should().Be(PrinterConnectionType.Network);
        snapshot.IpAddress.Should().Be("192.168.1.100");
        snapshot.Port.Should().Be(9100);
        snapshot.PaperWidth.Should().Be(80);
        snapshot.IsDefault.Should().BeTrue();
        snapshot.IsActive.Should().BeTrue();
        snapshot.IsOnline.Should().BeTrue();
        snapshot.CharacterSet.Should().Be("UTF-8");
        snapshot.SupportsCut.Should().BeTrue();
        snapshot.SupportsCashDrawer.Should().BeFalse();
        snapshot.LastPrintAt.Should().NotBeNull();
    }

    // Given: A printer grain that has never been registered
    // When: A snapshot is requested
    // Then: An exception is thrown indicating the printer is not initialized
    [Fact]
    public async Task GetSnapshotAsync_BeforeRegistration_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var act = () => grain.GetSnapshotAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    // Given: A registered receipt printer that has been updated multiple times
    // When: The snapshot is retrieved after the final update
    // Then: The snapshot reflects all accumulated changes from the sequence of updates
    [Fact]
    public async Task GetSnapshotAsync_AfterMultipleUpdates_ShouldReflectLatestState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Initial", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, false));

        // Act - multiple updates
        await grain.UpdateAsync(new UpdatePrinterCommand(
            Name: "Update 1", IpAddress: null, Port: null, MacAddress: null,
            PaperWidth: null, IsDefault: null, IsActive: null,
            CharacterSet: null, SupportsCut: null, SupportsCashDrawer: null));

        await grain.UpdateAsync(new UpdatePrinterCommand(
            Name: "Update 2", IpAddress: "192.168.1.200", Port: null, MacAddress: null,
            PaperWidth: null, IsDefault: null, IsActive: null,
            CharacterSet: null, SupportsCut: null, SupportsCashDrawer: null));

        await grain.UpdateAsync(new UpdatePrinterCommand(
            Name: "Final Name", IpAddress: null, Port: 9200, MacAddress: null,
            PaperWidth: null, IsDefault: true, IsActive: null,
            CharacterSet: null, SupportsCut: null, SupportsCashDrawer: null));

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Name.Should().Be("Final Name");
        snapshot.IpAddress.Should().Be("192.168.1.200");
        snapshot.Port.Should().Be(9200);
        snapshot.IsDefault.Should().BeTrue();
    }

    #endregion

    #region Edge Cases

    // Given: A new USB receipt printer with only required fields
    // When: The printer is registered without optional connection details
    // Then: The registration succeeds with the minimal configuration
    [Fact]
    public async Task RegisterAsync_WithMinimalRequiredFields_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPrinterCommand(
            LocationId: Guid.NewGuid(),
            Name: "Minimal Printer",
            PrinterType: PrinterType.Receipt,
            ConnectionType: PrinterConnectionType.Usb,
            IpAddress: null,
            Port: null,
            MacAddress: null,
            UsbVendorId: null,
            UsbProductId: null,
            PaperWidth: 80,
            IsDefault: false));

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Minimal Printer");
    }

    // Given: A new printer
    // When: The printer is registered with a name containing special characters
    // Then: The special characters are preserved in the name
    [Fact]
    public async Task RegisterAsync_WithSpecialCharactersInName_ShouldPreserveName()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPrinterCommand(
            LocationId: Guid.NewGuid(),
            Name: "Printer #1 (Kitchen-Hot) @ Bar",
            PrinterType: PrinterType.Kitchen,
            ConnectionType: PrinterConnectionType.Network,
            IpAddress: "192.168.1.100",
            Port: 9100,
            MacAddress: null,
            UsbVendorId: null,
            UsbProductId: null,
            PaperWidth: 80,
            IsDefault: false));

        // Assert
        result.Name.Should().Be("Printer #1 (Kitchen-Hot) @ Bar");
    }

    // Given: A new printer
    // When: The printer is registered with a Unicode name containing accented characters
    // Then: The Unicode characters are preserved in the name
    [Fact]
    public async Task RegisterAsync_WithUnicodeInName_ShouldPreserveName()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPrinterCommand(
            LocationId: Guid.NewGuid(),
            Name: "Imprimante principale",
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
        result.Name.Should().Be("Imprimante principale");
    }

    // Given: Two different organizations each with the same printer ID
    // When: Each organization registers its printer with different configurations
    // Then: The printers are isolated by tenant boundary and maintain independent state
    [Fact]
    public async Task Grain_WithDifferentOrganizations_ShouldBeIsolated()
    {
        // Arrange
        var org1 = Guid.NewGuid();
        var org2 = Guid.NewGuid();
        var printerId = Guid.NewGuid(); // Same printer ID, different orgs

        var grain1 = GetGrain(org1, printerId);
        var grain2 = GetGrain(org2, printerId);

        // Act
        await grain1.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Org1 Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        await grain2.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Org2 Printer", PrinterType.Kitchen,
            PrinterConnectionType.Network, "192.168.1.200", 9100,
            null, null, null, 80, false));

        // Assert
        var snapshot1 = await grain1.GetSnapshotAsync();
        var snapshot2 = await grain2.GetSnapshotAsync();

        snapshot1.Name.Should().Be("Org1 Printer");
        snapshot1.PrinterType.Should().Be(PrinterType.Receipt);

        snapshot2.Name.Should().Be("Org2 Printer");
        snapshot2.PrinterType.Should().Be(PrinterType.Kitchen);
    }

    // Given: A new network receipt printer
    // When: The printer is registered with a non-standard LPD port (515)
    // Then: The custom port number is preserved
    [Fact]
    public async Task RegisterAsync_WithCustomPort_ShouldPreservePort()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act - use non-standard port
        var result = await grain.RegisterAsync(new RegisterPrinterCommand(
            LocationId: Guid.NewGuid(),
            Name: "Custom Port Printer",
            PrinterType: PrinterType.Receipt,
            ConnectionType: PrinterConnectionType.Network,
            IpAddress: "192.168.1.100",
            Port: 515, // LPD port
            MacAddress: null,
            UsbVendorId: null,
            UsbProductId: null,
            PaperWidth: 80,
            IsDefault: false));

        // Assert
        result.Port.Should().Be(515);
    }

    #endregion

    #region State Persistence Tests

    // Given: A registered printer that has been updated, set online, and recorded a print
    // When: A new grain reference is obtained for the same printer
    // Then: All state including name, character set, capabilities, and online status is persisted
    [Fact]
    public async Task Printer_StatePersistedAcrossGrainRetrieval()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        // First grain instance
        var grain1 = GetGrain(orgId, printerId);
        await grain1.RegisterAsync(new RegisterPrinterCommand(
            locationId, "Persisted Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));
        await grain1.RecordPrintAsync();
        await grain1.UpdateAsync(new UpdatePrinterCommand(
            Name: "Updated Name", IpAddress: null, Port: null, MacAddress: null,
            PaperWidth: null, IsDefault: null, IsActive: null,
            CharacterSet: "UTF-8", SupportsCut: true, SupportsCashDrawer: false));

        // Get a new grain reference (simulates grain reactivation)
        var grain2 = GetGrain(orgId, printerId);

        // Act
        var snapshot = await grain2.GetSnapshotAsync();

        // Assert
        snapshot.PrinterId.Should().Be(printerId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.Name.Should().Be("Updated Name");
        snapshot.CharacterSet.Should().Be("UTF-8");
        snapshot.SupportsCut.Should().BeTrue();
        snapshot.IsOnline.Should().BeTrue();
        snapshot.LastPrintAt.Should().NotBeNull();
    }

    #endregion

    #region Connection Type Specific Tests

    // Given: A new network printer
    // When: The printer is registered with an IPv6 address
    // Then: The IPv6 address is stored correctly
    [Fact]
    public async Task RegisterAsync_NetworkPrinter_WithIPv6_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterPrinterCommand(
            LocationId: Guid.NewGuid(),
            Name: "IPv6 Printer",
            PrinterType: PrinterType.Receipt,
            ConnectionType: PrinterConnectionType.Network,
            IpAddress: "fe80::1",
            Port: 9100,
            MacAddress: null,
            UsbVendorId: null,
            UsbProductId: null,
            PaperWidth: 80,
            IsDefault: false));

        // Assert
        result.IpAddress.Should().Be("fe80::1");
    }

    // Given: A registered network printer with a known IP address and port
    // When: Both the IP address and port are updated
    // Then: The new network address is stored
    [Fact]
    public async Task UpdateAsync_ChangeNetworkAddress_ShouldUpdateAddress()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Network Printer", PrinterType.Receipt,
            PrinterConnectionType.Network, "192.168.1.100", 9100,
            null, null, null, 80, true));

        // Act - update IP and port
        var result = await grain.UpdateAsync(new UpdatePrinterCommand(
            Name: null,
            IpAddress: "192.168.2.50",
            Port: 9200,
            MacAddress: null,
            PaperWidth: null, IsDefault: null, IsActive: null,
            CharacterSet: null, SupportsCut: null, SupportsCashDrawer: null));

        // Assert
        result.IpAddress.Should().Be("192.168.2.50");
        result.Port.Should().Be(9200);
    }

    // Given: A registered Bluetooth printer with a known MAC address
    // When: The MAC address is updated to a new value
    // Then: The new MAC address is stored
    [Fact]
    public async Task UpdateAsync_ChangeMacAddress_ShouldUpdateAddress()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, printerId);
        await grain.RegisterAsync(new RegisterPrinterCommand(
            Guid.NewGuid(), "Bluetooth Printer", PrinterType.Receipt,
            PrinterConnectionType.Bluetooth, null, null,
            "AA:BB:CC:DD:EE:FF", null, null, 58, false));

        // Act
        var result = await grain.UpdateAsync(new UpdatePrinterCommand(
            Name: null, IpAddress: null, Port: null,
            MacAddress: "11:22:33:44:55:66",
            PaperWidth: null, IsDefault: null, IsActive: null,
            CharacterSet: null, SupportsCut: null, SupportsCashDrawer: null));

        // Assert
        result.MacAddress.Should().Be("11:22:33:44:55:66");
    }

    #endregion
}
