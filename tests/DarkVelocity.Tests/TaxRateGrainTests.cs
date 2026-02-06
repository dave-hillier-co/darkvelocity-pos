using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Tests for TaxRateGrain which manages tax rate configuration by country and fiscal code.
/// Tax rates track effective dates for proper historical reporting and rate changes.
/// </summary>
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

    #region CreateAsync Tests

    // Given: A new tax rate grain for Germany with fiscal code NORMAL
    // When: A 19% standard VAT rate is created effective from January 2024
    // Then: The tax rate is stored with the correct country, rate, fiscal code, and active status
    [Fact]
    public async Task CreateAsync_WithStandardVatRate_CreatesRate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "DE", "NORMAL");

        var command = new CreateTaxRateCommand(
            CountryCode: "DE",
            Rate: 19.00m,
            FiscalCode: "NORMAL",
            Description: "German standard VAT rate",
            EffectiveFrom: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.TaxRateId.Should().NotBe(Guid.Empty);
        snapshot.CountryCode.Should().Be("DE");
        snapshot.Rate.Should().Be(19.00m);
        snapshot.FiscalCode.Should().Be("NORMAL");
        snapshot.Description.Should().Be("German standard VAT rate");
        snapshot.IsActive.Should().BeTrue();
    }

    // Given: A new tax rate grain for Germany with fiscal code REDUCED
    // When: A 7% reduced VAT rate for food and books is created
    // Then: The reduced rate is stored correctly
    [Fact]
    public async Task CreateAsync_WithReducedVatRate_CreatesRate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "DE", "REDUCED");

        var command = new CreateTaxRateCommand(
            CountryCode: "DE",
            Rate: 7.00m,
            FiscalCode: "REDUCED",
            Description: "German reduced VAT rate (food, books)",
            EffectiveFrom: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.Rate.Should().Be(7.00m);
        snapshot.FiscalCode.Should().Be("REDUCED");
    }

    // Given: A new tax rate grain for Germany with fiscal code EXEMPT
    // When: A 0% tax-exempt rate is created
    // Then: The zero rate is stored and marked as active
    [Fact]
    public async Task CreateAsync_WithZeroRate_CreatesRate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "DE", "EXEMPT");

        var command = new CreateTaxRateCommand(
            CountryCode: "DE",
            Rate: 0.00m,
            FiscalCode: "EXEMPT",
            Description: "Tax exempt",
            EffectiveFrom: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.Rate.Should().Be(0.00m);
        snapshot.FiscalCode.Should().Be("EXEMPT");
        snapshot.IsActive.Should().BeTrue();
    }

    // Given: A new tax rate grain for Germany's temporary COVID relief rate
    // When: A 16% rate is created with a limited effective period (Jul 1 - Dec 31, 2020)
    // Then: Both effective-from and effective-to dates are stored correctly
    [Fact]
    public async Task CreateAsync_WithEffectiveDateRange_CreatesRate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "DE", "COVID_REDUCED");

        // Germany temporarily reduced VAT during COVID
        var command = new CreateTaxRateCommand(
            CountryCode: "DE",
            Rate: 16.00m,
            FiscalCode: "COVID_REDUCED",
            Description: "Temporary reduced standard rate (COVID relief)",
            EffectiveFrom: new DateTime(2020, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: new DateTime(2020, 12, 31, 23, 59, 59, DateTimeKind.Utc));

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.EffectiveFrom.Should().Be(new DateTime(2020, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        snapshot.EffectiveTo.Should().Be(new DateTime(2020, 12, 31, 23, 59, 59, DateTimeKind.Utc));
        snapshot.IsActive.Should().BeTrue();
    }

    // Given: A new tax rate grain for the United Kingdom
    // When: A 20% UK standard VAT rate is created
    // Then: The rate is stored with country code GB and the correct percentage
    [Fact]
    public async Task CreateAsync_ForUK_CreatesRate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "GB", "STANDARD");

        var command = new CreateTaxRateCommand(
            CountryCode: "GB",
            Rate: 20.00m,
            FiscalCode: "STANDARD",
            Description: "UK standard VAT rate",
            EffectiveFrom: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.CountryCode.Should().Be("GB");
        snapshot.Rate.Should().Be(20.00m);
    }

    // Given: A new tax rate grain for France
    // When: A 20% French standard TVA rate is created
    // Then: The rate is stored with country code FR and the correct percentage
    [Fact]
    public async Task CreateAsync_ForFrance_CreatesRate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "FR", "NORMAL");

        var command = new CreateTaxRateCommand(
            CountryCode: "FR",
            Rate: 20.00m,
            FiscalCode: "NORMAL",
            Description: "French standard TVA rate",
            EffectiveFrom: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.CountryCode.Should().Be("FR");
        snapshot.Rate.Should().Be(20.00m);
    }

    // Given: A new tax rate grain for California state sales tax
    // When: A 7.25% state sales tax rate is created with code US-CA
    // Then: The rate is stored with the state-level country code and correct percentage
    [Fact]
    public async Task CreateAsync_ForUS_SalesTax_CreatesRate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "US-CA", "STATE");

        var command = new CreateTaxRateCommand(
            CountryCode: "US-CA",
            Rate: 7.25m,
            FiscalCode: "STATE",
            Description: "California state sales tax",
            EffectiveFrom: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.CountryCode.Should().Be("US-CA");
        snapshot.Rate.Should().Be(7.25m);
    }

    // Given: A new tax rate grain for Switzerland
    // When: An 8.1% Swiss standard MWST rate is created
    // Then: The rate is stored with the correct decimal precision
    [Fact]
    public async Task CreateAsync_ForSwitzerland_CreatesRate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "CH", "NORMAL");

        var command = new CreateTaxRateCommand(
            CountryCode: "CH",
            Rate: 8.1m,
            FiscalCode: "NORMAL",
            Description: "Swiss standard MWST rate",
            EffectiveFrom: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.CountryCode.Should().Be("CH");
        snapshot.Rate.Should().Be(8.1m);
    }

    // Given: A tax rate grain that already has a 19% rate created for DE:DUPLICATE
    // When: A second tax rate with the same fiscal code is created
    // Then: An error is thrown because the tax rate already exists
    [Fact]
    public async Task CreateAsync_AlreadyExists_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "DE", "DUPLICATE");

        await grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "DE",
            Rate: 19.00m,
            FiscalCode: "DUPLICATE",
            Description: "First rate",
            EffectiveFrom: DateTime.UtcNow,
            EffectiveTo: null));

        // Act
        var act = () => grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "DE",
            Rate: 20.00m,
            FiscalCode: "DUPLICATE",
            Description: "Second rate",
            EffectiveFrom: DateTime.UtcNow,
            EffectiveTo: null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tax rate already exists");
    }

    #endregion

    #region GetSnapshotAsync Tests

    // Given: An Austrian 20% standard VAT rate that has been created
    // When: The tax rate snapshot is retrieved
    // Then: The snapshot contains the correct country code, rate, and fiscal code
    [Fact]
    public async Task GetSnapshotAsync_ReturnsCurrentState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "AT", "NORMAL");

        await grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "AT",
            Rate: 20.00m,
            FiscalCode: "NORMAL",
            Description: "Austrian standard VAT",
            EffectiveFrom: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: null));

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.CountryCode.Should().Be("AT");
        snapshot.Rate.Should().Be(20.00m);
        snapshot.FiscalCode.Should().Be("NORMAL");
    }

    // Given: A tax rate grain that has never been initialized
    // When: The snapshot is requested
    // Then: An error is thrown because the grain is not initialized
    [Fact]
    public async Task GetSnapshotAsync_OnUninitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "XX", "UNINITIALIZED");

        // Act
        var act = () => grain.GetSnapshotAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tax rate grain not initialized");
    }

    #endregion

    #region GetCurrentRateAsync Tests

    // Given: A Spanish 21% standard IVA rate effective from 30 days ago
    // When: The current rate is queried
    // Then: The rate returns 21%
    [Fact]
    public async Task GetCurrentRateAsync_ReturnsCorrectRate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "ES", "NORMAL");

        await grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "ES",
            Rate: 21.00m,
            FiscalCode: "NORMAL",
            Description: "Spanish standard IVA rate",
            EffectiveFrom: DateTime.UtcNow.AddDays(-30),
            EffectiveTo: null));

        // Act
        var rate = await grain.GetCurrentRateAsync();

        // Assert
        rate.Should().Be(21.00m);
    }

    // Given: A Spanish zero-rated tax for exempt supplies
    // When: The current rate is queried
    // Then: The rate returns 0%
    [Fact]
    public async Task GetCurrentRateAsync_WithZeroRate_ReturnsZero()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "ES", "ZERO");

        await grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "ES",
            Rate: 0.00m,
            FiscalCode: "ZERO",
            Description: "Zero-rated supplies",
            EffectiveFrom: DateTime.UtcNow.AddDays(-30),
            EffectiveTo: null));

        // Act
        var rate = await grain.GetCurrentRateAsync();

        // Assert
        rate.Should().Be(0.00m);
    }

    // Given: A tax rate grain that has never been initialized
    // When: The current rate is queried
    // Then: An error is thrown because the grain is not initialized
    [Fact]
    public async Task GetCurrentRateAsync_OnUninitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "XX", "NONINIT");

        // Act
        var act = () => grain.GetCurrentRateAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tax rate grain not initialized");
    }

    #endregion

    #region DeactivateAsync Tests

    // Given: An active Italian 22% standard IVA rate
    // When: The rate is deactivated with the current date as effective-to
    // Then: The rate is marked inactive and the effective-to date is set
    [Fact]
    public async Task DeactivateAsync_SetsIsActiveToFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "IT", "NORMAL");

        await grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "IT",
            Rate: 22.00m,
            FiscalCode: "NORMAL",
            Description: "Italian standard IVA",
            EffectiveFrom: DateTime.UtcNow.AddYears(-1),
            EffectiveTo: null));

        var effectiveTo = DateTime.UtcNow;

        // Act
        await grain.DeactivateAsync(effectiveTo);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
        snapshot.EffectiveTo.Should().BeCloseTo(effectiveTo, TimeSpan.FromSeconds(1));
    }

    // Given: An active Dutch 21% standard BTW rate
    // When: The rate is deactivated with a future effective-to date 3 months from now
    // Then: The rate is marked inactive with the future date stored
    [Fact]
    public async Task DeactivateAsync_WithFutureEffectiveDate_SetsDate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "NL", "STANDARD");

        await grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "NL",
            Rate: 21.00m,
            FiscalCode: "STANDARD",
            Description: "Dutch standard BTW",
            EffectiveFrom: DateTime.UtcNow.AddYears(-1),
            EffectiveTo: null));

        var futureDate = DateTime.UtcNow.AddMonths(3);

        // Act
        await grain.DeactivateAsync(futureDate);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
        snapshot.EffectiveTo.Should().BeCloseTo(futureDate, TimeSpan.FromSeconds(1));
    }

    // Given: A tax rate grain that has never been initialized
    // When: Deactivation is attempted
    // Then: An error is thrown because the grain is not initialized
    [Fact]
    public async Task DeactivateAsync_OnUninitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "XX", "DEACTIVATE_FAIL");

        // Act
        var act = () => grain.DeactivateAsync(DateTime.UtcNow);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tax rate grain not initialized");
    }

    #endregion

    #region IsActiveOnDateAsync Tests

    // Given: A Belgian 21% BTW rate effective from Jan 1 to Dec 31, 2024
    // When: The active status is checked for June 15, 2024 (within range)
    // Then: The rate is active on that date
    [Fact]
    public async Task IsActiveOnDateAsync_WithinRange_ReturnsTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "BE", "NORMAL");

        var effectiveFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var effectiveTo = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);

        await grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "BE",
            Rate: 21.00m,
            FiscalCode: "NORMAL",
            Description: "Belgian standard BTW",
            EffectiveFrom: effectiveFrom,
            EffectiveTo: effectiveTo));

        // Act
        var isActive = await grain.IsActiveOnDateAsync(new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        isActive.Should().BeTrue();
    }

    // Given: A Portuguese 23% IVA rate effective from January 1, 2024
    // When: The active status is checked for June 15, 2023 (before effective date)
    // Then: The rate is not active on that date
    [Fact]
    public async Task IsActiveOnDateAsync_BeforeEffectiveFrom_ReturnsFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "PT", "NORMAL");

        var effectiveFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "PT",
            Rate: 23.00m,
            FiscalCode: "NORMAL",
            Description: "Portuguese standard IVA",
            EffectiveFrom: effectiveFrom,
            EffectiveTo: null));

        // Act
        var isActive = await grain.IsActiveOnDateAsync(new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        isActive.Should().BeFalse();
    }

    // Given: A Greek temporary 13% rate effective from Jan 1 to June 30, 2024
    // When: The active status is checked for July 15, 2024 (after expiry)
    // Then: The rate is not active on that date
    [Fact]
    public async Task IsActiveOnDateAsync_AfterEffectiveTo_ReturnsFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "GR", "TEMP");

        var effectiveFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var effectiveTo = new DateTime(2024, 6, 30, 23, 59, 59, DateTimeKind.Utc);

        await grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "GR",
            Rate: 13.00m,
            FiscalCode: "TEMP",
            Description: "Greek temporary reduced rate",
            EffectiveFrom: effectiveFrom,
            EffectiveTo: effectiveTo));

        // Act
        var isActive = await grain.IsActiveOnDateAsync(new DateTime(2024, 7, 15, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        isActive.Should().BeFalse();
    }

    // Given: An Irish 23% VAT rate with effective-from of January 1, 2024
    // When: The active status is checked for exactly the effective-from date
    // Then: The rate is active on the boundary date (inclusive)
    [Fact]
    public async Task IsActiveOnDateAsync_ExactlyOnEffectiveFrom_ReturnsTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "IE", "STANDARD");

        var effectiveFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "IE",
            Rate: 23.00m,
            FiscalCode: "STANDARD",
            Description: "Irish standard VAT",
            EffectiveFrom: effectiveFrom,
            EffectiveTo: null));

        // Act
        var isActive = await grain.IsActiveOnDateAsync(effectiveFrom);

        // Assert
        isActive.Should().BeTrue();
    }

    // Given: A Finnish temporary 10% rate with effective-to of June 30, 2024
    // When: The active status is checked for exactly the effective-to date
    // Then: The rate is active on the boundary date (inclusive)
    [Fact]
    public async Task IsActiveOnDateAsync_ExactlyOnEffectiveTo_ReturnsTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "FI", "TEMP");

        var effectiveFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var effectiveTo = new DateTime(2024, 6, 30, 23, 59, 59, DateTimeKind.Utc);

        await grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "FI",
            Rate: 10.00m,
            FiscalCode: "TEMP",
            Description: "Finnish temporary rate",
            EffectiveFrom: effectiveFrom,
            EffectiveTo: effectiveTo));

        // Act
        var isActive = await grain.IsActiveOnDateAsync(effectiveTo);

        // Assert
        isActive.Should().BeTrue();
    }

    // Given: A Swedish 25% MOMS rate with no effective-to date (open-ended)
    // When: The active status is checked for a date far in the future (2050)
    // Then: The rate is active because there is no expiry date
    [Fact]
    public async Task IsActiveOnDateAsync_NoEffectiveTo_ReturnsTrue_AfterEffectiveFrom()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "SE", "STANDARD");

        var effectiveFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "SE",
            Rate: 25.00m,
            FiscalCode: "STANDARD",
            Description: "Swedish standard MOMS",
            EffectiveFrom: effectiveFrom,
            EffectiveTo: null));

        // Act - check a date far in the future
        var isActive = await grain.IsActiveOnDateAsync(new DateTime(2050, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        isActive.Should().BeTrue();
    }

    // Given: A tax rate grain that has never been initialized
    // When: An active-on-date check is attempted
    // Then: An error is thrown because the grain is not initialized
    [Fact]
    public async Task IsActiveOnDateAsync_OnUninitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "XX", "ISACTIVE_FAIL");

        // Act
        var act = () => grain.IsActiveOnDateAsync(DateTime.UtcNow);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tax rate grain not initialized");
    }

    #endregion

    #region Edge Cases and Special Scenarios

    // Given: A new tax rate grain for Quebec provincial sales tax
    // When: A 9.975% PST rate is created with three decimal places
    // Then: The full decimal precision is preserved
    [Fact]
    public async Task CreateAsync_WithHighPrecisionRate_PreservesDecimals()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "CA-QC", "PST");

        var command = new CreateTaxRateCommand(
            CountryCode: "CA-QC",
            Rate: 9.975m,
            FiscalCode: "PST",
            Description: "Quebec PST (TVQ)",
            EffectiveFrom: DateTime.UtcNow,
            EffectiveTo: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.Rate.Should().Be(9.975m);
    }

    // Given: A new tax rate grain for Japan's reduced consumption tax
    // When: A very small 0.08% rate is created
    // Then: The small rate value is preserved without rounding
    [Fact]
    public async Task CreateAsync_WithVerySmallRate_PreservesRate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "JP", "REDUCED");

        var command = new CreateTaxRateCommand(
            CountryCode: "JP",
            Rate: 0.08m,
            FiscalCode: "REDUCED",
            Description: "Japanese reduced consumption tax rate",
            EffectiveFrom: DateTime.UtcNow,
            EffectiveTo: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.Rate.Should().Be(0.08m);
    }

    // Given: Tax rate grains for Poland with three different fiscal codes (NORMAL, REDUCED, SUPER_REDUCED)
    // When: Three rates (23%, 8%, 5%) are created for the same country
    // Then: Each fiscal code stores its own independent rate
    [Fact]
    public async Task CreateAsync_MultipleTaxRatesForSameCountry_DifferentFiscalCodes()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grainNormal = GetGrain(orgId, "PL", "NORMAL");
        var grainReduced = GetGrain(orgId, "PL", "REDUCED");
        var grainSuperReduced = GetGrain(orgId, "PL", "SUPER_REDUCED");

        // Act
        var normalRate = await grainNormal.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "PL",
            Rate: 23.00m,
            FiscalCode: "NORMAL",
            Description: "Polish standard VAT",
            EffectiveFrom: DateTime.UtcNow,
            EffectiveTo: null));

        var reducedRate = await grainReduced.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "PL",
            Rate: 8.00m,
            FiscalCode: "REDUCED",
            Description: "Polish reduced VAT",
            EffectiveFrom: DateTime.UtcNow,
            EffectiveTo: null));

        var superReducedRate = await grainSuperReduced.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "PL",
            Rate: 5.00m,
            FiscalCode: "SUPER_REDUCED",
            Description: "Polish super-reduced VAT",
            EffectiveFrom: DateTime.UtcNow,
            EffectiveTo: null));

        // Assert
        normalRate.Rate.Should().Be(23.00m);
        reducedRate.Rate.Should().Be(8.00m);
        superReducedRate.Rate.Should().Be(5.00m);
    }

    // Given: A Hungarian 27% AFA rate effective from 2020, deactivated on January 1, 2024
    // When: Active status is checked for dates before and after deactivation
    // Then: The rate is active before the deactivation date and inactive after
    [Fact]
    public async Task DeactivateAsync_ThenCheckIsActiveOnDate_BeforeDeactivation_ReturnsTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "HU", "OLD_RATE");

        var effectiveFrom = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "HU",
            Rate: 27.00m,
            FiscalCode: "OLD_RATE",
            Description: "Hungarian standard AFA",
            EffectiveFrom: effectiveFrom,
            EffectiveTo: null));

        var deactivationDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await grain.DeactivateAsync(deactivationDate);

        // Act - check date before deactivation
        var isActiveBeforeDeactivation = await grain.IsActiveOnDateAsync(
            new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc));

        // Act - check date after deactivation
        var isActiveAfterDeactivation = await grain.IsActiveOnDateAsync(
            new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        isActiveBeforeDeactivation.Should().BeTrue();
        isActiveAfterDeactivation.Should().BeFalse();
    }

    // Given: A new tax rate grain for Czech Republic
    // When: A 21% rate is created with an empty description
    // Then: The rate is stored with an empty description (no validation error)
    [Fact]
    public async Task CreateAsync_WithEmptyDescription_CreatesRate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "CZ", "NORMAL");

        var command = new CreateTaxRateCommand(
            CountryCode: "CZ",
            Rate: 21.00m,
            FiscalCode: "NORMAL",
            Description: "",
            EffectiveFrom: DateTime.UtcNow,
            EffectiveTo: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.Description.Should().BeEmpty();
        snapshot.Rate.Should().Be(21.00m);
    }

    // Given: A new tax rate grain for Denmark
    // When: A 25% rate is created with a 500-character description
    // Then: The full long description is preserved
    [Fact]
    public async Task CreateAsync_WithLongDescription_CreatesRate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "DK", "NORMAL");

        var longDescription = new string('A', 500);

        var command = new CreateTaxRateCommand(
            CountryCode: "DK",
            Rate: 25.00m,
            FiscalCode: "NORMAL",
            Description: longDescription,
            EffectiveFrom: DateTime.UtcNow,
            EffectiveTo: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.Description.Should().Be(longDescription);
    }

    // Given: An old Luxembourg rate expiring end of 2023 and a new rate starting January 2024
    // When: Active status is checked for dates in 2023 and 2024 for both rates
    // Then: Each rate is only active within its own effective period (no overlap)
    [Fact]
    public async Task IsActiveOnDateAsync_WithHistoricalRateChange()
    {
        // Arrange - simulate a rate that was replaced
        var orgId = Guid.NewGuid();
        var grainOldRate = GetGrain(orgId, "LU", "OLD_STANDARD");
        var grainNewRate = GetGrain(orgId, "LU", "NEW_STANDARD");

        // Old rate effective until end of 2023
        await grainOldRate.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "LU",
            Rate: 17.00m,
            FiscalCode: "OLD_STANDARD",
            Description: "Luxembourg old standard rate",
            EffectiveFrom: new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: new DateTime(2023, 12, 31, 23, 59, 59, DateTimeKind.Utc)));

        // New rate effective from 2024
        await grainNewRate.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "LU",
            Rate: 17.00m,
            FiscalCode: "NEW_STANDARD",
            Description: "Luxembourg standard rate",
            EffectiveFrom: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: null));

        // Act
        var oldRateActive2023 = await grainOldRate.IsActiveOnDateAsync(
            new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var oldRateActive2024 = await grainOldRate.IsActiveOnDateAsync(
            new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var newRateActive2023 = await grainNewRate.IsActiveOnDateAsync(
            new DateTime(2023, 6, 15, 0, 0, 0, DateTimeKind.Utc));
        var newRateActive2024 = await grainNewRate.IsActiveOnDateAsync(
            new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc));

        // Assert
        oldRateActive2023.Should().BeTrue();
        oldRateActive2024.Should().BeFalse();
        newRateActive2023.Should().BeFalse();
        newRateActive2024.Should().BeTrue();
    }

    // Given: A Slovak 20% DPH rate that has been deactivated
    // When: The current rate is queried after deactivation
    // Then: The rate value of 20% is still returned (deactivation does not erase the rate)
    [Fact]
    public async Task GetCurrentRateAsync_AfterDeactivation_StillReturnsRate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "SK", "NORMAL");

        await grain.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "SK",
            Rate: 20.00m,
            FiscalCode: "NORMAL",
            Description: "Slovak standard DPH",
            EffectiveFrom: DateTime.UtcNow.AddYears(-1),
            EffectiveTo: null));

        await grain.DeactivateAsync(DateTime.UtcNow);

        // Act - GetCurrentRateAsync returns the rate value regardless of active status
        var rate = await grain.GetCurrentRateAsync();

        // Assert
        rate.Should().Be(20.00m);
    }

    #endregion

    #region Multiple Organization Isolation Tests

    // Given: Two different organizations both needing Romanian standard TVA rates
    // When: A 19% rate is created for each organization with the same country and fiscal code
    // Then: Each organization gets an independent tax rate with unique IDs (tenant isolation)
    [Fact]
    public async Task CreateAsync_DifferentOrganizations_AreIsolated()
    {
        // Arrange
        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();

        var grainOrg1 = GetGrain(org1Id, "RO", "NORMAL");
        var grainOrg2 = GetGrain(org2Id, "RO", "NORMAL");

        // Act
        var snapshot1 = await grainOrg1.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "RO",
            Rate: 19.00m,
            FiscalCode: "NORMAL",
            Description: "Romanian standard TVA for Org 1",
            EffectiveFrom: DateTime.UtcNow,
            EffectiveTo: null));

        var snapshot2 = await grainOrg2.CreateAsync(new CreateTaxRateCommand(
            CountryCode: "RO",
            Rate: 19.00m,
            FiscalCode: "NORMAL",
            Description: "Romanian standard TVA for Org 2",
            EffectiveFrom: DateTime.UtcNow,
            EffectiveTo: null));

        // Assert - Different TaxRateIds prove isolation
        snapshot1.TaxRateId.Should().NotBe(snapshot2.TaxRateId);
        snapshot1.Description.Should().Contain("Org 1");
        snapshot2.Description.Should().Contain("Org 2");
    }

    #endregion

    #region Real-World Country Tax Rate Scenarios

    // Given: A new tax rate grain for Germany's hospitality-specific fiscal code GASTRO
    // When: A 7% reduced rate for in-restaurant food consumption is created
    // Then: The hospitality-specific rate is stored with the GASTRO fiscal code
    [Fact]
    public async Task CreateAsync_GermanHospitalityReducedRate_CreatesRate()
    {
        // Arrange - Germany has a 7% reduced rate for restaurant food (not drinks)
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "DE", "GASTRO");

        var command = new CreateTaxRateCommand(
            CountryCode: "DE",
            Rate: 7.00m,
            FiscalCode: "GASTRO",
            Description: "German reduced VAT for in-restaurant food consumption",
            EffectiveFrom: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.Rate.Should().Be(7.00m);
        snapshot.FiscalCode.Should().Be("GASTRO");
    }

    // Given: A new tax rate grain for Switzerland's accommodation services
    // When: A special 3.8% MWST rate for accommodation is created
    // Then: The accommodation-specific rate is stored correctly
    [Fact]
    public async Task CreateAsync_SwissAccommodationRate_CreatesRate()
    {
        // Arrange - Switzerland has special 3.8% rate for accommodation
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "CH", "ACCOMMODATION");

        var command = new CreateTaxRateCommand(
            CountryCode: "CH",
            Rate: 3.8m,
            FiscalCode: "ACCOMMODATION",
            Description: "Swiss MWST for accommodation services",
            EffectiveFrom: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.Rate.Should().Be(3.8m);
    }

    // Given: A new tax rate grain for Ireland's tourism/hospitality sector
    // When: A 9% tourism VAT rate is created
    // Then: The tourism-specific rate is stored correctly
    [Fact]
    public async Task CreateAsync_IrishTourismRate_CreatesRate()
    {
        // Arrange - Ireland has 9% tourism/hospitality rate (when active)
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId, "IE", "TOURISM");

        var command = new CreateTaxRateCommand(
            CountryCode: "IE",
            Rate: 9.00m,
            FiscalCode: "TOURISM",
            Description: "Irish tourism/hospitality VAT rate",
            EffectiveFrom: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EffectiveTo: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.Rate.Should().Be(9.00m);
    }

    #endregion
}
