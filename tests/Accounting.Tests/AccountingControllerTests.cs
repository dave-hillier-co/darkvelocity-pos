using System.Net;
using System.Net.Http.Json;
using DarkVelocity.Accounting.Api.Dtos;
using DarkVelocity.Accounting.Api.Entities;
using DarkVelocity.Shared.Contracts.Hal;
using FluentAssertions;

namespace DarkVelocity.Accounting.Tests;

public class AccountingControllerTests : IClassFixture<AccountingApiFixture>
{
    private readonly AccountingApiFixture _fixture;
    private readonly HttpClient _client;

    public AccountingControllerTests(AccountingApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Client;
    }

    #region Accounts

    [Fact]
    public async Task GetAccounts_ReturnsAccountList()
    {
        var response = await _client.GetAsync("/api/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<AccountDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAccount_ReturnsAccount()
    {
        var response = await _client.GetAsync($"/api/accounts/{_fixture.TestAccountId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var account = await response.Content.ReadFromJsonAsync<AccountDto>();
        account.Should().NotBeNull();
        account!.AccountCode.Should().Be("1000");
        account.Name.Should().Be("Cash in Drawer");
        account.AccountType.Should().Be(AccountType.Asset);
    }

    [Fact]
    public async Task CreateAccount_ReturnsCreatedAccount()
    {
        var request = new CreateAccountRequest(
            AccountCode: "5100",
            Name: "Cost of Goods Sold",
            AccountType: AccountType.Expense,
            SubType: "COGS"
        );

        var response = await _client.PostAsJsonAsync("/api/accounts", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var account = await response.Content.ReadFromJsonAsync<AccountDto>();
        account.Should().NotBeNull();
        account!.AccountCode.Should().Be("5100");
        account.Name.Should().Be("Cost of Goods Sold");
        account.AccountType.Should().Be(AccountType.Expense);
    }

    [Fact]
    public async Task CreateAccount_WithDuplicateCode_ReturnsBadRequest()
    {
        var request = new CreateAccountRequest(
            AccountCode: "1000", // Already exists
            Name: "Duplicate Account",
            AccountType: AccountType.Asset
        );

        var response = await _client.PostAsJsonAsync("/api/accounts", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAccountTree_ReturnsHierarchicalAccounts()
    {
        var response = await _client.GetAsync("/api/accounts/tree");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<AccountTreeDto>>();
        collection.Should().NotBeNull();
    }

    #endregion

    #region Journal Entries

    [Fact]
    public async Task GetJournalEntries_ReturnsEntryList()
    {
        var response = await _client.GetAsync($"/api/locations/{_fixture.TestLocationId}/journal-entries");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<JournalEntryDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetJournalEntry_ReturnsEntryWithLines()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/journal-entries/{_fixture.TestJournalEntryId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var entry = await response.Content.ReadFromJsonAsync<JournalEntryDto>();
        entry.Should().NotBeNull();
        entry!.EntryNumber.Should().Be("JE-2026-00001");
        entry.TotalDebit.Should().Be(11.90m);
        entry.TotalCredit.Should().Be(11.90m);
        entry.Lines.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateJournalEntry_WithBalancedEntry_ReturnsCreated()
    {
        var request = new CreateJournalEntryRequest(
            EntryDate: new DateOnly(2026, 1, 20),
            Description: "Test manual entry",
            Currency: "EUR",
            Lines: new List<CreateJournalEntryLineRequest>
            {
                new("1000", 50.00m, 0, null, null, null, "Debit to cash"),
                new("4100", 0, 50.00m, null, null, null, "Credit to sales")
            }
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/journal-entries",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var entry = await response.Content.ReadFromJsonAsync<JournalEntryDto>();
        entry.Should().NotBeNull();
        entry!.TotalDebit.Should().Be(50.00m);
        entry.TotalCredit.Should().Be(50.00m);
        entry.Status.Should().Be(JournalEntryStatus.Posted);
    }

    [Fact]
    public async Task CreateJournalEntry_WithUnbalancedEntry_ReturnsBadRequest()
    {
        var request = new CreateJournalEntryRequest(
            EntryDate: new DateOnly(2026, 1, 20),
            Description: "Unbalanced entry",
            Currency: "EUR",
            Lines: new List<CreateJournalEntryLineRequest>
            {
                new("1000", 50.00m, 0, null, null, null, null),
                new("4100", 0, 30.00m, null, null, null, null) // Doesn't balance!
            }
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/journal-entries",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReverseJournalEntry_ReturnsReversalEntry()
    {
        // First create an entry to reverse
        var createRequest = new CreateJournalEntryRequest(
            EntryDate: new DateOnly(2026, 1, 25),
            Description: "Entry to reverse",
            Currency: "EUR",
            Lines: new List<CreateJournalEntryLineRequest>
            {
                new("1000", 25.00m, 0, null, null, null, null),
                new("4100", 0, 25.00m, null, null, null, null)
            }
        );

        var createResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/journal-entries",
            createRequest);

        var createdEntry = await createResponse.Content.ReadFromJsonAsync<JournalEntryDto>();

        // Now reverse it
        var reverseRequest = new ReverseJournalEntryRequest(Reason: "Test reversal");

        var reverseResponse = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/journal-entries/{createdEntry!.Id}/reverse",
            reverseRequest);

        reverseResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var reversalEntry = await reverseResponse.Content.ReadFromJsonAsync<JournalEntryDto>();
        reversalEntry.Should().NotBeNull();
        reversalEntry!.ReversesEntryId.Should().Be(createdEntry.Id);
        reversalEntry.TotalDebit.Should().Be(25.00m); // Swapped
        reversalEntry.TotalCredit.Should().Be(25.00m); // Swapped
    }

    #endregion

    #region Accounting Periods

    [Fact]
    public async Task GetAccountingPeriods_ReturnsPeriodList()
    {
        var response = await _client.GetAsync(
            $"/api/locations/{_fixture.TestLocationId}/accounting-periods");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<AccountingPeriodDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateAccountingPeriod_ReturnsCreatedPeriod()
    {
        var request = new CreateAccountingPeriodRequest(
            PeriodType: PeriodType.Monthly,
            StartDate: new DateOnly(2026, 2, 1),
            EndDate: new DateOnly(2026, 2, 28),
            Notes: "February 2026"
        );

        var response = await _client.PostAsJsonAsync(
            $"/api/locations/{_fixture.TestLocationId}/accounting-periods",
            request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var period = await response.Content.ReadFromJsonAsync<AccountingPeriodDto>();
        period.Should().NotBeNull();
        period!.PeriodType.Should().Be(PeriodType.Monthly);
        period.Status.Should().Be(PeriodStatus.Open);
    }

    #endregion

    #region Cost Centers

    [Fact]
    public async Task GetCostCenters_ReturnsCostCenterList()
    {
        var response = await _client.GetAsync("/api/cost-centers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var collection = await response.Content.ReadFromJsonAsync<HalCollection<CostCenterDto>>();
        collection.Should().NotBeNull();
        collection!.Embedded.Items.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateCostCenter_ReturnsCreatedCostCenter()
    {
        var request = new CreateCostCenterRequest(
            Code: "BOH",
            Name: "Back of House",
            Description: "Kitchen operations"
        );

        var response = await _client.PostAsJsonAsync("/api/cost-centers", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var costCenter = await response.Content.ReadFromJsonAsync<CostCenterDto>();
        costCenter.Should().NotBeNull();
        costCenter!.Code.Should().Be("BOH");
        costCenter.Name.Should().Be("Back of House");
    }

    #endregion

    #region Reports

    [Fact]
    public async Task GetTrialBalance_ReturnsReport()
    {
        var response = await _client.GetAsync("/api/reports/trial-balance?asOfDate=2026-01-31");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var report = await response.Content.ReadFromJsonAsync<TrialBalanceReportDto>();
        report.Should().NotBeNull();
        report!.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task GetProfitLoss_ReturnsReport()
    {
        var response = await _client.GetAsync(
            "/api/reports/profit-loss?startDate=2026-01-01&endDate=2026-01-31");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var report = await response.Content.ReadFromJsonAsync<ProfitLossReportDto>();
        report.Should().NotBeNull();
    }

    [Fact]
    public async Task GetBalanceSheet_ReturnsReport()
    {
        var response = await _client.GetAsync("/api/reports/balance-sheet?asOfDate=2026-01-31");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var report = await response.Content.ReadFromJsonAsync<BalanceSheetReportDto>();
        report.Should().NotBeNull();
    }

    #endregion

    #region Health Check

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}
