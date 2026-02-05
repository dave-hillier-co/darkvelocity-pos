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
