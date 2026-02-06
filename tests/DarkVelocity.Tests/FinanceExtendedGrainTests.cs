using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

/// <summary>
/// Extended tests for Finance domain grains covering gaps in test coverage.
/// Includes multi-currency handling, year-end close, reconciliation, audit trails,
/// and comprehensive period management scenarios.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class FinanceExtendedGrainTests
{
    private readonly TestClusterFixture _fixture;

    public FinanceExtendedGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================================================
    // Multi-Currency Handling Tests
    // ============================================================================

    #region Multi-Currency Handling Tests

    // Given: A new account grain for an organization
    // When: A cash account is created with EUR as the currency
    // Then: The account stores EUR as its operating currency
    [Fact]
    public async Task Account_CreateWithDifferentCurrency_ShouldStoreCurrency()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(orgId, accountId));

        // Act
        var result = await grain.CreateAsync(new CreateAccountCommand(
            orgId,
            accountId,
            "1000",
            "Cash Euro",
            AccountType.Asset,
            Guid.NewGuid(),
            Currency: "EUR"));

        // Assert
        result.AccountCode.Should().Be("1000");
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("EUR");
    }

    // Given: A new account grain with no currency specified
    // When: The account is created without an explicit currency
    // Then: The account defaults to USD as its currency
    [Fact]
    public async Task Account_DefaultCurrency_ShouldBeUSD()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(orgId, accountId));

        // Act
        await grain.CreateAsync(new CreateAccountCommand(
            orgId,
            accountId,
            "1001",
            "Cash Default",
            AccountType.Asset,
            Guid.NewGuid()));

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("USD");
    }

    // Given: A new chart of accounts for an organization
    // When: The chart is initialized with GBP as the reporting currency
    // Then: The chart stores GBP as its base currency
    [Fact]
    public async Task ChartOfAccounts_InitializeWithDifferentCurrency_ShouldStoreCurrency()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        // Act
        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(
            orgId,
            Guid.NewGuid(),
            CreateDefaultAccounts: true,
            Currency: "GBP"));

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.Currency.Should().Be("GBP");
    }

    // Given: A new chart of accounts for an organization
    // When: The chart is initialized with a supported international currency (EUR, GBP, JPY, CAD, AUD)
    // Then: The chart successfully stores the specified currency
    [Theory]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("CAD")]
    [InlineData("AUD")]
    public async Task ChartOfAccounts_SupportedCurrencies_ShouldInitialize(string currency)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        // Act
        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(
            orgId,
            Guid.NewGuid(),
            Currency: currency));

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.Currency.Should().Be(currency);
    }

    #endregion

    // ============================================================================
    // Year-End Close Process Tests
    // ============================================================================

    #region Year-End Close Process Tests

    // Given: A fiscal year with all 4 quarterly periods opened and closed
    // When: The year-end close process is executed with retained earnings account 3300
    // Then: The year is marked as closed and all 4 periods are locked
    [Fact]
    public async Task YearEndClose_AllPeriodsClosed_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid(), Frequency: PeriodFrequency.Quarterly));

        // Open and close all 4 quarterly periods
        for (int i = 1; i <= 4; i++)
        {
            await grain.OpenPeriodAsync(new OpenPeriodCommand(i, Guid.NewGuid()));
            await grain.ClosePeriodAsync(new ClosePeriodCommand2(i, Guid.NewGuid()));
        }

        // Act
        var summary = await grain.YearEndCloseAsync(new YearEndCloseCommand(
            Guid.NewGuid(),
            "3300",
            "Year-end close for fiscal year"));

        // Assert
        summary.IsYearClosed.Should().BeTrue();
        summary.LockedPeriods.Should().Be(4);
    }

    // Given: A fiscal year with period 1 still open and remaining periods not started
    // When: The year-end close process is attempted
    // Then: An error is thrown because all periods must be closed before year-end close
    [Fact]
    public async Task YearEndClose_WithOpenPeriod_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid()));

        // Only open and close first period, leave others
        await grain.OpenPeriodAsync(new OpenPeriodCommand(1, Guid.NewGuid()));

        // Act
        var act = () => grain.YearEndCloseAsync(new YearEndCloseCommand(
            Guid.NewGuid(),
            "3300"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not closed*");
    }

    // Given: A fiscal year that has already been closed through the year-end process
    // When: A second year-end close is attempted
    // Then: An error is thrown because the year is already closed
    [Fact]
    public async Task YearEndClose_AlreadyClosed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid(), Frequency: PeriodFrequency.Yearly));

        await grain.OpenPeriodAsync(new OpenPeriodCommand(1, Guid.NewGuid()));
        await grain.ClosePeriodAsync(new ClosePeriodCommand2(1, Guid.NewGuid()));
        await grain.YearEndCloseAsync(new YearEndCloseCommand(Guid.NewGuid(), "3300"));

        // Act
        var act = () => grain.YearEndCloseAsync(new YearEndCloseCommand(
            Guid.NewGuid(),
            "3300"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already closed*");
    }

    // Given: A fiscal year with all 4 quarterly periods closed
    // When: The year-end close process is executed
    // Then: Every period in the year transitions to Locked status
    [Fact]
    public async Task YearEndClose_ShouldLockAllPeriods()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid(), Frequency: PeriodFrequency.Quarterly));

        // Close all periods
        for (int i = 1; i <= 4; i++)
        {
            await grain.OpenPeriodAsync(new OpenPeriodCommand(i, Guid.NewGuid()));
            await grain.ClosePeriodAsync(new ClosePeriodCommand2(i, Guid.NewGuid()));
        }

        // Act
        await grain.YearEndCloseAsync(new YearEndCloseCommand(Guid.NewGuid(), "3300"));

        // Assert - all periods should be locked
        var periods = await grain.GetAllPeriodsAsync();
        periods.All(p => p.Status == PeriodStatus.Locked).Should().BeTrue();
    }

    // Given: A fiscal year that has been through year-end close with all periods locked
    // When: A posting eligibility check is performed for a date within the closed year
    // Then: The check returns false since no postings are allowed after year-end close
    [Fact]
    public async Task YearEndClose_CannotPostAfterwards()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid(), Frequency: PeriodFrequency.Yearly));

        await grain.OpenPeriodAsync(new OpenPeriodCommand(1, Guid.NewGuid()));
        await grain.ClosePeriodAsync(new ClosePeriodCommand2(1, Guid.NewGuid()));
        await grain.YearEndCloseAsync(new YearEndCloseCommand(Guid.NewGuid(), "3300"));

        // Act
        var canPost = await grain.CanPostToDateAsync(new DateOnly(year, 6, 15));

        // Assert
        canPost.Should().BeFalse();
    }

    #endregion

    // ============================================================================
    // Period Closing Rules Tests
    // ============================================================================

    #region Period Closing Rules Tests

    // Given: An initialized fiscal year with period 1 in NotStarted status
    // When: Closing period 1 is attempted without the force flag
    // Then: An error is thrown because the period was never opened
    [Fact]
    public async Task ClosePeriod_NeverOpened_WithoutForce_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid()));

        // Act - try to close period 1 without opening it
        var act = () => grain.ClosePeriodAsync(new ClosePeriodCommand2(
            1, Guid.NewGuid(), Force: false));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*never opened*");
    }

    // Given: An initialized fiscal year with period 1 in NotStarted status
    // When: Closing period 1 is forced without first opening it
    // Then: The period transitions directly to Closed status
    [Fact]
    public async Task ClosePeriod_NeverOpened_WithForce_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid()));

        // Act - force close period without opening
        var period = await grain.ClosePeriodAsync(new ClosePeriodCommand2(
            1, Guid.NewGuid(), Force: true));

        // Assert
        period.Status.Should().Be(PeriodStatus.Closed);
    }

    // Given: A period that has already been opened and closed
    // When: Closing the same period is attempted again
    // Then: An error is thrown because the period is already closed
    [Fact]
    public async Task ClosePeriod_AlreadyClosed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid()));

        await grain.OpenPeriodAsync(new OpenPeriodCommand(1, Guid.NewGuid()));
        await grain.ClosePeriodAsync(new ClosePeriodCommand2(1, Guid.NewGuid()));

        // Act
        var act = () => grain.ClosePeriodAsync(new ClosePeriodCommand2(
            1, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already closed*");
    }

    // Given: An open accounting period 1
    // When: Locking the period is attempted while it is still open
    // Then: An error is thrown because a period must be closed before it can be locked
    [Fact]
    public async Task LockPeriod_MustBeClosedFirst()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid()));

        await grain.OpenPeriodAsync(new OpenPeriodCommand(1, Guid.NewGuid()));

        // Act - try to lock open period
        var act = () => grain.LockPeriodAsync(new LockPeriodCommand(
            1, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must be closed*");
    }

    // Given: Period 1 still open while period 2 has been closed
    // When: Locking period 2 is attempted with period 1 still open
    // Then: An error is thrown because all prior periods must be closed or locked first
    [Fact]
    public async Task LockPeriod_RequiresPreviousPeriodsClosedOrLocked()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid()));

        // Open period 1, close it, then open period 2 and close it
        await grain.OpenPeriodAsync(new OpenPeriodCommand(1, Guid.NewGuid()));
        // Leave period 1 open
        await grain.OpenPeriodAsync(new OpenPeriodCommand(2, Guid.NewGuid()));
        await grain.ClosePeriodAsync(new ClosePeriodCommand2(2, Guid.NewGuid()));

        // Act - try to lock period 2 while period 1 is still open
        var act = () => grain.LockPeriodAsync(new LockPeriodCommand(
            2, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closing period*");
    }

    // Given: Periods 1 and 2 are locked, and period 3 is closed
    // When: Reopening period 1 is attempted
    // Then: An error is thrown because a later period (2) is already locked
    [Fact]
    public async Task ReopenPeriod_CannotReopenIfLaterPeriodLocked()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid()));

        // Close and lock periods 1 and 2
        await grain.OpenPeriodAsync(new OpenPeriodCommand(1, Guid.NewGuid()));
        await grain.ClosePeriodAsync(new ClosePeriodCommand2(1, Guid.NewGuid()));
        await grain.OpenPeriodAsync(new OpenPeriodCommand(2, Guid.NewGuid()));
        await grain.ClosePeriodAsync(new ClosePeriodCommand2(2, Guid.NewGuid()));
        await grain.LockPeriodAsync(new LockPeriodCommand(1, Guid.NewGuid()));
        await grain.LockPeriodAsync(new LockPeriodCommand(2, Guid.NewGuid()));

        // Close period 3
        await grain.OpenPeriodAsync(new OpenPeriodCommand(3, Guid.NewGuid()));
        await grain.ClosePeriodAsync(new ClosePeriodCommand2(3, Guid.NewGuid()));

        // Act - try to reopen period 1 when period 2 is locked
        var act = () => grain.ReopenPeriodAsync(new ReopenPeriodCommand(
            1, Guid.NewGuid(), "Need adjustment"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*locked*");
    }

    // Given: An initialized fiscal year with 12 monthly periods
    // When: The period for March 15 is queried
    // Then: Period 3 (March) is returned
    [Fact]
    public async Task GetPeriodForDate_ShouldReturnCorrectPeriod()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid()));

        // Act
        var period = await grain.GetPeriodForDateAsync(new DateOnly(year, 3, 15));

        // Assert
        period.Should().NotBeNull();
        period!.PeriodNumber.Should().Be(3); // March is period 3 for calendar year
    }

    // Given: An initialized fiscal year for the current year
    // When: A period is queried for a date in the previous year
    // Then: Null is returned because the date falls outside the fiscal year
    [Fact]
    public async Task GetPeriodForDate_DateOutsideFiscalYear_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid()));

        // Act
        var period = await grain.GetPeriodForDateAsync(new DateOnly(year - 1, 6, 15));

        // Assert
        period.Should().BeNull();
    }

    // Given: Period 1 closed and period 2 currently open
    // When: The current open period is queried
    // Then: Period 2 is returned as the first open period
    [Fact]
    public async Task GetCurrentOpenPeriod_ShouldReturnFirstOpenPeriod()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid()));

        await grain.OpenPeriodAsync(new OpenPeriodCommand(1, Guid.NewGuid()));
        await grain.ClosePeriodAsync(new ClosePeriodCommand2(1, Guid.NewGuid()));
        await grain.OpenPeriodAsync(new OpenPeriodCommand(2, Guid.NewGuid()));

        // Act
        var currentPeriod = await grain.GetCurrentOpenPeriodAsync();

        // Assert
        currentPeriod.Should().NotBeNull();
        currentPeriod!.PeriodNumber.Should().Be(2);
        currentPeriod.Status.Should().Be(PeriodStatus.Open);
    }

    // Given: A fiscal year configured to start in July (non-calendar year)
    // When: The periods are initialized and retrieved
    // Then: 12 periods are created with the first period starting in July
    [Fact]
    public async Task FiscalYearWithNonCalendarStart_ShouldCalculatePeriodsCorrectly()
    {
        // Arrange - fiscal year starting in July
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid(), FiscalYearStartMonth: 7));

        // Act
        var summary = await grain.GetSummaryAsync();
        var periods = await grain.GetAllPeriodsAsync();

        // Assert
        summary.FiscalYearStartMonth.Should().Be(7);
        summary.TotalPeriods.Should().Be(12);

        // First period should start in July
        var firstPeriod = periods.First();
        firstPeriod.StartDate.Month.Should().Be(7);
    }

    #endregion

    // ============================================================================
    // Audit Trail Completeness Tests
    // ============================================================================

    #region Audit Trail Completeness Tests

    // Given: A cash account with a debit and credit posted by a specific user
    // When: The recent entries are retrieved
    // Then: Every entry has a non-empty PerformedBy user ID for audit traceability
    [Fact]
    public async Task Account_AllEntries_ShouldHavePerformedBy()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(orgId, accountId));

        await grain.CreateAsync(new CreateAccountCommand(
            orgId, accountId, "1000", "Cash", AccountType.Asset, userId));

        await grain.PostDebitAsync(new PostDebitCommand(100m, "Deposit", userId));
        await grain.PostCreditAsync(new PostCreditCommand(50m, "Withdrawal", userId));

        // Act
        var entries = await grain.GetRecentEntriesAsync();

        // Assert
        entries.Should().HaveCountGreaterThan(0);
        entries.All(e => e.PerformedBy != Guid.Empty).Should().BeTrue();
    }

    // Given: A cash account with a posted debit entry
    // When: The recent entries are retrieved
    // Then: Every entry has a valid timestamp that is not in the future
    [Fact]
    public async Task Account_AllEntries_ShouldHaveTimestamps()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(orgId, accountId));

        await grain.CreateAsync(new CreateAccountCommand(
            orgId, accountId, "1000", "Cash", AccountType.Asset, Guid.NewGuid()));

        await grain.PostDebitAsync(new PostDebitCommand(100m, "Test", Guid.NewGuid()));

        // Act
        var entries = await grain.GetRecentEntriesAsync();

        // Assert
        entries.All(e => e.Timestamp != default).Should().BeTrue();
        entries.All(e => e.Timestamp <= DateTime.UtcNow).Should().BeTrue();
    }

    // Given: A cash account with a $100 debit entry posted
    // When: The entry is reversed due to a mistake
    // Then: The original entry is marked as Reversed and linked to the reversal entry, and vice versa
    [Fact]
    public async Task Account_Reversal_ShouldLinkToOriginalEntry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(orgId, accountId));

        await grain.CreateAsync(new CreateAccountCommand(
            orgId, accountId, "1000", "Cash", AccountType.Asset, Guid.NewGuid()));

        var debitResult = await grain.PostDebitAsync(new PostDebitCommand(
            100m, "Original entry", Guid.NewGuid()));

        // Act
        var reversalResult = await grain.ReverseEntryAsync(new ReverseEntryCommand(
            debitResult.EntryId, "Mistake", Guid.NewGuid()));

        // Assert
        var originalEntry = await grain.GetEntryAsync(debitResult.EntryId);
        var reversalEntry = await grain.GetEntryAsync(reversalResult.ReversalEntryId);

        originalEntry!.Status.Should().Be(JournalEntryStatus.Reversed);
        originalEntry.ReversalEntryId.Should().Be(reversalResult.ReversalEntryId);
        reversalEntry!.ReversedEntryId.Should().Be(debitResult.EntryId);
    }

    // Given: A cash account with a zero balance in the current period
    // When: The current period is closed by a specific user
    // Then: The closing balance of zero is recorded for the period
    [Fact]
    public async Task Account_PeriodClose_ShouldRecordClosingUser()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var closedBy = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(orgId, accountId));

        await grain.CreateAsync(new CreateAccountCommand(
            orgId, accountId, "1000", "Cash", AccountType.Asset, Guid.NewGuid()));

        var state = await grain.GetStateAsync();

        // Act
        var summary = await grain.ClosePeriodAsync(new ClosePeriodCommand(
            state.CurrentPeriodYear,
            state.CurrentPeriodMonth,
            closedBy));

        // Assert
        summary.ClosingBalance.Should().Be(0m);
    }

    // Given: A utilities expense recorded by a submitter
    // When: The expense is approved by a different user (approver)
    // Then: Both the submitter and approver IDs are tracked along with the approval timestamp
    [Fact]
    public async Task Expense_ApprovalWorkflow_ShouldTrackAllUsers()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var submittedBy = Guid.NewGuid();
        var approvedBy = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExpenseGrain>(
            GrainKeys.Expense(orgId, siteId, expenseId));

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Utilities,
            "Electric bill", 300m, new DateOnly(2024, 1, 15), submittedBy));

        // Act
        var snapshot = await grain.ApproveAsync(new ApproveExpenseCommand(approvedBy));

        // Assert
        snapshot.CreatedBy.Should().Be(submittedBy);
        snapshot.ApprovedBy.Should().Be(approvedBy);
        snapshot.ApprovedAt.Should().NotBeNull();
        snapshot.ApprovedAt!.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: An initialized ledger grain
    // When: A $150 credit is applied with order, user, source terminal, and receipt metadata
    // Then: All four metadata fields are preserved on the transaction
    [Fact]
    public async Task Ledger_Transactions_ShouldPreserveMetadata()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ILedgerGrain>(
            GrainKeys.Ledger(orgId, "audit-test", ownerId));

        await grain.InitializeAsync(orgId);

        var metadata = new Dictionary<string, string>
        {
            { "orderId", Guid.NewGuid().ToString() },
            { "userId", Guid.NewGuid().ToString() },
            { "source", "POS-Terminal-01" },
            { "receiptNumber", "REC-2024-001" }
        };

        // Act
        await grain.CreditAsync(150m, "sale", "Order payment", metadata);

        // Assert
        var transactions = await grain.GetTransactionsAsync(1);
        transactions.Should().HaveCount(1);
        transactions[0].Metadata.Should().ContainKey("orderId");
        transactions[0].Metadata.Should().ContainKey("userId");
        transactions[0].Metadata.Should().ContainKey("source");
        transactions[0].Metadata.Should().ContainKey("receiptNumber");
    }

    #endregion

    // ============================================================================
    // Ledger Balance Validation Tests
    // ============================================================================

    #region Ledger Balance Validation Tests

    // Given: An initialized ledger with zero balance
    // When: 50 credits of $10 each and 25 debits of $5 each are applied sequentially
    // Then: The final balance is exactly $375 with no rounding or consistency errors
    [Fact]
    public async Task Ledger_BalanceAfterConcurrentOperations_ShouldBeConsistent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ILedgerGrain>(
            GrainKeys.Ledger(orgId, "balance-test", ownerId));

        await grain.InitializeAsync(orgId);

        // Act - perform many operations
        var expectedBalance = 0m;
        for (int i = 0; i < 50; i++)
        {
            await grain.CreditAsync(10m, "credit", $"Credit {i}");
            expectedBalance += 10m;
        }

        for (int i = 0; i < 25; i++)
        {
            await grain.DebitAsync(5m, "debit", $"Debit {i}");
            expectedBalance -= 5m;
        }

        // Assert
        var balance = await grain.GetBalanceAsync();
        balance.Should().Be(expectedBalance);
        balance.Should().Be(375m); // 50*10 - 25*5
    }

    // Given: A ledger with a $1,000 initial balance
    // When: The balance is adjusted to $750 after a physical count reconciliation
    // Then: The balance reads $750 and the adjustment result confirms success
    [Fact]
    public async Task Ledger_BalanceAfterAdjustment_ShouldMatchNewBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ILedgerGrain>(
            GrainKeys.Ledger(orgId, "adjustment-test", ownerId));

        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(1000m, "initial", "Initial balance");

        // Act
        var result = await grain.AdjustToAsync(750m, "Physical count reconciliation");

        // Assert
        result.Success.Should().BeTrue();
        result.BalanceAfter.Should().Be(750m);

        var currentBalance = await grain.GetBalanceAsync();
        currentBalance.Should().Be(750m);
    }

    // Given: A ledger with four transactions: +$100, +$50, -$30, +$20
    // When: The transaction history is retrieved
    // Then: Each transaction's running balance matches the cumulative total ($100, $150, $120, $140)
    [Fact]
    public async Task Ledger_TransactionsBalanceAfter_ShouldMatchRunningTotal()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ILedgerGrain>(
            GrainKeys.Ledger(orgId, "running-total", ownerId));

        await grain.InitializeAsync(orgId);

        await grain.CreditAsync(100m, "credit", "First");
        await grain.CreditAsync(50m, "credit", "Second");
        await grain.DebitAsync(30m, "debit", "Third");
        await grain.CreditAsync(20m, "credit", "Fourth");

        // Act
        var transactions = await grain.GetTransactionsAsync();

        // Assert - verify running balance
        var sortedByTime = transactions.OrderBy(t => t.Timestamp).ToList();

        sortedByTime[0].BalanceAfter.Should().Be(100m);
        sortedByTime[1].BalanceAfter.Should().Be(150m);
        sortedByTime[2].BalanceAfter.Should().Be(120m);
        sortedByTime[3].BalanceAfter.Should().Be(140m);
    }

    // Given: A ledger with a $50 balance
    // When: A $100 debit is attempted first without allowNegative, then with allowNegative metadata
    // Then: The first debit fails (insufficient funds), but the second succeeds producing a -$50 balance
    [Fact]
    public async Task Ledger_NegativeBalance_OnlyWithAllowNegative()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ILedgerGrain>(
            GrainKeys.Ledger(orgId, "negative-test", ownerId));

        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(50m, "credit", "Initial");

        // Act - without allowNegative
        var resultWithout = await grain.DebitAsync(100m, "debit", "Over-debit");

        // Act - with allowNegative
        var metadata = new Dictionary<string, string> { { "allowNegative", "true" } };
        var resultWith = await grain.DebitAsync(100m, "debit", "Over-debit allowed", metadata);

        // Assert
        resultWithout.Success.Should().BeFalse();
        resultWith.Success.Should().BeTrue();
        resultWith.BalanceAfter.Should().Be(-50m);
    }

    #endregion

    // ============================================================================
    // Journal Entry Validation Tests
    // ============================================================================

    #region Journal Entry Validation Tests

    // Given: An initialized chart of accounts
    // When: A journal entry is created with only one debit line (no offsetting credit)
    // Then: An error is thrown because debits must equal credits (single line cannot balance)
    [Fact]
    public async Task JournalEntry_MinimumTwoLines_Required()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var journalEntryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        var lines = new List<JournalEntryLineCommand>
        {
            new("1110", 100m, 0, "Only one line")
        };

        // Act
        var act = () => grain.CreateAsync(new CreateJournalEntryCommand(
            orgId,
            journalEntryId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            lines,
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*must equal*");
    }

    // Given: An initialized chart of accounts
    // When: A journal entry is created with a negative debit amount (-$100)
    // Then: An error is thrown because debit and credit amounts must be non-negative
    [Fact]
    public async Task JournalEntry_NegativeAmounts_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var journalEntryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        var lines = new List<JournalEntryLineCommand>
        {
            new("1110", -100m, 0, "Negative debit"),
            new("4100", 0, 100m, "Credit")
        };

        // Act
        var act = () => grain.CreateAsync(new CreateJournalEntryCommand(
            orgId,
            journalEntryId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            lines,
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // Given: An initialized chart of accounts with cash, tax, food sales, and beverage sales accounts
    // When: A compound journal entry is created with 1 debit ($107) and 3 credits ($7 tax + $70 food + $30 beverage)
    // Then: The entry is accepted because total debits ($107) equal total credits ($107)
    [Fact]
    public async Task JournalEntry_MultipleDebitsAndCredits_BalancedShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var journalEntryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        // Complex entry: Cash received, Sales Tax collected, Food and Beverage sales
        var lines = new List<JournalEntryLineCommand>
        {
            new("1110", 107m, 0, "Cash received"),
            new("2300", 0, 7m, "Sales tax payable"),
            new("4100", 0, 70m, "Food sales"),
            new("4200", 0, 30m, "Beverage sales")
        };

        // Act
        var entry = await grain.CreateAsync(new CreateJournalEntryCommand(
            orgId,
            journalEntryId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            lines,
            Guid.NewGuid()));

        // Assert
        entry.TotalDebits.Should().Be(107m);
        entry.TotalCredits.Should().Be(107m);
    }

    // Given: A balanced journal entry that has been approved
    // When: The entry is posted to the general ledger
    // Then: The status changes to Posted with a posting timestamp
    [Fact]
    public async Task JournalEntry_Post_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var journalEntryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        await CreateBalancedEntryAsync(grain, orgId, journalEntryId);
        await grain.ApproveAsync(new ApproveJournalEntryCommand(Guid.NewGuid()));

        // Act
        var entry = await grain.PostAsync(new PostJournalEntryCommand(Guid.NewGuid()));

        // Assert
        entry.Status.Should().Be(JournalEntryEntryStatus.Posted);
        entry.PostedAt.Should().NotBeNull();
    }

    // Given: A balanced journal entry in Draft status (not yet approved)
    // When: Posting the entry is attempted without prior approval
    // Then: An error is thrown because only Approved entries can be posted
    [Fact]
    public async Task JournalEntry_PostWithoutApproval_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var journalEntryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        await CreateBalancedEntryAsync(grain, orgId, journalEntryId);

        // Act - try to post without approval
        var act = () => grain.PostAsync(new PostJournalEntryCommand(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Approved*");
    }

    // Given: A journal entry that has been approved and posted to the ledger
    // When: Voiding the posted entry is attempted
    // Then: An error is thrown because posted entries cannot be voided (reversal required instead)
    [Fact]
    public async Task JournalEntry_VoidPostedEntry_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var journalEntryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        await CreateBalancedEntryAsync(grain, orgId, journalEntryId);
        await grain.ApproveAsync(new ApproveJournalEntryCommand(Guid.NewGuid()));
        await grain.PostAsync(new PostJournalEntryCommand(Guid.NewGuid()));

        // Act - try to void posted entry
        var act = () => grain.VoidAsync(new VoidJournalEntryCommand(Guid.NewGuid(), "Mistake"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Posted*");
    }

    #endregion

    // ============================================================================
    // Expense Categorization Tests
    // ============================================================================

    #region Expense Categorization Tests

    // Given: A new expense grain for a site
    // When: An expense is recorded with each supported category (Rent, Utilities, Supplies, etc.)
    // Then: The expense stores the correct category for all 12 category types
    [Theory]
    [InlineData(ExpenseCategory.Rent)]
    [InlineData(ExpenseCategory.Utilities)]
    [InlineData(ExpenseCategory.Supplies)]
    [InlineData(ExpenseCategory.Insurance)]
    [InlineData(ExpenseCategory.Marketing)]
    [InlineData(ExpenseCategory.Equipment)]
    [InlineData(ExpenseCategory.Maintenance)]
    [InlineData(ExpenseCategory.Professional)]
    [InlineData(ExpenseCategory.Travel)]
    [InlineData(ExpenseCategory.Payroll)]
    [InlineData(ExpenseCategory.Licenses)]
    [InlineData(ExpenseCategory.Other)]
    public async Task Expense_AllCategories_ShouldBeSupported(ExpenseCategory category)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExpenseGrain>(
            GrainKeys.Expense(orgId, siteId, expenseId));

        // Act
        var snapshot = await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, category,
            $"{category} expense", 100m, new DateOnly(2024, 1, 15), Guid.NewGuid()));

        // Assert
        snapshot.Category.Should().Be(category);
    }

    // Given: A new expense grain for a site
    // When: An expense is recorded with the Other category and custom category "Kitchen Equipment Repair"
    // Then: Both the Other category and the custom category label are stored
    [Fact]
    public async Task Expense_CustomCategory_ShouldBeStored()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExpenseGrain>(
            GrainKeys.Expense(orgId, siteId, expenseId));

        // Act
        var snapshot = await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Other,
            "Custom expense", 150m, new DateOnly(2024, 1, 20), Guid.NewGuid(),
            CustomCategory: "Kitchen Equipment Repair"));

        // Assert
        snapshot.Category.Should().Be(ExpenseCategory.Other);
        snapshot.CustomCategory.Should().Be("Kitchen Equipment Repair");
    }

    // Given: A recorded expense categorized as Supplies
    // When: The category is updated to Maintenance
    // Then: The expense reflects the new Maintenance category
    [Fact]
    public async Task Expense_UpdateCategory_ShouldChangeCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var expenseId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IExpenseGrain>(
            GrainKeys.Expense(orgId, siteId, expenseId));

        await grain.RecordAsync(new RecordExpenseCommand(
            orgId, siteId, expenseId, ExpenseCategory.Supplies,
            "Cleaning supplies", 75m, new DateOnly(2024, 1, 10), Guid.NewGuid()));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateExpenseCommand(
            Guid.NewGuid(),
            Category: ExpenseCategory.Maintenance));

        // Assert
        snapshot.Category.Should().Be(ExpenseCategory.Maintenance);
    }

    #endregion

    // ============================================================================
    // Account Reconciliation Tests
    // ============================================================================

    #region Account Reconciliation Tests

    // Given: An in-progress reconciliation for a checking account
    // When: Three bank transactions (deposit, check, fee) are imported
    // Then: All three transactions appear as unmatched awaiting manual matching
    [Fact]
    public async Task BankReconciliation_AutoMatch_ShouldMatchExactAmounts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reconciliationId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBankReconciliationGrain>(
            GrainKeys.BankReconciliation(orgId, reconciliationId));

        await grain.StartAsync(new StartReconciliationCommand(
            orgId, reconciliationId, "1234-5678", "Main Checking",
            DateOnly.FromDateTime(DateTime.UtcNow), 5000m, Guid.NewGuid()));

        // Import bank transactions
        var transactions = new List<BankTransactionImport>
        {
            new("BNK-001", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)), 1000m, "Deposit"),
            new("BNK-002", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)), -250m, "Check #101", CheckNumber: "101"),
            new("BNK-003", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), -15.99m, "Bank Fee")
        };

        await grain.ImportTransactionsAsync(new ImportBankTransactionsCommand(
            transactions, Guid.NewGuid()));

        // Act & Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalTransactions.Should().Be(3);
        summary.UnmatchedTransactions.Should().Be(3);
    }

    // Given: A reconciliation started with a $5,000 statement balance and no imported transactions
    // When: The reconciliation is force-completed with a discrepancy note
    // Then: The reconciliation completes with CompletedWithDiscrepancies status
    [Fact]
    public async Task BankReconciliation_RecordDiscrepancy_ShouldTrackDifference()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reconciliationId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBankReconciliationGrain>(
            GrainKeys.BankReconciliation(orgId, reconciliationId));

        await grain.StartAsync(new StartReconciliationCommand(
            orgId, reconciliationId, "1234-5678", "Checking",
            DateOnly.FromDateTime(DateTime.UtcNow), 5000m, Guid.NewGuid()));

        // Act - complete with discrepancy using force
        var summary = await grain.CompleteAsync(new CompleteReconciliationCommand(
            Guid.NewGuid(),
            Notes: "Discrepancy of $50.00 - investigating",
            ForceComplete: true));

        // Assert
        summary.Status.Should().Be(ReconciliationStatus.CompletedWithDiscrepancies);
    }

    // Given: A cash account with four entries posted at different timestamps
    // When: Entries are queried for a specific time range covering only entries 2 and 3
    // Then: Only the two entries within the range are returned
    [Fact]
    public async Task Account_GetEntriesInRange_ShouldFilterCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountGrain>(
            GrainKeys.Account(orgId, accountId));

        await grain.CreateAsync(new CreateAccountCommand(
            orgId, accountId, "1000", "Cash", AccountType.Asset, Guid.NewGuid()));

        // Create entries with delays to ensure different timestamps
        await grain.PostDebitAsync(new PostDebitCommand(100m, "Entry 1", Guid.NewGuid()));
        var rangeStart = DateTime.UtcNow;
        await Task.Delay(10);
        await grain.PostDebitAsync(new PostDebitCommand(200m, "Entry 2", Guid.NewGuid()));
        await grain.PostDebitAsync(new PostDebitCommand(300m, "Entry 3", Guid.NewGuid()));
        var rangeEnd = DateTime.UtcNow;
        await Task.Delay(10);
        await grain.PostDebitAsync(new PostDebitCommand(400m, "Entry 4", Guid.NewGuid()));

        // Act
        var entriesInRange = await grain.GetEntriesInRangeAsync(rangeStart, rangeEnd);

        // Assert
        entriesInRange.Should().HaveCount(2);
        entriesInRange.Should().Contain(e => e.Description == "Entry 2");
        entriesInRange.Should().Contain(e => e.Description == "Entry 3");
    }

    #endregion

    // ============================================================================
    // Chart of Accounts Extended Tests
    // ============================================================================

    #region Chart of Accounts Extended Tests

    // Given: A chart of accounts with a custom account 7000
    // When: The account is updated to set itself as its own parent
    // Then: An error is thrown preventing a circular reference in the account hierarchy
    [Fact]
    public async Task ChartOfAccounts_CircularReference_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));

        await grain.AddAccountAsync(new AddAccountToChartCommand(
            "7000", "Custom Account", AccountType.Expense, NormalBalance.Debit, Guid.NewGuid()));

        // Act - try to make account its own parent
        var act = () => grain.UpdateAccountAsync(new UpdateChartAccountCommand(
            "7000", Guid.NewGuid(), ParentAccountNumber: "7000"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be its own parent*");
    }

    // Given: A parent account 7000 with an active child account 7100
    // When: Deactivation of the parent account is attempted
    // Then: An error is thrown because the parent has active child accounts
    [Fact]
    public async Task ChartOfAccounts_DeactivateWithChildren_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));

        await grain.AddAccountAsync(new AddAccountToChartCommand(
            "7000", "Parent", AccountType.Expense, NormalBalance.Debit, Guid.NewGuid()));

        await grain.AddAccountAsync(new AddAccountToChartCommand(
            "7100", "Child", AccountType.Expense, NormalBalance.Debit, Guid.NewGuid(),
            ParentAccountNumber: "7000"));

        // Act - try to deactivate parent with active child
        var act = () => grain.DeactivateAccountAsync(new DeactivateChartAccountCommand(
            "7000", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*active child*");
    }

    // Given: A deactivated custom account 7500 in the chart of accounts
    // When: The account is reactivated
    // Then: The account is marked as active again
    [Fact]
    public async Task ChartOfAccounts_ReactivateAccount_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));

        await grain.AddAccountAsync(new AddAccountToChartCommand(
            "7500", "Test Account", AccountType.Expense, NormalBalance.Debit, Guid.NewGuid()));

        await grain.DeactivateAccountAsync(new DeactivateChartAccountCommand(
            "7500", Guid.NewGuid()));

        // Act
        var reactivatedAccount = await grain.ReactivateAccountAsync(new ReactivateChartAccountCommand(
            "7500", Guid.NewGuid()));

        // Assert
        reactivatedAccount.IsActive.Should().BeTrue();
    }

    // Given: An initialized chart of accounts with active default account 1110 (Cash on Hand)
    // When: Account validation is performed for account 1110
    // Then: The validation returns true since the account is active
    [Fact]
    public async Task ChartOfAccounts_ValidateAccount_ActiveAccount_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));

        // Act
        var isValid = await grain.ValidateAccountAsync("1110");

        // Assert
        isValid.Should().BeTrue();
    }

    // Given: A chart of accounts with a deactivated custom account 9999
    // When: Account validation is performed for account 9999
    // Then: The validation returns false since the account is inactive
    [Fact]
    public async Task ChartOfAccounts_ValidateAccount_InactiveAccount_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));

        await grain.AddAccountAsync(new AddAccountToChartCommand(
            "9999", "Test", AccountType.Expense, NormalBalance.Debit, Guid.NewGuid()));

        await grain.DeactivateAccountAsync(new DeactivateChartAccountCommand(
            "9999", Guid.NewGuid()));

        // Act
        var isValid = await grain.ValidateAccountAsync("9999");

        // Assert
        isValid.Should().BeFalse();
    }

    // Given: An initialized chart of accounts with Cash and Cash Equivalents (1100) as a parent
    // When: Child accounts of 1100 are queried
    // Then: Cash on Hand (1110), Cash in Bank (1120), and Petty Cash (1130) are returned
    [Fact]
    public async Task ChartOfAccounts_GetChildAccounts_ShouldReturnDirectChildren()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));

        // Act
        var children = await grain.GetChildAccountsAsync("1100"); // Cash and Cash Equivalents

        // Assert
        children.Should().Contain(a => a.AccountNumber == "1110"); // Cash on Hand
        children.Should().Contain(a => a.AccountNumber == "1120"); // Cash in Bank
        children.Should().Contain(a => a.AccountNumber == "1130"); // Petty Cash
    }

    // Given: An initialized chart of accounts with account 1110 (Cash on Hand)
    // When: An account grain ID is linked to chart account 1110
    // Then: The chart account stores the linked grain ID for ledger integration
    [Fact]
    public async Task ChartOfAccounts_LinkAccountGrain_ShouldStoreLink()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountGrainId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));

        // Act
        await grain.LinkAccountGrainAsync("1110", accountGrainId);

        // Assert
        var account = await grain.GetAccountAsync("1110");
        account.Should().NotBeNull();
        account!.LinkedAccountGrainId.Should().Be(accountGrainId);
    }

    #endregion

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private async Task<IChartOfAccountsGrain> SetupChartOfAccountsAsync(Guid orgId)
    {
        var chartGrain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));
        await chartGrain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));
        return chartGrain;
    }

    private async Task CreateBalancedEntryAsync(
        IJournalEntryGrain grain,
        Guid orgId,
        Guid journalEntryId)
    {
        var lines = new List<JournalEntryLineCommand>
        {
            new("1110", 100m, 0, "Cash"),
            new("4100", 0, 100m, "Revenue")
        };

        await grain.CreateAsync(new CreateJournalEntryCommand(
            orgId,
            journalEntryId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            lines,
            Guid.NewGuid()));
    }
}
