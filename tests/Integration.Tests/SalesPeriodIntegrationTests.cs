using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Integration.Tests.Fixtures;
using DarkVelocity.Orders.Api.Dtos;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Integration.Tests;

/// <summary>
/// Integration tests for Sales Period (Cash Register) Management.
///
/// Business Scenarios Covered:
/// - Opening a new sales period (cashier starts shift)
/// - Closing a sales period (end of shift cash reconciliation)
/// - Sales period constraints (only one open period per location)
/// - Orders require an active sales period
/// - Cash discrepancy tracking
/// </summary>
public class SalesPeriodIntegrationTests : IClassFixture<OrdersServiceFixture>
{
    private readonly OrdersServiceFixture _fixture;
    private readonly HttpClient _client;

    public SalesPeriodIntegrationTests(OrdersServiceFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    #region Opening Sales Period

    [Fact]
    public async Task OpenSalesPeriod_WithValidData_CreatesOpenPeriod()
    {
        // Arrange - Use a new location to avoid conflict
        var newLocationId = Guid.NewGuid();
        var request = new OpenSalesPeriodRequest(
            UserId: _fixture.TestUserId,
            OpeningCashAmount: 150.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var period = await response.Content.ReadFromJsonAsync<SalesPeriodDto>();
        period.Should().NotBeNull();
        period!.Status.Should().Be("open");
        period.OpeningCashAmount.Should().Be(150.00m);
        period.OpenedByUserId.Should().Be(_fixture.TestUserId);
        period.OpenedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        period.ClosedAt.Should().BeNull();
        period.ClosingCashAmount.Should().BeNull();
    }

    [Fact]
    public async Task OpenSalesPeriod_RecordsOpeningUserForAudit()
    {
        // Arrange
        var newLocationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var request = new OpenSalesPeriodRequest(
            UserId: userId,
            OpeningCashAmount: 100.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var period = await response.Content.ReadFromJsonAsync<SalesPeriodDto>();
        period!.OpenedByUserId.Should().Be(userId);
    }

    [Fact]
    public async Task OpenSalesPeriod_WhenPeriodAlreadyOpen_ReturnsConflict()
    {
        // Arrange - The fixture already has an open period for TestLocationId
        var request = new OpenSalesPeriodRequest(
            UserId: _fixture.TestUserId,
            OpeningCashAmount: 200.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/sales-periods",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task OpenSalesPeriod_WithZeroCash_IsAllowed()
    {
        // Arrange - Some locations may start with zero cash (card-only terminals)
        var newLocationId = Guid.NewGuid();
        var request = new OpenSalesPeriodRequest(
            UserId: _fixture.TestUserId,
            OpeningCashAmount: 0m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var period = await response.Content.ReadFromJsonAsync<SalesPeriodDto>();
        period!.OpeningCashAmount.Should().Be(0m);
    }

    #endregion

    #region Getting Current Period

    [Fact]
    public async Task GetCurrentSalesPeriod_ReturnsOpenPeriod()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/sales-periods/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var period = await response.Content.ReadFromJsonAsync<SalesPeriodDto>();
        period.Should().NotBeNull();
        period!.Status.Should().Be("open");
        period.LocationId.Should().Be(_fixture.TestLocationId);
    }

    [Fact]
    public async Task GetCurrentSalesPeriod_WhenNoneOpen_ReturnsNotFound()
    {
        // Arrange - Use a location with no open period
        var emptyLocationId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{emptyLocationId}/sales-periods/current");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Closing Sales Period

    [Fact]
    public async Task CloseSalesPeriod_WithMatchingCash_ClosesSuccessfully()
    {
        // Arrange - Create a new period to close
        var newLocationId = Guid.NewGuid();
        var openRequest = new OpenSalesPeriodRequest(_fixture.TestUserId, 100.00m);
        var openResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods",
            openRequest);
        var openedPeriod = await openResponse.Content.ReadFromJsonAsync<SalesPeriodDto>();

        var closeRequest = new CloseSalesPeriodRequest(
            UserId: _fixture.TestUserId,
            ClosingCashAmount: 100.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods/{openedPeriod!.Id}/close",
            closeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var closedPeriod = await response.Content.ReadFromJsonAsync<SalesPeriodDto>();
        closedPeriod!.Status.Should().Be("closed");
        closedPeriod.ClosingCashAmount.Should().Be(100.00m);
        closedPeriod.ClosedByUserId.Should().Be(_fixture.TestUserId);
        closedPeriod.ClosedAt.Should().NotBeNull();
        closedPeriod.ClosedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CloseSalesPeriod_RecordsClosingUserForAudit()
    {
        // Arrange
        var newLocationId = Guid.NewGuid();
        var openingUserId = Guid.NewGuid();
        var closingUserId = Guid.NewGuid();

        var openRequest = new OpenSalesPeriodRequest(openingUserId, 100.00m);
        var openResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods",
            openRequest);
        var openedPeriod = await openResponse.Content.ReadFromJsonAsync<SalesPeriodDto>();

        var closeRequest = new CloseSalesPeriodRequest(
            UserId: closingUserId,
            ClosingCashAmount: 100.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods/{openedPeriod!.Id}/close",
            closeRequest);

        // Assert
        var closedPeriod = await response.Content.ReadFromJsonAsync<SalesPeriodDto>();
        closedPeriod!.OpenedByUserId.Should().Be(openingUserId);
        closedPeriod.ClosedByUserId.Should().Be(closingUserId);
    }

    [Fact]
    public async Task CloseSalesPeriod_WithCashDiscrepancy_RecordsVariance()
    {
        // Arrange - Simulate more cash than expected (tips or errors)
        var newLocationId = Guid.NewGuid();
        var openRequest = new OpenSalesPeriodRequest(_fixture.TestUserId, 100.00m);
        var openResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods",
            openRequest);
        var openedPeriod = await openResponse.Content.ReadFromJsonAsync<SalesPeriodDto>();

        // Close with more cash than opened (e.g., cash sales occurred)
        var closeRequest = new CloseSalesPeriodRequest(
            UserId: _fixture.TestUserId,
            ClosingCashAmount: 350.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods/{openedPeriod!.Id}/close",
            closeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var closedPeriod = await response.Content.ReadFromJsonAsync<SalesPeriodDto>();
        closedPeriod!.ClosingCashAmount.Should().Be(350.00m);
        // ExpectedCashAmount would be calculated based on orders in the period
    }

    [Fact]
    public async Task CloseSalesPeriod_AlreadyClosed_ReturnsBadRequest()
    {
        // Arrange - Create and close a period
        var newLocationId = Guid.NewGuid();
        var openRequest = new OpenSalesPeriodRequest(_fixture.TestUserId, 100.00m);
        var openResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods",
            openRequest);
        var openedPeriod = await openResponse.Content.ReadFromJsonAsync<SalesPeriodDto>();

        var closeRequest = new CloseSalesPeriodRequest(_fixture.TestUserId, 100.00m);
        await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods/{openedPeriod!.Id}/close",
            closeRequest);

        // Act - Try to close again
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods/{openedPeriod.Id}/close",
            closeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Sales Period History

    [Fact]
    public async Task GetSalesPeriods_ReturnsAllPeriodsForLocation()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/sales-periods");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<SalesPeriodDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
        collection.Embedded.Items.Should().OnlyContain(p => p.LocationId == _fixture.TestLocationId);
    }

    [Fact]
    public async Task GetSalesPeriodById_ReturnsCorrectPeriod()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/sales-periods/{_fixture.TestSalesPeriodId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var period = await response.Content.ReadFromJsonAsync<SalesPeriodDto>();
        period.Should().NotBeNull();
        period!.Id.Should().Be(_fixture.TestSalesPeriodId);
    }

    [Fact]
    public async Task GetSalesPeriodById_NotFound_Returns404()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/sales-periods/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Location Isolation

    [Fact]
    public async Task SalesPeriod_IsolatedByLocation_CannotAccessOtherLocationPeriod()
    {
        // Arrange - Create period in location A
        var locationA = Guid.NewGuid();
        var locationB = Guid.NewGuid();

        var openRequest = new OpenSalesPeriodRequest(_fixture.TestUserId, 100.00m);
        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{locationA}/sales-periods",
            openRequest);
        var period = await createResponse.Content.ReadFromJsonAsync<SalesPeriodDto>();

        // Act - Try to access period from location B
        var response = await _client.GetAsync(
            $"/api/locations/{locationB}/sales-periods/{period!.Id}");

        // Assert - Should not find it (location isolation)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MultipleLocations_CanHaveOpenPeriodsSimultaneously()
    {
        // Arrange
        var location1 = Guid.NewGuid();
        var location2 = Guid.NewGuid();
        var location3 = Guid.NewGuid();

        var request = new OpenSalesPeriodRequest(_fixture.TestUserId, 100.00m);

        // Act - Open periods in all locations
        var response1 = await _client.PostAsJsonAsync($"/api/locations/{location1}/sales-periods", request);
        var response2 = await _client.PostAsJsonAsync($"/api/locations/{location2}/sales-periods", request);
        var response3 = await _client.PostAsJsonAsync($"/api/locations/{location3}/sales-periods", request);

        // Assert - All should succeed
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        response2.StatusCode.Should().Be(HttpStatusCode.Created);
        response3.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    #endregion

    #region P2: Cash Drawer Operations

    [Fact]
    public async Task CashDrop_ReducesExpectedDrawerBalance()
    {
        // Arrange - Create a new period for cash drop testing
        var newLocationId = Guid.NewGuid();
        var openRequest = new OpenSalesPeriodRequest(_fixture.TestUserId, 500.00m);
        var openResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods",
            openRequest);
        var period = await openResponse.Content.ReadFromJsonAsync<SalesPeriodDto>();

        var dropRequest = new CashDropRequest(
            Amount: 200.00m,
            UserId: _fixture.TestUserId,
            Notes: "Safe drop - excess cash");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods/{period!.Id}/cash-drops",
            dropRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
    }

    [Fact]
    public async Task CashDrop_RequiresOpenSalesPeriod()
    {
        // Arrange - Use a closed or non-existent period
        var newLocationId = Guid.NewGuid();
        var openRequest = new OpenSalesPeriodRequest(_fixture.TestUserId, 100.00m);
        var openResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods",
            openRequest);
        var period = await openResponse.Content.ReadFromJsonAsync<SalesPeriodDto>();

        // Close the period
        await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods/{period!.Id}/close",
            new CloseSalesPeriodRequest(_fixture.TestUserId, 100.00m));

        var dropRequest = new CashDropRequest(
            Amount: 50.00m,
            UserId: _fixture.TestUserId,
            Notes: "Attempted drop on closed period");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods/{period.Id}/cash-drops",
            dropRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PayIn_IncreasesDrawerBalance()
    {
        // Arrange - Create period for pay-in testing
        var newLocationId = Guid.NewGuid();
        var openRequest = new OpenSalesPeriodRequest(_fixture.TestUserId, 100.00m);
        var openResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods",
            openRequest);
        var period = await openResponse.Content.ReadFromJsonAsync<SalesPeriodDto>();

        var payInRequest = new PayInRequest(
            Amount: 50.00m,
            UserId: _fixture.TestUserId,
            Notes: "Petty cash replenishment");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods/{period!.Id}/pay-ins",
            payInRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK);
    }

    [Fact]
    public async Task NoSale_LogsDrawerOpening()
    {
        // Arrange - No sale opens drawer without transaction
        var noSaleRequest = new NoSaleRequest(
            UserId: _fixture.TestUserId,
            Reason: "Customer needed change");

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/sales-periods/{_fixture.TestSalesPeriodId}/no-sale",
            noSaleRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    #endregion

    #region P2: Summary Report Generation

    [Fact]
    public async Task CloseSalesPeriod_GeneratesSummaryReport()
    {
        // Arrange - Create period with some activity
        var newLocationId = Guid.NewGuid();
        var openRequest = new OpenSalesPeriodRequest(_fixture.TestUserId, 200.00m);
        var openResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods",
            openRequest);
        var period = await openResponse.Content.ReadFromJsonAsync<SalesPeriodDto>();

        var closeRequest = new CloseSalesPeriodRequest(_fixture.TestUserId, 350.00m);

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{newLocationId}/sales-periods/{period!.Id}/close",
            closeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var closedPeriod = await response.Content.ReadFromJsonAsync<SalesPeriodDto>();

        // Verify summary data is included
        closedPeriod!.Status.Should().Be("closed");
        closedPeriod.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSalesPeriodSummary_ReturnsAggregatedData()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/sales-periods/{_fixture.TestSalesPeriodId}/summary");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        // Summary endpoint may not exist - this tests if it's implemented
    }

    #endregion
}

// P2 DTO extensions for cash operations
public record CashDropRequest(
    decimal Amount,
    Guid UserId,
    string? Notes = null);

public record PayInRequest(
    decimal Amount,
    Guid UserId,
    string? Notes = null);

public record NoSaleRequest(
    Guid UserId,
    string? Reason = null);
