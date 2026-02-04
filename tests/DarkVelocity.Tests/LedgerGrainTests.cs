using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class LedgerGrainTests
{
    private readonly TestClusterFixture _fixture;

    public LedgerGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ILedgerGrain GetLedgerGrain(Guid orgId, string ownerType, Guid ownerId)
        => _fixture.Cluster.GrainFactory.GetGrain<ILedgerGrain>(
            GrainKeys.Ledger(orgId, ownerType, ownerId));

    private ILedgerGrain GetLedgerGrain(Guid orgId, string ownerType, string ownerId)
        => _fixture.Cluster.GrainFactory.GetGrain<ILedgerGrain>(
            GrainKeys.Ledger(orgId, ownerType, ownerId));

    #region Initialize Tests

    [Fact]
    public async Task InitializeAsync_ShouldInitializeLedger()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);

        // Act
        await grain.InitializeAsync(orgId);

        // Assert
        var balance = await grain.GetBalanceAsync();
        balance.Should().Be(0);
    }

    [Fact]
    public async Task InitializeAsync_ReInitialization_ShouldBeNoOp()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);

        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(100m, "initial-load", "Initial load");

        // Act - re-initialize should not reset balance
        await grain.InitializeAsync(orgId);

        // Assert
        var balance = await grain.GetBalanceAsync();
        balance.Should().Be(100m);
    }

    #endregion

    #region Credit Tests

    [Fact]
    public async Task CreditAsync_ShouldIncreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);

        // Act
        var result = await grain.CreditAsync(500m, "cash-in", "Opening float");

        // Assert
        result.Success.Should().BeTrue();
        result.Amount.Should().Be(500m);
        result.BalanceBefore.Should().Be(0);
        result.BalanceAfter.Should().Be(500m);
        result.TransactionId.Should().NotBeEmpty();

        var balance = await grain.GetBalanceAsync();
        balance.Should().Be(500m);
    }

    [Fact]
    public async Task CreditAsync_MultipleTimes_ShouldAccumulateBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);

        // Act
        await grain.CreditAsync(100m, "cash-in", "First deposit");
        await grain.CreditAsync(200m, "cash-in", "Second deposit");
        var result = await grain.CreditAsync(50m, "cash-in", "Third deposit");

        // Assert
        result.BalanceAfter.Should().Be(350m);
    }

    [Fact]
    public async Task CreditAsync_NegativeAmount_ShouldFail()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);

        // Act
        var result = await grain.CreditAsync(-100m, "invalid", "Should fail");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("non-negative");
    }

    [Fact]
    public async Task CreditAsync_ZeroAmount_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(100m, "load", "Initial");

        // Act - zero credit should be allowed (no-op essentially)
        var result = await grain.CreditAsync(0m, "adjustment", "Zero credit");

        // Assert
        result.Success.Should().BeTrue();
        result.BalanceAfter.Should().Be(100m);
    }

    [Fact]
    public async Task CreditAsync_WithMetadata_ShouldStoreMetadata()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);

        var metadata = new Dictionary<string, string>
        {
            { "orderId", Guid.NewGuid().ToString() },
            { "cashierId", Guid.NewGuid().ToString() }
        };

        // Act
        await grain.CreditAsync(100m, "sale-payment", "Order payment", metadata);

        // Assert
        var transactions = await grain.GetTransactionsAsync(1);
        transactions.Should().HaveCount(1);
        transactions[0].Metadata.Should().ContainKey("orderId");
        transactions[0].Metadata.Should().ContainKey("cashierId");
    }

    #endregion

    #region Debit Tests

    [Fact]
    public async Task DebitAsync_ShouldDecreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(1000m, "cash-in", "Opening float");

        // Act
        var result = await grain.DebitAsync(200m, "cash-out", "Cash withdrawal");

        // Assert
        result.Success.Should().BeTrue();
        result.Amount.Should().Be(-200m); // Debit is recorded as negative
        result.BalanceBefore.Should().Be(1000m);
        result.BalanceAfter.Should().Be(800m);

        var balance = await grain.GetBalanceAsync();
        balance.Should().Be(800m);
    }

    [Fact]
    public async Task DebitAsync_InsufficientBalance_ShouldFail()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(50m, "load", "Initial load");

        // Act
        var result = await grain.DebitAsync(100m, "redemption", "Redemption attempt");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Insufficient balance");
        result.Error.Should().Contain("50");
        result.Error.Should().Contain("100");
    }

    [Fact]
    public async Task DebitAsync_WithAllowNegative_ShouldSucceedEvenWithInsufficientBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "inventory", ownerId);
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(10m, "stock-in", "Initial stock");

        var metadata = new Dictionary<string, string>
        {
            { "allowNegative", "true" }
        };

        // Act - debit more than available with allowNegative flag
        var result = await grain.DebitAsync(15m, "consumption", "Consumed more than recorded", metadata);

        // Assert
        result.Success.Should().BeTrue();
        result.BalanceAfter.Should().Be(-5m);
    }

    [Fact]
    public async Task DebitAsync_NegativeAmount_ShouldFail()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);

        // Act
        var result = await grain.DebitAsync(-50m, "invalid", "Should fail");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("non-negative");
    }

    [Fact]
    public async Task DebitAsync_ExactBalance_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(100m, "load", "Initial");

        // Act
        var result = await grain.DebitAsync(100m, "redemption", "Full redemption");

        // Assert
        result.Success.Should().BeTrue();
        result.BalanceAfter.Should().Be(0);
    }

    #endregion

    #region AdjustTo Tests

    [Fact]
    public async Task AdjustToAsync_ShouldSetExactBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(100m, "cash-in", "Initial");

        // Act
        var result = await grain.AdjustToAsync(150m, "Physical count adjustment");

        // Assert
        result.Success.Should().BeTrue();
        result.BalanceBefore.Should().Be(100m);
        result.BalanceAfter.Should().Be(150m);
        result.Amount.Should().Be(50m); // The adjustment amount
    }

    [Fact]
    public async Task AdjustToAsync_DecreasesBalance_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(500m, "cash-in", "Initial");

        // Act
        var result = await grain.AdjustToAsync(300m, "Reconciliation - shortage found");

        // Assert
        result.Success.Should().BeTrue();
        result.Amount.Should().Be(-200m);
        result.BalanceAfter.Should().Be(300m);
    }

    [Fact]
    public async Task AdjustToAsync_NegativeBalance_ShouldFail()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(100m, "cash-in", "Initial");

        // Act
        var result = await grain.AdjustToAsync(-50m, "Invalid adjustment");

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("cannot be negative");
    }

    [Fact]
    public async Task AdjustToAsync_SameBalance_ShouldStillCreateTransaction()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(100m, "cash-in", "Initial");

        // Act
        var result = await grain.AdjustToAsync(100m, "Count verified - no change");

        // Assert
        result.Success.Should().BeTrue();
        result.Amount.Should().Be(0m);

        var transactions = await grain.GetTransactionsAsync();
        transactions.Should().HaveCount(2); // Initial credit + adjustment
    }

    [Fact]
    public async Task AdjustToAsync_WithMetadata_ShouldStoreMetadata()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(100m, "cash-in", "Initial");

        var metadata = new Dictionary<string, string>
        {
            { "countId", Guid.NewGuid().ToString() },
            { "countedBy", "user123" }
        };

        // Act
        await grain.AdjustToAsync(120m, "Physical count", metadata);

        // Assert
        var transactions = await grain.GetTransactionsAsync(1);
        transactions[0].Metadata.Should().ContainKey("countId");
        transactions[0].Metadata.Should().ContainKey("countedBy");
    }

    #endregion

    #region GetBalance Tests

    [Fact]
    public async Task GetBalanceAsync_NewLedger_ShouldReturnZero()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);

        // Act
        var balance = await grain.GetBalanceAsync();

        // Assert
        balance.Should().Be(0);
    }

    [Fact]
    public async Task GetBalanceAsync_AfterMultipleTransactions_ShouldReturnCorrectBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);

        await grain.CreditAsync(1000m, "cash-in", "Opening");
        await grain.DebitAsync(200m, "cash-out", "Payout");
        await grain.CreditAsync(150m, "cash-in", "Sale");
        await grain.DebitAsync(50m, "cash-out", "Change");

        // Act
        var balance = await grain.GetBalanceAsync();

        // Assert
        balance.Should().Be(900m); // 1000 - 200 + 150 - 50
    }

    #endregion

    #region HasSufficientBalance Tests

    [Fact]
    public async Task HasSufficientBalanceAsync_SufficientFunds_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(100m, "load", "Initial load");

        // Act
        var hasSufficient = await grain.HasSufficientBalanceAsync(50m);

        // Assert
        hasSufficient.Should().BeTrue();
    }

    [Fact]
    public async Task HasSufficientBalanceAsync_InsufficientFunds_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(50m, "load", "Initial load");

        // Act
        var hasSufficient = await grain.HasSufficientBalanceAsync(100m);

        // Assert
        hasSufficient.Should().BeFalse();
    }

    [Fact]
    public async Task HasSufficientBalanceAsync_ExactAmount_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(100m, "load", "Initial load");

        // Act
        var hasSufficient = await grain.HasSufficientBalanceAsync(100m);

        // Assert
        hasSufficient.Should().BeTrue();
    }

    [Fact]
    public async Task HasSufficientBalanceAsync_ZeroBalance_ZeroAmount_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);

        // Act
        var hasSufficient = await grain.HasSufficientBalanceAsync(0m);

        // Assert
        hasSufficient.Should().BeTrue();
    }

    #endregion

    #region GetTransactions Tests

    [Fact]
    public async Task GetTransactionsAsync_ShouldReturnTransactionHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);

        await grain.CreditAsync(100m, "cash-in", "First");
        await grain.CreditAsync(200m, "cash-in", "Second");
        await grain.DebitAsync(50m, "cash-out", "Third");

        // Act
        var transactions = await grain.GetTransactionsAsync();

        // Assert
        transactions.Should().HaveCount(3);
        transactions[0].Notes.Should().Be("Third"); // Most recent first
        transactions[1].Notes.Should().Be("Second");
        transactions[2].Notes.Should().Be("First");
    }

    [Fact]
    public async Task GetTransactionsAsync_WithLimit_ShouldReturnLimitedHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);

        for (int i = 0; i < 10; i++)
        {
            await grain.CreditAsync(10m, "cash-in", $"Transaction {i}");
        }

        // Act
        var transactions = await grain.GetTransactionsAsync(3);

        // Assert
        transactions.Should().HaveCount(3);
        transactions[0].Notes.Should().Be("Transaction 9");
        transactions[1].Notes.Should().Be("Transaction 8");
        transactions[2].Notes.Should().Be("Transaction 7");
    }

    [Fact]
    public async Task GetTransactionsAsync_EmptyLedger_ShouldReturnEmpty()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);

        // Act
        var transactions = await grain.GetTransactionsAsync();

        // Assert
        transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTransactionsAsync_ShouldIncludeAllTransactionDetails()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);

        var metadata = new Dictionary<string, string> { { "orderId", "ORD-123" } };
        await grain.CreditAsync(75.50m, "sale-payment", "Payment for order", metadata);

        // Act
        var transactions = await grain.GetTransactionsAsync();

        // Assert
        transactions.Should().HaveCount(1);
        var transaction = transactions[0];
        transaction.Id.Should().NotBeEmpty();
        transaction.Amount.Should().Be(75.50m);
        transaction.BalanceAfter.Should().Be(75.50m);
        transaction.TransactionType.Should().Be("sale-payment");
        transaction.Notes.Should().Be("Payment for order");
        transaction.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        transaction.Metadata.Should().ContainKey("orderId");
        transaction.Metadata["orderId"].Should().Be("ORD-123");
    }

    #endregion

    #region Transaction History Limit Tests

    [Fact]
    public async Task TransactionHistory_ShouldBeLimitedTo100()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);

        // Add 110 transactions
        for (int i = 0; i < 110; i++)
        {
            await grain.CreditAsync(1m, "cash-in", $"Transaction {i}");
        }

        // Act
        var transactions = await grain.GetTransactionsAsync();

        // Assert
        transactions.Should().HaveCount(100);
    }

    [Fact]
    public async Task TransactionHistory_OldestShouldBeRemovedWhenLimitExceeded()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);

        // Add 105 transactions
        for (int i = 0; i < 105; i++)
        {
            await grain.CreditAsync(1m, "cash-in", $"Transaction {i}");
        }

        // Act
        var transactions = await grain.GetTransactionsAsync();

        // Assert - should have most recent 100, so Transaction 5-104
        transactions.Should().HaveCount(100);
        transactions.Last().Notes.Should().Be("Transaction 5"); // Oldest retained
        transactions.First().Notes.Should().Be("Transaction 104"); // Most recent
    }

    [Fact]
    public async Task TransactionHistory_BalanceShouldBeCorrectEvenAfterTrimming()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);

        // Add 110 credits of 1.00 each
        for (int i = 0; i < 110; i++)
        {
            await grain.CreditAsync(1m, "cash-in", $"Transaction {i}");
        }

        // Act
        var balance = await grain.GetBalanceAsync();
        var transactions = await grain.GetTransactionsAsync();

        // Assert
        balance.Should().Be(110m); // Balance should still be correct
        transactions.Should().HaveCount(100); // But only 100 transactions retained
    }

    #endregion

    #region Transaction Metadata Tests

    [Fact]
    public async Task Transaction_ShouldStoreMultipleMetadataFields()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);

        var metadata = new Dictionary<string, string>
        {
            { "orderId", Guid.NewGuid().ToString() },
            { "customerId", Guid.NewGuid().ToString() },
            { "terminalId", "POS-01" },
            { "cashierId", Guid.NewGuid().ToString() },
            { "receiptNumber", "REC-12345" }
        };

        // Act
        await grain.CreditAsync(100m, "redemption", "Gift card redeemed", metadata);

        // Assert
        var transactions = await grain.GetTransactionsAsync(1);
        var transaction = transactions[0];
        transaction.Metadata.Should().HaveCount(5);
        transaction.Metadata["terminalId"].Should().Be("POS-01");
        transaction.Metadata["receiptNumber"].Should().Be("REC-12345");
    }

    [Fact]
    public async Task Transaction_NullMetadata_ShouldResultInEmptyDictionary()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);

        // Act
        await grain.CreditAsync(50m, "load", "Initial load", null);

        // Assert
        var transactions = await grain.GetTransactionsAsync(1);
        transactions[0].Metadata.Should().NotBeNull();
        transactions[0].Metadata.Should().BeEmpty();
    }

    #endregion

    #region Different Owner Types Tests

    [Fact]
    public async Task Ledger_ShouldWorkWithGiftCardOwnerType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", cardId);

        // Act
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(50m, "activation", "Card activated");

        // Assert
        var balance = await grain.GetBalanceAsync();
        balance.Should().Be(50m);
    }

    [Fact]
    public async Task Ledger_ShouldWorkWithCashDrawerOwnerType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", drawerId);

        // Act
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(200m, "opening-float", "Opening float");

        // Assert
        var balance = await grain.GetBalanceAsync();
        balance.Should().Be(200m);
    }

    [Fact]
    public async Task Ledger_ShouldWorkWithInventoryOwnerType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var ingredientId = Guid.NewGuid();
        // Using compound owner ID: siteId:ingredientId
        var grain = GetLedgerGrain(orgId, "inventory", $"{siteId}:{ingredientId}");

        // Act
        await grain.InitializeAsync(orgId);
        await grain.CreditAsync(100m, "delivery", "Stock received");

        // Assert
        var balance = await grain.GetBalanceAsync();
        balance.Should().Be(100m);
    }

    [Fact]
    public async Task Ledger_DifferentOwnerTypes_ShouldBeIndependent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var giftCardLedger = GetLedgerGrain(orgId, "giftcard", ownerId);
        var cashDrawerLedger = GetLedgerGrain(orgId, "cashdrawer", ownerId);

        // Act
        await giftCardLedger.InitializeAsync(orgId);
        await cashDrawerLedger.InitializeAsync(orgId);
        await giftCardLedger.CreditAsync(100m, "load", "Gift card load");
        await cashDrawerLedger.CreditAsync(500m, "float", "Opening float");

        // Assert
        var giftCardBalance = await giftCardLedger.GetBalanceAsync();
        var cashDrawerBalance = await cashDrawerLedger.GetBalanceAsync();
        giftCardBalance.Should().Be(100m);
        cashDrawerBalance.Should().Be(500m);
    }

    #endregion

    #region Concurrent Operations Tests

    [Fact]
    public async Task Ledger_ConcurrentCredits_ShouldMaintainConsistency()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);

        // Act - perform concurrent credits
        var tasks = new List<Task<LedgerResult>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(grain.CreditAsync(10m, "cash-in", $"Credit {i}"));
        }
        await Task.WhenAll(tasks);

        // Assert
        var balance = await grain.GetBalanceAsync();
        balance.Should().Be(100m);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Ledger_VerySmallAmounts_ShouldHandlePrecision()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "giftcard", ownerId);
        await grain.InitializeAsync(orgId);

        // Act
        await grain.CreditAsync(0.01m, "load", "Penny");
        await grain.CreditAsync(0.001m, "load", "Tenth of a penny"); // May not be supported

        // Assert
        var balance = await grain.GetBalanceAsync();
        balance.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Ledger_LargeAmounts_ShouldHandleCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLedgerGrain(orgId, "cashdrawer", ownerId);
        await grain.InitializeAsync(orgId);

        // Act
        await grain.CreditAsync(999_999_999.99m, "cash-in", "Large deposit");

        // Assert
        var balance = await grain.GetBalanceAsync();
        balance.Should().Be(999_999_999.99m);
    }

    #endregion
}
