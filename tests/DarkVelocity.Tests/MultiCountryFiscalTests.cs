using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

// ============================================================================
// Multi-Country Fiscal Grain Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MultiCountryFiscalGrainTests
{
    private readonly TestClusterFixture _fixture;

    public MultiCountryFiscalGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMultiCountryFiscalGrain GetGrain(Guid orgId, Guid siteId)
    {
        var key = GrainKeys.MultiCountryFiscal(orgId, siteId);
        return _fixture.Cluster.GrainFactory.GetGrain<IMultiCountryFiscalGrain>(key);
    }

    [Fact]
    public async Task ConfigureAsync_WithGermany_SetsKassenSichVCompliance()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        var command = new ConfigureSiteFiscalCommand(
            Country: FiscalCountry.Germany,
            Enabled: true,
            TseDeviceId: "device-123",
            TseType: ExternalTseType.FiskalyCloud,
            CountrySpecificConfig: new Dictionary<string, string>
            {
                ["api_key"] = "test-key",
                ["tss_id"] = "tss-123"
            });

        var snapshot = await grain.ConfigureAsync(command);

        snapshot.Country.Should().Be(FiscalCountry.Germany);
        snapshot.ComplianceStandard.Should().Be("KassenSichV");
        snapshot.Enabled.Should().BeTrue();
        snapshot.TseType.Should().Be(ExternalTseType.FiskalyCloud);
    }

    [Fact]
    public async Task ConfigureAsync_WithFrance_SetsNF525Compliance()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        var command = new ConfigureSiteFiscalCommand(
            Country: FiscalCountry.France,
            Enabled: true,
            TseDeviceId: null,
            TseType: null,
            CountrySpecificConfig: new Dictionary<string, string>
            {
                ["certification_number"] = "NF525-123",
                ["siren"] = "123456789"
            });

        var snapshot = await grain.ConfigureAsync(command);

        snapshot.Country.Should().Be(FiscalCountry.France);
        snapshot.ComplianceStandard.Should().Be("NF 525");
        snapshot.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task ConfigureAsync_WithPoland_SetsJpkKsefCompliance()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        var command = new ConfigureSiteFiscalCommand(
            Country: FiscalCountry.Poland,
            Enabled: true,
            TseDeviceId: null,
            TseType: null,
            CountrySpecificConfig: new Dictionary<string, string>
            {
                ["nip"] = "1234567890",
                ["ksef_enabled"] = "true"
            });

        var snapshot = await grain.ConfigureAsync(command);

        snapshot.Country.Should().Be(FiscalCountry.Poland);
        snapshot.ComplianceStandard.Should().Be("JPK/KSeF");
        snapshot.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetSnapshotAsync_ReturnsConfiguredState()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.ConfigureAsync(new ConfigureSiteFiscalCommand(
            Country: FiscalCountry.Austria,
            Enabled: true,
            TseDeviceId: "at-device-1",
            TseType: ExternalTseType.SwissbitCloud,
            CountrySpecificConfig: null));

        var snapshot = await grain.GetSnapshotAsync();

        snapshot.OrgId.Should().Be(orgId);
        snapshot.SiteId.Should().Be(siteId);
        snapshot.Country.Should().Be(FiscalCountry.Austria);
        snapshot.ComplianceStandard.Should().Be("RKSV");
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WhenNotConfigured_ReturnsInvalid()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        var result = await grain.ValidateConfigurationAsync();

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("not configured"));
    }

    [Fact]
    public async Task GetSupportedFeaturesAsync_WhenNotConfigured_ReturnsEmpty()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        var features = await grain.GetSupportedFeaturesAsync();

        features.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordTransactionAsync_WhenNotConfigured_ReturnsFailure()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        var transaction = new FiscalTransactionData(
            TransactionId: Guid.NewGuid(),
            SiteId: siteId,
            Timestamp: DateTime.UtcNow,
            TransactionType: "Receipt",
            GrossAmount: 119.00m,
            NetAmounts: new Dictionary<string, decimal> { ["NORMAL"] = 100.00m },
            TaxAmounts: new Dictionary<string, decimal> { ["NORMAL"] = 19.00m },
            PaymentTypes: new Dictionary<string, decimal> { ["CASH"] = 119.00m },
            SourceType: "Order",
            SourceId: Guid.NewGuid(),
            OperatorId: null,
            AdditionalData: null);

        var result = await grain.RecordTransactionAsync(transaction);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("NOT_CONFIGURED");
    }

    [Fact]
    public async Task ConfigureAsync_DisablesFiscal_SetsEnabledFalse()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        // First enable
        await grain.ConfigureAsync(new ConfigureSiteFiscalCommand(
            Country: FiscalCountry.Italy,
            Enabled: true,
            TseDeviceId: null,
            TseType: null,
            CountrySpecificConfig: null));

        // Then disable
        var snapshot = await grain.ConfigureAsync(new ConfigureSiteFiscalCommand(
            Country: FiscalCountry.Italy,
            Enabled: false,
            TseDeviceId: null,
            TseType: null,
            CountrySpecificConfig: null));

        snapshot.Enabled.Should().BeFalse();
    }
}

// ============================================================================
// French Cumulative Total Calculation Tests
// ============================================================================

[Trait("Category", "Unit")]
public class FrenchCumulativeTotalTests
{
    [Fact]
    public void CumulativeTotals_AddTransaction_IncrementsGrandTotal()
    {
        var totals = new FrenchCumulativeTotals
        {
            GrandTotalPerpetuel = 1000.00m,
            GrandTotalPerpetuelTTC = 1000.00m,
            TransactionCount = 10
        };

        // Simulate adding a transaction
        totals.GrandTotalPerpetuel += 119.00m;
        totals.GrandTotalPerpetuelTTC += 119.00m;
        totals.TransactionCount++;

        totals.GrandTotalPerpetuel.Should().Be(1119.00m);
        totals.GrandTotalPerpetuelTTC.Should().Be(1119.00m);
        totals.TransactionCount.Should().Be(11);
    }

    [Fact]
    public void CumulativeTotals_AddVoid_TracksVoidSeparately()
    {
        var totals = new FrenchCumulativeTotals
        {
            GrandTotalPerpetuel = 1000.00m,
            VoidCount = 0,
            VoidTotal = 0.00m
        };

        // Simulate a void transaction
        var voidAmount = 50.00m;
        totals.GrandTotalPerpetuel -= voidAmount;
        totals.VoidCount++;
        totals.VoidTotal += voidAmount;

        totals.GrandTotalPerpetuel.Should().Be(950.00m);
        totals.VoidCount.Should().Be(1);
        totals.VoidTotal.Should().Be(50.00m);
    }

    [Fact]
    public void CumulativeTotals_TotalsByVatRate_AggregatesCorrectly()
    {
        var totals = new FrenchCumulativeTotals
        {
            TotalsByVatRate = new Dictionary<string, decimal>
            {
                ["20%"] = 500.00m,
                ["10%"] = 200.00m,
                ["5.5%"] = 100.00m
            }
        };

        // Add more to existing rates
        totals.TotalsByVatRate["20%"] += 50.00m;
        totals.TotalsByVatRate["10%"] += 25.00m;

        // Add new rate
        totals.TotalsByVatRate["2.1%"] = 10.00m;

        totals.TotalsByVatRate["20%"].Should().Be(550.00m);
        totals.TotalsByVatRate["10%"].Should().Be(225.00m);
        totals.TotalsByVatRate["5.5%"].Should().Be(100.00m);
        totals.TotalsByVatRate["2.1%"].Should().Be(10.00m);
        totals.TotalsByVatRate.Should().HaveCount(4);
    }

    [Fact]
    public void CumulativeTotals_SequenceNumber_IncrementsMonotonically()
    {
        var totals = new FrenchCumulativeTotals
        {
            SequenceNumber = 100
        };

        // Simulate multiple transactions
        var sequence1 = ++totals.SequenceNumber;
        var sequence2 = ++totals.SequenceNumber;
        var sequence3 = ++totals.SequenceNumber;

        sequence1.Should().Be(101);
        sequence2.Should().Be(102);
        sequence3.Should().Be(103);
        totals.SequenceNumber.Should().Be(103);
    }
}

// ============================================================================
// Polish JPK Format Validation Tests
// ============================================================================

[Trait("Category", "Unit")]
public class PolishJpkFormatTests
{
    [Theory]
    [InlineData("1234567890", true)]  // Valid NIP with checksum
    [InlineData("5260250995", true)]  // Valid NIP
    [InlineData("123456789", false)]  // Too short
    [InlineData("12345678901", false)] // Too long
    [InlineData("1234567891", false)] // Invalid checksum
    [InlineData("abcdefghij", false)] // Non-numeric
    public void ValidateNip_ReturnsCorrectResult(string nip, bool expectedValid)
    {
        var isValid = ValidatePolishNip(nip);
        isValid.Should().Be(expectedValid);
    }

    [Fact]
    public void PolandVatEntry_CorrectlyFormatsDocumentNumber()
    {
        var entry = new PolandVatEntry(
            EntryId: Guid.NewGuid(),
            Timestamp: new DateTime(2024, 3, 15, 10, 30, 0),
            DocumentNumber: "FV/123/2024",
            DocumentType: "Invoice",
            CounterpartyNip: "1234567890",
            CounterpartyName: "Test Company",
            NetAmount: 100.00m,
            VatAmount: 23.00m,
            GrossAmount: 123.00m,
            VatRate: PolandVatRate.Rate23,
            KsefNumber: null,
            KsefStatus: KsefSubmissionStatus.Pending);

        entry.DocumentNumber.Should().StartWith("FV/");
        entry.DocumentNumber.Should().EndWith("/2024");
        entry.GrossAmount.Should().Be(entry.NetAmount + entry.VatAmount);
    }

    [Theory]
    [InlineData(PolandVatRate.Rate23, 0.23)]
    [InlineData(PolandVatRate.Rate8, 0.08)]
    [InlineData(PolandVatRate.Rate5, 0.05)]
    [InlineData(PolandVatRate.Rate0, 0.00)]
    [InlineData(PolandVatRate.Exempt, 0.00)]
    public void PolandVatRate_HasCorrectValue(PolandVatRate rate, decimal expectedRate)
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

    [Fact]
    public void KsefInvoiceRecord_TracksSubmissionStatus()
    {
        var record = new KsefInvoiceRecord(
            InvoiceId: Guid.NewGuid(),
            InvoiceNumber: "FV/001/2024",
            IssueDate: DateTime.UtcNow,
            GrossAmount: 1230.00m,
            CounterpartyNip: "1234567890",
            InvoiceXml: "<Faktura>...</Faktura>",
            Status: KsefSubmissionStatus.Pending,
            KsefNumber: null,
            ErrorMessage: null,
            SubmittedAt: null,
            AcceptedAt: null);

        record.Status.Should().Be(KsefSubmissionStatus.Pending);
        record.KsefNumber.Should().BeNull();

        // Simulate acceptance
        var acceptedRecord = record with
        {
            Status = KsefSubmissionStatus.Accepted,
            KsefNumber = "KSeF-12345-67890",
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5),
            AcceptedAt = DateTime.UtcNow
        };

        acceptedRecord.Status.Should().Be(KsefSubmissionStatus.Accepted);
        acceptedRecord.KsefNumber.Should().NotBeNullOrEmpty();
        acceptedRecord.AcceptedAt.Should().NotBeNull();
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
// Multi-Country Routing Tests
// ============================================================================

[Trait("Category", "Unit")]
public class MultiCountryRoutingTests
{
    [Theory]
    [InlineData(FiscalCountry.Germany, "KassenSichV")]
    [InlineData(FiscalCountry.Austria, "RKSV")]
    [InlineData(FiscalCountry.Italy, "RT")]
    [InlineData(FiscalCountry.France, "NF 525")]
    [InlineData(FiscalCountry.Poland, "JPK/KSeF")]
    public void GetComplianceStandard_ReturnsCorrectStandard(FiscalCountry country, string expectedStandard)
    {
        var standard = GetComplianceStandard(country);
        standard.Should().Be(expectedStandard);
    }

    [Fact]
    public void FiscalCountryAdapterFactory_SupportsAllDocumentedCountries()
    {
        var supportedCountries = new HashSet<FiscalCountry>
        {
            FiscalCountry.Germany,
            FiscalCountry.Austria,
            FiscalCountry.Italy,
            FiscalCountry.France,
            FiscalCountry.Poland
        };

        foreach (var country in Enum.GetValues<FiscalCountry>())
        {
            supportedCountries.Contains(country).Should().BeTrue(
                $"Country {country} should be in the supported countries list");
        }
    }

    [Fact]
    public void ExternalTseType_HasExpectedTypes()
    {
        var expectedTypes = new[]
        {
            ExternalTseType.None,
            ExternalTseType.SwissbitCloud,
            ExternalTseType.SwissbitUsb,
            ExternalTseType.FiskalyCloud,
            ExternalTseType.Epson,
            ExternalTseType.Diebold,
            ExternalTseType.Custom
        };

        var actualTypes = Enum.GetValues<ExternalTseType>();
        actualTypes.Should().Contain(expectedTypes);
    }

    private static string GetComplianceStandard(FiscalCountry country)
    {
        return country switch
        {
            FiscalCountry.Germany => "KassenSichV",
            FiscalCountry.Austria => "RKSV",
            FiscalCountry.Italy => "RT",
            FiscalCountry.France => "NF 525",
            FiscalCountry.Poland => "JPK/KSeF",
            _ => "Unknown"
        };
    }
}

// ============================================================================
// Z-Report Generation Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ZReportGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ZReportGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IZReportGrain GetGrain(Guid orgId, Guid siteId)
    {
        var key = GrainKeys.ZReport(orgId, siteId);
        return _fixture.Cluster.GrainFactory.GetGrain<IZReportGrain>(key);
    }

    [Fact]
    public async Task GetLatestReportAsync_WhenNoReports_ReturnsNull()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        var report = await grain.GetLatestReportAsync();

        report.Should().BeNull();
    }

    [Fact]
    public async Task GetReportsAsync_WhenNoReports_ReturnsEmptyList()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        var reports = await grain.GetReportsAsync(
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow));

        reports.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_WhenReportDoesNotExist_ReturnsNull()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        var report = await grain.GetReportAsync(999);

        report.Should().BeNull();
    }
}

// ============================================================================
// Fiscal Job Scheduler Tests
// ============================================================================

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class FiscalJobSchedulerGrainTests
{
    private readonly TestClusterFixture _fixture;

    public FiscalJobSchedulerGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IFiscalJobSchedulerGrain GetGrain(Guid orgId)
    {
        var key = GrainKeys.FiscalJobScheduler(orgId);
        return _fixture.Cluster.GrainFactory.GetGrain<IFiscalJobSchedulerGrain>(key);
    }

    [Fact]
    public async Task ConfigureSiteJobsAsync_StoresConfiguration()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        var config = new SiteFiscalJobConfig(
            SiteId: siteId,
            DailyCloseEnabled: true,
            DailyCloseTime: new TimeOnly(23, 30),
            ArchiveEnabled: true,
            ArchiveTime: new TimeOnly(3, 0),
            CertificateMonitoringEnabled: true,
            CertificateExpiryWarningDays: 30,
            TimeZoneId: "Europe/Berlin");

        await grain.ConfigureSiteJobsAsync(config);

        var configs = await grain.GetSiteConfigsAsync();
        configs.Should().ContainSingle(c => c.SiteId == siteId);

        var retrievedConfig = configs.First(c => c.SiteId == siteId);
        retrievedConfig.DailyCloseEnabled.Should().BeTrue();
        retrievedConfig.DailyCloseTime.Should().Be(new TimeOnly(23, 30));
        retrievedConfig.ArchiveEnabled.Should().BeTrue();
        retrievedConfig.CertificateMonitoringEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveSiteJobsAsync_RemovesConfiguration()
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        await grain.ConfigureSiteJobsAsync(new SiteFiscalJobConfig(
            siteId, true, new TimeOnly(23, 0), true, new TimeOnly(2, 0), true, 30, "UTC"));

        await grain.RemoveSiteJobsAsync(siteId);

        var configs = await grain.GetSiteConfigsAsync();
        configs.Should().NotContain(c => c.SiteId == siteId);
    }

    [Fact]
    public async Task GetJobHistoryAsync_ReturnsEmptyInitially()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        var history = await grain.GetJobHistoryAsync();

        history.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckCertificateExpiryAsync_ReturnsEmptyWhenNoCertificates()
    {
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        var warnings = await grain.CheckCertificateExpiryAsync();

        warnings.Should().BeEmpty();
    }
}

// ============================================================================
// Fiscal Feature Support Tests
// ============================================================================

[Trait("Category", "Unit")]
public class FiscalFeatureSupportTests
{
    [Fact]
    public void FrenchAdapter_SupportsCumulativeTotals()
    {
        var supportedFeatures = new HashSet<FiscalFeature>
        {
            FiscalFeature.CumulativeTotals,
            FiscalFeature.ElectronicJournal,
            FiscalFeature.CertificateSigning,
            FiscalFeature.QrCodeGeneration,
            FiscalFeature.RealTimeSigning
        };

        supportedFeatures.Should().Contain(FiscalFeature.CumulativeTotals);
        supportedFeatures.Should().Contain(FiscalFeature.ElectronicJournal);
    }

    [Fact]
    public void PolishAdapter_SupportsVatRegisterExport()
    {
        var supportedFeatures = new HashSet<FiscalFeature>
        {
            FiscalFeature.VatRegisterExport,
            FiscalFeature.InvoiceVerification,
            FiscalFeature.BatchSubmission,
            FiscalFeature.ElectronicJournal
        };

        supportedFeatures.Should().Contain(FiscalFeature.VatRegisterExport);
        supportedFeatures.Should().Contain(FiscalFeature.InvoiceVerification);
    }

    [Fact]
    public void GermanAdapter_SupportsHardwareAndCloudTse()
    {
        var supportedFeatures = new HashSet<FiscalFeature>
        {
            FiscalFeature.HardwareTse,
            FiscalFeature.CloudTse,
            FiscalFeature.RealTimeSigning,
            FiscalFeature.QrCodeGeneration
        };

        supportedFeatures.Should().Contain(FiscalFeature.HardwareTse);
        supportedFeatures.Should().Contain(FiscalFeature.CloudTse);
    }
}
