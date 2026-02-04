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

    [Fact]
    public async Task RegisterAsync_AlreadyRegistered_ShouldThrow()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-001",
            null, null, null, null, null));

        var act = () => grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitUsb, "SB-002",
            null, null, null, null, null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal device already registered");
    }

    [Fact]
    public async Task Operations_OnUninitialized_ShouldThrow()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        var getSnapshotAct = () => grain.GetSnapshotAsync();
        var updateAct = () => grain.UpdateAsync(new UpdateFiscalDeviceCommand(null, null, null, null, null));
        var deactivateAct = () => grain.DeactivateAsync();
        var getNextTxAct = () => grain.GetNextTransactionCounterAsync();
        var getNextSigAct = () => grain.GetNextSignatureCounterAsync();
        var recordSyncAct = () => grain.RecordSyncAsync();
        var isCertExpiringAct = () => grain.IsCertificateExpiringAsync(30);

        await getSnapshotAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal device grain not initialized");
        await updateAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal device grain not initialized");
        await deactivateAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal device grain not initialized");
        await getNextTxAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal device grain not initialized");
        await getNextSigAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal device grain not initialized");
        await recordSyncAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal device grain not initialized");
        await isCertExpiringAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal device grain not initialized");
    }

    [Fact]
    public async Task RegisterAsync_EpsonDevice_ShouldSucceed()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        var command = new RegisterFiscalDeviceCommand(
            LocationId: locationId,
            DeviceType: FiscalDeviceType.Epson,
            SerialNumber: "EPSON-RT-88VI-001",
            PublicKey: null,
            CertificateExpiryDate: null,
            ApiEndpoint: null,
            ApiCredentialsEncrypted: null,
            ClientId: null);

        var snapshot = await grain.RegisterAsync(command);

        snapshot.FiscalDeviceId.Should().Be(deviceId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.DeviceType.Should().Be(FiscalDeviceType.Epson);
        snapshot.SerialNumber.Should().Be("EPSON-RT-88VI-001");
        snapshot.Status.Should().Be(FiscalDeviceStatus.Active);
    }

    [Fact]
    public async Task RegisterAsync_DieboldDevice_ShouldSucceed()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        var command = new RegisterFiscalDeviceCommand(
            LocationId: locationId,
            DeviceType: FiscalDeviceType.Diebold,
            SerialNumber: "DIEBOLD-TSE-2024-001",
            PublicKey: "diebold-public-key-data",
            CertificateExpiryDate: DateTime.UtcNow.AddYears(3),
            ApiEndpoint: "https://diebold.local/tse",
            ApiCredentialsEncrypted: "encrypted-creds",
            ClientId: "diebold-client-001");

        var snapshot = await grain.RegisterAsync(command);

        snapshot.FiscalDeviceId.Should().Be(deviceId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.DeviceType.Should().Be(FiscalDeviceType.Diebold);
        snapshot.SerialNumber.Should().Be("DIEBOLD-TSE-2024-001");
        snapshot.PublicKey.Should().Be("diebold-public-key-data");
        snapshot.Status.Should().Be(FiscalDeviceStatus.Active);
        snapshot.ClientId.Should().Be("diebold-client-001");
    }

    [Fact]
    public async Task UpdateAsync_StatusChange_ShouldUpdate()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-STATUS-TEST",
            null, null, null, null, null));

        var updateCommand = new UpdateFiscalDeviceCommand(
            Status: FiscalDeviceStatus.CertificateExpiring,
            PublicKey: null,
            CertificateExpiryDate: null,
            ApiEndpoint: null,
            ApiCredentialsEncrypted: null);

        var snapshot = await grain.UpdateAsync(updateCommand);

        snapshot.Status.Should().Be(FiscalDeviceStatus.CertificateExpiring);
    }

    [Fact]
    public async Task IsCertificateExpiringAsync_Expired_ShouldReturnTrue()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // Certificate expired 10 days ago
        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-EXPIRED",
            null, DateTime.UtcNow.AddDays(-10), null, null, null));

        var isExpiring = await grain.IsCertificateExpiringAsync(30);

        isExpiring.Should().BeTrue();
    }

    [Fact]
    public async Task IsCertificateExpiringAsync_ExactlyAtThreshold_ShouldReturnTrue()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // Certificate expires exactly at the 30-day threshold
        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SB-THRESHOLD",
            null, DateTime.UtcNow.AddDays(30), null, null, null));

        var isExpiring = await grain.IsCertificateExpiringAsync(30);

        isExpiring.Should().BeTrue();
    }

    [Fact]
    public async Task IsCertificateExpiringAsync_NoCertificate_ShouldHandle()
    {
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, deviceId);

        // No certificate expiry date set
        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.FiskalyCloud, "FISK-NO-CERT",
            null, null, null, null, null));

        var isExpiring = await grain.IsCertificateExpiringAsync(30);

        // When no certificate date is set, it should return false (not expiring)
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

    [Fact]
    public async Task CreateAsync_CancellationTransaction_ShouldSucceed()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var originalOrderId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        var command = new CreateFiscalTransactionCommand(
            FiscalDeviceId: deviceId,
            LocationId: locationId,
            TransactionType: FiscalTransactionType.Cancellation,
            ProcessType: FiscalProcessType.Kassenbeleg,
            SourceType: "Cancellation",
            SourceId: originalOrderId,
            GrossAmount: -119.00m,
            NetAmounts: new Dictionary<string, decimal>
            {
                ["NORMAL"] = -100.00m
            },
            TaxAmounts: new Dictionary<string, decimal>
            {
                ["NORMAL"] = -19.00m
            },
            PaymentTypes: new Dictionary<string, decimal>
            {
                ["CASH"] = -119.00m
            });

        var snapshot = await grain.CreateAsync(command);

        snapshot.FiscalTransactionId.Should().Be(transactionId);
        snapshot.TransactionType.Should().Be(FiscalTransactionType.Cancellation);
        snapshot.GrossAmount.Should().Be(-119.00m);
        snapshot.Status.Should().Be(FiscalTransactionStatus.Pending);
    }

    [Fact]
    public async Task CreateAsync_AVTransferProcess_ShouldSucceed()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        var command = new CreateFiscalTransactionCommand(
            FiscalDeviceId: deviceId,
            LocationId: locationId,
            TransactionType: FiscalTransactionType.Receipt,
            ProcessType: FiscalProcessType.AVTransfer,
            SourceType: "Transfer",
            SourceId: Guid.NewGuid(),
            GrossAmount: 500.00m,
            NetAmounts: new Dictionary<string, decimal>(),
            TaxAmounts: new Dictionary<string, decimal>(),
            PaymentTypes: new Dictionary<string, decimal>
            {
                ["TRANSFER"] = 500.00m
            });

        var snapshot = await grain.CreateAsync(command);

        snapshot.ProcessType.Should().Be(FiscalProcessType.AVTransfer);
        snapshot.GrossAmount.Should().Be(500.00m);
    }

    [Fact]
    public async Task CreateAsync_AVBestellungProcess_ShouldSucceed()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        var command = new CreateFiscalTransactionCommand(
            FiscalDeviceId: deviceId,
            LocationId: locationId,
            TransactionType: FiscalTransactionType.Receipt,
            ProcessType: FiscalProcessType.AVBestellung,
            SourceType: "PreOrder",
            SourceId: Guid.NewGuid(),
            GrossAmount: 250.00m,
            NetAmounts: new Dictionary<string, decimal>
            {
                ["NORMAL"] = 210.08m
            },
            TaxAmounts: new Dictionary<string, decimal>
            {
                ["NORMAL"] = 39.92m
            },
            PaymentTypes: new Dictionary<string, decimal>
            {
                ["CARD"] = 250.00m
            });

        var snapshot = await grain.CreateAsync(command);

        snapshot.ProcessType.Should().Be(FiscalProcessType.AVBestellung);
    }

    [Fact]
    public async Task CreateAsync_AVSonstigerProcess_ShouldSucceed()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        var command = new CreateFiscalTransactionCommand(
            FiscalDeviceId: deviceId,
            LocationId: locationId,
            TransactionType: FiscalTransactionType.Receipt,
            ProcessType: FiscalProcessType.AVSonstiger,
            SourceType: "Other",
            SourceId: Guid.NewGuid(),
            GrossAmount: 100.00m,
            NetAmounts: new Dictionary<string, decimal>(),
            TaxAmounts: new Dictionary<string, decimal>(),
            PaymentTypes: new Dictionary<string, decimal>());

        var snapshot = await grain.CreateAsync(command);

        snapshot.ProcessType.Should().Be(FiscalProcessType.AVSonstiger);
    }

    [Fact]
    public async Task CreateAsync_AlreadyCreated_ShouldThrow()
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

        var act = () => grain.CreateAsync(new CreateFiscalTransactionCommand(
            deviceId, locationId,
            FiscalTransactionType.Receipt, FiscalProcessType.Kassenbeleg,
            "Order", Guid.NewGuid(), 200.00m,
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>()));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal transaction already exists");
    }

    [Fact]
    public async Task SignAsync_AlreadySigned_ShouldThrow()
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

        await grain.SignAsync(new SignTransactionCommand(
            "signature-1", 1, "cert-1", "qr-1", "raw-1"));

        var act = () => grain.SignAsync(new SignTransactionCommand(
            "signature-2", 2, "cert-2", "qr-2", "raw-2"));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Transaction already signed");
    }

    [Fact]
    public async Task Operations_OnUninitialized_ShouldThrow()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var grain = GetGrain(orgId, transactionId);

        var getSnapshotAct = () => grain.GetSnapshotAsync();
        var signAct = () => grain.SignAsync(new SignTransactionCommand("sig", 1, "cert", "qr", "raw"));
        var markFailedAct = () => grain.MarkFailedAsync("error");
        var incrementRetryAct = () => grain.IncrementRetryAsync();
        var markExportedAct = () => grain.MarkExportedAsync();
        var getQrCodeAct = () => grain.GetQrCodeDataAsync();

        await getSnapshotAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal transaction grain not initialized");
        await signAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal transaction grain not initialized");
        await markFailedAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal transaction grain not initialized");
        await incrementRetryAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal transaction grain not initialized");
        await markExportedAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal transaction grain not initialized");
        await getQrCodeAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Fiscal transaction grain not initialized");
    }

    [Fact]
    public async Task FullLifecycle_Create_Sign_Export()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        // Create transaction
        var createSnapshot = await grain.CreateAsync(new CreateFiscalTransactionCommand(
            deviceId, locationId,
            FiscalTransactionType.Receipt, FiscalProcessType.Kassenbeleg,
            "Order", orderId, 238.00m,
            new Dictionary<string, decimal>
            {
                ["NORMAL"] = 150.00m,
                ["REDUCED"] = 50.00m
            },
            new Dictionary<string, decimal>
            {
                ["NORMAL"] = 28.50m,
                ["REDUCED"] = 9.50m
            },
            new Dictionary<string, decimal>
            {
                ["CASH"] = 138.00m,
                ["CARD"] = 100.00m
            }));

        createSnapshot.Status.Should().Be(FiscalTransactionStatus.Pending);
        createSnapshot.TransactionNumber.Should().BeGreaterThan(0);

        // Sign transaction
        var signSnapshot = await grain.SignAsync(new SignTransactionCommand(
            Signature: "MEUCIQDf7k8Jx+signature",
            SignatureCounter: 42,
            CertificateSerial: "TSE-CERT-2024-001",
            QrCodeData: "V0;STORE123;42;2024-06-15T14:30:00;238.00;MEUCIQDf7k8Jx+signature",
            TseResponseRaw: "{\"status\":\"ok\"}"));

        signSnapshot.Status.Should().Be(FiscalTransactionStatus.Signed);
        signSnapshot.Signature.Should().NotBeNullOrEmpty();
        signSnapshot.SignatureCounter.Should().Be(42);
        signSnapshot.EndTime.Should().NotBeNull();

        // Export transaction
        await grain.MarkExportedAsync();
        var finalSnapshot = await grain.GetSnapshotAsync();

        finalSnapshot.ExportedAt.Should().NotBeNull();
        finalSnapshot.ExportedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task FailedTransaction_Retry_Sign_ShouldWork()
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

        // First attempt fails
        await grain.MarkFailedAsync("TSE timeout");
        var failedSnapshot = await grain.GetSnapshotAsync();
        failedSnapshot.Status.Should().Be(FiscalTransactionStatus.Failed);
        failedSnapshot.ErrorMessage.Should().Be("TSE timeout");

        // Increment retry count
        await grain.IncrementRetryAsync();
        var retryingSnapshot = await grain.GetSnapshotAsync();
        retryingSnapshot.Status.Should().Be(FiscalTransactionStatus.Retrying);
        retryingSnapshot.RetryCount.Should().Be(1);

        // Second retry
        await grain.IncrementRetryAsync();
        var retry2Snapshot = await grain.GetSnapshotAsync();
        retry2Snapshot.RetryCount.Should().Be(2);

        // Successfully sign after retries
        var signSnapshot = await grain.SignAsync(new SignTransactionCommand(
            "retry-signature", 10, "cert", "qr-code", "raw"));

        signSnapshot.Status.Should().Be(FiscalTransactionStatus.Signed);
        signSnapshot.RetryCount.Should().Be(2);
    }

    [Fact]
    public async Task GetQrCodeDataAsync_BeforeSigning_ShouldReturnEmpty()
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

        var qrCode = await grain.GetQrCodeDataAsync();

        qrCode.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ComplexTaxBreakdown_MultipleTaxRates()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        // German tax scenario: food (7%), drinks (19%), zero-rated export
        var command = new CreateFiscalTransactionCommand(
            FiscalDeviceId: deviceId,
            LocationId: locationId,
            TransactionType: FiscalTransactionType.Receipt,
            ProcessType: FiscalProcessType.Kassenbeleg,
            SourceType: "Order",
            SourceId: Guid.NewGuid(),
            GrossAmount: 289.47m,
            NetAmounts: new Dictionary<string, decimal>
            {
                ["NORMAL"] = 100.00m,   // 19% rate
                ["REDUCED"] = 130.00m,  // 7% rate
                ["NULL"] = 20.00m       // 0% rate
            },
            TaxAmounts: new Dictionary<string, decimal>
            {
                ["NORMAL"] = 19.00m,
                ["REDUCED"] = 9.10m,
                ["NULL"] = 0.00m
            },
            PaymentTypes: new Dictionary<string, decimal>
            {
                ["CARD"] = 289.47m
            });

        var snapshot = await grain.CreateAsync(command);

        snapshot.GrossAmount.Should().Be(289.47m);
        snapshot.NetAmounts.Should().HaveCount(3);
        snapshot.NetAmounts["NORMAL"].Should().Be(100.00m);
        snapshot.NetAmounts["REDUCED"].Should().Be(130.00m);
        snapshot.NetAmounts["NULL"].Should().Be(20.00m);
        snapshot.TaxAmounts.Should().HaveCount(3);
        snapshot.TaxAmounts["NORMAL"].Should().Be(19.00m);
        snapshot.TaxAmounts["REDUCED"].Should().Be(9.10m);
        snapshot.TaxAmounts["NULL"].Should().Be(0.00m);
    }

    [Fact]
    public async Task CreateAsync_ComplexPaymentSplit_MultiplePaymentTypes()
    {
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var deviceId = await SetupDeviceAsync(orgId, locationId);
        var grain = GetGrain(orgId, transactionId);

        // Complex split payment scenario
        var command = new CreateFiscalTransactionCommand(
            FiscalDeviceId: deviceId,
            LocationId: locationId,
            TransactionType: FiscalTransactionType.Receipt,
            ProcessType: FiscalProcessType.Kassenbeleg,
            SourceType: "Order",
            SourceId: Guid.NewGuid(),
            GrossAmount: 500.00m,
            NetAmounts: new Dictionary<string, decimal>
            {
                ["NORMAL"] = 420.17m
            },
            TaxAmounts: new Dictionary<string, decimal>
            {
                ["NORMAL"] = 79.83m
            },
            PaymentTypes: new Dictionary<string, decimal>
            {
                ["CASH"] = 100.00m,
                ["CARD"] = 250.00m,
                ["GIFTCARD"] = 50.00m,
                ["VOUCHER"] = 75.00m,
                ["ONLINE"] = 25.00m
            });

        var snapshot = await grain.CreateAsync(command);

        snapshot.GrossAmount.Should().Be(500.00m);
        snapshot.PaymentTypes.Should().HaveCount(5);
        snapshot.PaymentTypes["CASH"].Should().Be(100.00m);
        snapshot.PaymentTypes["CARD"].Should().Be(250.00m);
        snapshot.PaymentTypes["GIFTCARD"].Should().Be(50.00m);
        snapshot.PaymentTypes["VOUCHER"].Should().Be(75.00m);
        snapshot.PaymentTypes["ONLINE"].Should().Be(25.00m);
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

    [Fact]
    public async Task LogEventAsync_DeviceDecommissioned_ShouldLog()
    {
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, date);

        var command = new LogFiscalEventCommand(
            LocationId: Guid.NewGuid(),
            EventType: FiscalEventType.DeviceDecommissioned,
            DeviceId: deviceId,
            TransactionId: null,
            ExportId: null,
            Details: "TSE device SB-123 decommissioned for replacement",
            IpAddress: "192.168.1.50",
            UserId: Guid.NewGuid(),
            Severity: FiscalEventSeverity.Warning);

        await grain.LogEventAsync(command);

        var entries = await grain.GetEntriesAsync();
        entries.Should().ContainSingle(e => e.EventType == FiscalEventType.DeviceDecommissioned);
        var entry = entries.First(e => e.EventType == FiscalEventType.DeviceDecommissioned);
        entry.DeviceId.Should().Be(deviceId);
        entry.Details.Should().Contain("decommissioned");
    }

    [Fact]
    public async Task LogEventAsync_ExportGenerated_ShouldLog()
    {
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var exportId = Guid.NewGuid();
        var grain = GetGrain(orgId, date);

        var command = new LogFiscalEventCommand(
            LocationId: Guid.NewGuid(),
            EventType: FiscalEventType.ExportGenerated,
            DeviceId: null,
            TransactionId: null,
            ExportId: exportId,
            Details: "DSFinV-K export generated: 2024-01 to 2024-03",
            IpAddress: "10.0.0.100",
            UserId: Guid.NewGuid(),
            Severity: FiscalEventSeverity.Info);

        await grain.LogEventAsync(command);

        var entries = await grain.GetEntriesAsync();
        entries.Should().ContainSingle(e => e.EventType == FiscalEventType.ExportGenerated);
        var entry = entries.First(e => e.EventType == FiscalEventType.ExportGenerated);
        entry.ExportId.Should().Be(exportId);
        entry.Details.Should().Contain("DSFinV-K");
    }

    [Fact]
    public async Task LogEventAsync_DeviceStatusChanged_ShouldLog()
    {
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, date);

        var command = new LogFiscalEventCommand(
            LocationId: Guid.NewGuid(),
            EventType: FiscalEventType.DeviceStatusChanged,
            DeviceId: deviceId,
            TransactionId: null,
            ExportId: null,
            Details: "Device status changed from Active to CertificateExpiring",
            IpAddress: null,
            UserId: null,
            Severity: FiscalEventSeverity.Warning);

        await grain.LogEventAsync(command);

        var entries = await grain.GetEntriesAsync();
        entries.Should().ContainSingle(e => e.EventType == FiscalEventType.DeviceStatusChanged);
        var entry = entries.First(e => e.EventType == FiscalEventType.DeviceStatusChanged);
        entry.DeviceId.Should().Be(deviceId);
        entry.Severity.Should().Be(FiscalEventSeverity.Warning);
    }

    [Fact]
    public async Task LogEventAsync_SelfTestPerformed_ShouldLog()
    {
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, date);

        var command = new LogFiscalEventCommand(
            LocationId: Guid.NewGuid(),
            EventType: FiscalEventType.SelfTestPerformed,
            DeviceId: deviceId,
            TransactionId: null,
            ExportId: null,
            Details: "Daily self-test completed successfully",
            IpAddress: "192.168.1.10",
            UserId: null,
            Severity: FiscalEventSeverity.Info);

        await grain.LogEventAsync(command);

        var entries = await grain.GetEntriesAsync();
        entries.Should().ContainSingle(e => e.EventType == FiscalEventType.SelfTestPerformed);
        var entry = entries.First(e => e.EventType == FiscalEventType.SelfTestPerformed);
        entry.DeviceId.Should().Be(deviceId);
        entry.Details.Should().Contain("self-test");
    }

    [Fact]
    public async Task LogEventAsync_WarningSeverity_ShouldLog()
    {
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, date);

        var command = new LogFiscalEventCommand(
            LocationId: Guid.NewGuid(),
            EventType: FiscalEventType.DeviceStatusChanged,
            DeviceId: Guid.NewGuid(),
            TransactionId: null,
            ExportId: null,
            Details: "Certificate will expire in 25 days",
            IpAddress: null,
            UserId: null,
            Severity: FiscalEventSeverity.Warning);

        await grain.LogEventAsync(command);

        var entries = await grain.GetEntriesAsync();
        entries.Should().ContainSingle();
        entries[0].Severity.Should().Be(FiscalEventSeverity.Warning);

        // Warning events should not appear in GetErrorsAsync
        var errors = await grain.GetErrorsAsync();
        errors.Should().BeEmpty();
    }

    [Fact]
    public async Task GetEntriesAsync_ShouldReturnChronological()
    {
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, date);

        // Log events with small delays to ensure different timestamps
        await grain.LogEventAsync(new LogFiscalEventCommand(
            null, FiscalEventType.TransactionSigned, null, null,
            null, "First event", null, null, FiscalEventSeverity.Info));

        await Task.Delay(10);

        await grain.LogEventAsync(new LogFiscalEventCommand(
            null, FiscalEventType.DeviceStatusChanged, null, null,
            null, "Second event", null, null, FiscalEventSeverity.Info));

        await Task.Delay(10);

        await grain.LogEventAsync(new LogFiscalEventCommand(
            null, FiscalEventType.ExportGenerated, null, null,
            null, "Third event", null, null, FiscalEventSeverity.Info));

        var entries = await grain.GetEntriesAsync();

        entries.Should().HaveCount(3);
        entries[0].Details.Should().Be("First event");
        entries[1].Details.Should().Be("Second event");
        entries[2].Details.Should().Be("Third event");

        // Verify timestamps are in ascending order
        entries[0].Timestamp.Should().BeBefore(entries[1].Timestamp);
        entries[1].Timestamp.Should().BeBefore(entries[2].Timestamp);
    }

    [Fact]
    public async Task GetEntriesAsync_ByLocation_ShouldFilter()
    {
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var location1 = Guid.NewGuid();
        var location2 = Guid.NewGuid();
        var grain = GetGrain(orgId, date);

        await grain.LogEventAsync(new LogFiscalEventCommand(
            location1, FiscalEventType.TransactionSigned, null, null,
            null, "Location 1 - Event 1", null, null, FiscalEventSeverity.Info));

        await grain.LogEventAsync(new LogFiscalEventCommand(
            location2, FiscalEventType.TransactionSigned, null, null,
            null, "Location 2 - Event 1", null, null, FiscalEventSeverity.Info));

        await grain.LogEventAsync(new LogFiscalEventCommand(
            location1, FiscalEventType.ExportGenerated, null, null,
            null, "Location 1 - Event 2", null, null, FiscalEventSeverity.Info));

        var entries = await grain.GetEntriesAsync();

        // Filter manually since grain doesn't expose GetEntriesByLocationAsync
        var location1Entries = entries.Where(e => e.LocationId == location1).ToList();
        var location2Entries = entries.Where(e => e.LocationId == location2).ToList();

        location1Entries.Should().HaveCount(2);
        location2Entries.Should().HaveCount(1);
        location1Entries.Should().OnlyContain(e => e.LocationId == location1);
        location2Entries.Should().OnlyContain(e => e.LocationId == location2);
    }

    [Fact]
    public async Task HighVolume_ManyEntries_ShouldHandle()
    {
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, date);

        const int entryCount = 100;
        var deviceId = Guid.NewGuid();

        // Log many events
        for (int i = 0; i < entryCount; i++)
        {
            await grain.LogEventAsync(new LogFiscalEventCommand(
                LocationId: Guid.NewGuid(),
                EventType: i % 2 == 0 ? FiscalEventType.TransactionSigned : FiscalEventType.SelfTestPerformed,
                DeviceId: deviceId,
                TransactionId: Guid.NewGuid(),
                ExportId: null,
                Details: $"High volume event {i + 1}",
                IpAddress: $"192.168.1.{i % 256}",
                UserId: null,
                Severity: FiscalEventSeverity.Info));
        }

        var entries = await grain.GetEntriesAsync();
        var count = await grain.GetEntryCountAsync();
        var deviceEntries = await grain.GetEntriesByDeviceAsync(deviceId);

        entries.Should().HaveCount(entryCount);
        count.Should().Be(entryCount);
        deviceEntries.Should().HaveCount(entryCount);

        // Verify all entries are unique
        entries.Select(e => e.EntryId).Distinct().Should().HaveCount(entryCount);
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

    [Fact]
    public async Task CreateAsync_AlreadyCreated_ShouldThrow()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "PL", "NORMAL");

        await grain.CreateAsync(new CreateTaxRateCommand(
            "PL", 23.0m, "NORMAL", "Polish Standard VAT",
            new DateTime(2024, 1, 1), null));

        var act = () => grain.CreateAsync(new CreateTaxRateCommand(
            "PL", 23.0m, "NORMAL", "Polish Standard VAT Duplicate",
            new DateTime(2024, 6, 1), null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tax rate already exists");
    }

    [Fact]
    public async Task Operations_OnUninitialized_ShouldThrow()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "XX", "UNINIT");

        var getSnapshotAct = () => grain.GetSnapshotAsync();
        var deactivateAct = () => grain.DeactivateAsync(DateTime.UtcNow);
        var getCurrentRateAct = () => grain.GetCurrentRateAsync();
        var isActiveAct = () => grain.IsActiveOnDateAsync(DateTime.UtcNow);

        await getSnapshotAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tax rate grain not initialized");
        await deactivateAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tax rate grain not initialized");
        await getCurrentRateAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tax rate grain not initialized");
        await isActiveAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tax rate grain not initialized");
    }

    [Fact]
    public async Task IsActiveOnDateAsync_ExactlyOnEffectiveFrom_ShouldReturnTrue()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "BE", "NORMAL");

        var effectiveFrom = new DateTime(2024, 7, 1);

        await grain.CreateAsync(new CreateTaxRateCommand(
            "BE", 21.0m, "NORMAL", "Belgian Standard VAT",
            effectiveFrom, new DateTime(2024, 12, 31)));

        var isActive = await grain.IsActiveOnDateAsync(effectiveFrom);

        isActive.Should().BeTrue();
    }

    [Fact]
    public async Task IsActiveOnDateAsync_ExactlyOnEffectiveTo_ShouldReturnTrue()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "NL", "NORMAL");

        var effectiveTo = new DateTime(2024, 12, 31);

        await grain.CreateAsync(new CreateTaxRateCommand(
            "NL", 21.0m, "NORMAL", "Dutch Standard VAT",
            new DateTime(2024, 1, 1), effectiveTo));

        var isActive = await grain.IsActiveOnDateAsync(effectiveTo);

        isActive.Should().BeTrue();
    }

    [Fact]
    public async Task IsActiveOnDateAsync_BeforeEffectiveFrom_ShouldReturnFalse()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "PT", "NORMAL");

        var effectiveFrom = new DateTime(2025, 1, 1);

        await grain.CreateAsync(new CreateTaxRateCommand(
            "PT", 23.0m, "NORMAL", "Portuguese Standard VAT",
            effectiveFrom, null));

        // Check a date before the effective from date
        var isActive = await grain.IsActiveOnDateAsync(new DateTime(2024, 12, 31));

        isActive.Should().BeFalse();
    }
}
