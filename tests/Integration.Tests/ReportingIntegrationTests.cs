using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using DarkVelocity.Reporting.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// Integration tests for Reporting and Analytics workflows.
///
/// Business Scenarios Covered:
/// - Daily sales reports and aggregation
/// - Item-level margin analysis
/// - Category-level margin analysis
/// - Margin threshold and alert management
/// - Supplier analysis
/// - Sales trend analysis
/// </summary>
public class ReportingIntegrationTests : IClassFixture<ReportingServiceFixture>
{
    private readonly ReportingServiceFixture _fixture;
    private readonly HttpClient _client;

    public ReportingIntegrationTests(ReportingServiceFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    #region Daily Sales Reports

    [Fact]
    public async Task GetDailySales_ReturnsAllDaysInRange()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/daily-sales");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DailySalesRangeDto>();
        result.Should().NotBeNull();
        result!.Days.Should().NotBeEmpty();
        result.Totals.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDailySales_WithDateRange_ReturnsFilteredResults()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);
        var startDate = yesterday.ToString("yyyy-MM-dd");
        var endDate = today.ToString("yyyy-MM-dd");

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/daily-sales?startDate={startDate}&endDate={endDate}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DailySalesRangeDto>();
        result!.Days.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetDailySales_CalculatesTotalsCorrectly()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/daily-sales");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<DailySalesRangeDto>();
        result!.Totals.GrossRevenue.Should().BeGreaterThan(0);
        result.Totals.NetRevenue.Should().BeGreaterThan(0);
        result.Totals.GrossProfit.Should().BeGreaterThan(0);
        result.Totals.OrderCount.Should().BeGreaterThan(0);

        // Net revenue should be less than or equal to gross (due to discounts)
        result.Totals.NetRevenue.Should().BeLessThanOrEqualTo(result.Totals.GrossRevenue);
    }

    [Fact]
    public async Task GetDailySalesByDate_ExistingDate_ReturnsSummary()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStr = today.ToString("yyyy-MM-dd");

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/daily-sales/{todayStr}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DailySalesSummaryDto>();
        result.Should().NotBeNull();
        result!.Date.Should().Be(today);
        result.GrossRevenue.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetDailySalesByDate_NonExistingDate_ReturnsNotFound()
    {
        // Arrange
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(10));
        var dateStr = futureDate.ToString("yyyy-MM-dd");

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/daily-sales/{dateStr}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GenerateDailySummary_CreatesNewRecord()
    {
        // Arrange
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var request = new GenerateDailySummaryRequest(
            Date: newDate,
            GrossRevenue: 1000.00m,
            DiscountTotal: 25.00m,
            TaxCollected: 97.50m,
            TotalCOGS: 350.00m,
            OrderCount: 30,
            ItemsSold: 75,
            TipsCollected: 50.00m,
            CashTotal: 400.00m,
            CardTotal: 550.00m,
            OtherPaymentTotal: 25.00m,
            RefundCount: 0,
            RefundTotal: 0m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/daily-sales",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<DailySalesSummaryDto>();
        result.Should().NotBeNull();
        result!.Date.Should().Be(newDate);
        result.GrossRevenue.Should().Be(1000.00m);
        result.NetRevenue.Should().Be(975.00m); // 1000 - 25
        result.GrossProfit.Should().Be(625.00m); // 975 - 350
        result.GrossMarginPercent.Should().BeGreaterThan(60m);
    }

    [Fact]
    public async Task GenerateDailySummary_CalculatesAverageOrderValue()
    {
        // Arrange
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-31));
        var request = new GenerateDailySummaryRequest(
            Date: newDate,
            GrossRevenue: 500.00m,
            DiscountTotal: 0m,
            TaxCollected: 50.00m,
            TotalCOGS: 150.00m,
            OrderCount: 10,
            ItemsSold: 25,
            TipsCollected: 25.00m,
            CashTotal: 200.00m,
            CardTotal: 300.00m,
            OtherPaymentTotal: 0m,
            RefundCount: 0,
            RefundTotal: 0m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/daily-sales",
            request);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<DailySalesSummaryDto>();
        result!.AverageOrderValue.Should().Be(50.00m); // 500 / 10
    }

    #endregion

    #region Item Margin Reports

    [Fact]
    public async Task GetItemMargins_ReturnsItemMarginsForLocation()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-margins");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ItemMarginReportDto>();
        result.Should().NotBeNull();
        result!.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetItemMargins_IncludesMarginCalculations()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-margins");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ItemMarginReportDto>();
        var items = result!.Items;

        items.Should().OnlyContain(i => i.GrossMarginPercent > 0);
        items.Should().OnlyContain(i => i.GrossProfit > 0);
    }

    [Fact]
    public async Task GetItemMarginsByItem_ReturnsSpecificItemMargins()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-margins/{_fixture.TestMenuItemId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ItemMarginReportDto>();
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].MenuItemId.Should().Be(_fixture.TestMenuItemId);
    }

    [Fact]
    public async Task GenerateItemSummary_CreatesRecord()
    {
        // Arrange
        var newItemId = Guid.NewGuid();
        var newDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-15));
        var request = new GenerateItemSummaryRequest(
            Date: newDate,
            MenuItemId: newItemId,
            MenuItemName: "Grilled Chicken",
            CategoryId: _fixture.TestCategoryId,
            CategoryName: "Mains",
            QuantitySold: 25,
            GrossRevenue: 400.00m,
            DiscountTotal: 15.00m,
            TotalCOGS: 125.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/item-margins",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<ItemSalesSummaryDto>();
        result.Should().NotBeNull();
        result!.MenuItemId.Should().Be(newItemId);
        result.NetRevenue.Should().Be(385.00m); // 400 - 15
        result.GrossProfit.Should().Be(260.00m); // 385 - 125
    }

    #endregion

    #region Category Margin Reports

    [Fact]
    public async Task GetCategoryMargins_ReturnsCategoryMarginsForLocation()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/category-margins");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<CategoryMarginReportDto>();
        result.Should().NotBeNull();
        result!.Categories.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetCategoryMargins_IncludesRevenuePercentage()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/category-margins");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<CategoryMarginReportDto>();
        result!.Categories.Should().OnlyContain(c => c.RevenuePercentOfTotal > 0);
    }

    #endregion

    #region Margin Thresholds

    [Fact]
    public async Task GetMarginThresholds_ReturnsAllThresholds()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/margin-thresholds");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<HalCollection<MarginThresholdDto>>();
        result.Should().NotBeNull();
        result!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateMarginThreshold_CreatesNewThreshold()
    {
        // Arrange
        var request = new CreateMarginThresholdRequest(
            ThresholdType: "item",
            MinimumMarginPercent: 60.00m,
            WarningMarginPercent: 70.00m,
            MenuItemId: Guid.NewGuid());

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/margin-thresholds",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<MarginThresholdDto>();
        result.Should().NotBeNull();
        result!.ThresholdType.Should().Be("item");
        result.MinimumMarginPercent.Should().Be(60.00m);
        result.WarningMarginPercent.Should().Be(70.00m);
    }

    [Fact]
    public async Task UpdateMarginThreshold_UpdatesExistingThreshold()
    {
        // Arrange
        var updateRequest = new UpdateMarginThresholdRequest(
            MinimumMarginPercent: 55.00m,
            WarningMarginPercent: 65.00m);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/margin-thresholds/{_fixture.TestThresholdId}",
            updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MarginThresholdDto>();
        result!.MinimumMarginPercent.Should().Be(55.00m);
        result.WarningMarginPercent.Should().Be(65.00m);
    }

    #endregion

    #region Margin Alerts

    [Fact]
    public async Task GetMarginAlerts_ReturnsAlerts()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/margin-alerts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<HalCollection<MarginAlertDto>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AcknowledgeMarginAlert_UpdatesAlertStatus()
    {
        // Arrange - First get alerts to find one to acknowledge
        var alertsResponse = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/margin-alerts");
        var alerts = await alertsResponse.Content.ReadFromJsonAsync<HalCollection<MarginAlertDto>>();

        if (alerts?.Embedded.Items.Any() == true)
        {
            var alertId = alerts.Embedded.Items.First().Id;
            var acknowledgeRequest = new AcknowledgeAlertRequest(
                Notes: "Reviewed and accepted");

            // Act
            var response = await _client.PostAsJsonAsync(
                $"/api/locations/{_fixture.TestLocationId}/reports/margin-alerts/{alertId}/acknowledge",
                acknowledgeRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await response.Content.ReadFromJsonAsync<MarginAlertDto>();
            result!.IsAcknowledged.Should().BeTrue();
        }
    }

    #endregion

    #region Supplier Analysis

    [Fact]
    public async Task GetSupplierAnalysis_ReturnsSupplierMetrics()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/supplier-analysis");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SupplierAnalysisReportDto>();
        result.Should().NotBeNull();
        result!.Suppliers.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetSupplierAnalysis_IncludesDeliveryMetrics()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/supplier-analysis");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<SupplierAnalysisReportDto>();
        var supplier = result!.Suppliers.First();

        supplier.DeliveryCount.Should().BeGreaterThan(0);
        supplier.OnTimePercentage.Should().BeGreaterThan(0);
        supplier.TotalSpend.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSupplierAnalysisBySupplier_ReturnsSpecificSupplierMetrics()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/supplier-analysis/{_fixture.TestSupplierId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SupplierSpendSummaryDto>();
        result.Should().NotBeNull();
        result!.SupplierId.Should().Be(_fixture.TestSupplierId);
    }

    #endregion

    #region Stock Consumption Reports

    [Fact]
    public async Task GetStockConsumptions_ReturnsConsumptionRecords()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/stock-consumptions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<HalCollection<StockConsumptionDto>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStockConsumptions_FiltersByDateRange()
    {
        // Arrange
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/reports/stock-consumptions?startDate={yesterday}&endDate={today}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Location Isolation

    [Fact]
    public async Task DailySalesReports_IsolatedByLocation()
    {
        // Arrange
        var otherLocationId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{otherLocationId}/reports/daily-sales");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DailySalesRangeDto>();
        result!.Days.Should().BeEmpty(); // No data for this location
    }

    #endregion
}
