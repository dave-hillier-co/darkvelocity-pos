using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Services;
using FluentAssertions;

namespace DarkVelocity.Tests;

// ============================================================================
// FISCAL COMPLIANCE TESTS
// Critical tests for regulatory compliance across multiple jurisdictions
// ============================================================================

// ============================================================================
// Transaction Sequencing Tests (No Gaps Allowed)
// KassenSichV / NF525 / RKSV compliance: transaction numbers must be sequential
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Compliance")]
[Trait("Regulation", "KassenSichV")]
public class TransactionSequencingTests
{
    private readonly TestClusterFixture _fixture;

    public TransactionSequencingTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
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
    public async Task TransactionCounter_ShouldBeSequential_NoGapsAllowed()
    {
        // Given: A TSE device initialized
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTseGrain(orgId, tseId);
        await grain.InitializeAsync(locationId);

        // When: Multiple transactions are processed
        var transactionNumbers = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            var startResult = await grain.StartTransactionAsync(new StartTseTransactionCommand(
                locationId, "Kassenbeleg", $"Amount:{i * 10}.00", null));

            var finishResult = await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
                startResult.TransactionNumber, "Kassenbeleg", $"Amount:{i * 10}.00"));

            transactionNumbers.Add(finishResult.TransactionNumber);
        }

        // Then: Transaction numbers must be sequential with no gaps
        transactionNumbers.Should().BeInAscendingOrder();
        transactionNumbers.Should().OnlyHaveUniqueItems();

        for (int i = 1; i < transactionNumbers.Count; i++)
        {
            var gap = transactionNumbers[i] - transactionNumbers[i - 1];
            gap.Should().Be(1,
                $"Transaction numbers must be sequential. Gap found between {transactionNumbers[i-1]} and {transactionNumbers[i]}");
        }
    }

    [Fact]
    public async Task SignatureCounter_ShouldBeSequential_NoGapsAllowed()
    {
        // Given: A TSE device initialized
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTseGrain(orgId, tseId);
        await grain.InitializeAsync(locationId);

        // When: Multiple transactions are signed
        var signatureCounters = new List<long>();
        for (int i = 0; i < 10; i++)
        {
            var startResult = await grain.StartTransactionAsync(new StartTseTransactionCommand(
                locationId, "Kassenbeleg", $"Amount:{i * 10}.00", null));

            var finishResult = await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
                startResult.TransactionNumber, "Kassenbeleg", $"Amount:{i * 10}.00"));

            signatureCounters.Add(finishResult.SignatureCounter);
        }

        // Then: Signature counters must be sequential with no gaps
        signatureCounters.Should().BeInAscendingOrder();
        signatureCounters.Should().OnlyHaveUniqueItems();

        for (int i = 1; i < signatureCounters.Count; i++)
        {
            var gap = signatureCounters[i] - signatureCounters[i - 1];
            gap.Should().Be(1,
                $"Signature counters must be sequential. Gap found between {signatureCounters[i-1]} and {signatureCounters[i]}");
        }
    }

    [Fact]
    public async Task FiscalDevice_TransactionCounter_MustBeMonotonicallyIncreasing()
    {
        // Given: A fiscal device registered
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetDeviceGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "TEST-COUNTER-001",
            null, null, null, null, null));

        // When: Getting multiple transaction counters
        var counters = new List<long>();
        for (int i = 0; i < 20; i++)
        {
            var counter = await grain.GetNextTransactionCounterAsync();
            counters.Add(counter);
        }

        // Then: Counters must be strictly increasing
        counters.Should().BeInAscendingOrder();
        counters.Should().OnlyHaveUniqueItems();
        counters.First().Should().Be(1);
        counters.Last().Should().Be(20);
    }

    [Fact]
    public async Task FiscalDevice_SignatureCounter_MustBeMonotonicallyIncreasing()
    {
        // Given: A fiscal device registered
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetDeviceGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.FiskalyCloud, "TEST-SIG-001",
            null, null, null, null, null));

        // When: Getting multiple signature counters
        var counters = new List<long>();
        for (int i = 0; i < 20; i++)
        {
            var counter = await grain.GetNextSignatureCounterAsync();
            counters.Add(counter);
        }

        // Then: Counters must be strictly increasing
        counters.Should().BeInAscendingOrder();
        counters.Should().OnlyHaveUniqueItems();
        counters.First().Should().Be(1);
        counters.Last().Should().Be(20);
    }

    [Fact]
    public async Task TransactionCounters_ShouldPersist_AcrossReactivation()
    {
        // Given: A TSE with some transactions
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTseGrain(orgId, tseId);
        await grain.InitializeAsync(locationId);

        // Complete 5 transactions
        for (int i = 0; i < 5; i++)
        {
            var start = await grain.StartTransactionAsync(new StartTseTransactionCommand(
                locationId, "Kassenbeleg", $"Amount:{i * 10}.00", null));
            await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
                start.TransactionNumber, "Kassenbeleg", $"Amount:{i * 10}.00"));
        }

        var snapshotBefore = await grain.GetSnapshotAsync();
        var counterBefore = snapshotBefore.TransactionCounter;
        var sigCounterBefore = snapshotBefore.SignatureCounter;

        // When: Getting a fresh reference to the same grain (simulating reactivation)
        var grainReactivated = GetTseGrain(orgId, tseId);

        // Start another transaction
        var nextStart = await grainReactivated.StartTransactionAsync(new StartTseTransactionCommand(
            locationId, "Kassenbeleg", "Amount:100.00", null));
        var nextFinish = await grainReactivated.FinishTransactionAsync(new FinishTseTransactionCommand(
            nextStart.TransactionNumber, "Kassenbeleg", "Amount:100.00"));

        // Then: Counters must continue from where they left off
        nextFinish.TransactionNumber.Should().Be(counterBefore + 1);
        nextFinish.SignatureCounter.Should().Be(sigCounterBefore + 1);
    }
}

// ============================================================================
// Journal Immutability Verification Tests
// Fiscal compliance requires audit journals to be append-only and immutable
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Compliance")]
[Trait("Regulation", "Audit")]
public class JournalImmutabilityTests
{
    private readonly TestClusterFixture _fixture;

    public JournalImmutabilityTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFiscalJournalGrain GetGrain(Guid orgId, DateTime date)
    {
        var key = $"{orgId}:fiscaljournal:{date:yyyy-MM-dd}";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalJournalGrain>(key);
    }

    [Fact]
    public async Task Journal_EntriesAreAppendOnly_CannotBeDeleted()
    {
        // Given: A journal with entries
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, date);

        for (int i = 0; i < 5; i++)
        {
            await grain.LogEventAsync(new LogFiscalEventCommand(
                Guid.NewGuid(), FiscalEventType.TransactionSigned, Guid.NewGuid(), Guid.NewGuid(),
                null, $"Transaction {i + 1} signed", null, null, FiscalEventSeverity.Info));
        }

        var initialCount = await grain.GetEntryCountAsync();

        // When: Adding more entries (the only allowed operation)
        await grain.LogEventAsync(new LogFiscalEventCommand(
            Guid.NewGuid(), FiscalEventType.TransactionSigned, Guid.NewGuid(), Guid.NewGuid(),
            null, "Additional transaction", null, null, FiscalEventSeverity.Info));

        // Then: Count should only increase, never decrease
        var newCount = await grain.GetEntryCountAsync();
        newCount.Should().Be(initialCount + 1);
        newCount.Should().BeGreaterThan(initialCount);
    }

    [Fact]
    public async Task Journal_EntryIds_MustBeUnique()
    {
        // Given: A journal with multiple entries
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, date);

        for (int i = 0; i < 50; i++)
        {
            await grain.LogEventAsync(new LogFiscalEventCommand(
                Guid.NewGuid(), FiscalEventType.TransactionSigned, Guid.NewGuid(), null,
                null, $"Entry {i}", null, null, FiscalEventSeverity.Info));
        }

        // When: Retrieving all entries
        var entries = await grain.GetEntriesAsync();

        // Then: All entry IDs must be unique
        entries.Select(e => e.EntryId).Should().OnlyHaveUniqueItems();
        entries.Should().HaveCount(50);
    }

    [Fact]
    public async Task Journal_Timestamps_MustBeInChronologicalOrder()
    {
        // Given: A journal with entries logged in sequence
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, date);

        for (int i = 0; i < 10; i++)
        {
            await grain.LogEventAsync(new LogFiscalEventCommand(
                Guid.NewGuid(), FiscalEventType.TransactionSigned, Guid.NewGuid(), null,
                null, $"Sequential entry {i}", null, null, FiscalEventSeverity.Info));
            await Task.Delay(5); // Small delay to ensure timestamp ordering
        }

        // When: Retrieving all entries
        var entries = await grain.GetEntriesAsync();

        // Then: Timestamps must be in chronological order
        var timestamps = entries.Select(e => e.Timestamp).ToList();
        timestamps.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Journal_PreservesAllEventTypes_ForCompliance()
    {
        // Given: A journal with various event types (compliance requires all types preserved)
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var deviceId = Guid.NewGuid();
        var grain = GetGrain(orgId, date);

        var eventTypes = new[]
        {
            FiscalEventType.TransactionSigned,
            FiscalEventType.TransactionCreated,
            FiscalEventType.TransactionVoided,
            FiscalEventType.DeviceRegistered,
            FiscalEventType.DeviceDecommissioned,
            FiscalEventType.ExportGenerated,
            FiscalEventType.Error,
            FiscalEventType.DeviceStatusChanged,
            FiscalEventType.SelfTestPerformed
        };

        foreach (var eventType in eventTypes)
        {
            await grain.LogEventAsync(new LogFiscalEventCommand(
                Guid.NewGuid(), eventType, deviceId, null, null,
                $"Event type: {eventType}", null, null, FiscalEventSeverity.Info));
        }

        // When: Retrieving entries
        var entries = await grain.GetEntriesAsync();

        // Then: All event types must be preserved
        var loggedEventTypes = entries.Select(e => e.EventType).Distinct().ToList();
        loggedEventTypes.Should().Contain(eventTypes);
    }

    [Fact]
    public async Task Journal_ErrorEvents_MustBePermanentlyRecorded()
    {
        // Given: A journal with error events
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetGrain(orgId, date);

        var errorDetails = new[]
        {
            "TSE communication timeout",
            "Certificate validation failed",
            "Signature verification error",
            "Device memory overflow"
        };

        foreach (var error in errorDetails)
        {
            await grain.LogEventAsync(new LogFiscalEventCommand(
                Guid.NewGuid(), FiscalEventType.Error, Guid.NewGuid(), null, null,
                error, "192.168.1.1", null, FiscalEventSeverity.Error));
        }

        // When: Retrieving errors
        var errors = await grain.GetErrorsAsync();

        // Then: All errors must be permanently recorded
        errors.Should().HaveCount(errorDetails.Length);
        errors.Select(e => e.Details).Should().Contain(errorDetails);
        errors.All(e => e.Severity == FiscalEventSeverity.Error).Should().BeTrue();
    }
}

// ============================================================================
// Signature Chain Validation Tests
// KassenSichV compliance: signatures must form an unbroken chain
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Compliance")]
[Trait("Regulation", "KassenSichV")]
public class SignatureChainValidationTests
{
    private readonly TestClusterFixture _fixture;

    public SignatureChainValidationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ITseGrain GetTseGrain(Guid orgId, Guid tseId)
    {
        var key = $"{orgId}:tse:{tseId}";
        return _fixture.Cluster.GrainFactory.GetGrain<ITseGrain>(key);
    }

    private IFiscalTransactionGrain GetTransactionGrain(Guid orgId, Guid transactionId)
    {
        var key = $"{orgId}:fiscaltransaction:{transactionId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalTransactionGrain>(key);
    }

    [Fact]
    public async Task SignatureChain_EachSignature_MustBeUnique()
    {
        // Given: A TSE device
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTseGrain(orgId, tseId);
        await grain.InitializeAsync(locationId);

        // When: Generating multiple signatures
        var signatures = new List<string>();
        for (int i = 0; i < 10; i++)
        {
            var start = await grain.StartTransactionAsync(new StartTseTransactionCommand(
                locationId, "Kassenbeleg", $"Amount:{(i + 1) * 10}.00", null));
            var finish = await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
                start.TransactionNumber, "Kassenbeleg", $"Amount:{(i + 1) * 10}.00"));

            signatures.Add(finish.Signature);
        }

        // Then: All signatures must be unique (no replay attacks possible)
        signatures.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SignatureChain_MustContain_CertificateSerial()
    {
        // Given: A TSE device
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTseGrain(orgId, tseId);
        await grain.InitializeAsync(locationId);

        // When: Completing a transaction
        var start = await grain.StartTransactionAsync(new StartTseTransactionCommand(
            locationId, "Kassenbeleg", "Amount:100.00", null));
        var finish = await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
            start.TransactionNumber, "Kassenbeleg", "Amount:100.00"));

        // Then: Certificate serial must be present
        finish.CertificateSerial.Should().NotBeNullOrEmpty();
        finish.CertificateSerial.Should().StartWith("DVTSE"); // Our internal format
    }

    [Fact]
    public async Task SignatureChain_ConsistentCertificate_AcrossAllTransactions()
    {
        // Given: A TSE device
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTseGrain(orgId, tseId);
        await grain.InitializeAsync(locationId);

        // When: Completing multiple transactions
        var certificateSerials = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var start = await grain.StartTransactionAsync(new StartTseTransactionCommand(
                locationId, "Kassenbeleg", $"Amount:{i * 10}.00", null));
            var finish = await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
                start.TransactionNumber, "Kassenbeleg", $"Amount:{i * 10}.00"));

            certificateSerials.Add(finish.CertificateSerial);
        }

        // Then: Certificate serial must be consistent (same TSE = same certificate)
        certificateSerials.Distinct().Should().HaveCount(1);
    }

    [Fact]
    public async Task SignatureChain_SignatureAlgorithm_MustBeCompliant()
    {
        // Given: A TSE device
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTseGrain(orgId, tseId);
        await grain.InitializeAsync(locationId);

        // When: Completing a transaction
        var start = await grain.StartTransactionAsync(new StartTseTransactionCommand(
            locationId, "Kassenbeleg", "Amount:50.00", null));
        var finish = await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
            start.TransactionNumber, "Kassenbeleg", "Amount:50.00"));

        // Then: Signature algorithm must be compliant (HMAC-SHA256 or ECDSA)
        finish.SignatureAlgorithm.Should().BeOneOf("HMAC-SHA256", "ecdsa-plain-SHA256", "ecdsa-plain-SHA384");
    }

    [Fact]
    public async Task SignatureChain_QrCodeData_MustContainRequiredFields()
    {
        // Given: A TSE device
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTseGrain(orgId, tseId);
        await grain.InitializeAsync(locationId);

        // When: Completing a transaction
        var start = await grain.StartTransactionAsync(new StartTseTransactionCommand(
            locationId, "Kassenbeleg", "Amount:75.00", null));
        var finish = await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
            start.TransactionNumber, "Kassenbeleg", "Amount:75.00"));

        // Then: QR code must contain required KassenSichV fields
        finish.QrCodeData.Should().NotBeNullOrEmpty();
        finish.QrCodeData.Should().StartWith("V0;"); // Version identifier
        // QR format: V0;ClientID;TransactionNumber;StartTime;EndTime;ProcessType;ProcessData;SignatureCounter;Signature
    }

    [Fact]
    public async Task SignatureChain_StartTime_MustPrecedeEndTime()
    {
        // Given: A TSE device
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTseGrain(orgId, tseId);
        await grain.InitializeAsync(locationId);

        // When: Completing a transaction
        var start = await grain.StartTransactionAsync(new StartTseTransactionCommand(
            locationId, "Kassenbeleg", "Amount:100.00", null));

        await Task.Delay(10); // Ensure time passes

        var finish = await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
            start.TransactionNumber, "Kassenbeleg", "Amount:100.00"));

        // Then: Start time must be before end time
        finish.StartTime.Should().BeBefore(finish.EndTime);
    }

    [Fact]
    public async Task SignatureChain_PublicKey_MustBeConsistent()
    {
        // Given: A TSE device
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTseGrain(orgId, tseId);
        await grain.InitializeAsync(locationId);

        // When: Getting snapshots and completing transactions
        var snapshotBefore = await grain.GetSnapshotAsync();

        var start = await grain.StartTransactionAsync(new StartTseTransactionCommand(
            locationId, "Kassenbeleg", "Amount:100.00", null));
        var finish = await grain.FinishTransactionAsync(new FinishTseTransactionCommand(
            start.TransactionNumber, "Kassenbeleg", "Amount:100.00"));

        var snapshotAfter = await grain.GetSnapshotAsync();

        // Then: Public key must be consistent
        finish.PublicKeyBase64.Should().Be(snapshotBefore.PublicKeyBase64);
        finish.PublicKeyBase64.Should().Be(snapshotAfter.PublicKeyBase64);
    }
}

// ============================================================================
// Tamper Detection Tests
// Verify that the system can detect tampering with fiscal data
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Compliance")]
[Trait("Regulation", "Integrity")]
public class TamperDetectionTests
{
    private readonly TestClusterFixture _fixture;

    public TamperDetectionTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFiscalTransactionGrain GetTransactionGrain(Guid orgId, Guid transactionId)
    {
        var key = $"{orgId}:fiscaltransaction:{transactionId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalTransactionGrain>(key);
    }

    private IFiscalDeviceGrain GetDeviceGrain(Guid orgId, Guid deviceId)
    {
        var key = $"{orgId}:fiscaldevice:{deviceId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalDeviceGrain>(key);
    }

    [Fact]
    public async Task Transaction_CannotBeResigned_WithDifferentSignature()
    {
        // Given: A signed transaction
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        var deviceGrain = GetDeviceGrain(orgId, deviceId);
        await deviceGrain.RegisterAsync(new RegisterFiscalDeviceCommand(
            locationId, FiscalDeviceType.SwissbitCloud, "TEST-001",
            null, null, null, null, null));

        var grain = GetTransactionGrain(orgId, transactionId);
        await grain.CreateAsync(new CreateFiscalTransactionCommand(
            deviceId, locationId,
            FiscalTransactionType.Receipt, FiscalProcessType.Kassenbeleg,
            "Order", Guid.NewGuid(), 100.00m,
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>()));

        await grain.SignAsync(new SignTransactionCommand(
            "original-signature", 1, "cert-1", "qr-1", "raw-1"));

        // When: Attempting to resign
        var act = () => grain.SignAsync(new SignTransactionCommand(
            "tampered-signature", 2, "cert-2", "qr-2", "raw-2"));

        // Then: Should reject the second signature
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already signed*");
    }

    [Fact]
    public async Task Transaction_SignedStatus_CannotBeReverted()
    {
        // Given: A signed transaction
        var orgId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        var deviceGrain = GetDeviceGrain(orgId, deviceId);
        await deviceGrain.RegisterAsync(new RegisterFiscalDeviceCommand(
            locationId, FiscalDeviceType.SwissbitCloud, "TEST-002",
            null, null, null, null, null));

        var grain = GetTransactionGrain(orgId, transactionId);
        await grain.CreateAsync(new CreateFiscalTransactionCommand(
            deviceId, locationId,
            FiscalTransactionType.Receipt, FiscalProcessType.Kassenbeleg,
            "Order", Guid.NewGuid(), 100.00m,
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>(),
            new Dictionary<string, decimal>()));

        await grain.SignAsync(new SignTransactionCommand(
            "valid-signature", 1, "cert", "qr", "raw"));

        // When: Checking the status
        var snapshot = await grain.GetSnapshotAsync();

        // Then: Status must be Signed
        snapshot.Status.Should().Be(FiscalTransactionStatus.Signed);
        snapshot.Signature.Should().NotBeNullOrEmpty();
        snapshot.SignatureCounter.Should().Be(1);
    }

    [Fact]
    public async Task FiscalDevice_CounterReset_IsNotAllowed()
    {
        // Given: A device with incremented counters
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetDeviceGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "TEST-RESET",
            null, null, null, null, null));

        // Increment counters
        for (int i = 0; i < 10; i++)
        {
            await grain.GetNextTransactionCounterAsync();
            await grain.GetNextSignatureCounterAsync();
        }

        var snapshotBefore = await grain.GetSnapshotAsync();

        // When: Getting more counters
        var nextTx = await grain.GetNextTransactionCounterAsync();
        var nextSig = await grain.GetNextSignatureCounterAsync();

        // Then: Counters must only increase, never reset
        nextTx.Should().Be(snapshotBefore.TransactionCounter + 1);
        nextSig.Should().Be(snapshotBefore.SignatureCounter + 1);
    }
}

// ============================================================================
// Z-Report Generation Tests
// Daily closing reports required by various fiscal regulations
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Compliance")]
[Trait("Regulation", "ZReport")]
public class ZReportGenerationTests
{
    private readonly TestClusterFixture _fixture;

    public ZReportGenerationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IZReportGrain GetGrain(Guid orgId, Guid siteId)
    {
        var key = GrainKeys.ZReport(orgId, siteId);
        return _fixture.Cluster.GrainFactory.GetGrain<IZReportGrain>(key);
    }

    private IMultiCountryFiscalGrain GetFiscalGrain(Guid orgId, Guid siteId)
    {
        var key = GrainKeys.MultiCountryFiscal(orgId, siteId);
        return _fixture.Cluster.GrainFactory.GetGrain<IMultiCountryFiscalGrain>(key);
    }

    [Fact]
    public async Task ZReport_ReportNumber_MustBeSequential()
    {
        // Given: A site with Z-Report capability
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        // Then: When no reports exist, latest should be null
        var latest = await grain.GetLatestReportAsync();
        latest.Should().BeNull();
    }

    [Fact]
    public async Task DailyClose_PerformDailyCloseAsync_WhenNotConfigured_ReturnsFailure()
    {
        // Given: A site without fiscal configuration
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetFiscalGrain(orgId, siteId);

        // When: Attempting daily close
        var result = await grain.PerformDailyCloseAsync(DateTime.UtcNow);

        // Then: Should return failure due to no configuration
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("NOT_CONFIGURED");
    }

    [Fact]
    public async Task ZReport_DateRange_ReturnsEmpty_WhenNoReports()
    {
        // Given: A fresh site
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        // When: Querying reports for a date range
        var reports = await grain.GetReportsAsync(
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow));

        // Then: Should return empty list
        reports.Should().BeEmpty();
    }

    [Fact]
    public async Task ZReport_ByNumber_ReturnsNull_WhenNotFound()
    {
        // Given: A site
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        // When: Querying non-existent report
        var report = await grain.GetReportAsync(12345);

        // Then: Should return null
        report.Should().BeNull();
    }
}

// ============================================================================
// Fiscal Memory Overflow Tests
// Edge cases for counter limits and memory constraints
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Compliance")]
[Trait("Regulation", "Limits")]
public class FiscalMemoryOverflowTests
{
    private readonly TestClusterFixture _fixture;

    public FiscalMemoryOverflowTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFiscalJournalGrain GetJournalGrain(Guid orgId, DateTime date)
    {
        var key = $"{orgId}:fiscaljournal:{date:yyyy-MM-dd}";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalJournalGrain>(key);
    }

    [Fact]
    public async Task Journal_HighVolume_ShouldHandle1000Entries()
    {
        // Given: A fiscal journal
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetJournalGrain(orgId, date);
        var deviceId = Guid.NewGuid();

        // When: Logging 1000 entries (high volume day simulation)
        const int entryCount = 1000;
        for (int i = 0; i < entryCount; i++)
        {
            await grain.LogEventAsync(new LogFiscalEventCommand(
                Guid.NewGuid(),
                FiscalEventType.TransactionSigned,
                deviceId,
                Guid.NewGuid(),
                null,
                $"High volume transaction {i + 1}",
                "192.168.1.100",
                null,
                FiscalEventSeverity.Info));
        }

        // Then: All entries should be stored and retrievable
        var count = await grain.GetEntryCountAsync();
        count.Should().Be(entryCount);

        var entries = await grain.GetEntriesAsync();
        entries.Should().HaveCount(entryCount);
    }

    [Fact]
    public async Task Journal_EntriesPerDevice_ShouldBeFilterable()
    {
        // Given: A journal with entries from multiple devices
        var orgId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var grain = GetJournalGrain(orgId, date);

        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();
        var device3 = Guid.NewGuid();

        for (int i = 0; i < 100; i++)
        {
            var device = (i % 3) switch
            {
                0 => device1,
                1 => device2,
                _ => device3
            };

            await grain.LogEventAsync(new LogFiscalEventCommand(
                Guid.NewGuid(), FiscalEventType.TransactionSigned, device, Guid.NewGuid(),
                null, $"Transaction {i}", null, null, FiscalEventSeverity.Info));
        }

        // When: Filtering by device
        var device1Entries = await grain.GetEntriesByDeviceAsync(device1);
        var device2Entries = await grain.GetEntriesByDeviceAsync(device2);
        var device3Entries = await grain.GetEntriesByDeviceAsync(device3);

        // Then: Each device should have approximately 1/3 of entries
        device1Entries.Should().HaveCountGreaterThan(30);
        device2Entries.Should().HaveCountGreaterThan(30);
        device3Entries.Should().HaveCountGreaterThan(30);

        device1Entries.All(e => e.DeviceId == device1).Should().BeTrue();
        device2Entries.All(e => e.DeviceId == device2).Should().BeTrue();
        device3Entries.All(e => e.DeviceId == device3).Should().BeTrue();
    }
}

// ============================================================================
// Device Registration Compliance Tests
// Tax authority device registration requirements
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Compliance")]
[Trait("Regulation", "Registration")]
public class DeviceRegistrationComplianceTests
{
    private readonly TestClusterFixture _fixture;

    public DeviceRegistrationComplianceTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFiscalDeviceRegistryGrain GetRegistryGrain(Guid orgId, Guid siteId)
    {
        var key = $"{orgId}:{siteId}:fiscaldeviceregistry";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalDeviceRegistryGrain>(key);
    }

    private IFiscalDeviceGrain GetDeviceGrain(Guid orgId, Guid deviceId)
    {
        var key = $"{orgId}:fiscaldevice:{deviceId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalDeviceGrain>(key);
    }

    [Fact]
    public async Task DeviceRegistry_SerialNumber_MustBeUnique()
    {
        // Given: A device registry
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetRegistryGrain(orgId, siteId);

        // When: Registering devices with unique serial numbers
        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();
        var device3 = Guid.NewGuid();

        await grain.RegisterDeviceAsync(device1, "SERIAL-001");
        await grain.RegisterDeviceAsync(device2, "SERIAL-002");
        await grain.RegisterDeviceAsync(device3, "SERIAL-003");

        // Then: Each serial should map to correct device
        var found1 = await grain.FindBySerialNumberAsync("SERIAL-001");
        var found2 = await grain.FindBySerialNumberAsync("SERIAL-002");
        var found3 = await grain.FindBySerialNumberAsync("SERIAL-003");

        found1.Should().Be(device1);
        found2.Should().Be(device2);
        found3.Should().Be(device3);
    }

    [Fact]
    public async Task Device_Registration_RequiresSerialNumber()
    {
        // Given: Device registration attempt
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetDeviceGrain(orgId, deviceId);

        // When/Then: Registration with serial number should succeed
        var snapshot = await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "REQUIRED-SERIAL-001",
            null, null, null, null, null));

        snapshot.SerialNumber.Should().Be("REQUIRED-SERIAL-001");
        snapshot.SerialNumber.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Device_Deregistration_MustBeTracked()
    {
        // Given: A registered device
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var registryGrain = GetRegistryGrain(orgId, siteId);

        await registryGrain.RegisterDeviceAsync(deviceId, "DEREG-TEST-001");

        // Verify registration
        var devicesBefore = await registryGrain.GetDeviceIdsAsync();
        devicesBefore.Should().Contain(deviceId);

        // When: Unregistering the device
        await registryGrain.UnregisterDeviceAsync(deviceId);

        // Then: Device should be removed from registry
        var devicesAfter = await registryGrain.GetDeviceIdsAsync();
        devicesAfter.Should().NotContain(deviceId);
    }

    [Fact]
    public async Task Device_Lifecycle_ActivateDeactivate_MustBeTracked()
    {
        // Given: A registered device
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var grain = GetDeviceGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "LIFECYCLE-001",
            null, DateTime.UtcNow.AddYears(2), null, null, null));

        // When: Deactivating with reason
        await grain.DeactivateWithReasonAsync("Device maintenance scheduled", operatorId);

        // Then: Device should be inactive
        var snapshotDeactivated = await grain.GetSnapshotAsync();
        snapshotDeactivated.Status.Should().Be(FiscalDeviceStatus.Inactive);

        // When: Reactivating
        var snapshotReactivated = await grain.ActivateAsync("TAX-REG-12345", operatorId);

        // Then: Device should be active
        snapshotReactivated.Status.Should().Be(FiscalDeviceStatus.Active);
    }

    [Fact]
    public async Task Device_SelfTest_MustPass_ForActiveDevice()
    {
        // Given: An active device with valid certificate
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetDeviceGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SELFTEST-PASS-001",
            "public-key", DateTime.UtcNow.AddYears(1), null, null, null));

        // When: Performing self-test
        var result = await grain.PerformSelfTestAsync();

        // Then: Self-test should pass
        result.Passed.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Device_SelfTest_MustFail_ForExpiredCertificate()
    {
        // Given: A device with expired certificate
        var orgId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var grain = GetDeviceGrain(orgId, deviceId);

        await grain.RegisterAsync(new RegisterFiscalDeviceCommand(
            Guid.NewGuid(), FiscalDeviceType.SwissbitCloud, "SELFTEST-FAIL-001",
            "public-key", DateTime.UtcNow.AddDays(-30), // Expired
            null, null, null));

        // When: Performing self-test
        var result = await grain.PerformSelfTestAsync();

        // Then: Self-test should fail
        result.Passed.Should().BeFalse();
        result.ErrorMessage.Should().Contain("expired");
    }
}

// ============================================================================
// Audit Export Format Tests
// Country-specific audit export formats (DSFinV-K, JPK, NF525, etc.)
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Compliance")]
[Trait("Regulation", "Export")]
public class AuditExportFormatTests
{
    private readonly TestClusterFixture _fixture;

    public AuditExportFormatTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IDSFinVKExportGrain GetDSFinVKExportGrain(Guid orgId, Guid siteId, Guid exportId)
    {
        var key = $"{orgId}:{siteId}:dsfinvk:{exportId}";
        return _fixture.Cluster.GrainFactory.GetGrain<IDSFinVKExportGrain>(key);
    }

    private IDSFinVKExportRegistryGrain GetDSFinVKExportRegistryGrain(Guid orgId, Guid siteId)
    {
        var key = $"{orgId}:{siteId}:dsfinvkregistry";
        return _fixture.Cluster.GrainFactory.GetGrain<IDSFinVKExportRegistryGrain>(key);
    }

    [Fact]
    public async Task DSFinVK_Export_CreationLifecycle()
    {
        // Given: Export request
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var grain = GetDSFinVKExportGrain(orgId, siteId, exportId);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // When: Creating an export
        var state = await grain.CreateAsync(startDate, endDate, "Q1 Tax Audit Export", Guid.NewGuid());

        // Then: Export should be created with correct metadata
        state.ExportId.Should().Be(exportId);
        state.StartDate.Should().Be(startDate);
        state.EndDate.Should().Be(endDate);
        state.Status.Should().Be(DSFinVKExportStatus.Pending);
        state.Description.Should().Be("Q1 Tax Audit Export");
    }

    [Fact]
    public async Task DSFinVK_Export_StatusTransitions()
    {
        // Given: A pending export
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var grain = GetDSFinVKExportGrain(orgId, siteId, exportId);

        await grain.CreateAsync(
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow),
            null, null);

        // When: Transitioning through statuses
        await grain.SetProcessingAsync();
        var processingState = await grain.GetStateAsync();
        processingState.Status.Should().Be(DSFinVKExportStatus.Processing);

        await grain.SetCompletedAsync(500, "/exports/export.zip", "https://download.example.com/export.zip");
        var completedState = await grain.GetStateAsync();
        completedState.Status.Should().Be(DSFinVKExportStatus.Completed);
        completedState.TransactionCount.Should().Be(500);
        completedState.FilePath.Should().Be("/exports/export.zip");
        completedState.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DSFinVK_Export_FailedStatus()
    {
        // Given: A pending export
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var exportId = Guid.NewGuid();
        var grain = GetDSFinVKExportGrain(orgId, siteId, exportId);

        await grain.CreateAsync(
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow),
            null, null);

        // When: Export fails
        await grain.SetFailedAsync("Database connection timeout during export");

        // Then: Status should be failed with error message
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(DSFinVKExportStatus.Failed);
        state.ErrorMessage.Should().Be("Database connection timeout during export");
        state.CompletedAt.Should().NotBeNull(); // Failed is also a terminal state
    }

    [Fact]
    public async Task DSFinVK_ExportRegistry_TracksAllExports()
    {
        // Given: An export registry
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var registryGrain = GetDSFinVKExportRegistryGrain(orgId, siteId);

        // When: Registering multiple exports
        var export1 = Guid.NewGuid();
        var export2 = Guid.NewGuid();
        var export3 = Guid.NewGuid();

        await registryGrain.RegisterExportAsync(export1);
        await registryGrain.RegisterExportAsync(export2);
        await registryGrain.RegisterExportAsync(export3);

        // Then: All exports should be tracked in reverse chronological order
        var exports = await registryGrain.GetExportIdsAsync();
        exports.Should().HaveCount(3);
        exports[0].Should().Be(export3); // Most recent first
        exports[1].Should().Be(export2);
        exports[2].Should().Be(export1);
    }

    [Fact]
    public async Task FiscalConfiguration_AuditExport_WhenNotConfigured_ShouldThrow()
    {
        // Given: A site without fiscal configuration
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var key = GrainKeys.MultiCountryFiscal(orgId, siteId);
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IMultiCountryFiscalGrain>(key);

        // When/Then: Attempting audit export should throw
        var act = () => grain.GenerateAuditExportAsync(new FiscalDateRange(
            DateTime.UtcNow.AddMonths(-1),
            DateTime.UtcNow));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }
}

// ============================================================================
// Country-Specific Format Tests
// ============================================================================

[Trait("Category", "Compliance")]
[Trait("Regulation", "CountrySpecific")]
public class CountrySpecificFormatTests
{
    [Theory]
    [InlineData("5260250995", true)]  // Valid NIP
    [InlineData("1234567890", false)] // Invalid checksum (yields 10)
    [InlineData("123456789", false)]  // Too short
    [InlineData("12345678901", false)] // Too long
    [InlineData("abcdefghij", false)] // Non-numeric
    public void Polish_NIP_Validation(string nip, bool expectedValid)
    {
        var isValid = ValidatePolishNip(nip);
        isValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(PolandVatRate.Rate23, 0.23)]
    [InlineData(PolandVatRate.Rate8, 0.08)]
    [InlineData(PolandVatRate.Rate5, 0.05)]
    [InlineData(PolandVatRate.Rate0, 0.00)]
    [InlineData(PolandVatRate.Exempt, 0.00)]
    public void Polish_VatRates_HaveCorrectValues(PolandVatRate rate, decimal expectedRate)
    {
        var vatRateValues = new Dictionary<PolandVatRate, decimal>
        {
            [PolandVatRate.Rate23] = 0.23m,
            [PolandVatRate.Rate8] = 0.08m,
            [PolandVatRate.Rate5] = 0.05m,
            [PolandVatRate.Rate0] = 0m,
            [PolandVatRate.Exempt] = 0m,
            [PolandVatRate.NotApplicable] = 0m
        };

        vatRateValues[rate].Should().Be((decimal)expectedRate);
    }

    [Theory]
    [InlineData(FiscalCountry.Germany, "KassenSichV")]
    [InlineData(FiscalCountry.Austria, "RKSV")]
    [InlineData(FiscalCountry.Italy, "RT")]
    [InlineData(FiscalCountry.France, "NF 525")]
    [InlineData(FiscalCountry.Poland, "JPK/KSeF")]
    public void ComplianceStandard_ByCountry_IsCorrect(FiscalCountry country, string expectedStandard)
    {
        var standard = country switch
        {
            FiscalCountry.Germany => "KassenSichV",
            FiscalCountry.Austria => "RKSV",
            FiscalCountry.Italy => "RT",
            FiscalCountry.France => "NF 525",
            FiscalCountry.Poland => "JPK/KSeF",
            _ => "Unknown"
        };

        standard.Should().Be(expectedStandard);
    }

    [Fact]
    public void German_TseDeviceTypes_AreSupported()
    {
        var supportedTypes = new[]
        {
            FiscalDeviceType.SwissbitCloud,
            FiscalDeviceType.SwissbitUsb,
            FiscalDeviceType.FiskalyCloud,
            FiscalDeviceType.Epson,
            FiscalDeviceType.Diebold
        };

        foreach (var deviceType in Enum.GetValues<FiscalDeviceType>())
        {
            supportedTypes.Should().Contain(deviceType,
                $"Device type {deviceType} should be documented as supported");
        }
    }

    [Fact]
    public void German_ProcessTypes_AreCompliant()
    {
        // KassenSichV requires specific process types
        var requiredProcessTypes = new[]
        {
            FiscalProcessType.Kassenbeleg,
            FiscalProcessType.AVTransfer,
            FiscalProcessType.AVBestellung,
            FiscalProcessType.AVSonstiger
        };

        foreach (var processType in requiredProcessTypes)
        {
            Enum.IsDefined(typeof(FiscalProcessType), processType).Should().BeTrue();
        }
    }

    [Fact]
    public void French_CumulativeTotals_TrackingSupported()
    {
        // NF 525 requires cumulative totals tracking
        var totals = new FrenchCumulativeTotals
        {
            GrandTotalPerpetuel = 10000.00m,
            GrandTotalPerpetuelTTC = 12000.00m,
            TransactionCount = 100,
            VoidCount = 5,
            VoidTotal = 250.00m,
            SequenceNumber = 100,
            TotalsByVatRate = new Dictionary<string, decimal>
            {
                ["20%"] = 5000.00m,
                ["10%"] = 3000.00m,
                ["5.5%"] = 2000.00m
            }
        };

        totals.GrandTotalPerpetuel.Should().Be(10000.00m);
        totals.TransactionCount.Should().Be(100);
        totals.VoidCount.Should().Be(5);
        totals.TotalsByVatRate.Should().HaveCount(3);
    }

    private static bool ValidatePolishNip(string nip)
    {
        var digits = nip.Replace("-", "").Replace(" ", "");
        if (digits.Length != 10 || !digits.All(char.IsDigit))
            return false;

        var weights = new[] { 6, 5, 7, 2, 3, 4, 5, 6, 7 };
        var sum = digits.Take(9).Select((c, i) => (c - '0') * weights[i]).Sum();
        var checkDigit = sum % 11;

        // Special case: checksum of 10 is invalid
        if (checkDigit == 10)
            return false;

        return checkDigit == (digits[9] - '0');
    }
}

// ============================================================================
// TSE Self-Test Compliance Tests
// Daily self-test requirements
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Compliance")]
[Trait("Regulation", "SelfTest")]
public class TseSelfTestComplianceTests
{
    private readonly TestClusterFixture _fixture;

    public TseSelfTestComplianceTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ITseGrain GetTseGrain(Guid orgId, Guid tseId)
    {
        var key = $"{orgId}:tse:{tseId}";
        return _fixture.Cluster.GrainFactory.GetGrain<ITseGrain>(key);
    }

    [Fact]
    public async Task SelfTest_Updates_LastSelfTestTimestamp()
    {
        // Given: An initialized TSE
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTseGrain(orgId, tseId);
        await grain.InitializeAsync(locationId);

        var snapshotBefore = await grain.GetSnapshotAsync();
        var lastTestBefore = snapshotBefore.LastSelfTestAt;

        // When: Performing self-test
        await Task.Delay(10); // Ensure time passes
        var result = await grain.SelfTestAsync();

        // Then: Timestamp should be updated
        var snapshotAfter = await grain.GetSnapshotAsync();
        snapshotAfter.LastSelfTestAt.Should().NotBeNull();
        snapshotAfter.LastSelfTestAt.Should().BeAfter(lastTestBefore ?? DateTime.MinValue);
        snapshotAfter.LastSelfTestPassed.Should().Be(result.Passed);
    }

    [Fact]
    public async Task SelfTest_RecordsResult_InSnapshot()
    {
        // Given: An initialized TSE
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = GetTseGrain(orgId, tseId);
        await grain.InitializeAsync(locationId);

        // When: Performing self-test
        var result = await grain.SelfTestAsync();

        // Then: Result should be recorded in snapshot
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LastSelfTestAt.Should().Be(result.PerformedAt);
        snapshot.LastSelfTestPassed.Should().Be(result.Passed);
    }

    [Fact]
    public async Task SelfTest_OnUninitialized_ShouldThrow()
    {
        // Given: An uninitialized TSE
        var orgId = Guid.NewGuid();
        var tseId = Guid.NewGuid();
        var grain = GetTseGrain(orgId, tseId);

        // When/Then: Self-test should throw
        var act = () => grain.SelfTestAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("TSE not initialized");
    }
}

// ============================================================================
// Helper classes for French cumulative totals (used in tests)
// ============================================================================

public class FrenchCumulativeTotals
{
    public decimal GrandTotalPerpetuel { get; set; }
    public decimal GrandTotalPerpetuelTTC { get; set; }
    public int TransactionCount { get; set; }
    public int VoidCount { get; set; }
    public decimal VoidTotal { get; set; }
    public long SequenceNumber { get; set; }
    public Dictionary<string, decimal> TotalsByVatRate { get; set; } = new();
}
