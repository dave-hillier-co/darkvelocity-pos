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

    // Given: A new fiscal device grain
    // When: Registering a Swissbit Cloud TSE with serial number, API endpoint, and credentials
    // Then: The device is registered as Active with zero transaction and signature counters
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

    // Given: A new fiscal device grain
    // When: Registering a Swissbit USB TSE with serial number
    // Then: The device is registered with the SwissbitUsb device type
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

    // Given: A new fiscal device grain
    // When: Registering a Fiskaly Cloud TSE with API endpoint and credentials
    // Then: The device is registered with the FiskalyCloud type and correct API endpoint
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

    // Given: A registered Epson fiscal device
    // When: Updating the device with a new public key, certificate expiry, and API endpoint
    // Then: The updated fields reflect the new values
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

    // Given: A registered active Diebold fiscal device
    // When: Deactivating the device
    // Then: The device status transitions to Inactive
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

    // Given: A registered Swissbit Cloud fiscal device
    // When: Requesting 3 sequential transaction counters
    // Then: Counters increment from 1 to 3
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

    // Given: A registered Fiskaly Cloud fiscal device
    // When: Requesting 2 sequential signature counters
    // Then: Counters increment from 1 to 2
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

    // Given: A registered Swissbit USB fiscal device
    // When: Recording a sync event
    // Then: The LastSyncAt timestamp is set to approximately now
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

    // Given: A fiscal device with a certificate expiring in 15 days
    // When: Checking if the certificate is expiring within 30 days
    // Then: The check returns true
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

    // Given: A fiscal device with a certificate expiring in 1 year
    // When: Checking if the certificate is expiring within 30 days
    // Then: The check returns false
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

    // Given: An already registered fiscal device
    // When: Attempting to register the same device grain again
    // Then: An InvalidOperationException is thrown with "Fiscal device already registered"
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

    // Given: An uninitialized fiscal device grain (never registered)
    // When: Attempting any operation (snapshot, update, deactivate, counters, sync, certificate check)
    // Then: Each operation throws InvalidOperationException with "Fiscal device grain not initialized"
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

    // Given: A new fiscal device grain
    // When: Registering an Epson RT-88VI fiscal printer
    // Then: The device is registered as Active with the Epson device type
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

    // Given: A new fiscal device grain
    // When: Registering a Diebold TSE with public key, API endpoint, and client ID
    // Then: The device is registered with all fields including public key and client ID
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

    // Given: A registered Swissbit Cloud fiscal device
    // When: Updating the device status to CertificateExpiring
    // Then: The device status reflects CertificateExpiring
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

    // Given: A fiscal device with a certificate that expired 10 days ago
    // When: Checking if the certificate is expiring within 30 days
    // Then: The check returns true (already expired)
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

    // Given: A fiscal device with a certificate expiring in exactly 30 days
    // When: Checking if the certificate is expiring within 30 days
    // Then: The check returns true (boundary is inclusive)
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

    // Given: A Fiskaly Cloud device with no certificate expiry date set
    // When: Checking if the certificate is expiring within 30 days
    // Then: The check returns false (no certificate to expire)
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

    // Given: A registered fiscal device at a location
    // When: Creating a receipt transaction for a cash order of 119.00 with 19% VAT
    // Then: The transaction is created in Pending status with correct amounts and type
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

    // Given: A registered fiscal device at a location
    // When: Creating a training receipt transaction
    // Then: The transaction is created with TrainingReceipt type
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

    // Given: A registered fiscal device at a location
    // When: Creating a void transaction with negative amounts (-25.00)
    // Then: The transaction is created with Void type and negative gross amount
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

    // Given: A pending fiscal transaction for a receipt
    // When: Signing the transaction with signature data, counter, and certificate serial
    // Then: The transaction status transitions to Signed with all signature fields populated
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

    // Given: A pending fiscal transaction
    // When: Marking the transaction as failed with error "TSE device not responding"
    // Then: The transaction status transitions to Failed with the error message
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

    // Given: A pending fiscal transaction
    // When: Incrementing the retry count twice
    // Then: The retry count is 2 and status transitions to Retrying
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

    // Given: A signed fiscal transaction
    // When: Marking the transaction as exported for DSFinV-K
    // Then: The ExportedAt timestamp is set
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

    // Given: A signed fiscal transaction with QR code data containing "V0;STORE;5"
    // When: Retrieving the QR code data
    // Then: The QR code data string contains the expected KassenSichV-formatted content
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

    // Given: A registered fiscal device at a location
    // When: Creating a cancellation transaction with negative amounts (-119.00) referencing an original order
    // Then: The transaction is created with Cancellation type and negative gross amount
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

    // Given: A registered fiscal device at a location
    // When: Creating a receipt transaction with AVTransfer process type (cash transfer)
    // Then: The transaction is created with AVTransfer process type
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

    // Given: A registered fiscal device at a location
    // When: Creating a receipt transaction with AVBestellung (pre-order) process type
    // Then: The transaction is created with AVBestellung process type
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

    // Given: A registered fiscal device at a location
    // When: Creating a receipt transaction with AVSonstiger (other) process type
    // Then: The transaction is created with AVSonstiger process type
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

    // Given: A fiscal transaction that has already been created
    // When: Attempting to create the same transaction grain again
    // Then: An InvalidOperationException is thrown with "Fiscal transaction already exists"
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

    // Given: A fiscal transaction that has already been signed
    // When: Attempting to sign the transaction a second time
    // Then: An InvalidOperationException is thrown with "Transaction already signed"
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

    // Given: An uninitialized fiscal transaction grain (never created)
    // When: Attempting any operation (snapshot, sign, fail, retry, export, QR code)
    // Then: Each operation throws InvalidOperationException with "Fiscal transaction grain not initialized"
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

    // Given: An empty fiscal journal for today's date
    // When: Logging a TransactionSigned event with IP address and details
    // Then: The journal contains one entry of type TransactionSigned with Info severity
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

    // Given: An empty fiscal journal for today's date
    // When: Logging a DeviceRegistered event for a new TSE device
    // Then: The journal contains one DeviceRegistered entry
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

    // Given: An empty fiscal journal for today's date
    // When: Logging an Error event for a TSE communication timeout
    // Then: The error appears in the journal's error list with Error severity
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

    // Given: A fiscal journal with entries from two different devices
    // When: Querying entries for device 1
    // Then: Only device 1's 2 entries are returned
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

    // Given: A fiscal journal with Info, Error, and Warning events
    // When: Retrieving only error events
    // Then: Only the 2 Error-severity entries are returned
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

    // Given: A fiscal journal with 3 entries of different event types
    // When: Querying the total entry count
    // Then: The count returns 3
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

    // Given: An empty fiscal journal for today's date
    // When: Logging a DeviceDecommissioned event for a TSE replacement
    // Then: The entry is logged with Warning severity and decommission details
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

    // Given: An empty fiscal journal for today's date
    // When: Logging an ExportGenerated event for a DSFinV-K export
    // Then: The entry is logged with the export ID and export details
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

    // Given: An empty fiscal journal for today's date
    // When: Logging a DeviceStatusChanged event for a certificate expiring warning
    // Then: The entry is logged with Warning severity and the device ID
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

    // Given: An empty fiscal journal for today's date
    // When: Logging a SelfTestPerformed event for a daily self-test
    // Then: The entry is logged with the device ID and self-test details
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

    // Given: An empty fiscal journal for today's date
    // When: Logging a Warning-severity event about certificate expiry
    // Then: The entry is logged with Warning severity and does not appear in error queries
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

    // Given: A fiscal journal with 3 events logged sequentially with small delays
    // When: Retrieving all entries
    // Then: Entries are returned in chronological order with ascending timestamps
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

    // Given: A fiscal journal with entries from 2 different locations
    // When: Filtering entries by location
    // Then: Location 1 has 2 entries and location 2 has 1 entry
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

    // Given: A fiscal journal for a high-traffic day
    // When: Logging 100 entries to a single journal
    // Then: All 100 entries are stored with unique IDs and can be queried by device
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

    // Given: A new uninitialized TSE grain
    // When: Initializing the TSE for a specific location
    // Then: The TSE is initialized with zero transaction and signature counters and valid certificate data
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

    // Given: An initialized TSE device at a location
    // When: Starting a new Kassenbeleg transaction with process data and client ID "POS-001"
    // Then: The transaction starts successfully with transaction number 1 and the correct client ID
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

    // Given: An initialized TSE with an active Kassenbeleg transaction
    // When: Finishing the transaction with final process data
    // Then: A valid HMAC-SHA256 signature is generated with a KassenSichV-compliant QR code starting with "V0;"
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

    // Given: An initialized TSE with an active transaction containing initial process data
    // When: Updating the transaction's process data with revised amounts
    // Then: The update succeeds
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

    // Given: An initialized TSE device
    // When: Performing a self-test diagnostic check
    // Then: The self-test passes and the last self-test timestamp is recorded
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

    // Given: An initialized TSE device
    // When: Configuring an external mapping to a Fiskaly Cloud TSE with forwarding enabled
    // Then: The external mapping is stored with the correct external device ID, TSE type, and forwarding settings
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

    // Given: An initialized TSE device with no prior transactions
    // When: Starting and finishing three sequential Kassenbeleg transactions
    // Then: Transaction numbers increment from 1 to 3 and signature counters match, with the TSE tracking all 3
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

    // Given: An initialized TSE device with no active transactions
    // When: Attempting to finish a non-existent transaction number 999
    // Then: The operation fails with a "not found" error
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

    // Given: An initialized TSE with an active transaction
    // When: Receiving an external TSE response with external transaction ID, signature, and certificate
    // Then: The external response is processed without error
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

    // Given: A registered fiscal device and an initialized TSE at a location
    // When: Creating and signing a 119.00 EUR receipt transaction through the TSE with CASH payment
    // Then: The transaction is signed with a valid signature, QR code (V0; prefix), and incrementing counters
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

    // Given: A registered Fiskaly Cloud fiscal device and an initialized TSE
    // When: Creating and signing a void transaction for -50.00 EUR
    // Then: The void transaction is signed with negative amounts and a valid signature
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

    // Given: A registered Swissbit USB fiscal device and an initialized TSE
    // When: Creating and signing three sequential receipt transactions through the TSE
    // Then: Transaction numbers and signature counters increment from 1 to 3, and the TSE tracks all 3
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
    // Given: A fresh internal TSE provider with no prior transactions
    // When: Starting three sequential Kassenbeleg transactions
    // Then: Transaction numbers increment from 1 to 3
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

    // Given: An internal TSE provider with an active transaction
    // When: Finishing the transaction
    // Then: A valid HMAC-SHA256 signature is generated with signature counter 1
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

    // Given: An internal TSE provider with an active Kassenbeleg transaction
    // When: Finishing the transaction
    // Then: A KassenSichV-compliant QR code is generated containing "V0;", "HMAC-SHA256", "utcTime", and the signature counter
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

    // Given: An internal TSE provider with no active transactions
    // When: Attempting to finish a non-existent transaction number 999
    // Then: The operation fails with a "not found" error
    [Fact]
    public async Task FinishTransactionAsync_WithUnknownTransaction_ReturnsFailed()
    {
        var provider = new InternalTseProvider();

        var result = await provider.FinishTransactionAsync(999, "Kassenbeleg", "data");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    // Given: An internal TSE provider with an active transaction containing initial process data
    // When: Updating the transaction's process data
    // Then: The update succeeds
    [Fact]
    public async Task UpdateTransactionAsync_UpdatesProcessData()
    {
        var provider = new InternalTseProvider();

        var start = await provider.StartTransactionAsync("Kassenbeleg", "initial", null);
        var updated = await provider.UpdateTransactionAsync(start.TransactionNumber, "updated");

        updated.Should().BeTrue();
    }

    // Given: An internal TSE provider
    // When: Running the self-test diagnostic
    // Then: The self-test passes with no error message
    [Fact]
    public async Task SelfTestAsync_ReturnsPassingResult()
    {
        var provider = new InternalTseProvider();

        var result = await provider.SelfTestAsync();

        result.Passed.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    // Given: An internal TSE provider
    // When: Retrieving the certificate serial number
    // Then: A non-empty serial starting with "DVTSE" is returned
    [Fact]
    public async Task GetCertificateSerialAsync_ReturnsCertificateSerial()
    {
        var provider = new InternalTseProvider();

        var serial = await provider.GetCertificateSerialAsync();

        serial.Should().NotBeNullOrEmpty();
        serial.Should().StartWith("DVTSE");
    }

    // Given: An internal TSE provider instance
    // When: Checking the IsInternal property
    // Then: It returns true indicating this is a built-in TSE implementation
    [Fact]
    public void IsInternal_ReturnsTrue()
    {
        var provider = new InternalTseProvider();

        provider.IsInternal.Should().BeTrue();
    }

    // Given: An internal TSE provider instance
    // When: Checking the ProviderType property
    // Then: It returns "InternalTse" identifying the provider type
    [Fact]
    public void ProviderType_ReturnsInternalTse()
    {
        var provider = new InternalTseProvider();

        provider.ProviderType.Should().Be("InternalTse");
    }

    // Given: An internal TSE provider
    // When: Signing two transactions with different process data
    // Then: Each transaction produces a unique signature and unique QR code data
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

// ============================================================================
// Fiskaly Configuration Tests
// ============================================================================

public class FiskalyConfigurationTests
{
    // Given: A Fiskaly configuration for Germany in the Test environment
    // When: Resolving the base URL
    // Then: The KassenSichV middleware v2 URL is returned
    [Fact]
    public void GetBaseUrl_Germany_Test_ReturnsCorrectUrl()
    {
        var config = new FiskalyConfiguration(
            Enabled: true,
            Region: FiskalyRegion.Germany,
            Environment: FiskalyEnvironment.Test,
            ApiKey: "test-key",
            ApiSecret: "test-secret",
            TssId: "tss-123",
            ClientId: "client-123",
            OrganizationId: null);

        var url = config.GetBaseUrl();

        url.Should().Be("https://kassensichv-middleware.fiskaly.com/api/v2");
    }

    // Given: A Fiskaly configuration for Germany in the Production environment
    // When: Resolving the base URL
    // Then: The KassenSichV middleware v2 URL is returned
    [Fact]
    public void GetBaseUrl_Germany_Production_ReturnsCorrectUrl()
    {
        var config = new FiskalyConfiguration(
            Enabled: true,
            Region: FiskalyRegion.Germany,
            Environment: FiskalyEnvironment.Production,
            ApiKey: "prod-key",
            ApiSecret: "prod-secret",
            TssId: "tss-456",
            ClientId: "client-456",
            OrganizationId: null);

        var url = config.GetBaseUrl();

        url.Should().Be("https://kassensichv-middleware.fiskaly.com/api/v2");
    }

    // Given: A Fiskaly configuration for Austria in the Test environment
    // When: Resolving the base URL
    // Then: The RKSV v1 URL is returned
    [Fact]
    public void GetBaseUrl_Austria_Test_ReturnsCorrectUrl()
    {
        var config = new FiskalyConfiguration(
            Enabled: true,
            Region: FiskalyRegion.Austria,
            Environment: FiskalyEnvironment.Test,
            ApiKey: "test-key",
            ApiSecret: "test-secret",
            TssId: null,
            ClientId: "register-123",
            OrganizationId: null);

        var url = config.GetBaseUrl();

        url.Should().Be("https://rksv.fiskaly.com/api/v1");
    }

    // Given: A Fiskaly configuration for Austria in the Production environment
    // When: Resolving the base URL
    // Then: The RKSV v1 URL is returned
    [Fact]
    public void GetBaseUrl_Austria_Production_ReturnsCorrectUrl()
    {
        var config = new FiskalyConfiguration(
            Enabled: true,
            Region: FiskalyRegion.Austria,
            Environment: FiskalyEnvironment.Production,
            ApiKey: "prod-key",
            ApiSecret: "prod-secret",
            TssId: null,
            ClientId: "register-456",
            OrganizationId: null);

        var url = config.GetBaseUrl();

        url.Should().Be("https://rksv.fiskaly.com/api/v1");
    }

    // Given: A Fiskaly configuration for Italy in the Test environment
    // When: Resolving the base URL
    // Then: The RT (Registratore Telematico) v1 URL is returned
    [Fact]
    public void GetBaseUrl_Italy_Test_ReturnsCorrectUrl()
    {
        var config = new FiskalyConfiguration(
            Enabled: true,
            Region: FiskalyRegion.Italy,
            Environment: FiskalyEnvironment.Test,
            ApiKey: "test-key",
            ApiSecret: "test-secret",
            TssId: null,
            ClientId: "rt-123",
            OrganizationId: null);

        var url = config.GetBaseUrl();

        url.Should().Be("https://rt.fiskaly.com/api/v1");
    }

    // Given: A Fiskaly configuration for Italy in the Production environment
    // When: Resolving the base URL
    // Then: The RT (Registratore Telematico) v1 URL is returned
    [Fact]
    public void GetBaseUrl_Italy_Production_ReturnsCorrectUrl()
    {
        var config = new FiskalyConfiguration(
            Enabled: true,
            Region: FiskalyRegion.Italy,
            Environment: FiskalyEnvironment.Production,
            ApiKey: "prod-key",
            ApiSecret: "prod-secret",
            TssId: null,
            ClientId: "rt-456",
            OrganizationId: null);

        var url = config.GetBaseUrl();

        url.Should().Be("https://rt.fiskaly.com/api/v1");
    }
}

// ============================================================================
// Fiskaly Integration Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class FiskalyIntegrationGrainTests
{
    private readonly TestClusterFixture _fixture;

    public FiskalyIntegrationGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFiskalyIntegrationGrain GetGrain(Guid orgId)
    {
        var key = $"{orgId}:fiskaly";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiskalyIntegrationGrain>(key);
    }

    // Given: A new Fiskaly integration grain that has not been configured
    // When: Retrieving the integration snapshot
    // Then: The snapshot shows the integration as disabled with no TSS ID
    [Fact]
    public async Task GetSnapshotAsync_WhenNotConfigured_ReturnsDisabledSnapshot()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        var snapshot = await grain.GetSnapshotAsync();

        snapshot.Enabled.Should().BeFalse();
        snapshot.TssId.Should().BeNull();
    }

    // Given: A Fiskaly integration grain that has not been configured
    // When: Testing the connection to the Fiskaly service
    // Then: The connection test returns false
    [Fact]
    public async Task TestConnectionAsync_WhenNotConfigured_ReturnsFalse()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        var result = await grain.TestConnectionAsync();

        result.Should().BeFalse();
    }
}

// ============================================================================
// Fiskaly DTO Tests
// ============================================================================

public class FiskalyDtoTests
{
    // Given: VAT amounts for NORMAL and REDUCED_1 rates, and payment amounts for CASH and NON_CASH
    // When: Creating a Fiskaly receipt DTO with receipt type "RECEIPT"
    // Then: The receipt contains 2 VAT rate amounts and 2 payment type amounts
    [Fact]
    public void FiskalyReceipt_CanBeCreated()
    {
        var receipt = new FiskalyReceipt(
            ReceiptType: "RECEIPT",
            AmountsPerVatRate: new List<FiskalyVatAmount>
            {
                new("NORMAL", "100.00"),
                new("REDUCED_1", "50.00")
            },
            AmountsPerPaymentType: new List<FiskalyPaymentAmount>
            {
                new("CASH", "100.00"),
                new("NON_CASH", "50.00")
            });

        receipt.ReceiptType.Should().Be("RECEIPT");
        receipt.AmountsPerVatRate.Should().HaveCount(2);
        receipt.AmountsPerPaymentType.Should().HaveCount(2);
    }

    // Given: RKSV amounts for Normal (100.00), Reduced1 (50.00), and Zero (10.00) tax categories
    // When: Creating an Austrian RKSV receipt request with type "STANDARD"
    // Then: The receipt contains the correct amounts mapped to each Austrian tax category
    [Fact]
    public void FiskalyRksvReceiptRequest_CanBeCreated()
    {
        var receipt = new FiskalyRksvReceiptRequest(
            ReceiptType: "STANDARD",
            Amounts: new FiskalyRksvAmounts(
                Normal: "100.00",
                Reduced1: "50.00",
                Reduced2: null,
                Zero: "10.00",
                Special: null));

        receipt.ReceiptType.Should().Be("STANDARD");
        receipt.Amounts.Normal.Should().Be("100.00");
        receipt.Amounts.Reduced1.Should().Be("50.00");
        receipt.Amounts.Zero.Should().Be("10.00");
    }

    // Given: A Fiskaly receipt with type "RECEIPT"
    // When: Wrapping it in a transaction schema using the Standard V1 format
    // Then: The schema's StandardV1 property contains the receipt with the correct type
    [Fact]
    public void FiskalyTransactionSchema_CanBeCreatedWithStandardV1()
    {
        var receipt = new FiskalyReceipt(
            ReceiptType: "RECEIPT",
            AmountsPerVatRate: new List<FiskalyVatAmount>(),
            AmountsPerPaymentType: new List<FiskalyPaymentAmount>());

        var schema = new FiskalyTransactionSchema(
            StandardV1: new FiskalyStandardV1(receipt));

        schema.StandardV1.Should().NotBeNull();
        schema.StandardV1!.Receipt.ReceiptType.Should().Be("RECEIPT");
    }

    // Given: A VAT rate of "NORMAL" with amount "119.00"
    // When: Creating a FiskalyVatAmount DTO
    // Then: The VatRate and Amount properties are correctly mapped
    [Fact]
    public void FiskalyVatAmount_MapsToCorrectProperties()
    {
        var vatAmount = new FiskalyVatAmount("NORMAL", "119.00");

        vatAmount.VatRate.Should().Be("NORMAL");
        vatAmount.Amount.Should().Be("119.00");
    }

    // Given: A payment type of "CASH" with amount "50.00"
    // When: Creating a FiskalyPaymentAmount DTO
    // Then: The PaymentType and Amount properties are correctly mapped
    [Fact]
    public void FiskalyPaymentAmount_MapsToCorrectProperties()
    {
        var paymentAmount = new FiskalyPaymentAmount("CASH", "50.00");

        paymentAmount.PaymentType.Should().Be("CASH");
        paymentAmount.Amount.Should().Be("50.00");
    }
}

// ============================================================================
// Fiskaly Region Tests
// ============================================================================

public class FiskalyRegionTests
{
    // Given: A Fiskaly region (Germany/KassenSichV, Austria/RKSV, or Italy/RT)
    // When: Creating a configuration for that region and resolving the base URL
    // Then: A valid non-empty URL is returned for each supported region
    [Theory]
    [InlineData(FiskalyRegion.Germany, "KassenSichV")]
    [InlineData(FiskalyRegion.Austria, "RKSV")]
    [InlineData(FiskalyRegion.Italy, "RT")]
    public void AllRegions_AreSupported(FiskalyRegion region, string description)
    {
        // Verify all regions can create valid configurations
        var config = new FiskalyConfiguration(
            Enabled: true,
            Region: region,
            Environment: FiskalyEnvironment.Test,
            ApiKey: "key",
            ApiSecret: "secret",
            TssId: "tss",
            ClientId: "client",
            OrganizationId: null);

        config.Region.Should().Be(region);
        config.GetBaseUrl().Should().NotBeNullOrEmpty();
    }

    // Given: A Fiskaly environment (Test or Production)
    // When: Creating a configuration for that environment and resolving the base URL
    // Then: A valid non-empty URL is returned for each supported environment
    [Theory]
    [InlineData(FiskalyEnvironment.Test)]
    [InlineData(FiskalyEnvironment.Production)]
    public void AllEnvironments_AreSupported(FiskalyEnvironment environment)
    {
        var config = new FiskalyConfiguration(
            Enabled: true,
            Region: FiskalyRegion.Germany,
            Environment: environment,
            ApiKey: "key",
            ApiSecret: "secret",
            TssId: "tss",
            ClientId: "client",
            OrganizationId: null);

        config.Environment.Should().Be(environment);
        config.GetBaseUrl().Should().NotBeNullOrEmpty();
    }
}

// ============================================================================
// Fiskaly Config Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class FiskalyConfigGrainTests
{
    private readonly TestClusterFixture _fixture;

    public FiskalyConfigGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFiskalyConfigGrain GetGrain(Guid orgId)
    {
        var key = $"{orgId}:fiskaly:config";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiskalyConfigGrain>(key);
    }

    // Given: A new Fiskaly config grain that has never been configured
    // When: Retrieving the tenant configuration
    // Then: Default values are returned with Fiskaly disabled, no credentials, and Germany as default region
    [Fact]
    public async Task GetConfigAsync_WhenNew_ReturnsDefaultConfig()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        var config = await grain.GetConfigAsync();

        config.TenantId.Should().Be(orgId);
        config.Enabled.Should().BeFalse();
        config.HasCredentials.Should().BeFalse();
        config.Region.Should().Be(FiskalyRegion.Germany); // Default
    }

    // Given: A new Fiskaly config grain
    // When: Updating the configuration with Austria Production settings, API credentials, and forwarding enabled
    // Then: All fields are persisted including region, environment, credentials, client ID, and version is incremented
    [Fact]
    public async Task UpdateConfigAsync_UpdatesAllFields()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        var result = await grain.UpdateConfigAsync(new UpdateFiskalyTenantConfigCommand(
            Enabled: true,
            Region: FiskalyRegion.Austria,
            Environment: FiskalyEnvironment.Production,
            ApiKey: "test-api-key",
            ApiSecret: "test-api-secret",
            TssId: null,
            ClientId: "register-001",
            OrganizationId: "org-001",
            ForwardAllEvents: true,
            RequireExternalSignature: false));

        result.Enabled.Should().BeTrue();
        result.Region.Should().Be(FiskalyRegion.Austria);
        result.Environment.Should().Be(FiskalyEnvironment.Production);
        result.HasCredentials.Should().BeTrue();
        result.ClientId.Should().Be("register-001");
        result.OrganizationId.Should().Be("org-001");
        result.ForwardAllEvents.Should().BeTrue();
        result.RequireExternalSignature.Should().BeFalse();
        result.LastUpdatedAt.Should().NotBeNull();
        result.Version.Should().BeGreaterThan(0);
    }

    // Given: A Fiskaly config grain configured with credentials but currently disabled
    // When: Enabling the Fiskaly integration
    // Then: The configuration is marked as enabled
    [Fact]
    public async Task EnableAsync_EnablesConfig()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        await grain.UpdateConfigAsync(new UpdateFiskalyTenantConfigCommand(
            Enabled: false,
            Region: FiskalyRegion.Germany,
            Environment: FiskalyEnvironment.Test,
            ApiKey: "key",
            ApiSecret: "secret",
            TssId: "tss",
            ClientId: "client",
            OrganizationId: null,
            ForwardAllEvents: true,
            RequireExternalSignature: true));

        var result = await grain.EnableAsync();

        result.Enabled.Should().BeTrue();
    }

    // Given: A Fiskaly config grain that is currently enabled with credentials
    // When: Disabling the Fiskaly integration
    // Then: The configuration is marked as disabled
    [Fact]
    public async Task DisableAsync_DisablesConfig()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        await grain.UpdateConfigAsync(new UpdateFiskalyTenantConfigCommand(
            Enabled: true,
            Region: FiskalyRegion.Germany,
            Environment: FiskalyEnvironment.Test,
            ApiKey: "key",
            ApiSecret: "secret",
            TssId: "tss",
            ClientId: "client",
            OrganizationId: null,
            ForwardAllEvents: true,
            RequireExternalSignature: true));

        var result = await grain.DisableAsync();

        result.Enabled.Should().BeFalse();
    }

    // Given: A Fiskaly config grain with a null API key
    // When: Validating the configuration
    // Then: Validation fails with an error mentioning "API Key"
    [Fact]
    public async Task ValidateAsync_WithMissingApiKey_ReturnsError()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        await grain.UpdateConfigAsync(new UpdateFiskalyTenantConfigCommand(
            Enabled: true,
            Region: FiskalyRegion.Germany,
            Environment: FiskalyEnvironment.Test,
            ApiKey: null,
            ApiSecret: "secret",
            TssId: "tss",
            ClientId: "client",
            OrganizationId: null,
            ForwardAllEvents: true,
            RequireExternalSignature: true));

        var result = await grain.ValidateAsync();

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("API Key"));
    }

    // Given: A Fiskaly config grain for Germany with a null TSS ID (required for KassenSichV)
    // When: Validating the configuration
    // Then: Validation fails with an error mentioning "TSS ID"
    [Fact]
    public async Task ValidateAsync_WithMissingTssIdForGermany_ReturnsError()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        await grain.UpdateConfigAsync(new UpdateFiskalyTenantConfigCommand(
            Enabled: true,
            Region: FiskalyRegion.Germany,
            Environment: FiskalyEnvironment.Test,
            ApiKey: "key",
            ApiSecret: "secret",
            TssId: null,
            ClientId: "client",
            OrganizationId: null,
            ForwardAllEvents: true,
            RequireExternalSignature: true));

        var result = await grain.ValidateAsync();

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("TSS ID"));
    }

    // Given: A Fiskaly config grain for Germany Production with all required fields populated
    // When: Validating the configuration
    // Then: Validation passes with no errors
    [Fact]
    public async Task ValidateAsync_WithValidConfig_ReturnsValid()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        await grain.UpdateConfigAsync(new UpdateFiskalyTenantConfigCommand(
            Enabled: true,
            Region: FiskalyRegion.Germany,
            Environment: FiskalyEnvironment.Production,
            ApiKey: "key",
            ApiSecret: "secret",
            TssId: "tss-123",
            ClientId: "client-123",
            OrganizationId: null,
            ForwardAllEvents: true,
            RequireExternalSignature: true));

        var result = await grain.ValidateAsync();

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    // Given: A Fiskaly config grain for Germany in the Test environment with all required fields
    // When: Validating the configuration
    // Then: Validation passes but includes a warning about using the "Test environment"
    [Fact]
    public async Task ValidateAsync_WithTestEnvironment_ReturnsWarning()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        await grain.UpdateConfigAsync(new UpdateFiskalyTenantConfigCommand(
            Enabled: true,
            Region: FiskalyRegion.Germany,
            Environment: FiskalyEnvironment.Test,
            ApiKey: "key",
            ApiSecret: "secret",
            TssId: "tss-123",
            ClientId: "client-123",
            OrganizationId: null,
            ForwardAllEvents: true,
            RequireExternalSignature: true));

        var result = await grain.ValidateAsync();

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("Test environment"));
    }

    // Given: A Fiskaly config grain enabled for Austria Production with API credentials
    // When: Retrieving the Fiskaly configuration object
    // Then: A non-null configuration is returned with the correct region, environment, and credentials
    [Fact]
    public async Task GetFiskalyConfigurationAsync_WhenEnabled_ReturnsConfig()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        await grain.UpdateConfigAsync(new UpdateFiskalyTenantConfigCommand(
            Enabled: true,
            Region: FiskalyRegion.Austria,
            Environment: FiskalyEnvironment.Production,
            ApiKey: "my-api-key",
            ApiSecret: "my-api-secret",
            TssId: null,
            ClientId: "register-123",
            OrganizationId: "org-456",
            ForwardAllEvents: true,
            RequireExternalSignature: true));

        var config = await grain.GetFiskalyConfigurationAsync();

        config.Should().NotBeNull();
        config!.Enabled.Should().BeTrue();
        config.Region.Should().Be(FiskalyRegion.Austria);
        config.Environment.Should().Be(FiskalyEnvironment.Production);
        config.ApiKey.Should().Be("my-api-key");
        config.ApiSecret.Should().Be("my-api-secret");
        config.ClientId.Should().Be("register-123");
    }

    // Given: A Fiskaly config grain that is explicitly disabled
    // When: Retrieving the Fiskaly configuration object
    // Then: Null is returned since the integration is disabled
    [Fact]
    public async Task GetFiskalyConfigurationAsync_WhenDisabled_ReturnsNull()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        await grain.UpdateConfigAsync(new UpdateFiskalyTenantConfigCommand(
            Enabled: false,
            Region: FiskalyRegion.Germany,
            Environment: FiskalyEnvironment.Test,
            ApiKey: "key",
            ApiSecret: "secret",
            TssId: "tss",
            ClientId: "client",
            OrganizationId: null,
            ForwardAllEvents: true,
            RequireExternalSignature: true));

        var config = await grain.GetFiskalyConfigurationAsync();

        config.Should().BeNull();
    }

    // Given: A Fiskaly config grain enabled but with null API key and API secret
    // When: Retrieving the Fiskaly configuration object
    // Then: Null is returned since credentials are missing
    [Fact]
    public async Task GetFiskalyConfigurationAsync_WithMissingCredentials_ReturnsNull()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        await grain.UpdateConfigAsync(new UpdateFiskalyTenantConfigCommand(
            Enabled: true,
            Region: FiskalyRegion.Germany,
            Environment: FiskalyEnvironment.Test,
            ApiKey: null,
            ApiSecret: null,
            TssId: "tss",
            ClientId: "client",
            OrganizationId: null,
            ForwardAllEvents: true,
            RequireExternalSignature: true));

        var config = await grain.GetFiskalyConfigurationAsync();

        config.Should().BeNull();
    }
}

// ============================================================================
// Fiskaly Options Tests
// ============================================================================

public class FiskalyOptionsTests
{
    // Given: A default FiskalyOptions instance
    // When: Inspecting the default property values
    // Then: Fiskaly is disabled, defaults to Germany/Test, 30s timeout, 3 retries with exponential backoff
    [Fact]
    public void FiskalyOptions_HasCorrectDefaults()
    {
        var options = new FiskalyOptions();

        options.Enabled.Should().BeFalse();
        options.DefaultRegion.Should().Be("Germany");
        options.DefaultEnvironment.Should().Be("Test");
        options.HttpTimeoutSeconds.Should().Be(30);
        options.RetryAttempts.Should().Be(3);
        options.RetryDelayMs.Should().Be(1000);
        options.UseExponentialBackoff.Should().BeTrue();
        options.AutoSubscribeToEvents.Should().BeTrue();
        options.TokenRefreshBufferMinutes.Should().Be(5);
    }

    // Given: A default FiskalyRegionOptions instance
    // When: Inspecting the default URL mappings for each region
    // Then: Germany uses KassenSichV v2 URLs, Austria uses RKSV v1 URLs, and Italy uses RT v1 URLs
    [Fact]
    public void FiskalyRegionOptions_HasCorrectDefaultUrls()
    {
        var options = new FiskalyRegionOptions();

        options.Germany.TestUrl.Should().Be("https://kassensichv-middleware.fiskaly.com/api/v2");
        options.Germany.ProductionUrl.Should().Be("https://kassensichv-middleware.fiskaly.com/api/v2");
        options.Austria.TestUrl.Should().Be("https://rksv.fiskaly.com/api/v1");
        options.Austria.ProductionUrl.Should().Be("https://rksv.fiskaly.com/api/v1");
        options.Italy.TestUrl.Should().Be("https://rt.fiskaly.com/api/v1");
        options.Italy.ProductionUrl.Should().Be("https://rt.fiskaly.com/api/v1");
    }

    // Given: A default FiskalyTenantOptions instance
    // When: Inspecting the default property values
    // Then: Fiskaly is disabled, defaults to Germany/Test, with forwarding and external signature required
    [Fact]
    public void FiskalyTenantOptions_HasCorrectDefaults()
    {
        var options = new FiskalyTenantOptions();

        options.Enabled.Should().BeFalse();
        options.Region.Should().Be("Germany");
        options.Environment.Should().Be("Test");
        options.ForwardAllEvents.Should().BeTrue();
        options.RequireExternalSignature.Should().BeTrue();
    }
}
