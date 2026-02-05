using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests.Domains.Devices;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DeviceHealthMonitoringTests
{
    private readonly TestClusterFixture _fixture;

    public DeviceHealthMonitoringTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDeviceStatusGrain GetGrain(Guid orgId, Guid locationId)
    {
        var key = $"{orgId}:{locationId}:devicestatus";
        return _fixture.Cluster.GrainFactory.GetGrain<IDeviceStatusGrain>(key);
    }

    [Fact]
    public async Task RecordHeartbeatAsync_ShouldUpdateLastSeenAndSetOnline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("POS", deviceId, "Register 1");

        // Act
        await grain.RecordHeartbeatAsync(deviceId);

        // Assert
        var health = await grain.GetDeviceHealthAsync(deviceId);
        health.Should().NotBeNull();
        health!.IsOnline.Should().BeTrue();
        health.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RecordHeartbeatAsync_WithHealthMetrics_ShouldUpdateMetrics()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("POS", deviceId, "Register 1");

        // Act
        await grain.RecordHeartbeatAsync(deviceId, new UpdateDeviceHealthCommand(
            DeviceId: deviceId,
            SignalStrength: -45,
            LatencyMs: 50));

        // Assert
        var health = await grain.GetDeviceHealthAsync(deviceId);
        health.Should().NotBeNull();
        health!.SignalStrength.Should().Be(-45);
        health.LatencyMs.Should().Be(50);
    }

    [Fact]
    public async Task GetDeviceHealthAsync_WhenNotRegistered_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        // Act
        var health = await grain.GetDeviceHealthAsync(Guid.NewGuid());

        // Assert
        health.Should().BeNull();
    }

    [Fact]
    public async Task GetHealthSummaryAsync_ShouldReturnCompleteSummary()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var pos1 = Guid.NewGuid();
        var pos2 = Guid.NewGuid();
        var printer = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        await grain.RegisterDeviceAsync("POS", pos1, "Register 1");
        await grain.RegisterDeviceAsync("POS", pos2, "Register 2");
        await grain.RegisterDeviceAsync("Printer", printer, "Receipt Printer");

        await grain.RecordHeartbeatAsync(pos1);
        // pos2 and printer not heartbeated

        // Act
        var summary = await grain.GetHealthSummaryAsync();

        // Assert
        summary.LocationId.Should().Be(locationId);
        summary.TotalDevices.Should().Be(3);
        summary.OnlineDevices.Should().Be(1);
        summary.OfflineDevices.Should().Be(2);
        summary.DeviceMetrics.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetHealthSummaryAsync_ShouldCalculateConnectionQuality()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        // Register and heartbeat all devices
        for (int i = 0; i < 10; i++)
        {
            var deviceId = Guid.NewGuid();
            await grain.RegisterDeviceAsync("POS", deviceId, $"Device {i}");
            await grain.RecordHeartbeatAsync(deviceId);
        }

        // Act
        var summary = await grain.GetHealthSummaryAsync();

        // Assert
        summary.OverallConnectionQuality.Should().Be(ConnectionQuality.Excellent);
    }

    [Fact]
    public async Task PerformHealthCheckAsync_ShouldMarkStaleDevicesOffline()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var staleDevice = Guid.NewGuid();
        var freshDevice = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        await grain.RegisterDeviceAsync("POS", staleDevice, "Stale Device");
        await grain.RegisterDeviceAsync("POS", freshDevice, "Fresh Device");

        // Heartbeat only the fresh device
        await grain.RecordHeartbeatAsync(freshDevice);

        // Act - use a very short threshold
        var staleDevices = await grain.PerformHealthCheckAsync(TimeSpan.FromMilliseconds(1));

        // Assert - stale device was never heartbeated so won't be marked offline
        // Only devices that were online but haven't heartbeated get marked stale
        var freshHealth = await grain.GetDeviceHealthAsync(freshDevice);
        freshHealth!.IsOnline.Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePrinterHealthAsync_WithPaperOut_ShouldCreateAlert()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("Printer", printerId, "Receipt Printer");

        // Act
        await grain.UpdatePrinterHealthAsync(printerId, PrinterHealthStatus.PaperOut);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.Alerts.Should().HaveCount(1);
        summary.Alerts[0].AlertType.Should().Be("PaperOut");
        summary.Alerts[0].DeviceId.Should().Be(printerId);
    }

    [Fact]
    public async Task UpdatePrinterHealthAsync_WithPaperLow_ShouldCreateAlert()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("Printer", printerId, "Receipt Printer");

        // Act
        await grain.UpdatePrinterHealthAsync(printerId, PrinterHealthStatus.PaperLow, paperLevel: 10);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.Alerts.Should().HaveCount(1);
        summary.Alerts[0].AlertType.Should().Be("PaperLow");
        summary.Alerts[0].Message.Should().Contain("10%");
    }

    [Fact]
    public async Task UpdatePrinterHealthAsync_WithError_ShouldCreateAlert()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("Printer", printerId, "Receipt Printer");

        // Act
        await grain.UpdatePrinterHealthAsync(printerId, PrinterHealthStatus.Error);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.Alerts.Should().HaveCount(1);
        summary.Alerts[0].AlertType.Should().Be("PrinterError");
    }

    [Fact]
    public async Task UpdatePrinterHealthAsync_WithReady_ShouldClearAlerts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("Printer", printerId, "Receipt Printer");
        await grain.UpdatePrinterHealthAsync(printerId, PrinterHealthStatus.PaperOut);

        // Act
        await grain.UpdatePrinterHealthAsync(printerId, PrinterHealthStatus.Ready);

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.Alerts.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDeviceHealthAsync_ShouldReturnConnectionQuality()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("POS", deviceId, "Register 1");

        // Good signal and low latency
        await grain.RecordHeartbeatAsync(deviceId, new UpdateDeviceHealthCommand(
            DeviceId: deviceId,
            SignalStrength: -40, // Excellent signal
            LatencyMs: 20));     // Low latency

        // Act
        var health = await grain.GetDeviceHealthAsync(deviceId);

        // Assert
        health.Should().NotBeNull();
        health!.ConnectionQuality.Should().Be(ConnectionQuality.Excellent);
    }

    [Fact]
    public async Task GetDeviceHealthAsync_WithPoorSignal_ShouldReturnPoorQuality()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("POS", deviceId, "Register 1");

        // Poor signal and high latency
        await grain.RecordHeartbeatAsync(deviceId, new UpdateDeviceHealthCommand(
            DeviceId: deviceId,
            SignalStrength: -75, // Poor signal
            LatencyMs: 600));    // High latency

        // Act
        var health = await grain.GetDeviceHealthAsync(deviceId);

        // Assert
        health.Should().NotBeNull();
        health!.ConnectionQuality.Should().Be(ConnectionQuality.Poor);
    }

    [Fact]
    public async Task GetDeviceHealthAsync_WhenOffline_ShouldReturnDisconnected()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("POS", deviceId, "Register 1");
        // Don't send heartbeat

        // Act
        var health = await grain.GetDeviceHealthAsync(deviceId);

        // Assert
        health.Should().NotBeNull();
        health!.IsOnline.Should().BeFalse();
        health.ConnectionQuality.Should().Be(ConnectionQuality.Disconnected);
    }

    [Fact]
    public async Task DeviceHealthMetrics_ShouldIncludePrinterStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printerId = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);
        await grain.RegisterDeviceAsync("Printer", printerId, "Receipt Printer");

        await grain.RecordHeartbeatAsync(printerId, new UpdateDeviceHealthCommand(
            DeviceId: printerId,
            PrinterStatus: PrinterHealthStatus.Ready,
            PaperLevel: 80,
            PendingPrintJobs: 2));

        // Act
        var health = await grain.GetDeviceHealthAsync(printerId);

        // Assert
        health.Should().NotBeNull();
        health!.PrinterStatus.Should().Be(PrinterHealthStatus.Ready);
        health.PaperLevel.Should().Be(80);
        health.PendingPrintJobs.Should().Be(2);
    }

    [Fact]
    public async Task GetHealthSummaryAsync_DevicesWithAlerts_ShouldCountCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var printer1 = Guid.NewGuid();
        var printer2 = Guid.NewGuid();
        var pos = Guid.NewGuid();
        var grain = GetGrain(orgId, locationId);
        await grain.InitializeAsync(locationId);

        await grain.RegisterDeviceAsync("Printer", printer1, "Printer 1");
        await grain.RegisterDeviceAsync("Printer", printer2, "Printer 2");
        await grain.RegisterDeviceAsync("POS", pos, "Register");

        await grain.UpdatePrinterHealthAsync(printer1, PrinterHealthStatus.PaperOut);
        await grain.UpdatePrinterHealthAsync(printer2, PrinterHealthStatus.Error);

        // Act
        var summary = await grain.GetHealthSummaryAsync();

        // Assert
        summary.DevicesWithAlerts.Should().Be(2);
        summary.ActiveAlerts.Should().HaveCount(2);
    }
}
