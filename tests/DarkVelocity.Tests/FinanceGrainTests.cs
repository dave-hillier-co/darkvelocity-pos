using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class FinanceGrainTests
{
    private readonly TestClusterFixture _fixture;

    public FinanceGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================================================
    // Chart of Accounts Tests
    // ============================================================================

    #region Chart of Accounts Tests

    // Given: A new, uninitialized chart of accounts for an organization
    // When: The chart is initialized with default accounts and USD currency
    // Then: The chart contains the standard default accounts with USD as the reporting currency
    [Fact]
    public async Task ChartOfAccounts_Initialize_ShouldCreateDefaultAccounts()
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
            Currency: "USD"));

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.OrganizationId.Should().Be(orgId);
        summary.TotalAccounts.Should().BeGreaterThan(0);
        summary.Currency.Should().Be("USD");
    }

    // Given: A chart of accounts that has already been initialized
    // When: A second initialization is attempted
    // Then: An error is thrown because the chart is already initialized
    [Fact]
    public async Task ChartOfAccounts_Initialize_AlreadyInitialized_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(
            orgId, Guid.NewGuid()));

        // Act
        var act = () => grain.InitializeAsync(new InitializeChartOfAccountsCommand(
            orgId, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already initialized*");
    }

    // Given: An initialized chart of accounts with default accounts
    // When: A custom expense account (7000) is added under the expenses parent (6000)
    // Then: The new account appears in the chart with the correct type, name, and active status
    [Fact]
    public async Task ChartOfAccounts_AddAccount_ShouldAddToChart()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));

        // Act
        var account = await grain.AddAccountAsync(new AddAccountToChartCommand(
            "7000",
            "Custom Expense",
            AccountType.Expense,
            NormalBalance.Debit,
            Guid.NewGuid(),
            ParentAccountNumber: "6000",
            Description: "A custom expense category"));

        // Assert
        account.AccountNumber.Should().Be("7000");
        account.Name.Should().Be("Custom Expense");
        account.AccountType.Should().Be(AccountType.Expense);
        account.IsActive.Should().BeTrue();
    }

    // Given: An initialized chart of accounts with default account 1000 (Assets)
    // When: An account with the duplicate number 1000 is added
    // Then: An error is thrown because the account number already exists
    [Fact]
    public async Task ChartOfAccounts_AddAccount_DuplicateNumber_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));

        // Act - try to add account with existing number
        var act = () => grain.AddAccountAsync(new AddAccountToChartCommand(
            "1000", // Already exists in defaults
            "Duplicate",
            AccountType.Asset,
            NormalBalance.Debit,
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    // Given: A chart of accounts with a custom account 8000 (Test Account)
    // When: The custom account is deactivated with a reason
    // Then: The account is marked as inactive in the chart
    [Fact]
    public async Task ChartOfAccounts_DeactivateAccount_ShouldDeactivate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));

        await grain.AddAccountAsync(new AddAccountToChartCommand(
            "8000", "Test Account", AccountType.Expense, NormalBalance.Debit, Guid.NewGuid()));

        // Act
        await grain.DeactivateAccountAsync(new DeactivateChartAccountCommand(
            "8000", Guid.NewGuid(), "No longer needed"));

        // Assert
        var account = await grain.GetAccountAsync("8000");
        account.Should().NotBeNull();
        account!.IsActive.Should().BeFalse();
    }

    // Given: An initialized chart of accounts with system account 1000 (Assets)
    // When: Deactivation of the system account is attempted
    // Then: An error is thrown because system accounts cannot be deactivated
    [Fact]
    public async Task ChartOfAccounts_DeactivateSystemAccount_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));

        // Act - try to deactivate system account
        var act = () => grain.DeactivateAccountAsync(new DeactivateChartAccountCommand(
            "1000", // Assets (system account)
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*System accounts*");
    }

    // Given: An initialized chart of accounts with parent and child accounts
    // When: The account hierarchy is retrieved
    // Then: The Assets account (1000) has child accounts nested under it
    [Fact]
    public async Task ChartOfAccounts_GetHierarchy_ShouldReturnNestedStructure()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));

        // Act
        var hierarchy = await grain.GetHierarchyAsync();

        // Assert
        hierarchy.Should().NotBeEmpty();
        // Assets (1000) should have children
        var assets = hierarchy.FirstOrDefault(h => h.Account.AccountNumber == "1000");
        assets.Should().NotBeNull();
        assets!.Children.Should().NotBeEmpty();
    }

    // Given: An initialized chart of accounts with various account types
    // When: Accounts are filtered by the Revenue type
    // Then: Only revenue accounts are returned
    [Fact]
    public async Task ChartOfAccounts_GetAccountsByType_ShouldFilterCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));

        await grain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));

        // Act
        var revenueAccounts = await grain.GetAccountsByTypeAsync(AccountType.Revenue);

        // Assert
        revenueAccounts.Should().NotBeEmpty();
        revenueAccounts.All(a => a.AccountType == AccountType.Revenue).Should().BeTrue();
    }

    #endregion

    // ============================================================================
    // Journal Entry Tests
    // ============================================================================

    #region Journal Entry Tests

    private async Task<IChartOfAccountsGrain> SetupChartOfAccountsAsync(Guid orgId)
    {
        var chartGrain = _fixture.Cluster.GrainFactory.GetGrain<IChartOfAccountsGrain>(
            GrainKeys.ChartOfAccounts(orgId));
        await chartGrain.InitializeAsync(new InitializeChartOfAccountsCommand(orgId, Guid.NewGuid()));
        return chartGrain;
    }

    // Given: An initialized chart of accounts with cash (1110) and food sales (4100) accounts
    // When: A balanced journal entry is created debiting cash $100 and crediting food sales $100
    // Then: The entry is created in Draft status with equal debits and credits
    [Fact]
    public async Task JournalEntry_Create_BalancedEntry_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var journalEntryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        var lines = new List<JournalEntryLineCommand>
        {
            new("1110", 100m, 0, "Cash received"),
            new("4100", 0, 100m, "Food sales")
        };

        // Act
        var entry = await grain.CreateAsync(new CreateJournalEntryCommand(
            orgId,
            journalEntryId,
            DateOnly.FromDateTime(DateTime.UtcNow),
            lines,
            Guid.NewGuid(),
            "Test entry"));

        // Assert
        entry.JournalEntryId.Should().Be(journalEntryId);
        entry.TotalDebits.Should().Be(100m);
        entry.TotalCredits.Should().Be(100m);
        entry.Status.Should().Be(JournalEntryEntryStatus.Draft);
    }

    // Given: An initialized chart of accounts
    // When: An unbalanced journal entry is created with $100 debit and only $50 credit
    // Then: An error is thrown because debits must equal credits
    [Fact]
    public async Task JournalEntry_Create_UnbalancedEntry_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var journalEntryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        var lines = new List<JournalEntryLineCommand>
        {
            new("1110", 100m, 0, "Cash"),
            new("4100", 0, 50m, "Sales") // Unbalanced!
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
            .WithMessage("*Debits*must equal*Credits*");
    }

    // Given: An initialized chart of accounts
    // When: A journal entry line has both a $100 debit and $50 credit on the same line
    // Then: An error is thrown because a single line cannot have both debit and credit amounts
    [Fact]
    public async Task JournalEntry_Create_LineWithBothDebitAndCredit_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var journalEntryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        var lines = new List<JournalEntryLineCommand>
        {
            new("1110", 100m, 50m, "Invalid line") // Both debit and credit!
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
            .WithMessage("*cannot have both debit and credit*");
    }

    // Given: An initialized chart of accounts
    // When: A journal entry line is created with both debit and credit set to zero
    // Then: An error is thrown because at least one amount must be non-zero
    [Fact]
    public async Task JournalEntry_Create_ZeroAmounts_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var journalEntryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        var lines = new List<JournalEntryLineCommand>
        {
            new("1110", 0, 0, "No amounts")
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

    // Given: An initialized chart of accounts without account 9999
    // When: A journal entry is created referencing non-existent account 9999
    // Then: An error is thrown because the account is not found or inactive
    [Fact]
    public async Task JournalEntry_Create_InvalidAccount_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var journalEntryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        var lines = new List<JournalEntryLineCommand>
        {
            new("9999", 100m, 0, "Invalid account"),
            new("4100", 0, 100m, "Sales")
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
            .WithMessage("*not found or inactive*");
    }

    // Given: A balanced journal entry in Draft status
    // When: The entry is approved by a reviewer
    // Then: The status changes to Approved with the approval timestamp and approver recorded
    [Fact]
    public async Task JournalEntry_Approve_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var journalEntryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        await CreateBalancedEntryAsync(grain, orgId, journalEntryId);

        // Act
        var entry = await grain.ApproveAsync(new ApproveJournalEntryCommand(
            Guid.NewGuid(), "Approved"));

        // Assert
        entry.Status.Should().Be(JournalEntryEntryStatus.Approved);
        entry.ApprovedAt.Should().NotBeNull();
        entry.ApprovedBy.Should().NotBeEmpty();
    }

    // Given: A balanced journal entry in Draft status
    // When: The entry is rejected with reason "Incorrect amount"
    // Then: The status changes to Rejected
    [Fact]
    public async Task JournalEntry_Reject_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var journalEntryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        await CreateBalancedEntryAsync(grain, orgId, journalEntryId);

        // Act
        var entry = await grain.RejectAsync(new RejectJournalEntryCommand(
            Guid.NewGuid(), "Incorrect amount"));

        // Assert
        entry.Status.Should().Be(JournalEntryEntryStatus.Rejected);
    }

    // Given: A balanced journal entry in Draft status that has not been posted
    // When: The entry is voided with reason "Entry made in error"
    // Then: The entry status changes to Voided
    [Fact]
    public async Task JournalEntry_Void_UnpostedEntry_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var journalEntryId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IJournalEntryGrain>(
            GrainKeys.JournalEntry(orgId, journalEntryId));

        await CreateBalancedEntryAsync(grain, orgId, journalEntryId);

        // Act
        await grain.VoidAsync(new VoidJournalEntryCommand(
            Guid.NewGuid(), "Entry made in error"));

        // Assert
        var status = await grain.GetStatusAsync();
        status.Should().Be(JournalEntryEntryStatus.Voided);
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

    #endregion

    // ============================================================================
    // Accounting Period Tests
    // ============================================================================

    #region Accounting Period Tests

    // Given: A new accounting period grain for a fiscal year
    // When: The fiscal year is initialized with monthly period frequency
    // Then: 12 monthly periods are created for the year
    [Fact]
    public async Task AccountingPeriod_Initialize_Monthly_ShouldCreate12Periods()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        // Act
        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId,
            year,
            Guid.NewGuid(),
            FiscalYearStartMonth: 1,
            Frequency: PeriodFrequency.Monthly));

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalPeriods.Should().Be(12);
        summary.Frequency.Should().Be(PeriodFrequency.Monthly);
    }

    // Given: A new accounting period grain for a fiscal year
    // When: The fiscal year is initialized with quarterly period frequency
    // Then: 4 quarterly periods are created for the year
    [Fact]
    public async Task AccountingPeriod_Initialize_Quarterly_ShouldCreate4Periods()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        // Act
        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId,
            year,
            Guid.NewGuid(),
            FiscalYearStartMonth: 1,
            Frequency: PeriodFrequency.Quarterly));

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.TotalPeriods.Should().Be(4);
    }

    // Given: An initialized fiscal year with monthly periods, all in NotStarted status
    // When: Period 1 (January) is opened
    // Then: The period status changes to Open with an opening timestamp
    [Fact]
    public async Task AccountingPeriod_OpenPeriod_ShouldOpenPeriod()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid()));

        // Act
        var period = await grain.OpenPeriodAsync(new OpenPeriodCommand(
            1, Guid.NewGuid()));

        // Assert
        period.Status.Should().Be(PeriodStatus.Open);
        period.OpenedAt.Should().NotBeNull();
    }

    // Given: An initialized fiscal year with period 1 still not opened
    // When: Period 2 is opened without first opening period 1
    // Then: An error is thrown enforcing sequential period opening
    [Fact]
    public async Task AccountingPeriod_OpenPeriod_SkipPeriod_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid()));

        // Act - try to open period 2 without opening period 1
        var act = () => grain.OpenPeriodAsync(new OpenPeriodCommand(
            2, Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*before opening period 1*");
    }

    // Given: An open accounting period 1 (January)
    // When: The period is closed
    // Then: The period status changes to Closed with a closing timestamp
    [Fact]
    public async Task AccountingPeriod_ClosePeriod_ShouldClosePeriod()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid()));

        await grain.OpenPeriodAsync(new OpenPeriodCommand(1, Guid.NewGuid()));

        // Act
        var period = await grain.ClosePeriodAsync(new ClosePeriodCommand2(
            1, Guid.NewGuid()));

        // Assert
        period.Status.Should().Be(PeriodStatus.Closed);
        period.ClosedAt.Should().NotBeNull();
    }

    // Given: A closed accounting period 1 that needs adjustment entries
    // When: The period is reopened with reason "Need to add adjustment"
    // Then: The period status returns to Open
    [Fact]
    public async Task AccountingPeriod_ReopenPeriod_ShouldReopenClosedPeriod()
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
        var period = await grain.ReopenPeriodAsync(new ReopenPeriodCommand(
            1, Guid.NewGuid(), "Need to add adjustment"));

        // Assert
        period.Status.Should().Be(PeriodStatus.Open);
    }

    // Given: A closed accounting period 1
    // When: The period is permanently locked
    // Then: The period status changes to Locked, preventing further modifications
    [Fact]
    public async Task AccountingPeriod_LockPeriod_ShouldPermanentlyLock()
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
        var period = await grain.LockPeriodAsync(new LockPeriodCommand(
            1, Guid.NewGuid()));

        // Assert
        period.Status.Should().Be(PeriodStatus.Locked);
    }

    // Given: A permanently locked accounting period 1
    // When: Reopening the locked period is attempted
    // Then: An error is thrown because locked periods cannot be reopened
    [Fact]
    public async Task AccountingPeriod_ReopenLockedPeriod_ShouldFail()
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
        await grain.LockPeriodAsync(new LockPeriodCommand(1, Guid.NewGuid()));

        // Act
        var act = () => grain.ReopenPeriodAsync(new ReopenPeriodCommand(
            1, Guid.NewGuid(), "Try to reopen"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not closed*"); // Because it's Locked, not Closed
    }

    // Given: An open accounting period 1 covering January
    // When: A posting eligibility check is performed for January 15
    // Then: The check returns true since the period is open
    [Fact]
    public async Task AccountingPeriod_CanPostToDate_OpenPeriod_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var year = DateTime.UtcNow.Year;
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IAccountingPeriodGrain>(
            GrainKeys.AccountingPeriod(orgId, year));

        await grain.InitializeAsync(new InitializeFiscalYearCommand(
            orgId, year, Guid.NewGuid()));

        await grain.OpenPeriodAsync(new OpenPeriodCommand(1, Guid.NewGuid()));

        // Act
        var canPost = await grain.CanPostToDateAsync(new DateOnly(year, 1, 15));

        // Assert
        canPost.Should().BeTrue();
    }

    // Given: A closed accounting period 1 covering January
    // When: A posting eligibility check is performed for January 15
    // Then: The check returns false since the period is closed
    [Fact]
    public async Task AccountingPeriod_CanPostToDate_ClosedPeriod_ShouldReturnFalse()
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
        var canPost = await grain.CanPostToDateAsync(new DateOnly(year, 1, 15));

        // Assert
        canPost.Should().BeFalse();
    }

    #endregion

    // ============================================================================
    // Bank Reconciliation Tests
    // ============================================================================

    #region Bank Reconciliation Tests

    // Given: A new bank reconciliation grain
    // When: A reconciliation is started for checking account 1234-5678 with a $10,000 statement ending balance
    // Then: The reconciliation is created in InProgress status with the bank account and balance details
    [Fact]
    public async Task BankReconciliation_Start_ShouldCreateReconciliation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reconciliationId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBankReconciliationGrain>(
            GrainKeys.BankReconciliation(orgId, reconciliationId));

        // Act
        var summary = await grain.StartAsync(new StartReconciliationCommand(
            orgId,
            reconciliationId,
            "1234-5678",
            "Business Checking",
            DateOnly.FromDateTime(DateTime.UtcNow),
            10000m,
            Guid.NewGuid()));

        // Assert
        summary.ReconciliationId.Should().Be(reconciliationId);
        summary.BankAccountNumber.Should().Be("1234-5678");
        summary.StatementEndingBalance.Should().Be(10000m);
        summary.Status.Should().Be(ReconciliationStatus.InProgress);
    }

    // Given: An in-progress bank reconciliation
    // When: Three bank transactions (deposit, check, fee) are imported from CSV
    // Then: All three transactions are added as unmatched entries
    [Fact]
    public async Task BankReconciliation_ImportTransactions_ShouldAddTransactions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reconciliationId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBankReconciliationGrain>(
            GrainKeys.BankReconciliation(orgId, reconciliationId));

        await grain.StartAsync(new StartReconciliationCommand(
            orgId, reconciliationId, "1234-5678", "Checking",
            DateOnly.FromDateTime(DateTime.UtcNow), 10000m, Guid.NewGuid()));

        var transactions = new List<BankTransactionImport>
        {
            new("TXN-001", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)), 500m, "Deposit"),
            new("TXN-002", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)), -100m, "Check #123", CheckNumber: "123"),
            new("TXN-003", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), -50m, "Bank Fee")
        };

        // Act
        var summary = await grain.ImportTransactionsAsync(new ImportBankTransactionsCommand(
            transactions, Guid.NewGuid(), "CSV Import"));

        // Assert
        summary.TotalTransactions.Should().Be(3);
        summary.UnmatchedTransactions.Should().Be(3);
    }

    // Given: A reconciliation with one imported unmatched bank transaction
    // When: The transaction is matched to a journal entry
    // Then: The matched count increases to 1 and unmatched count drops to 0
    [Fact]
    public async Task BankReconciliation_MatchTransaction_ShouldUpdateStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reconciliationId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBankReconciliationGrain>(
            GrainKeys.BankReconciliation(orgId, reconciliationId));

        await grain.StartAsync(new StartReconciliationCommand(
            orgId, reconciliationId, "1234-5678", "Checking",
            DateOnly.FromDateTime(DateTime.UtcNow), 10000m, Guid.NewGuid()));

        await grain.ImportTransactionsAsync(new ImportBankTransactionsCommand(
            new List<BankTransactionImport>
            {
                new("TXN-001", DateOnly.FromDateTime(DateTime.UtcNow), 500m, "Deposit")
            },
            Guid.NewGuid()));

        // Act
        await grain.MatchTransactionAsync(new MatchTransactionCommand(
            "TXN-001", Guid.NewGuid(), Guid.NewGuid(), "Matched to JE-001"));

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.MatchedTransactions.Should().Be(1);
        summary.UnmatchedTransactions.Should().Be(0);
    }

    // Given: A reconciliation with one matched bank transaction
    // When: The transaction match is reverted with reason "Wrong match"
    // Then: The matched count drops to 0 and unmatched count returns to 1
    [Fact]
    public async Task BankReconciliation_UnmatchTransaction_ShouldRevertMatch()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reconciliationId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBankReconciliationGrain>(
            GrainKeys.BankReconciliation(orgId, reconciliationId));

        await grain.StartAsync(new StartReconciliationCommand(
            orgId, reconciliationId, "1234-5678", "Checking",
            DateOnly.FromDateTime(DateTime.UtcNow), 10000m, Guid.NewGuid()));

        await grain.ImportTransactionsAsync(new ImportBankTransactionsCommand(
            new List<BankTransactionImport>
            {
                new("TXN-001", DateOnly.FromDateTime(DateTime.UtcNow), 500m, "Deposit")
            },
            Guid.NewGuid()));

        await grain.MatchTransactionAsync(new MatchTransactionCommand(
            "TXN-001", Guid.NewGuid(), Guid.NewGuid()));

        // Act
        await grain.UnmatchTransactionAsync(new UnmatchTransactionCommand(
            "TXN-001", Guid.NewGuid(), "Wrong match"));

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.MatchedTransactions.Should().Be(0);
        summary.UnmatchedTransactions.Should().Be(1);
    }

    // Given: An in-progress reconciliation with unmatched transactions creating a discrepancy
    // When: Completion is attempted without the force flag
    // Then: An error is thrown because the reconciliation is not balanced
    [Fact]
    public async Task BankReconciliation_Complete_WithDiscrepancy_WithoutForce_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reconciliationId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBankReconciliationGrain>(
            GrainKeys.BankReconciliation(orgId, reconciliationId));

        await grain.StartAsync(new StartReconciliationCommand(
            orgId, reconciliationId, "1234-5678", "Checking",
            DateOnly.FromDateTime(DateTime.UtcNow), 10000m, Guid.NewGuid()));

        // Don't match any transactions - there will be a discrepancy

        // Act
        var act = () => grain.CompleteAsync(new CompleteReconciliationCommand(
            Guid.NewGuid(), ForceComplete: false));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not balanced*");
    }

    // Given: An in-progress reconciliation with unresolved discrepancies
    // When: Completion is forced with an accepted discrepancy note
    // Then: The reconciliation completes with CompletedWithDiscrepancies status
    [Fact]
    public async Task BankReconciliation_Complete_WithForce_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reconciliationId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBankReconciliationGrain>(
            GrainKeys.BankReconciliation(orgId, reconciliationId));

        await grain.StartAsync(new StartReconciliationCommand(
            orgId, reconciliationId, "1234-5678", "Checking",
            DateOnly.FromDateTime(DateTime.UtcNow), 10000m, Guid.NewGuid()));

        // Act
        var summary = await grain.CompleteAsync(new CompleteReconciliationCommand(
            Guid.NewGuid(), Notes: "Accepted discrepancy", ForceComplete: true));

        // Assert
        summary.Status.Should().Be(ReconciliationStatus.CompletedWithDiscrepancies);
    }

    // Given: An in-progress bank reconciliation started with the wrong statement
    // When: The reconciliation is voided with reason "Started with wrong statement"
    // Then: The reconciliation status changes to Voided
    [Fact]
    public async Task BankReconciliation_Void_ShouldVoidReconciliation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var reconciliationId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IBankReconciliationGrain>(
            GrainKeys.BankReconciliation(orgId, reconciliationId));

        await grain.StartAsync(new StartReconciliationCommand(
            orgId, reconciliationId, "1234-5678", "Checking",
            DateOnly.FromDateTime(DateTime.UtcNow), 10000m, Guid.NewGuid()));

        // Act
        await grain.VoidAsync(new VoidReconciliationCommand(
            Guid.NewGuid(), "Started with wrong statement"));

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.Status.Should().Be(ReconciliationStatus.Voided);
    }

    #endregion

    // ============================================================================
    // Financial Reports Tests
    // ============================================================================

    #region Financial Reports Tests

    // Given: An initialized chart of accounts for the organization
    // When: A trial balance report is generated as of today
    // Then: The report shows balanced debits and credits for the organization
    [Fact]
    public async Task FinancialReports_TrialBalance_ShouldGenerateReport()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IFinancialReportsGrain>(
            GrainKeys.FinancialReports(orgId));

        // Act
        var report = await grain.GenerateTrialBalanceAsync(new TrialBalanceRequest(
            DateOnly.FromDateTime(DateTime.UtcNow)));

        // Assert
        report.OrganizationId.Should().Be(orgId);
        report.IsBalanced.Should().BeTrue();
        report.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: An initialized chart of accounts for the organization
    // When: An income statement is generated for the past month
    // Then: The report includes revenue and operating expense sections for the date range
    [Fact]
    public async Task FinancialReports_IncomeStatement_ShouldGenerateReport()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IFinancialReportsGrain>(
            GrainKeys.FinancialReports(orgId));

        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        var toDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var report = await grain.GenerateIncomeStatementAsync(new IncomeStatementRequest(
            fromDate, toDate));

        // Assert
        report.OrganizationId.Should().Be(orgId);
        report.FromDate.Should().Be(fromDate);
        report.ToDate.Should().Be(toDate);
        report.Revenue.Should().NotBeNull();
        report.OperatingExpenses.Should().NotBeNull();
    }

    // Given: An initialized chart of accounts for the organization
    // When: A balance sheet is generated as of today
    // Then: The report includes assets, liabilities, and equity sections
    [Fact]
    public async Task FinancialReports_BalanceSheet_ShouldGenerateReport()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        await SetupChartOfAccountsAsync(orgId);

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IFinancialReportsGrain>(
            GrainKeys.FinancialReports(orgId));

        // Act
        var report = await grain.GenerateBalanceSheetAsync(new BalanceSheetRequest(
            DateOnly.FromDateTime(DateTime.UtcNow)));

        // Assert
        report.OrganizationId.Should().Be(orgId);
        report.Assets.Should().NotBeNull();
        report.Liabilities.Should().NotBeNull();
        report.Equity.Should().NotBeNull();
    }

    // Given: A financial reports grain for an organization
    // When: A cash flow statement is generated for the past month
    // Then: The report includes operating, investing, and financing activity sections
    [Fact]
    public async Task FinancialReports_CashFlowStatement_ShouldGenerateReport()
    {
        // Arrange
        var orgId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<IFinancialReportsGrain>(
            GrainKeys.FinancialReports(orgId));

        var fromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1));
        var toDate = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        var report = await grain.GenerateCashFlowStatementAsync(new CashFlowStatementRequest(
            fromDate, toDate));

        // Assert
        report.OrganizationId.Should().Be(orgId);
        report.OperatingActivities.Should().NotBeNull();
        report.InvestingActivities.Should().NotBeNull();
        report.FinancingActivities.Should().NotBeNull();
    }

    #endregion
}
