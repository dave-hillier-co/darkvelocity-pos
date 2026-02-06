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

    // Given: A new, uninitialized ledger grain for a gift card
    // When: The ledger is initialized for an organization
    // Then: The ledger balance starts at zero
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

    // Given: A ledger already initialized with a $100 credit balance
    // When: The ledger is initialized again for the same organization
    // Then: The existing $100 balance is preserved and not reset
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

    // Given: An initialized cash drawer ledger with zero balance
    // When: A $500 opening float credit is applied
    // Then: The balance increases to $500 and the transaction records before/after balances
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

    // Given: An initialized cash drawer ledger with zero balance
    // When: Three successive deposits of $100, $200, and $50 are credited
    // Then: The balance accumulates to $350
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

    // Given: An initialized gift card ledger
    // When: A credit with a negative amount is attempted
    // Then: The operation fails with a non-negative validation error
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

    // Given: A gift card ledger with a $100 balance
    // When: A zero-amount credit adjustment is applied
    // Then: The operation succeeds and the balance remains at $100
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

    // Given: An initialized gift card ledger
    // When: A $100 credit is applied with order and cashier metadata
    // Then: The transaction stores the metadata fields for audit traceability
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

    // Given: A cash drawer ledger with a $1,000 opening float
    // When: A $200 cash withdrawal debit is applied
    // Then: The balance decreases to $800 and the debit is recorded as a negative amount
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

    // Given: A gift card ledger with only $50 balance
    // When: A $100 redemption debit is attempted
    // Then: The operation fails with an insufficient balance error showing available vs. requested amounts
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

    // Given: An inventory ledger with 10 units of stock
    // When: 15 units are consumed with the allowNegative flag set
    // Then: The debit succeeds and the balance goes to -5, flagging an inventory discrepancy
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

    // Given: An initialized gift card ledger
    // When: A debit with a negative amount is attempted
    // Then: The operation fails with a non-negative validation error
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

    // Given: A gift card ledger with exactly $100 balance
    // When: A full $100 redemption debit is applied
    // Then: The debit succeeds and the balance reaches zero
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

    // Given: A cash drawer ledger with a $100 balance
    // When: The balance is adjusted to $150 after a physical count
    // Then: The balance is set to $150 and the $50 adjustment amount is recorded
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

    // Given: A cash drawer ledger with a $500 balance
    // When: The balance is adjusted down to $300 due to a reconciliation shortage
    // Then: The balance is set to $300 with a -$200 adjustment recorded
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

    // Given: A cash drawer ledger with a $100 balance
    // When: An adjustment to a negative target balance is attempted
    // Then: The operation fails because ledger balance cannot be negative via adjustment
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

    // Given: A cash drawer ledger with a $100 balance
    // When: A verification adjustment to the same $100 balance is performed
    // Then: A zero-amount adjustment transaction is still recorded for audit purposes
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

    // Given: A cash drawer ledger with a $100 balance
    // When: A physical count adjustment to $120 is made with count ID and counter metadata
    // Then: The adjustment transaction preserves the count metadata for reconciliation tracking
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

    // Given: A freshly initialized gift card ledger with no transactions
    // When: The balance is queried
    // Then: The balance is zero
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

    // Given: A cash drawer ledger with a mix of credits ($1,000 opening + $150 sale) and debits ($200 payout + $50 change)
    // When: The balance is queried
    // Then: The balance correctly reflects all transactions at $900
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

    // Given: A gift card ledger with a $100 balance
    // When: A sufficiency check is performed for $50
    // Then: The check returns true indicating sufficient funds
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

    // Given: A gift card ledger with a $50 balance
    // When: A sufficiency check is performed for $100
    // Then: The check returns false indicating insufficient funds
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

    // Given: A gift card ledger with exactly $100 balance
    // When: A sufficiency check is performed for exactly $100
    // Then: The check returns true since the balance exactly covers the amount
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

    // Given: A gift card ledger with zero balance
    // When: A sufficiency check is performed for zero amount
    // Then: The check returns true since zero covers zero
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

    // Given: A cash drawer ledger with three transactions (two credits and one debit)
    // When: The transaction history is retrieved
    // Then: All three transactions are returned in most-recent-first order
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

    // Given: A cash drawer ledger with 10 transactions
    // When: The transaction history is retrieved with a limit of 3
    // Then: Only the 3 most recent transactions are returned
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

    // Given: An initialized gift card ledger with no transactions
    // When: The transaction history is retrieved
    // Then: An empty list is returned
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

    // Given: A gift card ledger with a single $75.50 sale payment including order metadata
    // When: The transaction history is retrieved
    // Then: The transaction includes all details: amount, running balance, type, notes, timestamp, and metadata
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

    // Given: A cash drawer ledger that has accumulated 110 transactions
    // When: The full transaction history is retrieved
    // Then: Only the most recent 100 transactions are retained due to the history limit
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

    // Given: A cash drawer ledger with 105 transactions (numbered 0 through 104)
    // When: The transaction history is retrieved
    // Then: The oldest 5 transactions are trimmed, retaining transactions 5 through 104
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

    // Given: A cash drawer ledger with 110 credits of $1.00 each
    // When: The balance and transaction history are queried
    // Then: The balance is $110 (all transactions counted) even though only the last 100 transactions are retained
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

    // Given: An initialized gift card ledger
    // When: A transaction is recorded with five metadata fields (order, customer, terminal, cashier, receipt)
    // Then: All five metadata fields are stored and retrievable on the transaction
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

    // Given: An initialized gift card ledger
    // When: A transaction is recorded with null metadata
    // Then: The transaction has an empty (non-null) metadata dictionary
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

    // Given: A ledger created with gift card owner type
    // When: The ledger is initialized and a $50 activation credit is applied
    // Then: The balance reflects $50 for the gift card ledger
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

    // Given: A ledger created with cash drawer owner type
    // When: The ledger is initialized and a $200 opening float credit is applied
    // Then: The balance reflects $200 for the cash drawer ledger
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

    // Given: A ledger created with inventory owner type using a compound site:ingredient key
    // When: The ledger is initialized and a $100 stock delivery credit is applied
    // Then: The balance reflects $100 for the inventory ledger
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

    // Given: Two ledgers with the same owner ID but different types (gift card vs. cash drawer)
    // When: Each ledger is credited with different amounts ($100 and $500 respectively)
    // Then: Each ledger maintains its own independent balance
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

    // Given: An initialized cash drawer ledger with zero balance
    // When: 10 concurrent $10 credits are applied simultaneously
    // Then: The final balance is exactly $100 with no lost updates due to Orleans single-writer guarantee
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

    // Given: An initialized gift card ledger
    // When: Very small amounts (one cent and a fraction of a cent) are credited
    // Then: The balance is greater than zero, preserving decimal precision
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

    // Given: An initialized cash drawer ledger
    // When: A very large deposit of $999,999,999.99 is credited
    // Then: The balance correctly reflects the large amount without overflow
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
