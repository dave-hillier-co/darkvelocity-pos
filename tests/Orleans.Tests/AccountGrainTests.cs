using DarkVelocity.Orleans.Abstractions;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using FluentAssertions;

namespace DarkVelocity.Orleans.Tests;

[Collection(ClusterCollection.Name)]
public class AccountGrainTests
{
    private readonly TestClusterFixture _fixture;

    public AccountGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IAccountGrain GetAccountGrain(Guid orgId, Guid accountId)
        => _fixture.Cluster.GrainFactory.GetGrain<IAccountGrain>(GrainKeys.Account(orgId, accountId));

    private async Task<IAccountGrain> CreateAssetAccountAsync(
        Guid orgId,
        Guid accountId,
        string accountCode = "1000",
        string name = "Cash",
        decimal openingBalance = 0)
    {
        var grain = GetAccountGrain(orgId, accountId);
        await grain.CreateAsync(new CreateAccountCommand(
            orgId,
            accountId,
            accountCode,
            name,
            AccountType.Asset,
            Guid.NewGuid(),
            OpeningBalance: openingBalance));
        return grain;
    }

    private async Task<IAccountGrain> CreateRevenueAccountAsync(
        Guid orgId,
        Guid accountId,
        string accountCode = "4000",
        string name = "Sales Revenue")
    {
        var grain = GetAccountGrain(orgId, accountId);
        await grain.CreateAsync(new CreateAccountCommand(
            orgId,
            accountId,
            accountCode,
            name,
            AccountType.Revenue,
            Guid.NewGuid()));
        return grain;
    }

    #region Create Tests

    [Fact]
    public async Task CreateAsync_ShouldCreateAccount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = GetAccountGrain(orgId, accountId);

        // Act
        var result = await grain.CreateAsync(new CreateAccountCommand(
            orgId,
            accountId,
            "1000",
            "Cash on Hand",
            AccountType.Asset,
            Guid.NewGuid(),
            SubType: "Cash",
            Description: "Main cash account"));

        // Assert
        result.AccountId.Should().Be(accountId);
        result.AccountCode.Should().Be("1000");
        result.Balance.Should().Be(0);

        var state = await grain.GetStateAsync();
        state.Name.Should().Be("Cash on Hand");
        state.AccountType.Should().Be(AccountType.Asset);
        state.SubType.Should().Be("Cash");
        state.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithOpeningBalance_ShouldSetBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = GetAccountGrain(orgId, accountId);

        // Act
        var result = await grain.CreateAsync(new CreateAccountCommand(
            orgId,
            accountId,
            "1000",
            "Cash",
            AccountType.Asset,
            Guid.NewGuid(),
            OpeningBalance: 1000m));

        // Assert
        result.Balance.Should().Be(1000m);

        var entries = await grain.GetRecentEntriesAsync();
        entries.Should().HaveCount(1);
        entries[0].EntryType.Should().Be(JournalEntryType.Opening);
        entries[0].Amount.Should().Be(1000m);
    }

    [Fact]
    public async Task CreateAsync_AlreadyExists_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId);

        // Act
        var act = () => grain.CreateAsync(new CreateAccountCommand(
            orgId,
            accountId,
            "1001",
            "Another Account",
            AccountType.Asset,
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Account already exists");
    }

    [Fact]
    public async Task CreateAsync_EmptyAccountCode_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = GetAccountGrain(orgId, accountId);

        // Act
        var act = () => grain.CreateAsync(new CreateAccountCommand(
            orgId,
            accountId,
            "",
            "Cash",
            AccountType.Asset,
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Account code is required*");
    }

    #endregion

    #region Debit/Credit Tests

    [Fact]
    public async Task PostDebitAsync_AssetAccount_ShouldIncreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId, openingBalance: 1000m);

        // Act
        var result = await grain.PostDebitAsync(new PostDebitCommand(
            500m,
            "Cash received",
            Guid.NewGuid()));

        // Assert
        result.NewBalance.Should().Be(1500m);
        result.Amount.Should().Be(500m);
        result.EntryType.Should().Be(JournalEntryType.Debit);
    }

    [Fact]
    public async Task PostCreditAsync_AssetAccount_ShouldDecreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId, openingBalance: 1000m);

        // Act
        var result = await grain.PostCreditAsync(new PostCreditCommand(
            300m,
            "Cash paid out",
            Guid.NewGuid()));

        // Assert
        result.NewBalance.Should().Be(700m);
        result.Amount.Should().Be(300m);
        result.EntryType.Should().Be(JournalEntryType.Credit);
    }

    [Fact]
    public async Task PostDebitAsync_RevenueAccount_ShouldDecreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateRevenueAccountAsync(orgId, accountId);

        // First add some revenue via credit
        await grain.PostCreditAsync(new PostCreditCommand(1000m, "Sales", Guid.NewGuid()));

        // Act - debit to revenue decreases it (e.g., sales return)
        var result = await grain.PostDebitAsync(new PostDebitCommand(
            200m,
            "Sales return",
            Guid.NewGuid()));

        // Assert
        result.NewBalance.Should().Be(800m);
    }

    [Fact]
    public async Task PostCreditAsync_RevenueAccount_ShouldIncreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateRevenueAccountAsync(orgId, accountId);

        // Act
        var result = await grain.PostCreditAsync(new PostCreditCommand(
            1500m,
            "Sales revenue",
            Guid.NewGuid()));

        // Assert
        result.NewBalance.Should().Be(1500m);
    }

    [Fact]
    public async Task PostDebitAsync_WithReference_ShouldStoreReference()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId);
        var orderId = Guid.NewGuid();

        // Act
        var result = await grain.PostDebitAsync(new PostDebitCommand(
            100m,
            "Order payment",
            Guid.NewGuid(),
            ReferenceNumber: "INV-001",
            ReferenceType: "Order",
            ReferenceId: orderId));

        // Assert
        var entry = await grain.GetEntryAsync(result.EntryId);
        entry.Should().NotBeNull();
        entry!.ReferenceNumber.Should().Be("INV-001");
        entry.ReferenceType.Should().Be("Order");
        entry.ReferenceId.Should().Be(orderId);
    }

    [Fact]
    public async Task PostDebitAsync_ZeroAmount_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId);

        // Act
        var act = () => grain.PostDebitAsync(new PostDebitCommand(
            0m,
            "Invalid",
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Amount must be positive*");
    }

    [Fact]
    public async Task PostDebitAsync_InactiveAccount_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId);
        await grain.DeactivateAsync(Guid.NewGuid());

        // Act
        var act = () => grain.PostDebitAsync(new PostDebitCommand(
            100m,
            "Should fail",
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Account is not active");
    }

    #endregion

    #region Adjustment Tests

    [Fact]
    public async Task AdjustBalanceAsync_ShouldSetNewBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId, openingBalance: 1000m);

        // Act
        var result = await grain.AdjustBalanceAsync(new AdjustBalanceCommand(
            1200m,
            "Physical count adjustment",
            Guid.NewGuid()));

        // Assert
        result.NewBalance.Should().Be(1200m);
        result.Amount.Should().Be(200m);
        result.EntryType.Should().Be(JournalEntryType.Adjustment);
    }

    [Fact]
    public async Task AdjustBalanceAsync_SameBalance_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId, openingBalance: 1000m);

        // Act
        var act = () => grain.AdjustBalanceAsync(new AdjustBalanceCommand(
            1000m,
            "No change",
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("New balance is the same as current balance");
    }

    #endregion

    #region Reversal Tests

    [Fact]
    public async Task ReverseEntryAsync_ShouldReverseDebit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId, openingBalance: 1000m);

        var debitResult = await grain.PostDebitAsync(new PostDebitCommand(
            500m,
            "Cash received",
            Guid.NewGuid()));

        // Act
        var result = await grain.ReverseEntryAsync(new ReverseEntryCommand(
            debitResult.EntryId,
            "Entry made in error",
            Guid.NewGuid()));

        // Assert
        result.Amount.Should().Be(500m);
        result.NewBalance.Should().Be(1000m); // Back to original

        // Check original entry is marked reversed
        var originalEntry = await grain.GetEntryAsync(debitResult.EntryId);
        originalEntry!.Status.Should().Be(JournalEntryStatus.Reversed);
        originalEntry.ReversalEntryId.Should().Be(result.ReversalEntryId);

        // Check reversal entry
        var reversalEntry = await grain.GetEntryAsync(result.ReversalEntryId);
        reversalEntry!.EntryType.Should().Be(JournalEntryType.Reversal);
        reversalEntry.ReversedEntryId.Should().Be(debitResult.EntryId);
    }

    [Fact]
    public async Task ReverseEntryAsync_ShouldReverseCredit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId, openingBalance: 1000m);

        var creditResult = await grain.PostCreditAsync(new PostCreditCommand(
            300m,
            "Cash paid",
            Guid.NewGuid()));

        // Balance should be 700

        // Act
        var result = await grain.ReverseEntryAsync(new ReverseEntryCommand(
            creditResult.EntryId,
            "Entry made in error",
            Guid.NewGuid()));

        // Assert
        result.NewBalance.Should().Be(1000m); // Back to original
    }

    [Fact]
    public async Task ReverseEntryAsync_AlreadyReversed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId);

        var debitResult = await grain.PostDebitAsync(new PostDebitCommand(
            100m,
            "Test",
            Guid.NewGuid()));

        await grain.ReverseEntryAsync(new ReverseEntryCommand(
            debitResult.EntryId,
            "First reversal",
            Guid.NewGuid()));

        // Act
        var act = () => grain.ReverseEntryAsync(new ReverseEntryCommand(
            debitResult.EntryId,
            "Second reversal",
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Entry has already been reversed");
    }

    [Fact]
    public async Task ReverseEntryAsync_ReversalEntry_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId);

        var debitResult = await grain.PostDebitAsync(new PostDebitCommand(
            100m,
            "Test",
            Guid.NewGuid()));

        var reversalResult = await grain.ReverseEntryAsync(new ReverseEntryCommand(
            debitResult.EntryId,
            "Reversal",
            Guid.NewGuid()));

        // Act
        var act = () => grain.ReverseEntryAsync(new ReverseEntryCommand(
            reversalResult.ReversalEntryId,
            "Reverse the reversal",
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot reverse a reversal entry");
    }

    #endregion

    #region Query Tests

    [Fact]
    public async Task GetBalanceAsync_ShouldReturnCurrentBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId, openingBalance: 1000m);

        await grain.PostDebitAsync(new PostDebitCommand(500m, "Deposit", Guid.NewGuid()));
        await grain.PostCreditAsync(new PostCreditCommand(200m, "Withdrawal", Guid.NewGuid()));

        // Act
        var balance = await grain.GetBalanceAsync();

        // Assert
        balance.Should().Be(1300m);
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnAccountSummary()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId, "1000", "Cash", openingBalance: 1000m);

        await grain.PostDebitAsync(new PostDebitCommand(500m, "Deposit", Guid.NewGuid()));
        await grain.PostCreditAsync(new PostCreditCommand(200m, "Withdrawal", Guid.NewGuid()));

        // Act
        var summary = await grain.GetSummaryAsync();

        // Assert
        summary.AccountId.Should().Be(accountId);
        summary.AccountCode.Should().Be("1000");
        summary.Name.Should().Be("Cash");
        summary.AccountType.Should().Be(AccountType.Asset);
        summary.Balance.Should().Be(1300m);
        summary.TotalDebits.Should().Be(1500m); // 1000 opening + 500 deposit
        summary.TotalCredits.Should().Be(200m);
        summary.TotalEntryCount.Should().Be(3);
        summary.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetRecentEntriesAsync_ShouldReturnEntriesInOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId);

        await grain.PostDebitAsync(new PostDebitCommand(100m, "First", Guid.NewGuid()));
        await grain.PostDebitAsync(new PostDebitCommand(200m, "Second", Guid.NewGuid()));
        await grain.PostDebitAsync(new PostDebitCommand(300m, "Third", Guid.NewGuid()));

        // Act
        var entries = await grain.GetRecentEntriesAsync(2);

        // Assert
        entries.Should().HaveCount(2);
        entries[0].Description.Should().Be("Third"); // Most recent first
        entries[1].Description.Should().Be("Second");
    }

    [Fact]
    public async Task GetEntriesByReferenceAsync_ShouldReturnMatchingEntries()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId);
        var orderId = Guid.NewGuid();

        await grain.PostDebitAsync(new PostDebitCommand(100m, "Order 1", Guid.NewGuid(),
            ReferenceType: "Order", ReferenceId: orderId));
        await grain.PostDebitAsync(new PostDebitCommand(50m, "Order 1 tip", Guid.NewGuid(),
            ReferenceType: "Order", ReferenceId: orderId));
        await grain.PostDebitAsync(new PostDebitCommand(200m, "Other", Guid.NewGuid()));

        // Act
        var entries = await grain.GetEntriesByReferenceAsync("Order", orderId);

        // Assert
        entries.Should().HaveCount(2);
        entries.All(e => e.ReferenceId == orderId).Should().BeTrue();
    }

    [Fact]
    public async Task GetBalanceAtAsync_ShouldReturnHistoricalBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId, openingBalance: 1000m);

        await grain.PostDebitAsync(new PostDebitCommand(500m, "Deposit 1", Guid.NewGuid()));
        var checkpoint = DateTime.UtcNow;
        await Task.Delay(10); // Ensure timestamp difference
        await grain.PostDebitAsync(new PostDebitCommand(300m, "Deposit 2", Guid.NewGuid()));

        // Act
        var balanceAtCheckpoint = await grain.GetBalanceAtAsync(checkpoint);
        var currentBalance = await grain.GetBalanceAsync();

        // Assert
        balanceAtCheckpoint.Should().Be(1500m);
        currentBalance.Should().Be(1800m);
    }

    #endregion

    #region Period Close Tests

    [Fact]
    public async Task ClosePeriodAsync_ShouldCreatePeriodSummary()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId, openingBalance: 1000m);

        await grain.PostDebitAsync(new PostDebitCommand(500m, "Deposit", Guid.NewGuid()));
        await grain.PostCreditAsync(new PostCreditCommand(200m, "Withdrawal", Guid.NewGuid()));

        var state = await grain.GetStateAsync();

        // Act
        var summary = await grain.ClosePeriodAsync(new ClosePeriodCommand(
            state.CurrentPeriodYear,
            state.CurrentPeriodMonth,
            Guid.NewGuid()));

        // Assert
        summary.TotalDebits.Should().Be(500m);
        summary.TotalCredits.Should().Be(200m);
        summary.ClosingBalance.Should().Be(1300m);
        summary.EntryCount.Should().Be(2); // Excludes opening

        var summaries = await grain.GetPeriodSummariesAsync();
        summaries.Should().HaveCount(1);
    }

    [Fact]
    public async Task ClosePeriodAsync_WrongPeriod_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId);

        // Act
        var act = () => grain.ClosePeriodAsync(new ClosePeriodCommand(
            2020,
            1,
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot close period*");
    }

    [Fact]
    public async Task ClosePeriodAsync_AlreadyClosed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId);

        var state = await grain.GetStateAsync();
        await grain.ClosePeriodAsync(new ClosePeriodCommand(
            state.CurrentPeriodYear,
            state.CurrentPeriodMonth,
            Guid.NewGuid()));

        // Get updated state for the new period
        var newState = await grain.GetStateAsync();

        // Close another month to have multiple summaries
        await grain.ClosePeriodAsync(new ClosePeriodCommand(
            newState.CurrentPeriodYear,
            newState.CurrentPeriodMonth,
            Guid.NewGuid()));

        // Act - try to close the first period again
        var act = () => grain.ClosePeriodAsync(new ClosePeriodCommand(
            state.CurrentPeriodYear,
            state.CurrentPeriodMonth,
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Lifecycle Tests

    [Fact]
    public async Task ExistsAsync_NewGrain_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = GetAccountGrain(orgId, accountId);

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_CreatedGrain_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId);

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateAsync_ShouldDeactivateAccount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId);

        // Act
        await grain.DeactivateAsync(Guid.NewGuid());

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ActivateAsync_ShouldReactivateAccount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId);
        await grain.DeactivateAsync(Guid.NewGuid());

        // Act
        await grain.ActivateAsync(Guid.NewGuid());

        // Assert
        var summary = await grain.GetSummaryAsync();
        summary.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateAsync_SystemAccount_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = GetAccountGrain(orgId, accountId);

        await grain.CreateAsync(new CreateAccountCommand(
            orgId,
            accountId,
            "1000",
            "System Cash",
            AccountType.Asset,
            Guid.NewGuid(),
            IsSystemAccount: true));

        // Act
        var act = () => grain.DeactivateAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("System accounts cannot be deactivated");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateAccountDetails()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = await CreateAssetAccountAsync(orgId, accountId);

        // Act
        await grain.UpdateAsync(new UpdateAccountCommand(
            Name: "Updated Cash Account",
            Description: "Updated description",
            TaxCode: "TAX01",
            UpdatedBy: Guid.NewGuid()));

        // Assert
        var state = await grain.GetStateAsync();
        state.Name.Should().Be("Updated Cash Account");
        state.Description.Should().Be("Updated description");
        state.TaxCode.Should().Be("TAX01");
        state.LastModifiedAt.Should().NotBeNull();
    }

    #endregion

    #region All Account Types Tests

    [Theory]
    [InlineData(AccountType.Asset)]
    [InlineData(AccountType.Expense)]
    public async Task DebitNormalAccounts_Debit_ShouldIncrease(AccountType accountType)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = GetAccountGrain(orgId, accountId);

        await grain.CreateAsync(new CreateAccountCommand(
            orgId,
            accountId,
            "1000",
            "Test Account",
            accountType,
            Guid.NewGuid()));

        // Act
        var result = await grain.PostDebitAsync(new PostDebitCommand(
            100m,
            "Test debit",
            Guid.NewGuid()));

        // Assert
        result.NewBalance.Should().Be(100m);
    }

    [Theory]
    [InlineData(AccountType.Liability)]
    [InlineData(AccountType.Equity)]
    [InlineData(AccountType.Revenue)]
    public async Task CreditNormalAccounts_Credit_ShouldIncrease(AccountType accountType)
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var grain = GetAccountGrain(orgId, accountId);

        await grain.CreateAsync(new CreateAccountCommand(
            orgId,
            accountId,
            "2000",
            "Test Account",
            accountType,
            Guid.NewGuid()));

        // Act
        var result = await grain.PostCreditAsync(new PostCreditCommand(
            100m,
            "Test credit",
            Guid.NewGuid()));

        // Assert
        result.NewBalance.Should().Be(100m);
    }

    #endregion
}
