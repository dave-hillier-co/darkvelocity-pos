using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for Terminal grain covering:
/// - Terminal registration (pairing)
/// - Terminal deactivation (unpairing)
/// - Heartbeat monitoring
/// - Online/offline status detection
/// - Status management
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TerminalGrainTests
{
    private readonly TestClusterFixture _fixture;

    public TerminalGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ITerminalGrain GetTerminalGrain(Guid orgId, Guid terminalId)
        => _fixture.Cluster.GrainFactory.GetGrain<ITerminalGrain>($"{orgId}:terminal:{terminalId}");

    // =========================================================================
    // Terminal Registration (Pairing) Tests
    // =========================================================================

    // Given: A new unregistered payment terminal with device type, serial number, and station metadata
    // When: The terminal is registered at a site location
    // Then: The terminal is created with Active status and all provided configuration details
    [Fact]
    public async Task RegisterTerminal_ValidCommand_ShouldCreateTerminal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        var command = new RegisterTerminalCommand(
            locationId,
            "Counter Terminal 1",
            "stripe_m2",
            "SN12345678",
            Metadata: new Dictionary<string, string> { ["station"] = "front" });

        // Act
        var snapshot = await grain.RegisterAsync(command);

        // Assert
        snapshot.TerminalId.Should().Be(terminalId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.Label.Should().Be("Counter Terminal 1");
        snapshot.DeviceType.Should().Be("stripe_m2");
        snapshot.SerialNumber.Should().Be("SN12345678");
        snapshot.Status.Should().Be(TerminalStatus.Active);
        snapshot.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    // Given: A new unregistered payment terminal with only a label and location
    // When: The terminal is registered without optional device details
    // Then: The terminal is created with Active status and the provided label
    [Fact]
    public async Task RegisterTerminal_MinimalCommand_ShouldCreateTerminal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        var command = new RegisterTerminalCommand(
            locationId,
            "Basic Terminal",
            DeviceType: null,
            SerialNumber: null,
            Metadata: null);

        // Act
        var snapshot = await grain.RegisterAsync(command);

        // Assert
        snapshot.TerminalId.Should().Be(terminalId);
        snapshot.Label.Should().Be("Basic Terminal");
        snapshot.Status.Should().Be(TerminalStatus.Active);
    }

    // Given: A payment terminal that has already been registered at a location
    // When: A second registration is attempted for the same terminal
    // Then: The registration is rejected because the terminal already exists
    [Fact]
    public async Task RegisterTerminal_AlreadyExists_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            locationId, "Terminal 1", null, null, null));

        // Act
        var act = () => grain.RegisterAsync(new RegisterTerminalCommand(
            locationId, "Terminal 2", null, null, null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    // Given: A terminal identifier that has never been registered
    // When: Checking whether the terminal exists
    // Then: The terminal is reported as not existing
    [Fact]
    public async Task ExistsAsync_NewTerminal_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeFalse();
    }

    // Given: A payment terminal that has been registered
    // When: Checking whether the terminal exists
    // Then: The terminal is reported as existing
    [Fact]
    public async Task ExistsAsync_RegisteredTerminal_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Terminal", null, null, null));

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeTrue();
    }

    // =========================================================================
    // Terminal Deactivation (Unpairing) Tests
    // =========================================================================

    // Given: An active registered payment terminal
    // When: The terminal is deactivated (unpaired)
    // Then: The terminal status transitions to Inactive with an updated timestamp
    [Fact]
    public async Task DeactivateTerminal_ActiveTerminal_ShouldSetStatusToInactive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Terminal to Deactivate", "stripe_m2", null, null));

        // Act
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(TerminalStatus.Inactive);
        snapshot.UpdatedAt.Should().NotBeNull();
    }

    // Given: A terminal identifier that has never been registered
    // When: A deactivation is attempted
    // Then: The deactivation is rejected because the terminal is not found
    [Fact]
    public async Task DeactivateTerminal_NonExistent_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        // Act
        var act = () => grain.DeactivateAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // Given: A payment terminal that has already been deactivated
    // When: A second deactivation is attempted
    // Then: The terminal remains in Inactive status
    [Fact]
    public async Task DeactivateTerminal_AlreadyInactive_ShouldRemainInactive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Terminal", null, null, null));

        await grain.DeactivateAsync();

        // Act - Deactivate again
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(TerminalStatus.Inactive);
    }

    // =========================================================================
    // Terminal Update Tests
    // =========================================================================

    // Given: A registered payment terminal with an original label
    // When: The terminal label is updated
    // Then: The terminal reflects the new label
    [Fact]
    public async Task UpdateTerminal_ChangeLabel_ShouldUpdateLabel()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Original Label", null, null, null));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateTerminalCommand(
            Label: "New Label",
            LocationId: null,
            Metadata: null,
            Status: null));

        // Assert
        snapshot.Label.Should().Be("New Label");
    }

    // Given: A registered payment terminal assigned to an original location
    // When: The terminal is reassigned to a different site location
    // Then: The terminal reflects the new location
    [Fact]
    public async Task UpdateTerminal_ChangeLocation_ShouldUpdateLocation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var originalLocationId = Guid.NewGuid();
        var newLocationId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            originalLocationId, "Terminal", null, null, null));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateTerminalCommand(
            Label: null,
            LocationId: newLocationId,
            Metadata: null,
            Status: null));

        // Assert
        snapshot.LocationId.Should().Be(newLocationId);
    }

    // Given: An active registered payment terminal
    // When: The terminal status is changed to Offline
    // Then: The terminal reflects the Offline status
    [Fact]
    public async Task UpdateTerminal_ChangeStatus_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Terminal", null, null, null));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateTerminalCommand(
            Label: null,
            LocationId: null,
            Metadata: null,
            Status: TerminalStatus.Offline));

        // Assert
        snapshot.Status.Should().Be(TerminalStatus.Offline);
    }

    // Given: A registered payment terminal with initial configuration
    // When: Multiple terminal fields (label, location, status) are updated simultaneously
    // Then: All fields are updated and the update timestamp is recorded
    [Fact]
    public async Task UpdateTerminal_MultipleFields_ShouldUpdateAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var newLocationId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Original", "stripe_m2", "SN123", null));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateTerminalCommand(
            Label: "Updated Terminal",
            LocationId: newLocationId,
            Metadata: null,
            Status: TerminalStatus.Active));

        // Assert
        snapshot.Label.Should().Be("Updated Terminal");
        snapshot.LocationId.Should().Be(newLocationId);
        snapshot.Status.Should().Be(TerminalStatus.Active);
        snapshot.UpdatedAt.Should().NotBeNull();
    }

    // =========================================================================
    // Heartbeat Monitoring Tests
    // =========================================================================

    // Given: A registered terminal that has not yet sent a heartbeat
    // When: The terminal sends its first heartbeat with IP address and software version
    // Then: The terminal records the heartbeat timestamp, IP, and version
    [Fact]
    public async Task Heartbeat_FirstCall_ShouldSetLastSeenAt()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Terminal", null, null, null));

        // Act
        await grain.HeartbeatAsync("192.168.1.100", "1.0.0");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastSeenAt.Should().NotBeNull();
        snapshot.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
        snapshot.IpAddress.Should().Be("192.168.1.100");
        snapshot.SoftwareVersion.Should().Be("1.0.0");
    }

    // Given: A registered terminal that has already sent a heartbeat
    // When: The terminal sends a subsequent heartbeat with an updated software version
    // Then: The last-seen timestamp advances and the version is updated
    [Fact]
    public async Task Heartbeat_SubsequentCall_ShouldUpdateLastSeenAt()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Terminal", null, null, null));

        await grain.HeartbeatAsync("192.168.1.100", "1.0.0");
        var firstSnapshot = await grain.GetSnapshotAsync();
        var firstLastSeenAt = firstSnapshot.LastSeenAt;

        await Task.Delay(10); // Small delay to ensure different timestamp

        // Act
        await grain.HeartbeatAsync("192.168.1.100", "1.0.1");

        // Assert
        var secondSnapshot = await grain.GetSnapshotAsync();
        secondSnapshot.LastSeenAt.Should().BeAfter(firstLastSeenAt!.Value);
        secondSnapshot.SoftwareVersion.Should().Be("1.0.1");
    }

    // Given: A registered terminal with Offline status
    // When: The terminal sends a heartbeat
    // Then: The terminal is automatically reactivated to Active status
    [Fact]
    public async Task Heartbeat_OfflineTerminal_ShouldReactivate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Terminal", null, null, null));

        // Set to offline
        await grain.UpdateAsync(new UpdateTerminalCommand(
            null, null, null, TerminalStatus.Offline));

        var offlineSnapshot = await grain.GetSnapshotAsync();
        offlineSnapshot.Status.Should().Be(TerminalStatus.Offline);

        // Act - Heartbeat should reactivate
        await grain.HeartbeatAsync("192.168.1.100", "1.0.0");

        // Assert
        var reactivatedSnapshot = await grain.GetSnapshotAsync();
        reactivatedSnapshot.Status.Should().Be(TerminalStatus.Active);
    }

    // Given: A registered terminal with no prior heartbeat
    // When: The terminal sends a heartbeat without IP address or software version
    // Then: The last-seen timestamp is set but IP and version remain null
    [Fact]
    public async Task Heartbeat_WithNullValues_ShouldStillUpdateLastSeenAt()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Terminal", null, null, null));

        // Act
        await grain.HeartbeatAsync(null, null);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastSeenAt.Should().NotBeNull();
        snapshot.IpAddress.Should().BeNull();
        snapshot.SoftwareVersion.Should().BeNull();
    }

    // =========================================================================
    // Online/Offline Status Detection Tests
    // =========================================================================

    // Given: A registered terminal that has never sent a heartbeat
    // When: Checking whether the terminal is online
    // Then: The terminal is reported as offline
    [Fact]
    public async Task IsOnline_NewTerminalNoHeartbeat_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Terminal", null, null, null));

        // Act
        var isOnline = await grain.IsOnlineAsync();

        // Assert
        isOnline.Should().BeFalse("no heartbeat received yet");
    }

    // Given: A registered terminal that just sent a heartbeat
    // When: Checking whether the terminal is online
    // Then: The terminal is reported as online
    [Fact]
    public async Task IsOnline_RecentHeartbeat_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Terminal", null, null, null));

        await grain.HeartbeatAsync("192.168.1.100", "1.0.0");

        // Act
        var isOnline = await grain.IsOnlineAsync();

        // Assert
        isOnline.Should().BeTrue("heartbeat was just received");
    }

    // Given: A terminal that has sent a heartbeat but was subsequently deactivated
    // When: Checking whether the terminal is online
    // Then: The terminal is reported as offline because it is deactivated
    [Fact]
    public async Task IsOnline_InactiveTerminal_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Terminal", null, null, null));

        await grain.HeartbeatAsync("192.168.1.100", "1.0.0");
        await grain.DeactivateAsync();

        // Act
        var isOnline = await grain.IsOnlineAsync();

        // Assert
        isOnline.Should().BeFalse("terminal is inactive/deactivated");
    }

    // Given: A terminal that has sent a heartbeat but was set to Offline status
    // When: Checking whether the terminal is online
    // Then: The terminal is reported as offline
    [Fact]
    public async Task IsOnline_OfflineStatus_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Terminal", null, null, null));

        await grain.HeartbeatAsync("192.168.1.100", "1.0.0");
        await grain.UpdateAsync(new UpdateTerminalCommand(
            null, null, null, TerminalStatus.Offline));

        // Act
        var isOnline = await grain.IsOnlineAsync();

        // Assert
        isOnline.Should().BeFalse("terminal status is offline");
    }

    // =========================================================================
    // GetSnapshot Tests
    // =========================================================================

    // Given: A terminal identifier that has never been registered
    // When: Retrieving the terminal snapshot
    // Then: The request is rejected because the terminal is not found
    [Fact]
    public async Task GetSnapshot_NonExistent_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        // Act
        var act = () => grain.GetSnapshotAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // Given: A registered terminal with full configuration and a recorded heartbeat
    // When: Retrieving the terminal snapshot
    // Then: All terminal details including device type, serial number, IP, and version are returned
    [Fact]
    public async Task GetSnapshot_ExistingTerminal_ShouldReturnAllFields()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var terminalId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTerminalGrain(orgId, terminalId);

        await grain.RegisterAsync(new RegisterTerminalCommand(
            locationId, "Full Terminal", "adyen_p400", "SN987654", null));

        await grain.HeartbeatAsync("10.0.0.50", "2.1.0");

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.TerminalId.Should().Be(terminalId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.Label.Should().Be("Full Terminal");
        snapshot.DeviceType.Should().Be("adyen_p400");
        snapshot.SerialNumber.Should().Be("SN987654");
        snapshot.Status.Should().Be(TerminalStatus.Active);
        snapshot.IpAddress.Should().Be("10.0.0.50");
        snapshot.SoftwareVersion.Should().Be("2.1.0");
        snapshot.LastSeenAt.Should().NotBeNull();
        snapshot.CreatedAt.Should().NotBe(default);
    }

    // =========================================================================
    // Multiple Terminals Per Location Tests
    // =========================================================================

    // Given: Two terminals registered at the same site location
    // When: One terminal receives a heartbeat and the other is deactivated
    // Then: Each terminal maintains independent state while sharing the same location
    [Fact]
    public async Task MultipleTerminals_SameLocation_ShouldBeIndependent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        var terminal1Id = Guid.NewGuid();
        var terminal2Id = Guid.NewGuid();

        var grain1 = GetTerminalGrain(orgId, terminal1Id);
        var grain2 = GetTerminalGrain(orgId, terminal2Id);

        // Act
        await grain1.RegisterAsync(new RegisterTerminalCommand(
            locationId, "Terminal 1", "stripe_m2", "SN001", null));

        await grain2.RegisterAsync(new RegisterTerminalCommand(
            locationId, "Terminal 2", "stripe_m2", "SN002", null));

        await grain1.HeartbeatAsync("192.168.1.100", "1.0.0");
        await grain2.DeactivateAsync();

        // Assert
        var snapshot1 = await grain1.GetSnapshotAsync();
        var snapshot2 = await grain2.GetSnapshotAsync();

        snapshot1.Status.Should().Be(TerminalStatus.Active);
        snapshot1.LastSeenAt.Should().NotBeNull();

        snapshot2.Status.Should().Be(TerminalStatus.Inactive);
        snapshot2.LastSeenAt.Should().BeNull();

        // Both should be at same location
        snapshot1.LocationId.Should().Be(locationId);
        snapshot2.LocationId.Should().Be(locationId);
    }

    // Given: Two terminals with the same ID registered under different organizations
    // When: Both terminals are registered with different labels
    // Then: Each organization's terminal is fully isolated with its own state
    [Fact]
    public async Task MultipleTerminals_DifferentOrgs_ShouldBeIsolated()
    {
        // Arrange
        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();
        var terminalId = Guid.NewGuid(); // Same terminal ID

        var grain1 = GetTerminalGrain(org1Id, terminalId);
        var grain2 = GetTerminalGrain(org2Id, terminalId);

        // Act
        await grain1.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Org1 Terminal", null, null, null));

        await grain2.RegisterAsync(new RegisterTerminalCommand(
            Guid.NewGuid(), "Org2 Terminal", null, null, null));

        // Assert
        var snapshot1 = await grain1.GetSnapshotAsync();
        var snapshot2 = await grain2.GetSnapshotAsync();

        snapshot1.Label.Should().Be("Org1 Terminal");
        snapshot2.Label.Should().Be("Org2 Terminal");
    }
}
