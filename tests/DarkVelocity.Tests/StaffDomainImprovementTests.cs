using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.Services;
using DarkVelocity.Host.State;
using FluentAssertions;
using Orleans.TestingHost;
using Xunit;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class StaffDomainImprovementTests
{
    private readonly TestCluster _cluster;

    public StaffDomainImprovementTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    // ============================================================================
    // Labor Law Compliance Grain Tests
    // ============================================================================

    [Fact]
    public async Task LaborLawComplianceGrain_InitializeDefaults_ConfiguresCommonJurisdictions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        // Act
        await grain.InitializeDefaultsAsync();
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Jurisdictions.Should().NotBeEmpty();
        snapshot.Jurisdictions.Should().Contain(j => j.JurisdictionCode == "US-FEDERAL");
        snapshot.Jurisdictions.Should().Contain(j => j.JurisdictionCode == "US-CA");
        snapshot.Jurisdictions.Should().Contain(j => j.JurisdictionCode == "US-NY");
        snapshot.DefaultJurisdictionCode.Should().Be("US-FEDERAL");
    }

    [Fact]
    public async Task LaborLawComplianceGrain_GetCaliforniaConfig_HasCorrectOvertimeRules()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        await grain.InitializeDefaultsAsync();

        // Act
        var config = await grain.GetJurisdictionConfigAsync("US-CA");

        // Assert
        config.Should().NotBeNull();
        config!.OvertimeRule.DailyThresholdHours.Should().Be(8m);
        config.OvertimeRule.DailyDoubleThresholdHours.Should().Be(12m);
        config.OvertimeRule.WeeklyThresholdHours.Should().Be(40m);
        config.OvertimeRule.SeventhConsecutiveDayRule.Should().BeTrue();
        config.OvertimeRule.OvertimeMultiplier.Should().Be(1.5m);
        config.OvertimeRule.DoubleOvertimeMultiplier.Should().Be(2.0m);
    }

    [Fact]
    public async Task LaborLawComplianceGrain_CalculateOvertime_CaliforniaDaily_Over8Hours()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        await grain.InitializeDefaultsAsync();

        var employeeId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var periodEnd = periodStart.AddDays(6);

        // 10 hour day - should be 8 regular + 2 OT
        var timeEntries = new List<TimeEntryForCalculation>
        {
            new(Guid.NewGuid(), employeeId, periodStart, 10m, 30)
        };

        // Act
        var result = await grain.CalculateOvertimeAsync(
            employeeId, "US-CA", periodStart, periodEnd, timeEntries);

        // Assert
        result.TotalHours.Should().Be(10m);
        result.RegularHours.Should().Be(8m);
        result.OvertimeHours.Should().Be(2m);
        result.DoubleOvertimeHours.Should().Be(0m);
    }

    [Fact]
    public async Task LaborLawComplianceGrain_CalculateOvertime_CaliforniaDaily_Over12Hours()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        await grain.InitializeDefaultsAsync();

        var employeeId = Guid.NewGuid();
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        var periodEnd = periodStart.AddDays(6);

        // 14 hour day - should be 8 regular + 4 OT (8-12) + 2 double OT (>12)
        var timeEntries = new List<TimeEntryForCalculation>
        {
            new(Guid.NewGuid(), employeeId, periodStart, 14m, 30)
        };

        // Act
        var result = await grain.CalculateOvertimeAsync(
            employeeId, "US-CA", periodStart, periodEnd, timeEntries);

        // Assert
        result.TotalHours.Should().Be(14m);
        result.RegularHours.Should().Be(8m);
        result.OvertimeHours.Should().Be(4m);
        result.DoubleOvertimeHours.Should().Be(2m);
    }

    [Fact]
    public async Task LaborLawComplianceGrain_CalculateOvertime_FederalWeekly_Over40Hours()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        await grain.InitializeDefaultsAsync();

        var employeeId = Guid.NewGuid();
        // Start on a Sunday
        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-(int)DateTime.UtcNow.DayOfWeek));
        var periodEnd = periodStart.AddDays(6);

        // 5 days of 10 hours each = 50 hours total (40 regular + 10 OT)
        var timeEntries = new List<TimeEntryForCalculation>();
        for (int i = 0; i < 5; i++)
        {
            timeEntries.Add(new TimeEntryForCalculation(
                Guid.NewGuid(), employeeId, periodStart.AddDays(i), 10m, 30));
        }

        // Act
        var result = await grain.CalculateOvertimeAsync(
            employeeId, "US-FEDERAL", periodStart, periodEnd, timeEntries);

        // Assert
        result.TotalHours.Should().Be(50m);
        // Federal has no daily OT, only weekly at 40 hours
        result.RegularHours.Should().Be(40m);
        result.OvertimeHours.Should().Be(10m);
    }

    [Fact]
    public async Task LaborLawComplianceGrain_CheckBreakCompliance_CaliforniaMealBreak()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        await grain.InitializeDefaultsAsync();

        var employeeId = Guid.NewGuid();
        var timeEntry = new TimeEntryForCalculation(
            Guid.NewGuid(), employeeId, DateOnly.FromDateTime(DateTime.UtcNow), 8m, 30);

        // No meal break provided
        var breaks = new List<BreakRecord>();

        // Act
        var result = await grain.CheckBreakComplianceAsync("US-CA", timeEntry, breaks);

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.Violations.Should().NotBeEmpty();
        result.Violations.Should().Contain(v => v.ViolationType == "INSUFFICIENT_BREAK");
    }

    [Fact]
    public async Task LaborLawComplianceGrain_CheckBreakCompliance_WithValidBreak_IsCompliant()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<ILaborLawComplianceGrain>(
            $"org:{orgId}:laborlaw");

        await grain.InitializeDefaultsAsync();

        var employeeId = Guid.NewGuid();
        var timeEntry = new TimeEntryForCalculation(
            Guid.NewGuid(), employeeId, DateOnly.FromDateTime(DateTime.UtcNow), 6m, 30);

        // 30 minute meal break at appropriate time
        var breaks = new List<BreakRecord>
        {
            new(TimeSpan.FromHours(12), TimeSpan.FromHours(12.5), false, "meal")
        };

        // Act
        var result = await grain.CheckBreakComplianceAsync("US-CA", timeEntry, breaks);

        // Assert
        result.IsCompliant.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    // ============================================================================
    // Break Tracking Tests
    // ============================================================================

    [Fact]
    public async Task EmployeeGrain_StartBreak_StartsBreakSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-100", "Test", "Employee", "test@example.com"));
        await grain.ClockInAsync(new ClockInCommand(siteId));

        // Act
        var result = await grain.StartBreakAsync(new StartBreakCommand("meal", false));

        // Assert
        result.BreakId.Should().NotBeEmpty();
        result.BreakType.Should().Be("meal");
        result.IsPaid.Should().BeFalse();
        (await grain.IsOnBreakAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task EmployeeGrain_EndBreak_EndsBreakAndCalculatesDuration()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-101", "Test", "Employee", "test@example.com"));
        await grain.ClockInAsync(new ClockInCommand(siteId));
        await grain.StartBreakAsync(new StartBreakCommand("meal", false));

        await Task.Delay(100); // Small delay

        // Act
        var result = await grain.EndBreakAsync();

        // Assert
        result.BreakId.Should().NotBeEmpty();
        result.DurationMinutes.Should().BeGreaterThanOrEqualTo(0);
        (await grain.IsOnBreakAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task EmployeeGrain_GetBreakSummary_ReturnsCorrectTotals()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-102", "Test", "Employee", "test@example.com"));
        await grain.ClockInAsync(new ClockInCommand(siteId));

        // Take a paid break
        await grain.StartBreakAsync(new StartBreakCommand("rest", true));
        await grain.EndBreakAsync();

        // Take an unpaid break
        await grain.StartBreakAsync(new StartBreakCommand("meal", false));
        await grain.EndBreakAsync();

        // Act
        var summary = await grain.GetBreakSummaryAsync();

        // Assert
        summary.BreakCount.Should().Be(2);
        summary.TotalPaidBreakMinutes.Should().BeGreaterThanOrEqualTo(0);
        summary.TotalUnpaidBreakMinutes.Should().BeGreaterThanOrEqualTo(0);
        summary.IsCurrentlyOnBreak.Should().BeFalse();
    }

    [Fact]
    public async Task EmployeeGrain_StartBreak_ThrowsIfNotClockedIn()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-103", "Test", "Employee", "test@example.com"));

        // Act & Assert
        var act = () => grain.StartBreakAsync(new StartBreakCommand("meal", false));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Employee is not clocked in");
    }

    [Fact]
    public async Task EmployeeGrain_StartBreak_ThrowsIfAlreadyOnBreak()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-104", "Test", "Employee", "test@example.com"));
        await grain.ClockInAsync(new ClockInCommand(siteId));
        await grain.StartBreakAsync(new StartBreakCommand("meal", false));

        // Act & Assert
        var act = () => grain.StartBreakAsync(new StartBreakCommand("rest", true));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Employee is already on break");
    }

    // ============================================================================
    // Certification Tracking Tests
    // ============================================================================

    [Fact]
    public async Task EmployeeGrain_AddCertification_AddsCertificationSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-200", "Test", "Employee", "test@example.com"));

        var issuedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
        var expirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));

        // Act
        var result = await grain.AddCertificationAsync(new AddCertificationCommand(
            CertificationType: "food_handler",
            CertificationName: "ServSafe Food Handler",
            IssuedDate: issuedDate,
            ExpirationDate: expirationDate,
            CertificationNumber: "FSH-123456",
            IssuingAuthority: "National Restaurant Association"));

        // Assert
        result.Id.Should().NotBeEmpty();
        result.CertificationType.Should().Be("food_handler");
        result.CertificationName.Should().Be("ServSafe Food Handler");
        result.CertificationNumber.Should().Be("FSH-123456");
        result.Status.Should().Be("Valid");
        result.DaysUntilExpiration.Should().BeGreaterThan(300);
    }

    [Fact]
    public async Task EmployeeGrain_AddCertification_MarksAsExpired_WhenPastExpirationDate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-201", "Test", "Employee", "test@example.com"));

        var issuedDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2));
        var expirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        // Act
        var result = await grain.AddCertificationAsync(new AddCertificationCommand(
            CertificationType: "alcohol_service",
            CertificationName: "TIPS Certification",
            IssuedDate: issuedDate,
            ExpirationDate: expirationDate));

        // Assert
        result.Status.Should().Be("Expired");
        result.DaysUntilExpiration.Should().BeLessThan(0);
    }

    [Fact]
    public async Task EmployeeGrain_GetCertifications_ReturnsAllCertifications()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-202", "Test", "Employee", "test@example.com"));

        var expirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));

        await grain.AddCertificationAsync(new AddCertificationCommand(
            "food_handler", "Food Handler", DateOnly.FromDateTime(DateTime.UtcNow), expirationDate));
        await grain.AddCertificationAsync(new AddCertificationCommand(
            "alcohol_service", "TIPS", DateOnly.FromDateTime(DateTime.UtcNow), expirationDate));
        await grain.AddCertificationAsync(new AddCertificationCommand(
            "servsafe", "ServSafe Manager", DateOnly.FromDateTime(DateTime.UtcNow), expirationDate));

        // Act
        var certifications = await grain.GetCertificationsAsync();

        // Assert
        certifications.Should().HaveCount(3);
        certifications.Select(c => c.CertificationType).Should()
            .Contain(new[] { "food_handler", "alcohol_service", "servsafe" });
    }

    [Fact]
    public async Task EmployeeGrain_CheckCertificationCompliance_ReturnsCompliant_WhenAllCertsValid()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-203", "Test", "Employee", "test@example.com"));

        var expirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));

        await grain.AddCertificationAsync(new AddCertificationCommand(
            "food_handler", "Food Handler", DateOnly.FromDateTime(DateTime.UtcNow), expirationDate));
        await grain.AddCertificationAsync(new AddCertificationCommand(
            "alcohol_service", "TIPS", DateOnly.FromDateTime(DateTime.UtcNow), expirationDate));

        // Act
        var result = await grain.CheckCertificationComplianceAsync(
            new List<string> { "food_handler", "alcohol_service" });

        // Assert
        result.IsCompliant.Should().BeTrue();
        result.MissingCertifications.Should().BeEmpty();
        result.ExpiredCertifications.Should().BeEmpty();
    }

    [Fact]
    public async Task EmployeeGrain_CheckCertificationCompliance_ReturnsMissing_WhenCertsMissing()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-204", "Test", "Employee", "test@example.com"));

        var expirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));

        await grain.AddCertificationAsync(new AddCertificationCommand(
            "food_handler", "Food Handler", DateOnly.FromDateTime(DateTime.UtcNow), expirationDate));

        // Act - require both food_handler and alcohol_service
        var result = await grain.CheckCertificationComplianceAsync(
            new List<string> { "food_handler", "alcohol_service" });

        // Assert
        result.IsCompliant.Should().BeFalse();
        result.MissingCertifications.Should().Contain("alcohol_service");
    }

    [Fact]
    public async Task EmployeeGrain_UpdateCertification_UpdatesExpirationDate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-205", "Test", "Employee", "test@example.com"));

        var originalExpiration = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var cert = await grain.AddCertificationAsync(new AddCertificationCommand(
            "food_handler", "Food Handler", DateOnly.FromDateTime(DateTime.UtcNow), originalExpiration));

        var newExpiration = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2));

        // Act
        var updated = await grain.UpdateCertificationAsync(new UpdateCertificationCommand(
            cert.Id, NewExpirationDate: newExpiration));

        // Assert
        updated.ExpirationDate.Should().Be(newExpiration);
        updated.Status.Should().Be("Valid");
        updated.DaysUntilExpiration.Should().BeGreaterThan(700);
    }

    [Fact]
    public async Task EmployeeGrain_RemoveCertification_RemovesCertification()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-206", "Test", "Employee", "test@example.com"));

        var expirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1));
        var cert = await grain.AddCertificationAsync(new AddCertificationCommand(
            "food_handler", "Food Handler", DateOnly.FromDateTime(DateTime.UtcNow), expirationDate));

        // Act
        await grain.RemoveCertificationAsync(cert.Id, "Certification revoked");

        // Assert
        var certifications = await grain.GetCertificationsAsync();
        certifications.Should().BeEmpty();
    }

    [Fact]
    public async Task EmployeeGrain_CheckCertificationExpirations_AlertsForExpiringSoon()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var employeeId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IEmployeeGrain>(
            GrainKeys.Employee(orgId, employeeId));

        await grain.CreateAsync(new CreateEmployeeCommand(
            orgId, Guid.NewGuid(), siteId, "EMP-207", "Test", "Employee", "test@example.com"));

        // Add a certification expiring in 5 days
        var expirationDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5));
        await grain.AddCertificationAsync(new AddCertificationCommand(
            "food_handler", "Food Handler", DateOnly.FromDateTime(DateTime.UtcNow), expirationDate));

        // Add a valid certification
        await grain.AddCertificationAsync(new AddCertificationCommand(
            "alcohol_service", "TIPS", DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))));

        // Act
        var alerts = await grain.CheckCertificationExpirationsAsync(warningDays: 30, criticalDays: 7);

        // Assert
        alerts.Should().HaveCount(1);
        alerts[0].CertificationType.Should().Be("food_handler");
        alerts[0].DaysUntilExpiration.Should().BeLessThanOrEqualTo(7);
    }

    // ============================================================================
    // Tax Calculation Service Tests
    // ============================================================================

    [Fact]
    public void TaxCalculationService_CalculateWithholding_FederalTaxes()
    {
        // Arrange
        var service = new TaxCalculationService();
        var config = service.GetTaxConfiguration("US-FEDERAL");
        var grossPay = 1000m;

        // Act
        var withholding = service.CalculateWithholding(grossPay, config);

        // Assert
        withholding.FederalWithholding.Should().Be(220m); // 22%
        withholding.SocialSecurityWithholding.Should().Be(62m); // 6.2%
        withholding.MedicareWithholding.Should().Be(14.5m); // 1.45%
        withholding.StateWithholding.Should().Be(0m);
        withholding.LocalWithholding.Should().Be(0m);
        withholding.TotalWithholding.Should().Be(296.5m);
    }

    [Fact]
    public void TaxCalculationService_CalculateWithholding_CaliforniaTaxes()
    {
        // Arrange
        var service = new TaxCalculationService();
        var config = service.GetTaxConfiguration("US-CA");
        var grossPay = 1000m;

        // Act
        var withholding = service.CalculateWithholding(grossPay, config);

        // Assert
        withholding.FederalWithholding.Should().Be(220m); // 22%
        withholding.StateWithholding.Should().Be(72.5m); // 7.25%
        withholding.SocialSecurityWithholding.Should().Be(62m); // 6.2%
        withholding.TotalWithholding.Should().BeGreaterThan(350m);
    }

    [Fact]
    public void TaxCalculationService_CalculateWithholding_NewYorkWithLocalTax()
    {
        // Arrange
        var service = new TaxCalculationService();
        var config = service.GetTaxConfiguration("US-NY");
        var grossPay = 1000m;

        // Act
        var withholding = service.CalculateWithholding(grossPay, config);

        // Assert
        withholding.LocalWithholding.Should().BeGreaterThan(0); // NYC local tax
        withholding.TotalWithholding.Should().BeGreaterThan(
            withholding.FederalWithholding + withholding.StateWithholding);
    }

    [Fact]
    public void TaxCalculationService_CalculateWithholding_TexasNoStateTax()
    {
        // Arrange
        var service = new TaxCalculationService();
        var config = service.GetTaxConfiguration("US-TX");
        var grossPay = 1000m;

        // Act
        var withholding = service.CalculateWithholding(grossPay, config);

        // Assert
        withholding.StateWithholding.Should().Be(0m); // Texas has no state income tax
        withholding.LocalWithholding.Should().Be(0m);
    }

    [Fact]
    public void TaxCalculationService_CalculateWithholding_SocialSecurityCap()
    {
        // Arrange
        var service = new TaxCalculationService();
        var config = service.GetTaxConfiguration("US-FEDERAL");
        var grossPay = 10000m;
        var ytdGrossPay = 165000m; // Just under the SS limit

        // Act
        var withholding = service.CalculateWithholding(grossPay, config, ytdGrossPay);

        // Assert - SS should be calculated only on remaining amount under the cap
        withholding.SocialSecurityWithholding.Should().BeLessThan(grossPay * 0.062m);
    }

    // ============================================================================
    // Payroll Export Service Tests
    // ============================================================================

    [Fact]
    public async Task PayrollExportService_GenerateCsv_CreatesValidCsv()
    {
        // Arrange
        var taxService = new TaxCalculationService();
        var exportService = new PayrollExportService(taxService);

        var entries = new List<PayrollExportEntry>
        {
            new(
                EmployeeId: Guid.NewGuid(),
                EmployeeNumber: "EMP-001",
                FirstName: "John",
                LastName: "Doe",
                Email: "john.doe@example.com",
                RegularHours: 40m,
                OvertimeHours: 5m,
                DoubleOvertimeHours: 0m,
                HourlyRate: 20m,
                OvertimeRate: 30m,
                DoubleOvertimeRate: 40m,
                RegularPay: 800m,
                OvertimePay: 150m,
                DoubleOvertimePay: 0m,
                TipsReceived: 100m,
                GrossPay: 1050m,
                TaxWithholding: new TaxWithholding(231m, 75.9m, 0m, 65.1m, 15.23m, 387.23m),
                Deductions: 50m,
                NetPay: 612.77m,
                Department: "Front of House",
                JobTitle: "Server")
        };

        // Act
        var csv = exportService.GenerateCsv(entries, true);

        // Assert
        csv.Should().Contain("Employee ID");
        csv.Should().Contain("EMP-001");
        csv.Should().Contain("John");
        csv.Should().Contain("Doe");
        csv.Should().Contain("40.00");
        csv.Should().Contain("Federal Tax");
    }

    [Fact]
    public async Task PayrollExportService_ExportToAdp_CreatesAdpFormat()
    {
        // Arrange
        var taxService = new TaxCalculationService();
        var exportService = new PayrollExportService(taxService);

        var entries = new List<PayrollExportEntry>
        {
            new(
                EmployeeId: Guid.NewGuid(),
                EmployeeNumber: "EMP-001",
                FirstName: "John",
                LastName: "Doe",
                Email: "john.doe@example.com",
                RegularHours: 40m,
                OvertimeHours: 5m,
                DoubleOvertimeHours: 0m,
                HourlyRate: 20m,
                OvertimeRate: 30m,
                DoubleOvertimeRate: 40m,
                RegularPay: 800m,
                OvertimePay: 150m,
                DoubleOvertimePay: 0m,
                TipsReceived: 100m,
                GrossPay: 1050m,
                TaxWithholding: null,
                Deductions: 0m,
                NetPay: 1050m,
                Department: "Front of House",
                JobTitle: "Server")
        };

        // Act
        var adpContent = exportService.GenerateAdpFormat(entries);

        // Assert
        adpContent.Should().Contain("H,ADP_PAYROLL_IMPORT");
        adpContent.Should().Contain("EMP-001");
        adpContent.Should().Contain("REG");
        adpContent.Should().Contain("OT");
        adpContent.Should().Contain("TIPS");
    }

    [Fact]
    public async Task PayrollExportService_ExportToGusto_CreatesGustoFormat()
    {
        // Arrange
        var taxService = new TaxCalculationService();
        var exportService = new PayrollExportService(taxService);

        var entries = new List<PayrollExportEntry>
        {
            new(
                EmployeeId: Guid.NewGuid(),
                EmployeeNumber: "EMP-001",
                FirstName: "John",
                LastName: "Doe",
                Email: "john.doe@example.com",
                RegularHours: 40m,
                OvertimeHours: 5m,
                DoubleOvertimeHours: 2m,
                HourlyRate: 20m,
                OvertimeRate: 30m,
                DoubleOvertimeRate: 40m,
                RegularPay: 800m,
                OvertimePay: 150m,
                DoubleOvertimePay: 80m,
                TipsReceived: 100m,
                GrossPay: 1130m,
                TaxWithholding: null,
                Deductions: 0m,
                NetPay: 1130m,
                Department: "Front of House",
                JobTitle: "Server")
        };

        // Act
        var gustoContent = exportService.GenerateGustoFormat(entries);

        // Assert
        gustoContent.Should().Contain("employee_id");
        gustoContent.Should().Contain("regular_hours");
        gustoContent.Should().Contain("overtime_hours");
        gustoContent.Should().Contain("double_overtime_hours");
        gustoContent.Should().Contain("EMP-001");
        gustoContent.Should().Contain("40.00");
        gustoContent.Should().Contain("5.00");
        gustoContent.Should().Contain("2.00");
    }

    [Fact]
    public async Task PayrollExportService_GeneratePreview_CalculatesTotals()
    {
        // Arrange
        var taxService = new TaxCalculationService();
        var exportService = new PayrollExportService(taxService);

        var entries = new List<PayrollExportEntry>
        {
            new(Guid.NewGuid(), "EMP-001", "John", "Doe", "john@example.com",
                40m, 5m, 0m, 20m, 30m, 40m, 800m, 150m, 0m, 100m, 1050m,
                new TaxWithholding(231m, 75.9m, 0m, 65.1m, 15.23m, 387.23m),
                50m, 612.77m, "FOH", "Server"),
            new(Guid.NewGuid(), "EMP-002", "Jane", "Smith", "jane@example.com",
                35m, 0m, 0m, 18m, 27m, 36m, 630m, 0m, 0m, 80m, 710m,
                new TaxWithholding(156.2m, 51.28m, 0m, 44.02m, 10.3m, 261.8m),
                30m, 418.2m, "FOH", "Hostess")
        };

        var periodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14));
        var periodEnd = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var preview = await exportService.GeneratePreviewAsync(entries, periodStart, periodEnd);

        // Assert
        preview.EmployeeCount.Should().Be(2);
        preview.TotalRegularHours.Should().Be(75m); // 40 + 35
        preview.TotalOvertimeHours.Should().Be(5m);
        preview.TotalGrossPay.Should().Be(1760m); // 1050 + 710
        preview.TotalTaxWithholdings.Should().Be(649.03m); // 387.23 + 261.8
        preview.TotalNetPay.Should().Be(1030.97m); // 612.77 + 418.2
    }

    [Fact]
    public async Task PayrollExportService_Export_ReturnsCorrectMetadata()
    {
        // Arrange
        var taxService = new TaxCalculationService();
        var exportService = new PayrollExportService(taxService);

        var entries = new List<PayrollExportEntry>
        {
            new(Guid.NewGuid(), "EMP-001", "John", "Doe", "john@example.com",
                40m, 5m, 0m, 20m, 30m, 40m, 800m, 150m, 0m, 100m, 1050m,
                null, 0m, 1050m, "FOH", "Server")
        };

        var request = new PayrollExportRequest(
            OrgId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            PeriodStart: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14)),
            PeriodEnd: DateOnly.FromDateTime(DateTime.UtcNow),
            Format: PayrollExportFormat.Csv);

        // Act
        var result = await exportService.ExportAsync(entries, request);

        // Assert
        result.ExportId.Should().NotBeEmpty();
        result.Format.Should().Be(PayrollExportFormat.Csv);
        result.EmployeeCount.Should().Be(1);
        result.FileName.Should().EndWith(".csv");
        result.ContentType.Should().Be("text/csv");
        result.FileContent.Should().NotBeEmpty();
    }
}
