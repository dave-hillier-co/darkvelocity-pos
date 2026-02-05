using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Services;
using FluentAssertions;

namespace DarkVelocity.Tests;

// ============================================================================
// Fiscal Device Registry Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class FiscalDeviceRegistryGrainTests
{
    private readonly TestClusterFixture _fixture;

    public FiscalDeviceRegistryGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFiscalDeviceRegistryGrain GetGrain(Guid orgId, Guid siteId)
    {
        var key = $"{orgId}:{siteId}:fiscaldeviceregistry";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalDeviceRegistryGrain>(key);
    }

    [Fact]
    public async Task RegisterDeviceAsync_AddsDeviceToRegistry()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.RegisterDeviceAsync(deviceId, "TSE-12345");

        var devices = await grain.GetDeviceIdsAsync();
        devices.Should().Contain(deviceId);
    }

    [Fact]
    public async Task UnregisterDeviceAsync_RemovesDeviceFromRegistry()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.RegisterDeviceAsync(deviceId, "TSE-67890");
        await grain.UnregisterDeviceAsync(deviceId);

        var devices = await grain.GetDeviceIdsAsync();
        devices.Should().NotContain(deviceId);
    }

    [Fact]
    public async Task FindBySerialNumberAsync_ReturnsCorrectDevice()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var serialNumber = "UNIQUE-SERIAL-001";
        var grain = GetGrain(orgId, siteId);

        await grain.RegisterDeviceAsync(deviceId, serialNumber);

        var foundId = await grain.FindBySerialNumberAsync(serialNumber);
        foundId.Should().Be(deviceId);
    }

    [Fact]
    public async Task FindBySerialNumberAsync_ReturnsNull_WhenNotFound()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        var foundId = await grain.FindBySerialNumberAsync("NONEXISTENT");
        foundId.Should().BeNull();
    }

    [Fact]
    public async Task GetDeviceCountAsync_ReturnsCorrectCount()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.RegisterDeviceAsync(Guid.NewGuid(), "TSE-001");
        await grain.RegisterDeviceAsync(Guid.NewGuid(), "TSE-002");
        await grain.RegisterDeviceAsync(Guid.NewGuid(), "TSE-003");

        var count = await grain.GetDeviceCountAsync();
        count.Should().Be(3);
    }
}

// ============================================================================
// Fiscal Transaction Registry Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class FiscalTransactionRegistryGrainTests
{
    private readonly TestClusterFixture _fixture;

    public FiscalTransactionRegistryGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFiscalTransactionRegistryGrain GetGrain(Guid orgId, Guid siteId)
    {
        var key = $"{orgId}:{siteId}:fiscaltxregistry";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalTransactionRegistryGrain>(key);
    }

    [Fact]
    public async Task RegisterTransactionAsync_AddsTransactionToRegistry()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.RegisterTransactionAsync(transactionId, deviceId, DateOnly.FromDateTime(DateTime.UtcNow));

        var transactions = await grain.GetTransactionIdsAsync(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        transactions.Should().Contain(transactionId);
    }

    [Fact]
    public async Task GetTransactionIdsAsync_FiltersbyDateRange()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);
        var twoDaysAgo = today.AddDays(-2);

        var txToday = Guid.NewGuid();
        var txYesterday = Guid.NewGuid();
        var txTwoDaysAgo = Guid.NewGuid();

        await grain.RegisterTransactionAsync(txToday, deviceId, today);
        await grain.RegisterTransactionAsync(txYesterday, deviceId, yesterday);
        await grain.RegisterTransactionAsync(txTwoDaysAgo, deviceId, twoDaysAgo);

        var transactions = await grain.GetTransactionIdsAsync(yesterday, today);

        transactions.Should().Contain(txToday);
        transactions.Should().Contain(txYesterday);
        transactions.Should().NotContain(txTwoDaysAgo);
    }

    [Fact]
    public async Task GetTransactionIdsAsync_FiltersByDeviceId()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var txDevice1 = Guid.NewGuid();
        var txDevice2 = Guid.NewGuid();

        await grain.RegisterTransactionAsync(txDevice1, device1, today);
        await grain.RegisterTransactionAsync(txDevice2, device2, today);

        var transactions = await grain.GetTransactionIdsAsync(
            today.AddDays(-1),
            today.AddDays(1),
            device1);

        transactions.Should().Contain(txDevice1);
        transactions.Should().NotContain(txDevice2);
    }

    [Fact]
    public async Task GetTransactionCountAsync_ReturnsCorrectCount()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await grain.RegisterTransactionAsync(Guid.NewGuid(), deviceId, today);
        await grain.RegisterTransactionAsync(Guid.NewGuid(), deviceId, today);
        await grain.RegisterTransactionAsync(Guid.NewGuid(), deviceId, today);

        var count = await grain.GetTransactionCountAsync();
        count.Should().Be(3);
    }
}

// ============================================================================
// Fiscal Device Lifecycle Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class FiscalDeviceLifecycleTests
{
    private readonly TestClusterFixture _fixture;

    public FiscalDeviceLifecycleTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFiscalDeviceGrain GetGrain(Guid orgId, Guid deviceId)
    {
        var key = $"{orgId}:fiscaldevice:{deviceId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalDeviceGrain>(key);
    }

    [Fact]
    public async Task ActivateAsync_SetsDeviceToActive()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-LIFECYCLE-001",
            null, null, null, null, null));

        // Set to inactive first
        await grain.DeactivateAsync();

        // Now activate
        var snapshot = await grain.ActivateAsync("TAX-REG-001", operatorId);

        snapshot.Status.Should().Be(FiscalDeviceStatus.Active);
    }

    [Fact]
    public async Task DeactivateWithReasonAsync_SetsDeviceToInactive()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-LIFECYCLE-002",
            null, null, null, null, null));

        await grain.DeactivateWithReasonAsync("Device maintenance", operatorId);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(FiscalDeviceStatus.Inactive);
    }

    [Fact]
    public async Task GetHealthStatusAsync_ReturnsCorrectStatus()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-HEALTH-001",
            null, DateTime.UtcNow.AddDays(30), null, null, null));

        var health = await grain.GetHealthStatusAsync();

        health.DeviceId.Should().Be(deviceId);
        health.IsOnline.Should().BeTrue();
        health.CertificateValid.Should().BeTrue();
        health.DaysUntilCertificateExpiry.Should().BeInRange(29, 31);
    }

    [Fact]
    public async Task GetHealthStatusAsync_ExpiredCertificate_ReturnsInvalid()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-HEALTH-002",
            null, DateTime.UtcNow.AddDays(-10), null, null, null));

        var health = await grain.GetHealthStatusAsync();

        health.CertificateValid.Should().BeFalse();
        health.DaysUntilCertificateExpiry.Should().BeNegative();
    }

    [Fact]
    public async Task PerformSelfTestAsync_ActiveDevice_Passes()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-SELFTEST-001",
            null, DateTime.UtcNow.AddYears(1), null, null, null));

        var result = await grain.PerformSelfTestAsync();

        result.Passed.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task PerformSelfTestAsync_ExpiredCertificate_Fails()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-SELFTEST-002",
            null, DateTime.UtcNow.AddDays(-1), null, null, null));

        var result = await grain.PerformSelfTestAsync();

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("expired");
    }

    [Fact]
    public async Task PerformSelfTestAsync_InactiveDevice_Fails()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-SELFTEST-003",
            null, null, null, null, null));

        await grain.DeactivateAsync();

        var result = await grain.PerformSelfTestAsync();

        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not active");
    }

    [Fact]
    public async Task RefreshCertificateAsync_UpdatesLastSyncAt()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-REFRESH-001",
            null, null, null, null, null));

        var before = await grain.GetSnapshotAsync();
        await Task.Delay(10); // Small delay to ensure time difference

        var after = await grain.RefreshCertificateAsync();

        after.LastSyncAt.Should().NotBeNull();
        after.LastSyncAt.Should().BeAfter(before.LastSyncAt ?? DateTime.MinValue);
    }
}

// ============================================================================
// DSFinV-K Export Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DSFinVKExportGrainTests
{
    private readonly TestClusterFixture _fixture;

    public DSFinVKExportGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDSFinVKExportGrain GetGrain(Guid orgId, Guid siteId, Guid exportId)
    {
        var key = $"{orgId}:{siteId}:dsfinvk:{exportId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IDSFinVKExportGrain>(key);
    }

    [Fact]
    public async Task CreateAsync_CreatesExportWithPendingStatus()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, exportId);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        var state = await grain.CreateAsync(startDate, endDate, "Test export", Guid.NewGuid());

        state.ExportId.Should().Be(exportId);
        state.StartDate.Should().Be(startDate);
        state.EndDate.Should().Be(endDate);
        state.Status.Should().Be(DSFinVKExportStatus.Pending);
    }

    [Fact]
    public async Task SetProcessingAsync_UpdatesStatus()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, exportId);

        await grain.CreateAsync(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow),
            null,
            null);

        await grain.SetProcessingAsync();

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(DSFinVKExportStatus.Processing);
    }

    [Fact]
    public async Task SetCompletedAsync_UpdatesStatusAndMetadata()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, exportId);

        await grain.CreateAsync(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow),
            null,
            null);

        await grain.SetCompletedAsync(100, "/path/to/export.zip", "/download/url");

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(DSFinVKExportStatus.Completed);
        state.TransactionCount.Should().Be(100);
        state.FilePath.Should().Be("/path/to/export.zip");
        state.DownloadUrl.Should().Be("/download/url");
        state.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SetFailedAsync_UpdatesStatusAndError()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId, exportId);

        await grain.CreateAsync(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            DateOnly.FromDateTime(DateTime.UtcNow),
            null,
            null);

        await grain.SetFailedAsync("Export failed: no transactions");

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(DSFinVKExportStatus.Failed);
        state.ErrorMessage.Should().Be("Export failed: no transactions");
        state.CompletedAt.Should().NotBeNull();
    }
}

// ============================================================================
// DSFinV-K Export Registry Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class DSFinVKExportRegistryGrainTests
{
    private readonly TestClusterFixture _fixture;

    public DSFinVKExportRegistryGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDSFinVKExportRegistryGrain GetGrain(Guid orgId, Guid siteId)
    {
        var key = $"{orgId}:{siteId}:dsfinvkregistry";
        return _fixture.Cluster.GrainFactory.GetGrain<IDSFinVKExportRegistryGrain>(key);
    }

    [Fact]
    public async Task RegisterExportAsync_AddsExportToRegistry()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.RegisterExportAsync(exportId);

        var exports = await grain.GetExportIdsAsync();
        exports.Should().Contain(exportId);
    }

    [Fact]
    public async Task GetExportIdsAsync_ReturnsInReverseChronologicalOrder()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        var export1 = Guid.NewGuid();
        var export2 = Guid.NewGuid();
        var export3 = Guid.NewGuid();

        await grain.RegisterExportAsync(export1);
        await grain.RegisterExportAsync(export2);
        await grain.RegisterExportAsync(export3);

        var exports = await grain.GetExportIdsAsync();

        // Most recent should be first
        exports[0].Should().Be(export3);
        exports[1].Should().Be(export2);
        exports[2].Should().Be(export1);
    }
}
