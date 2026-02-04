using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

// ============================================================================
// Fiscal Device Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class FiscalDeviceGrainTests
{
    private readonly TestClusterFixture _fixture;

    public FiscalDeviceGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFiscalDeviceGrain GetGrain(Guid orgId, Guid deviceId)
    {
        var key = $"{orgId}:fiscaldevice:{deviceId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalDeviceGrain>(key);
    }

    [Fact]
    public async Task RegisterAsync_WithSwissbitCloud_RegistersDevice()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        var command = new RegisterFiscalDeviceCommand(
            LocationId: locationId,
            DeviceType: FiscalDeviceType.SwissbitCloud,
            SerialNumber: "SB-CLOUD-123456",
            PublicKey: "public-key-data",
            CertificateExpiryDate: DateTime.UtcNow.AddYears(2),
            ApiEndpoint: "https://swissbit.cloud/api",
            ApiCredentialsEncrypted: "encrypted-credentials",
            ClientId: "client-123");

        var snapshot = await grain.RegisterAsync(command);

        snapshot.FiscalDeviceId.Should().Be(deviceId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.DeviceType.Should().Be(FiscalDeviceType.SwissbitCloud);
        snapshot.SerialNumber.Should().Be("SB-CLOUD-123456");
        snapshot.Status.Should().Be(FiscalDeviceStatus.Active);
        snapshot.TransactionCounter.Should().Be(0);
        snapshot.SignatureCounter.Should().Be(0);
    }

    [Fact]
    public async Task RegisterAsync_WithSwissbitUsb_RegistersDevice()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        var command = new RegisterFiscalDeviceCommand(
            LocationId: Guid.NewGuid(),
            DeviceType: FiscalDeviceType.SwissbitUsb,
            SerialNumber: "USB-TSE-789",
            PublicKey: null,
            CertificateExpiryDate: DateTime.UtcNow.AddYears(1),
            ApiEndpoint: null,
            ApiCredentialsEncrypted: null,
            ClientId: null);

        var snapshot = await grain.RegisterAsync(command);

        snapshot.DeviceType.Should().Be(FiscalDeviceType.SwissbitUsb);
    }

    [Fact]
    public async Task RegisterAsync_WithFiskalyCloud_RegistersDevice()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        var command = new RegisterFiscalDeviceCommand(
            LocationId: Guid.NewGuid(),
            DeviceType: FiscalDeviceType.FiskalyCloud,
            SerialNumber: "FISKALY-001",
            PublicKey: null,
            CertificateExpiryDate: null,
            ApiEndpoint: "https://kassensichv.fiskaly.com",
            ApiCredentialsEncrypted: "fiskaly-creds",
            ClientId: "fiskaly-client");

        var snapshot = await grain.RegisterAsync(command);

        snapshot.DeviceType.Should().Be(FiscalDeviceType.FiskalyCloud);
        snapshot.ApiEndpoint.Should().Be("https://kassensichv.fiskaly.com");
    }

    [Fact]
    public async Task UpdateAsync_UpdatesDeviceDetails()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.Epson, "EPSON-123",
            null, null, null, null, null));

        var updateCommand = new UpdateFiscalDeviceCommand(
            Status: null,
            PublicKey: "new-public-key",
            CertificateExpiryDate: DateTime.UtcNow.AddYears(2),
            ApiEndpoint: "https://new-endpoint.com",
            ApiCredentialsEncrypted: "new-encrypted-creds");

        var snapshot = await grain.UpdateAsync(updateCommand);

        snapshot.PublicKey.Should().Be("new-public-key");
        snapshot.ApiEndpoint.Should().Be("https://new-endpoint.com");
    }

    [Fact]
    public async Task DeactivateAsync_SetsStatusToInactive()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.Diebold, "DIEBOLD-001",
            null, null, null, null, null));

        await grain.DeactivateAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(FiscalDeviceStatus.Inactive);
    }

    [Fact]
    public async Task GetNextTransactionCounterAsync_IncrementsCounter()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-001",
            null, null, null, null, null));

        var counter1 = await grain.GetNextTransactionCounterAsync();
        var counter2 = await grain.GetNextTransactionCounterAsync();
        var counter3 = await grain.GetNextTransactionCounterAsync();

        counter1.Should().Be(1);
        counter2.Should().Be(2);
        counter3.Should().Be(3);
    }

    [Fact]
    public async Task GetNextSignatureCounterAsync_IncrementsCounter()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.FiskalyCloud, "FISK-001",
            null, null, null, null, null));

        var sig1 = await grain.GetNextSignatureCounterAsync();
        var sig2 = await grain.GetNextSignatureCounterAsync();

        sig1.Should().Be(1);
        sig2.Should().Be(2);
    }

    [Fact]
    public async Task RecordSyncAsync_UpdatesLastSyncAt()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitUsb, "USB-001",
            null, null, null, null, null));

        await grain.RecordSyncAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastSyncAt.Should().NotBeNull();
        snapshot.LastSyncAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task IsCertificateExpiringAsync_WhenExpiringWithin30Days_ReturnsTrue()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-EXP",
            null, DateTime.UtcNow.AddDays(15), null, null, null));

        var isExpiring = await grain.IsCertificateExpiringAsync(30);

        isExpiring.Should().BeTrue();
    }

    [Fact]
    public async Task IsCertificateExpiringAsync_WhenNotExpiring_ReturnsFalse()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-VALID",
            null, DateTime.UtcNow.AddYears(1), null, null, null));

        var isExpiring = await grain.IsCertificateExpiringAsync(30);

        isExpiring.Should().BeFalse();
    }
}

// ============================================================================
// Fiscal Transaction Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class FiscalTransactionGrainTests
{
    private readonly TestClusterFixture _fixture;

    public FiscalTransactionGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFiscalTransactionGrain GetGrain(Guid orgId, Guid transactionId)
    {
        var key = $"{orgId}:fiscaltransaction:{transactionId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalTransactionGrain>(key);
    }

    private IFiscalDeviceGrain GetDeviceGrain(Guid orgId, Guid deviceId)
    {
        var key = $"{orgId}:fiscaldevice:{deviceId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalDeviceGrain>(key);
    }

    private async Task<Guid> SetupDeviceAsync(Guid orgId, Guid locationId)
    {
        var deviceId = Guid.NewGuid();
        var deviceGrain = GetDeviceGrain(orgId, deviceId);
        await deviceGrain.RegisterAsync(new RegisterFiscalDeviceCommand(
            locationId, FiscalDeviceType.SwissbitCloud, $"SB-{deviceId:N}".Substring(0, 20),
            null, null, null, null, null));
        return deviceId;
    }

    [Fact]
    public async Task CreateAsync_WithReceipt_CreatesTransaction()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        var command = new CreateFiscalTransactionCommand(
            FiscalDeviceId: deviceId,
            LocationId: locationId,
            TransactionType: FiscalTransactionType.Receipt,
            ProcessType: FiscalProcessType.Kassenbeleg,
            SourceType: "Order",
            SourceId: orderId,
            GrossAmount: 119.00m,
            NetAmounts: new Dictionary<string, decimal>
            {
                ["NORMAL"] = 100.00m
            },
            TaxAmounts: new Dictionary<string, decimal>
            {
                ["NORMAL"] = 19.00m
            },
            PaymentTypes: new Dictionary<string, decimal>
            {
                ["CASH"] = 119.00m
            });

        var snapshot = await grain.CreateAsync(command);

        snapshot.FiscalTransactionId.Should().Be(transactionId);
        snapshot.FiscalDeviceId.Should().Be(deviceId);
        snapshot.TransactionType.Should().Be(FiscalTransactionType.Receipt);
        snapshot.ProcessType.Should().Be(FiscalProcessType.Kassenbeleg);
        snapshot.GrossAmount.Should().Be(119.00m);
        snapshot.Status.Should().Be(FiscalTransactionStatus.Pending);
    }

    [Fact]
    public async Task CreateAsync_WithTrainingReceipt_CreatesTransaction()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        var command = new CreateFiscalTransactionCommand(
            FiscalDeviceId: deviceId,
            LocationId: locationId,
            TransactionType: FiscalTransactionType.TrainingReceipt,
            ProcessType: FiscalProcessType.Kassenbeleg,
            SourceType: "Training",
            SourceId: Guid.NewGuid(),
            GrossAmount: 50.00m,
            NetAmounts: new Dictionary<string, decimal>(),
            TaxAmounts: new Dictionary<string, decimal>(),
            PaymentTypes: new Dictionary<string, decimal>());

        var snapshot = await grain.CreateAsync(command);

        snapshot.TransactionType.Should().Be(FiscalTransactionType.TrainingReceipt);
    }

    [Fact]
    public async Task CreateAsync_WithVoid_CreatesTransaction()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        var command = new CreateFiscalTransactionCommand(
            FiscalDeviceId: deviceId,
            LocationId: locationId,
            TransactionType: FiscalTransactionType.Void,
            ProcessType: FiscalProcessType.Kassenbeleg,
            SourceType: "Void",
            SourceId: Guid.NewGuid(),
            GrossAmount: -25.00m,
            NetAmounts: new Dictionary<string, decimal> { ["NORMAL"] = -21.01m },
            TaxAmounts: new Dictionary<string, decimal> { ["NORMAL"] = -3.99m },
            PaymentTypes: new Dictionary<string, decimal> { ["CARD"] = -25.00m });

        var snapshot = await grain.CreateAsync(command);

        snapshot.TransactionType.Should().Be(FiscalTransactionType.Void);
        snapshot.GrossAmount.Should().Be(-25.00m);
    }

    [Fact]
    public async Task SignAsync_SignsTransaction()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        await grain.CreateAsync(new CreateFiscalTransactionCommand(
            deviceId, locationId,
            FiscalTransactionType.Receipt, FiscalProcessType.Kassenbeleg,
            "Order", Guid.NewGuid(), 100.00m,
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>()));

        var signCommand = new SignTransactionCommand(
            Signature: "base64-signature-data",
            SignatureCounter: 42,
            CertificateSerial: "CERT-2024-001",
            QrCodeData: "V0;123456;1;2024-01-15T10:30:00;100.00;signature",
            TseResponseRaw: "{\"raw\":\"response\"}");

        var snapshot = await grain.SignAsync(signCommand);

        snapshot.Status.Should().Be(FiscalTransactionStatus.Signed);
        snapshot.Signature.Should().Be("base64-signature-data");
        snapshot.SignatureCounter.Should().Be(42);
        snapshot.CertificateSerial.Should().Be("CERT-2024-001");
        snapshot.QrCodeData.Should().NotBeNullOrEmpty();
        snapshot.EndTime.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkFailedAsync_SetsStatusToFailed()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        await grain.CreateAsync(new CreateFiscalTransactionCommand(
            deviceId, locationId,
            FiscalTransactionType.Receipt, FiscalProcessType.Kassenbeleg,
            "Order", Guid.NewGuid(), 50.00m,
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>()));

        await grain.MarkFailedAsync("TSE device not responding");

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Status.Should().Be(FiscalTransactionStatus.Failed);
        snapshot.ErrorMessage.Should().Be("TSE device not responding");
    }

    [Fact]
    public async Task IncrementRetryAsync_IncrementsRetryCount()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        await grain.CreateAsync(new CreateFiscalTransactionCommand(
            deviceId, locationId,
            FiscalTransactionType.Receipt, FiscalProcessType.Kassenbeleg,
            "Order", Guid.NewGuid(), 75.00m,
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>()));

        await grain.IncrementRetryAsync();
        await grain.IncrementRetryAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.RetryCount.Should().Be(2);
        snapshot.Status.Should().Be(FiscalTransactionStatus.Retrying);
    }

    [Fact]
    public async Task MarkExportedAsync_SetsExportedAt()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        await grain.CreateAsync(new CreateFiscalTransactionCommand(
            deviceId, locationId,
            FiscalTransactionType.Receipt, FiscalProcessType.Kassenbeleg,
            "Order", Guid.NewGuid(), 200.00m,
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>()));

        await grain.SignAsync(new SignTransactionCommand(
            "sig", 1, "cert", "qr", "raw"));

        await grain.MarkExportedAsync();

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ExportedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetQrCodeDataAsync_ReturnsQrCode()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        await grain.CreateAsync(new CreateFiscalTransactionCommand(
            deviceId, locationId,
            FiscalTransactionType.Receipt, FiscalProcessType.Kassenbeleg,
            "Order", Guid.NewGuid(), 150.00m,
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>()));

        await grain.SignAsync(new SignTransactionCommand(
            "sig", 5, "cert", "V0;STORE;5;2024-01-15;150.00;sig-data", "raw"));

        var qrCode = await grain.GetQrCodeDataAsync();

        qrCode.Should().Contain("V0;STORE;5");
    }
}

// ============================================================================
// Fiscal Journal Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class FiscalJournalGrainTests
{
    private readonly TestClusterFixture _fixture;

    public FiscalJournalGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFiscalJournalGrain GetGrain(Guid orgId, DateTime date)
    {
        var key = $"{orgId}:fiscaljournal:{date:yyyy-MM-dd}";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalJournalGrain>(key);
    }

    [Fact]
    public async Task LogEventAsync_LogsTransactionSignedEvent()
    {
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, date);

        var command = new LogFiscalEventCommand(
            LocationId: Guid.NewGuid(),
            EventType: FiscalEventType.TransactionSigned,
            DeviceId: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            ExportId: null,
            Details: "Transaction 12345 signed successfully",
            IpAddress: "192.168.1.100",
            UserId: Guid.NewGuid(),
            Severity: FiscalEventSeverity.Info);

        await grain.LogEventAsync(command);

        var entries = await grain.GetEntriesAsync();
        entries.Should().ContainSingle();
        entries[0].EventType.Should().Be(FiscalEventType.TransactionSigned);
        entries[0].Severity.Should().Be(FiscalEventSeverity.Info);
    }

    [Fact]
    public async Task LogEventAsync_LogsDeviceRegisteredEvent()
    {
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, date);

        var command = new LogFiscalEventCommand(
            LocationId: Guid.NewGuid(),
            EventType: FiscalEventType.DeviceRegistered,
            DeviceId: Guid.NewGuid(),
            TransactionId: null,
            ExportId: null,
            Details: "New TSE device registered: SB-123456",
            IpAddress: null,
            UserId: Guid.NewGuid(),
            Severity: FiscalEventSeverity.Info);

        await grain.LogEventAsync(command);

        var entries = await grain.GetEntriesAsync();
        entries.Should().ContainSingle(e => e.EventType == FiscalEventType.DeviceRegistered);
    }

    [Fact]
    public async Task LogEventAsync_LogsErrorEvent()
    {
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, date);

        var command = new LogFiscalEventCommand(
            LocationId: Guid.NewGuid(),
            EventType: FiscalEventType.Error,
            DeviceId: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            ExportId: null,
            Details: "TSE communication timeout after 30 seconds",
            IpAddress: "10.0.0.50",
            UserId: null,
            Severity: FiscalEventSeverity.Error);

        await grain.LogEventAsync(command);

        var errors = await grain.GetErrorsAsync();
        errors.Should().ContainSingle();
        errors[0].Severity.Should().Be(FiscalEventSeverity.Error);
    }

    [Fact]
    public async Task GetEntriesByDeviceAsync_FiltersEntriesByDevice()
    {
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var deviceId1 = Guid.NewGuid();
        var deviceId2 = Guid.NewGuid();
        var grain = GetGrain(orgId, date);

        await grain.LogEventAsync(new LogFiscalEventCommand(
            null, FiscalEventType.TransactionSigned, deviceId1, Guid.NewGuid(),
            null, "Transaction on device 1", null, null, FiscalEventSeverity.Info));

        await grain.LogEventAsync(new LogFiscalEventCommand(
            null, FiscalEventType.TransactionSigned, deviceId2, Guid.NewGuid(),
            null, "Transaction on device 2", null, null, FiscalEventSeverity.Info));

        await grain.LogEventAsync(new LogFiscalEventCommand(
            null, FiscalEventType.TransactionSigned, deviceId1, Guid.NewGuid(),
            null, "Another transaction on device 1", null, null, FiscalEventSeverity.Info));

        var device1Entries = await grain.GetEntriesByDeviceAsync(deviceId1);

        device1Entries.Should().HaveCount(2);
        device1Entries.Should().OnlyContain(e => e.DeviceId == deviceId1);
    }

    [Fact]
    public async Task GetErrorsAsync_ReturnsOnlyErrors()
    {
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, date);

        await grain.LogEventAsync(new LogFiscalEventCommand(
            null, FiscalEventType.TransactionSigned, Guid.NewGuid(), null,
            null, "Success", null, null, FiscalEventSeverity.Info));

        await grain.LogEventAsync(new LogFiscalEventCommand(
            null, FiscalEventType.Error, Guid.NewGuid(), null,
            null, "Error 1", null, null, FiscalEventSeverity.Error));

        await grain.LogEventAsync(new LogFiscalEventCommand(
            null, FiscalEventType.DeviceStatusChanged, Guid.NewGuid(), null,
            null, "Warning", null, null, FiscalEventSeverity.Warning));

        await grain.LogEventAsync(new LogFiscalEventCommand(
            null, FiscalEventType.Error, Guid.NewGuid(), null,
            null, "Error 2", null, null, FiscalEventSeverity.Error));

        var errors = await grain.GetErrorsAsync();

        errors.Should().HaveCount(2);
        errors.Should().OnlyContain(e => e.Severity == FiscalEventSeverity.Error);
    }

    [Fact]
    public async Task GetEntryCountAsync_ReturnsTotalCount()
    {
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, date);

        await grain.LogEventAsync(new LogFiscalEventCommand(
            null, FiscalEventType.TransactionSigned, null, null,
            null, "Event 1", null, null, FiscalEventSeverity.Info));

        await grain.LogEventAsync(new LogFiscalEventCommand(
            null, FiscalEventType.SelfTestPerformed, null, null,
            null, "Event 2", null, null, FiscalEventSeverity.Info));

        await grain.LogEventAsync(new LogFiscalEventCommand(
            null, FiscalEventType.ExportGenerated, null, null,
            null, "Event 3", null, null, FiscalEventSeverity.Info));

        var count = await grain.GetEntryCountAsync();

        count.Should().Be(3);
    }
}

// ============================================================================
// Tax Rate Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TaxRateGrainTests
{
    private readonly TestClusterFixture _fixture;

    public TaxRateGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ITaxRateGrain GetGrain(Guid orgId, string countryCode, string fiscalCode)
    {
        var key = $"{orgId}:taxrate:{countryCode}:{fiscalCode}";
        return _fixture.Cluster.GrainFactory.GetGrain<ITaxRateGrain>(key);
    }

    [Fact]
    public async Task CreateAsync_WithGermanStandardRate_CreatesTaxRate()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "DE", "NORMAL");

        var command = new CreateTaxRateCommand(
            CountryCode: "DE",
            Rate: 19.0m,
            FiscalCode: "NORMAL",
            Description: "German Standard VAT Rate",
            EffectiveFrom: new DateTime(2024, 1, 1),
            EffectiveTo: null);

        var snapshot = await grain.CreateAsync(command);

        snapshot.CountryCode.Should().Be("DE");
        snapshot.Rate.Should().Be(19.0m);
        snapshot.FiscalCode.Should().Be("NORMAL");
        snapshot.Description.Should().Be("German Standard VAT Rate");
        snapshot.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithReducedRate_CreatesTaxRate()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "DE", "REDUCED");

        var command = new CreateTaxRateCommand(
            CountryCode: "DE",
            Rate: 7.0m,
            FiscalCode: "REDUCED",
            Description: "German Reduced VAT Rate",
            EffectiveFrom: new DateTime(2024, 1, 1),
            EffectiveTo: null);

        var snapshot = await grain.CreateAsync(command);

        snapshot.Rate.Should().Be(7.0m);
        snapshot.FiscalCode.Should().Be("REDUCED");
    }

    [Fact]
    public async Task CreateAsync_WithZeroRate_CreatesTaxRate()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "DE", "NULL");

        var command = new CreateTaxRateCommand(
            CountryCode: "DE",
            Rate: 0m,
            FiscalCode: "NULL",
            Description: "Zero Rate / Exempt",
            EffectiveFrom: new DateTime(2024, 1, 1),
            EffectiveTo: null);

        var snapshot = await grain.CreateAsync(command);

        snapshot.Rate.Should().Be(0m);
    }

    [Fact]
    public async Task DeactivateAsync_SetsEffectiveTo()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "AT", "NORMAL");

        await grain.CreateAsync(new CreateTaxRateCommand(
            "AT", 20.0m, "NORMAL", "Austrian Standard VAT",
            new DateTime(2024, 1, 1), null));

        var effectiveTo = new DateTime(2024, 12, 31);
        await grain.DeactivateAsync(effectiveTo);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.EffectiveTo.Should().Be(effectiveTo);
        snapshot.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetCurrentRateAsync_ReturnsCurrentRate()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "CH", "NORMAL");

        await grain.CreateAsync(new CreateTaxRateCommand(
            "CH", 8.1m, "NORMAL", "Swiss Standard VAT",
            new DateTime(2024, 1, 1), null));

        var rate = await grain.GetCurrentRateAsync();

        rate.Should().Be(8.1m);
    }

    [Fact]
    public async Task IsActiveOnDateAsync_WhenDateInRange_ReturnsTrue()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "FR", "NORMAL");

        await grain.CreateAsync(new CreateTaxRateCommand(
            "FR", 20.0m, "NORMAL", "French Standard VAT",
            new DateTime(2024, 1, 1), new DateTime(2024, 12, 31)));

        var isActive = await grain.IsActiveOnDateAsync(new DateTime(2024, 6, 15));

        isActive.Should().BeTrue();
    }

    [Fact]
    public async Task IsActiveOnDateAsync_WhenDateOutOfRange_ReturnsFalse()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "IT", "NORMAL");

        await grain.CreateAsync(new CreateTaxRateCommand(
            "IT", 22.0m, "NORMAL", "Italian Standard VAT",
            new DateTime(2024, 1, 1), new DateTime(2024, 6, 30)));

        var isActive = await grain.IsActiveOnDateAsync(new DateTime(2024, 12, 1));

        isActive.Should().BeFalse();
    }

    [Fact]
    public async Task IsActiveOnDateAsync_WhenNoEndDate_ReturnsTrue()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "ES", "NORMAL");

        await grain.CreateAsync(new CreateTaxRateCommand(
            "ES", 21.0m, "NORMAL", "Spanish Standard VAT",
            new DateTime(2024, 1, 1), null));

        var isActive = await grain.IsActiveOnDateAsync(new DateTime(2030, 1, 1));

        isActive.Should().BeTrue();
    }
}

// ============================================================================
// TSE Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TseGrainTests
{
    private readonly TestClusterFixture _fixture;

    public TseGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ITseGrain GetGrain(Guid orgId, Guid tseId)
    {
        var key = $"{orgId}:tse:{tseId}";
        return _fixture.Cluster.GrainFactory.GetGrain<ITseGrain>(key);
    }

    [Fact]
    public async Task InitializeAsync_InitializesTse()
    {
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, tseId);

        var snapshot = await grain.InitializeAsync(locationId);

        snapshot.TseId.Should().Be(tseId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.IsInitialized.Should().BeTrue();
        snapshot.TransactionCounter.Should().Be(0);
        snapshot.SignatureCounter.Should().Be(0);
        snapshot.CertificateSerial.Should().NotBeNullOrEmpty();
        snapshot.PublicKeyBase64.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task StartTransactionAsync_StartsTransaction()
    {
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, tseId);

        await grain.InitializeAsync(locationId);

        var result = await grain.StartTransactionAsync(new StartTseTransactionCommand(
            LocationId: locationId,
            ProcessType: "Kassenbeleg",
            ProcessData: "100.00^NORMAL:84.03^NORMAL:15.97^CASH:100.00",
            ClientId: "POS-001"));

        result.Success.Should().BeTrue();
        result.TransactionNumber.Should().Be(1);
        result.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.ClientId.Should().Be("POS-001");
    }

    [Fact]
    public async Task FinishTransactionAsync_GeneratesSignature()
    {
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, tseId);

        await grain.InitializeAsync(locationId);

        var startResult = await grain.StartTransactionAsync(new StartTseTransactionCommand(
            LocationId: locationId,
            ProcessType: "Kassenbeleg",
            ProcessData: "50.00^NORMAL:42.02^NORMAL:7.98^CARD:50.00",
            ClientId: null));

        var finishResult = await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
            TransactionNumber: startResult.TransactionNumber,
            ProcessType: "Kassenbeleg",
            ProcessData: "50.00^NORMAL:42.02^NORMAL:7.98^CARD:50.00"));

        finishResult.Success.Should().BeTrue();
        finishResult.TransactionNumber.Should().Be(1);
        finishResult.SignatureCounter.Should().Be(1);
        finishResult.Signature.Should().NotBeNullOrEmpty();
        finishResult.SignatureAlgorithm.Should().Be("HMAC-SHA256");
        finishResult.CertificateSerial.Should().NotBeNullOrEmpty();
        finishResult.QrCodeData.Should().NotBeNullOrEmpty();
        finishResult.QrCodeData.Should().StartWith("V0;");
        finishResult.EndTime.Should().BeAfter(finishResult.StartTime);
    }

    [Fact]
    public async Task UpdateTransactionAsync_UpdatesProcessData()
    {
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, tseId);

        await grain.InitializeAsync(locationId);

        var startResult = await grain.StartTransactionAsync(new StartTseTransactionCommand(
            LocationId: locationId,
            ProcessType: "Kassenbeleg",
            ProcessData: "25.00",
            ClientId: null));

        var updateSuccess = await grain.UpdateTransactionAsync(new UpdateTseTransactionCommand(
            TransactionNumber: startResult.TransactionNumber,
            ProcessData: "75.00^NORMAL:63.03^NORMAL:11.97^CASH:75.00"));

        updateSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SelfTestAsync_PerformsSelfTest()
    {
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, tseId);

        await grain.InitializeAsync(locationId);

        var result = await grain.SelfTestAsync();

        result.Passed.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.PerformedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastSelfTestAt.Should().NotBeNull();
        snapshot.LastSelfTestPassed.Should().BeTrue();
    }

    [Fact]
    public async Task ConfigureExternalMappingAsync_ConfiguresMapping()
    {
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var externalDeviceId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, tseId);

        await grain.InitializeAsync(locationId);

        var snapshot = await grain.ConfigureExternalMappingAsync(new ConfigureExternalTseMappingCommand(
            Enabled: true,
            ExternalDeviceId: externalDeviceId,
            ExternalTseType: ExternalTseType.FiskalyCloud,
            ApiEndpoint: "https://kassensichv.fiskaly.com",
            ClientId: "fiskaly-client-123",
            ForwardAllEvents: true,
            RequireExternalSignature: false));

        snapshot.ExternalMapping.Should().NotBeNull();
        snapshot.ExternalMapping!.Enabled.Should().BeTrue();
        snapshot.ExternalMapping.ExternalDeviceId.Should().Be(externalDeviceId);
        snapshot.ExternalMapping.ExternalTseType.Should().Be(ExternalTseType.FiskalyCloud);
        snapshot.ExternalMapping.ForwardAllEvents.Should().BeTrue();
    }

    [Fact]
    public async Task MultipleTransactions_IncrementCounters()
    {
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, tseId);

        await grain.InitializeAsync(locationId);

        // First transaction
        var start1 = await grain.StartTransactionAsync(new StartTseTransactionCommand(
            locationId, "Kassenbeleg", "10.00", null));
        var finish1 = await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
            start1.TransactionNumber, "Kassenbeleg", "10.00"));

        // Second transaction
        var start2 = await grain.StartTransactionAsync(new StartTseTransactionCommand(
            locationId, "Kassenbeleg", "20.00", null));
        var finish2 = await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
            start2.TransactionNumber, "Kassenbeleg", "20.00"));

        // Third transaction
        var start3 = await grain.StartTransactionAsync(new StartTseTransactionCommand(
            locationId, "Kassenbeleg", "30.00", null));
        var finish3 = await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
            start3.TransactionNumber, "Kassenbeleg", "30.00"));

        finish1.TransactionNumber.Should().Be(1);
        finish1.SignatureCounter.Should().Be(1);
        finish2.TransactionNumber.Should().Be(2);
        finish2.SignatureCounter.Should().Be(2);
        finish3.TransactionNumber.Should().Be(3);
        finish3.SignatureCounter.Should().Be(3);

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.TransactionCounter.Should().Be(3);
        snapshot.SignatureCounter.Should().Be(3);
    }

    [Fact]
    public async Task FinishTransactionAsync_WithInvalidTransactionNumber_Fails()
    {
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, tseId);

        await grain.InitializeAsync(locationId);

        var result = await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
            TransactionNumber: 999,
            ProcessType: "Kassenbeleg",
            ProcessData: "100.00"));

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task ReceiveExternalResponseAsync_ProcessesExternalResponse()
    {
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, tseId);

        await grain.InitializeAsync(locationId);

        // Start a transaction
        var startResult = await grain.StartTransactionAsync(new StartTseTransactionCommand(
            locationId, "Kassenbeleg", "100.00", null));

        // Simulate receiving an external TSE response
        await grain.ReceiveExternalResponseAsync(
            transactionNumber: startResult.TransactionNumber,
            externalTransactionId: "ext-txn-12345",
            externalSignature: "external-signature-base64",
            externalCertificateSerial: "EXT-CERT-001",
            externalSignatureCounter: 42,
            externalTimestamp: DateTime.UtcNow,
            rawResponse: "{\"status\":\"ok\"}");

        // The method should complete without throwing
        // In a real implementation, this would update the transaction state
    }
}

// ============================================================================
// Fiscal Transaction Grain with TSE Integration Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class FiscalTransactionWithTseTests
{
    private readonly TestClusterFixture _fixture;

    public FiscalTransactionWithTseTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFiscalTransactionGrain GetTransactionGrain(Guid orgId, Guid transactionId)
    {
        var key = $"{orgId}:fiscaltransaction:{transactionId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalTransactionGrain>(key);
    }

    private ITseGrain GetTseGrain(Guid orgId, Guid tseId)
    {
        var key = $"{orgId}:tse:{tseId}";
        return _fixture.Cluster.GrainFactory.GetGrain<ITseGrain>(key);
    }

    private IFiscalDeviceGrain GetDeviceGrain(Guid orgId, Guid deviceId)
    {
        var key = $"{orgId}:fiscaldevice:{deviceId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalDeviceGrain>(key);
    }

    [Fact]
    public async Task CreateAndSignWithTseAsync_CreatesAndSignsTransaction()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        // Setup fiscal device
        var deviceGrain = GetDeviceGrain(orgId, deviceId);
        await deviceGrain.RegisterAsync(new RegisterFiscalDeviceCommand(
            locationId, FiscalDeviceType.SwissbitCloud, "SB-TSE-001",
            null, null, null, null, null));

        // Initialize TSE
        var tseGrain = GetTseGrain(orgId, tseId);
        await tseGrain.InitializeAsync(locationId);

        // Create and sign transaction with TSE
        var transactionGrain = GetTransactionGrain(orgId, transactionId);
        var command = new CreateAndSignWithTseCommand(
            FiscalDeviceId: deviceId,
            LocationId: locationId,
            TseId: tseId,
            TransactionType: FiscalTransactionType.Receipt,
            ProcessType: FiscalProcessType.Kassenbeleg,
            SourceType: "Order",
            SourceId: orderId,
            GrossAmount: 119.00m,
            NetAmounts: new Dictionary<string, decimal> { ["NORMAL"] = 100.00m },
            TaxAmounts: new Dictionary<string, decimal> { ["NORMAL"] = 19.00m },
            PaymentTypes: new Dictionary<string, decimal> { ["CASH"] = 119.00m },
            ClientId: "POS-001");

        var snapshot = await transactionGrain.CreateAndSignWithTseAsync(command);

        snapshot.FiscalTransactionId.Should().Be(transactionId);
        snapshot.Status.Should().Be(FiscalTransactionStatus.Signed);
        snapshot.TransactionNumber.Should().Be(1);
        snapshot.SignatureCounter.Should().Be(1);
        snapshot.Signature.Should().NotBeNullOrEmpty();
        snapshot.CertificateSerial.Should().NotBeNullOrEmpty();
        snapshot.QrCodeData.Should().NotBeNullOrEmpty();
        snapshot.QrCodeData.Should().StartWith("V0;");
        snapshot.StartTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        snapshot.EndTime.Should().NotBeNull();
        snapshot.EndTime.Should().BeAfter(snapshot.StartTime);
    }

    [Fact]
    public async Task CreateAndSignWithTseAsync_WithVoid_CreatesSignedVoidTransaction()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var tseId = Guid.NewGuid();

        // Setup fiscal device
        var deviceGrain = GetDeviceGrain(orgId, deviceId);
        await deviceGrain.RegisterAsync(new RegisterFiscalDeviceCommand(
            locationId, FiscalDeviceType.FiskalyCloud, "FISK-001",
            null, null, null, null, null));

        // Initialize TSE
        var tseGrain = GetTseGrain(orgId, tseId);
        await tseGrain.InitializeAsync(locationId);

        // Create and sign void transaction
        var transactionGrain = GetTransactionGrain(orgId, transactionId);
        var command = new CreateAndSignWithTseCommand(
            FiscalDeviceId: deviceId,
            LocationId: locationId,
            TseId: tseId,
            TransactionType: FiscalTransactionType.Void,
            ProcessType: FiscalProcessType.Kassenbeleg,
            SourceType: "Void",
            SourceId: Guid.NewGuid(),
            GrossAmount: -50.00m,
            NetAmounts: new Dictionary<string, decimal> { ["NORMAL"] = -42.02m },
            TaxAmounts: new Dictionary<string, decimal> { ["NORMAL"] = -7.98m },
            PaymentTypes: new Dictionary<string, decimal> { ["CASH"] = -50.00m },
            ClientId: null);

        var snapshot = await transactionGrain.CreateAndSignWithTseAsync(command);

        snapshot.TransactionType.Should().Be(FiscalTransactionType.Void);
        snapshot.GrossAmount.Should().Be(-50.00m);
        snapshot.Status.Should().Be(FiscalTransactionStatus.Signed);
        snapshot.Signature.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateAndSignWithTseAsync_MultipleTransactions_IncrementCounters()
    {
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var tseId = Guid.NewGuid();

        // Setup
        var deviceGrain = GetDeviceGrain(orgId, deviceId);
        await deviceGrain.RegisterAsync(new RegisterFiscalDeviceCommand(
            locationId, FiscalDeviceType.SwissbitUsb, "USB-001",
            null, null, null, null, null));

        var tseGrain = GetTseGrain(orgId, tseId);
        await tseGrain.InitializeAsync(locationId);

        // First transaction
        var tx1 = GetTransactionGrain(orgId, Guid.NewGuid());
        var snapshot1 = await tx1.CreateAndSignWithTseAsync(new CreateAndSignWithTseCommand(
            deviceId, locationId, tseId,
            FiscalTransactionType.Receipt, FiscalProcessType.Kassenbeleg,
            "Order", Guid.NewGuid(), 10.00m,
            new Dictionary<string, decimal>(), new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>(), null));

        // Second transaction
        var tx2 = GetTransactionGrain(orgId, Guid.NewGuid());
        var snapshot2 = await tx2.CreateAndSignWithTseAsync(new CreateAndSignWithTseCommand(
            deviceId, locationId, tseId,
            FiscalTransactionType.Receipt, FiscalProcessType.Kassenbeleg,
            "Order", Guid.NewGuid(), 20.00m,
            new Dictionary<string, decimal>(), new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>(), null));

        // Third transaction
        var tx3 = GetTransactionGrain(orgId, Guid.NewGuid());
        var snapshot3 = await tx3.CreateAndSignWithTseAsync(new CreateAndSignWithTseCommand(
            deviceId, locationId, tseId,
            FiscalTransactionType.Receipt, FiscalProcessType.Kassenbeleg,
            "Order", Guid.NewGuid(), 30.00m,
            new Dictionary<string, decimal>(), new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>(), null));

        snapshot1.TransactionNumber.Should().Be(1);
        snapshot1.SignatureCounter.Should().Be(1);
        snapshot2.TransactionNumber.Should().Be(2);
        snapshot2.SignatureCounter.Should().Be(2);
        snapshot3.TransactionNumber.Should().Be(3);
        snapshot3.SignatureCounter.Should().Be(3);

        // Verify TSE counters
        var tseSnapshot = await tseGrain.GetSnapshotAsync();
        tseSnapshot.TransactionCounter.Should().Be(3);
        tseSnapshot.SignatureCounter.Should().Be(3);
    }
}

// ============================================================================
// Internal TSE Provider Unit Tests
// ============================================================================

public class InternalTseProviderTests
{
    [Fact]
    public async Task StartTransactionAsync_ReturnsIncrementingTransactionNumbers()
    {
        var provider = new InternalTseProvider();

        var result1 = await provider.StartTransactionAsync("Kassenbeleg", "data1", "client1");
        var result2 = await provider.StartTransactionAsync("Kassenbeleg", "data2", "client2");
        var result3 = await provider.StartTransactionAsync("Kassenbeleg", "data3", "client3");

        result1.TransactionNumber.Should().Be(1);
        result2.TransactionNumber.Should().Be(2);
        result3.TransactionNumber.Should().Be(3);
    }

    [Fact]
    public async Task FinishTransactionAsync_GeneratesValidSignature()
    {
        var provider = new InternalTseProvider();

        var start = await provider.StartTransactionAsync("Kassenbeleg", "100.00", null);
        var finish = await provider.FinishTransactionAsync(
            start.TransactionNumber, "Kassenbeleg", "100.00");

        finish.Success.Should().BeTrue();
        finish.Signature.Should().NotBeNullOrEmpty();
        finish.SignatureAlgorithm.Should().Be("HMAC-SHA256");
        finish.SignatureCounter.Should().Be(1);
    }

    [Fact]
    public async Task FinishTransactionAsync_GeneratesKassenSichVQrCode()
    {
        var provider = new InternalTseProvider();

        var start = await provider.StartTransactionAsync("Kassenbeleg", "50.00", null);
        var finish = await provider.FinishTransactionAsync(
            start.TransactionNumber, "Kassenbeleg", "50.00");

        // QR code should follow KassenSichV format: V0;TseSerial;SignAlgo;TimeFormat;...
        finish.QrCodeData.Should().StartWith("V0;");
        finish.QrCodeData.Should().Contain("HMAC-SHA256");
        finish.QrCodeData.Should().Contain("utcTime");
        finish.QrCodeData.Should().Contain(finish.SignatureCounter.ToString());
    }

    [Fact]
    public async Task FinishTransactionAsync_WithUnknownTransaction_ReturnsFailed()
    {
        var provider = new InternalTseProvider();

        var result = await provider.FinishTransactionAsync(999, "Kassenbeleg", "data");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateTransactionAsync_UpdatesProcessData()
    {
        var provider = new InternalTseProvider();

        var start = await provider.StartTransactionAsync("Kassenbeleg", "initial", null);
        var updated = await provider.UpdateTransactionAsync(start.TransactionNumber, "updated");

        updated.Should().BeTrue();
    }

    [Fact]
    public async Task SelfTestAsync_ReturnsPassingResult()
    {
        var provider = new InternalTseProvider();

        var result = await provider.SelfTestAsync();

        result.Passed.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task GetCertificateSerialAsync_ReturnsCertificateSerial()
    {
        var provider = new InternalTseProvider();

        var serial = await provider.GetCertificateSerialAsync();

        serial.Should().NotBeNullOrEmpty();
        serial.Should().StartWith("DVTSE");
    }

    [Fact]
    public void IsInternal_ReturnsTrue()
    {
        var provider = new InternalTseProvider();

        provider.IsInternal.Should().BeTrue();
    }

    [Fact]
    public void ProviderType_ReturnsInternalTse()
    {
        var provider = new InternalTseProvider();

        provider.ProviderType.Should().Be("InternalTse");
    }

    [Fact]
    public async Task MultipleSignatures_HaveUniqueValues()
    {
        var provider = new InternalTseProvider();

        var start1 = await provider.StartTransactionAsync("Kassenbeleg", "data1", null);
        var finish1 = await provider.FinishTransactionAsync(start1.TransactionNumber, "Kassenbeleg", "data1");

        var start2 = await provider.StartTransactionAsync("Kassenbeleg", "data2", null);
        var finish2 = await provider.FinishTransactionAsync(start2.TransactionNumber, "Kassenbeleg", "data2");

        // Different data should produce different signatures
        finish1.Signature.Should().NotBe(finish2.Signature);
        finish1.QrCodeData.Should().NotBe(finish2.QrCodeData);
    }
}
