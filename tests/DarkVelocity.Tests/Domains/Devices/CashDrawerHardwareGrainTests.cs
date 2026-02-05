using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests.Domains.Devices;

/// <summary>
/// Comprehensive tests for CashDrawerHardwareGrain.
/// Tests cash drawer hardware registration, configuration, and operations.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
[Trait("Domain", "Devices")]
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

    #region Registration Tests

    [Fact]
    public async Task RegisterAsync_PrinterConnected_ShouldRegisterDrawerWithPrinterId()
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
        result.IpAddress.Should().BeNull();
        result.Port.Should().BeNull();
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_NetworkConnected_ShouldRegisterWithNetworkDetails()
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
        result.PrinterId.Should().BeNull();
        result.IpAddress.Should().BeNull();
        result.Port.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_ShouldSetDefaultPulseSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterCashDrawerCommand(
            LocationId: Guid.NewGuid(),
            Name: "Default Pulse Drawer",
            PrinterId: Guid.NewGuid(),
            ConnectionType: CashDrawerConnectionType.Printer,
            IpAddress: null,
            Port: null));

        // Assert
        result.KickPulsePin.Should().Be(0);
        result.KickPulseOnTime.Should().Be(100);
        result.KickPulseOffTime.Should().Be(100);
    }

    [Fact]
    public async Task RegisterAsync_ShouldInitializeLastOpenedAtAsNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterCashDrawerCommand(
            LocationId: Guid.NewGuid(),
            Name: "New Drawer",
            PrinterId: null,
            ConnectionType: CashDrawerConnectionType.Usb,
            IpAddress: null,
            Port: null));

        // Assert
        result.LastOpenedAt.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_AlreadyRegistered_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "First Drawer", null,
            CashDrawerConnectionType.Usb, null, null));

        // Act
        var act = () => grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Second Drawer", null,
            CashDrawerConnectionType.Usb, null, null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public async Task RegisterAsync_NetworkWithoutPort_ShouldRegisterWithNullPort()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterCashDrawerCommand(
            LocationId: Guid.NewGuid(),
            Name: "Network No Port",
            PrinterId: null,
            ConnectionType: CashDrawerConnectionType.Network,
            IpAddress: "10.0.0.50",
            Port: null));

        // Assert
        result.IpAddress.Should().Be("10.0.0.50");
        result.Port.Should().BeNull();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateAsync_ShouldUpdateName()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Original Name", null,
            CashDrawerConnectionType.Usb, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdateCashDrawerCommand(
            Name: "Updated Name",
            PrinterId: null,
            IpAddress: null,
            Port: null,
            IsActive: null,
            KickPulsePin: null,
            KickPulseOnTime: null,
            KickPulseOffTime: null));

        // Assert
        result.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdatePrinterId()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var originalPrinterId = Guid.NewGuid();
        var newPrinterId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Drawer", originalPrinterId,
            CashDrawerConnectionType.Printer, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdateCashDrawerCommand(
            Name: null,
            PrinterId: newPrinterId,
            IpAddress: null,
            Port: null,
            IsActive: null,
            KickPulsePin: null,
            KickPulseOnTime: null,
            KickPulseOffTime: null));

        // Assert
        result.PrinterId.Should().Be(newPrinterId);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateNetworkSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Network Drawer", null,
            CashDrawerConnectionType.Network, "192.168.1.100", 4000));

        // Act
        var result = await grain.UpdateAsync(new UpdateCashDrawerCommand(
            Name: null,
            PrinterId: null,
            IpAddress: "192.168.1.200",
            Port: 4001,
            IsActive: null,
            KickPulsePin: null,
            KickPulseOnTime: null,
            KickPulseOffTime: null));

        // Assert
        result.IpAddress.Should().Be("192.168.1.200");
        result.Port.Should().Be(4001);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdatePulseSettings()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Pulse Test", Guid.NewGuid(),
            CashDrawerConnectionType.Printer, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdateCashDrawerCommand(
            Name: null,
            PrinterId: null,
            IpAddress: null,
            Port: null,
            IsActive: null,
            KickPulsePin: 1,
            KickPulseOnTime: 200,
            KickPulseOffTime: 150));

        // Assert
        result.KickPulsePin.Should().Be(1);
        result.KickPulseOnTime.Should().Be(200);
        result.KickPulseOffTime.Should().Be(150);
    }

    [Fact]
    public async Task UpdateAsync_ShouldDeactivateViaIsActive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Active Drawer", null,
            CashDrawerConnectionType.Usb, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdateCashDrawerCommand(
            Name: null,
            PrinterId: null,
            IpAddress: null,
            Port: null,
            IsActive: false,
            KickPulsePin: null,
            KickPulseOnTime: null,
            KickPulseOffTime: null));

        // Assert
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_ShouldReactivateDeactivatedDrawer()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Reactivate Test", null,
            CashDrawerConnectionType.Usb, null, null));
        await grain.DeactivateAsync();

        // Act
        var result = await grain.UpdateAsync(new UpdateCashDrawerCommand(
            Name: null,
            PrinterId: null,
            IpAddress: null,
            Port: null,
            IsActive: true,
            KickPulsePin: null,
            KickPulseOnTime: null,
            KickPulseOffTime: null));

        // Assert
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_WithAllNullFields_ShouldNotChangeState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "No Change Test", printerId,
            CashDrawerConnectionType.Printer, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdateCashDrawerCommand(
            Name: null,
            PrinterId: null,
            IpAddress: null,
            Port: null,
            IsActive: null,
            KickPulsePin: null,
            KickPulseOnTime: null,
            KickPulseOffTime: null));

        // Assert
        result.Name.Should().Be("No Change Test");
        result.PrinterId.Should().Be(printerId);
        result.IsActive.Should().BeTrue();
        result.KickPulsePin.Should().Be(0);
        result.KickPulseOnTime.Should().Be(100);
        result.KickPulseOffTime.Should().Be(100);
    }

    [Fact]
    public async Task UpdateAsync_OnUninitializedGrain_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act
        var act = () => grain.UpdateAsync(new UpdateCashDrawerCommand(
            Name: "Test",
            PrinterId: null,
            IpAddress: null,
            Port: null,
            IsActive: null,
            KickPulsePin: null,
            KickPulseOnTime: null,
            KickPulseOffTime: null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public async Task UpdateAsync_MultipleFieldsAtOnce_ShouldUpdateAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var newPrinterId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Multi Update", null,
            CashDrawerConnectionType.Usb, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdateCashDrawerCommand(
            Name: "New Name",
            PrinterId: newPrinterId,
            IpAddress: "10.0.0.1",
            Port: 5000,
            IsActive: true,
            KickPulsePin: 1,
            KickPulseOnTime: 300,
            KickPulseOffTime: 250));

        // Assert
        result.Name.Should().Be("New Name");
        result.PrinterId.Should().Be(newPrinterId);
        result.IpAddress.Should().Be("10.0.0.1");
        result.Port.Should().Be(5000);
        result.IsActive.Should().BeTrue();
        result.KickPulsePin.Should().Be(1);
        result.KickPulseOnTime.Should().Be(300);
        result.KickPulseOffTime.Should().Be(250);
    }

    #endregion

    #region Deactivate Tests

    [Fact]
    public async Task DeactivateAsync_ShouldSetIsActiveFalse()
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

    [Fact]
    public async Task DeactivateAsync_OnUninitializedGrain_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act
        var act = () => grain.DeactivateAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public async Task DeactivateAsync_AlreadyDeactivated_ShouldRemainDeactivated()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Double Deactivate", null,
            CashDrawerConnectionType.Usb, null, null));
        await grain.DeactivateAsync();

        // Act
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivateAsync_ShouldPreserveOtherProperties()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            locationId, "Preserve Props", printerId,
            CashDrawerConnectionType.Printer, null, null));

        // Act
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Name.Should().Be("Preserve Props");
        snapshot.LocationId.Should().Be(locationId);
        snapshot.PrinterId.Should().Be(printerId);
        snapshot.ConnectionType.Should().Be(CashDrawerConnectionType.Printer);
    }

    #endregion

    #region RecordOpen Tests

    [Fact]
    public async Task RecordOpenAsync_ShouldSetLastOpenedAt()
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
        snapshot.LastOpenedAt.Should().NotBeNull();
        snapshot.LastOpenedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RecordOpenAsync_MultipleTimes_ShouldUpdateTimestamp()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Multi Open", Guid.NewGuid(),
            CashDrawerConnectionType.Printer, null, null));

        // Act
        await grain.RecordOpenAsync();
        var firstOpen = (await grain.GetSnapshotAsync()).LastOpenedAt;

        await Task.Delay(100); // Small delay to ensure different timestamp

        await grain.RecordOpenAsync();
        var secondOpen = (await grain.GetSnapshotAsync()).LastOpenedAt;

        // Assert
        secondOpen.Should().BeAfter(firstOpen!.Value);
    }

    [Fact]
    public async Task RecordOpenAsync_OnUninitializedGrain_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act
        var act = () => grain.RecordOpenAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public async Task RecordOpenAsync_OnDeactivatedDrawer_ShouldStillRecord()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Deactivated Open", null,
            CashDrawerConnectionType.Usb, null, null));
        await grain.DeactivateAsync();

        // Act
        await grain.RecordOpenAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastOpenedAt.Should().NotBeNull();
        snapshot.IsActive.Should().BeFalse();
    }

    #endregion

    #region GetKickCommand Tests

    [Fact]
    public async Task GetKickCommandAsync_ShouldReturnEscPosFormat()
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
        kickCommand.Should().StartWith("\\x1B\\x70");
    }

    [Fact]
    public async Task GetKickCommandAsync_WithDefaultSettings_ShouldReturnPin0()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Default Pin", Guid.NewGuid(),
            CashDrawerConnectionType.Printer, null, null));

        // Act
        var kickCommand = await grain.GetKickCommandAsync();

        // Assert
        // Default pin is 0, so command should contain \x00 for pin
        kickCommand.Should().Contain("\\x00");
    }

    [Fact]
    public async Task GetKickCommandAsync_WithCustomPin_ShouldReflectInCommand()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Custom Pin", Guid.NewGuid(),
            CashDrawerConnectionType.Printer, null, null));
        await grain.UpdateAsync(new UpdateCashDrawerCommand(
            null, null, null, null, null, 1, null, null));

        // Act
        var kickCommand = await grain.GetKickCommandAsync();

        // Assert
        // Pin 1 should be \x01 in the command
        kickCommand.Should().Contain("\\x01");
    }

    [Fact]
    public async Task GetKickCommandAsync_WithCustomTiming_ShouldCalculateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Custom Timing", Guid.NewGuid(),
            CashDrawerConnectionType.Printer, null, null));
        await grain.UpdateAsync(new UpdateCashDrawerCommand(
            null, null, null, null, null, 0, 200, 200));

        // Act
        var kickCommand = await grain.GetKickCommandAsync();

        // Assert
        // ESC/POS timing is divided by 2, so 200ms becomes 100 (0x64)
        kickCommand.Should().Contain("\\x64");
    }

    [Fact]
    public async Task GetKickCommandAsync_OnUninitializedGrain_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act
        var act = () => grain.GetKickCommandAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public async Task GetKickCommandAsync_UsbDrawer_ShouldStillReturnCommand()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "USB Kick", null,
            CashDrawerConnectionType.Usb, null, null));

        // Act
        var kickCommand = await grain.GetKickCommandAsync();

        // Assert
        kickCommand.Should().StartWith("\\x1B\\x70");
    }

    #endregion

    #region GetSnapshot Tests

    [Fact]
    public async Task GetSnapshotAsync_ShouldReturnCompleteState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            locationId, "Complete Snapshot", printerId,
            CashDrawerConnectionType.Printer, null, null));
        await grain.UpdateAsync(new UpdateCashDrawerCommand(
            null, null, null, null, null, 1, 150, 120));
        await grain.RecordOpenAsync();

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.CashDrawerId.Should().Be(drawerId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.Name.Should().Be("Complete Snapshot");
        snapshot.PrinterId.Should().Be(printerId);
        snapshot.ConnectionType.Should().Be(CashDrawerConnectionType.Printer);
        snapshot.IsActive.Should().BeTrue();
        snapshot.KickPulsePin.Should().Be(1);
        snapshot.KickPulseOnTime.Should().Be(150);
        snapshot.KickPulseOffTime.Should().Be(120);
        snapshot.LastOpenedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSnapshotAsync_OnUninitializedGrain_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act
        var act = () => grain.GetSnapshotAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public async Task GetSnapshotAsync_AfterMultipleOperations_ShouldReflectLatestState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Initial", null,
            CashDrawerConnectionType.Usb, null, null));

        // Act - multiple operations
        await grain.UpdateAsync(new UpdateCashDrawerCommand(
            "Updated Once", null, null, null, null, null, null, null));
        await grain.UpdateAsync(new UpdateCashDrawerCommand(
            "Updated Twice", null, null, null, null, null, null, null));
        await grain.DeactivateAsync();
        await grain.UpdateAsync(new UpdateCashDrawerCommand(
            null, null, null, null, true, null, null, null));
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Name.Should().Be("Updated Twice");
        snapshot.IsActive.Should().BeTrue();
    }

    #endregion

    #region Connection Type Specific Tests

    [Fact]
    public async Task PrinterConnectionType_ShouldRequirePrinterId()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act - register without printer ID but with printer connection type
        var result = await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "No Printer Link", null,
            CashDrawerConnectionType.Printer, null, null));

        // Assert - should still work, just no printer linked
        result.ConnectionType.Should().Be(CashDrawerConnectionType.Printer);
        result.PrinterId.Should().BeNull();
    }

    [Fact]
    public async Task NetworkConnectionType_ShouldStoreNetworkDetails()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Network Details",
            null, CashDrawerConnectionType.Network,
            "192.168.1.100", 9100));

        // Assert
        result.ConnectionType.Should().Be(CashDrawerConnectionType.Network);
        result.IpAddress.Should().Be("192.168.1.100");
        result.Port.Should().Be(9100);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task RegisterAsync_WithEmptyName_ShouldAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act
        var result = await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), string.Empty, null,
            CashDrawerConnectionType.Usb, null, null));

        // Assert
        result.Name.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateAsync_WithZeroPulseTiming_ShouldAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Zero Timing", null,
            CashDrawerConnectionType.Usb, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdateCashDrawerCommand(
            null, null, null, null, null, 0, 0, 0));

        // Assert
        result.KickPulseOnTime.Should().Be(0);
        result.KickPulseOffTime.Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_WithLargePulseTiming_ShouldAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "Large Timing", null,
            CashDrawerConnectionType.Usb, null, null));

        // Act
        var result = await grain.UpdateAsync(new UpdateCashDrawerCommand(
            null, null, null, null, null, 0, 1000, 1000));

        // Assert
        result.KickPulseOnTime.Should().Be(1000);
        result.KickPulseOffTime.Should().Be(1000);
    }

    [Fact]
    public async Task RegisterAsync_WithLongName_ShouldAccept()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        var longName = new string('A', 500);

        // Act
        var result = await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), longName, null,
            CashDrawerConnectionType.Usb, null, null));

        // Assert
        result.Name.Should().Be(longName);
    }

    [Fact]
    public async Task UpdateAsync_IpAddressFormat_ShouldAcceptAnyString()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            Guid.NewGuid(), "IP Test", null,
            CashDrawerConnectionType.Network, null, null));

        // Act - test with various IP formats
        var result = await grain.UpdateAsync(new UpdateCashDrawerCommand(
            null, null, "drawer.local", 80, null, null, null, null));

        // Assert
        result.IpAddress.Should().Be("drawer.local");
    }

    #endregion

    #region State Persistence Tests

    [Fact]
    public async Task StateChanges_ShouldPersistAcrossMultipleOperations()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, drawerId);

        // Act - perform multiple operations
        await grain.RegisterAsync(new RegisterCashDrawerCommand(
            locationId, "Persist Test", printerId,
            CashDrawerConnectionType.Printer, null, null));

        await grain.UpdateAsync(new UpdateCashDrawerCommand(
            "Updated Name", null, null, null, null, 1, 150, 150));

        await grain.RecordOpenAsync();

        await grain.DeactivateAsync();

        // Assert
        var finalSnapshot = await grain.GetSnapshotAsync();
        finalSnapshot.Name.Should().Be("Updated Name");
        finalSnapshot.KickPulsePin.Should().Be(1);
        finalSnapshot.KickPulseOnTime.Should().Be(150);
        finalSnapshot.KickPulseOffTime.Should().Be(150);
        finalSnapshot.LastOpenedAt.Should().NotBeNull();
        finalSnapshot.IsActive.Should().BeFalse();
    }

    #endregion
}
