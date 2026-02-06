using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests.Domains.Payments;

/// <summary>
/// Tests for cash drawer reconciliation scenarios including variance calculations,
/// multiple transactions, and edge cases.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class CashDrawerReconciliationTests
{
    private readonly TestClusterFixture _fixture;

    public CashDrawerReconciliationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================================================
    // Variance Calculation Tests
    // ============================================================================

    // Given: A cash drawer opened with $100 float and $200 in cash sales received
    // When: The drawer is closed with an actual count of $305 against an expected $300
    // Then: A positive variance of $5.00 is recorded indicating the drawer is over
    [Fact]
    public async Task CashDrawer_Reconciliation_PositiveVariance_OverByExactAmount()
    {
        // Arrange - Drawer is over by exactly $5.00
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(100m);

        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 200m));

        // Act - Count shows $305 but expected is $300
        var result = await grain.CloseAsync(new CloseDrawerCommand(305m, userId));

        // Assert
        result.ExpectedBalance.Should().Be(300m);
        result.ActualBalance.Should().Be(305m);
        result.Variance.Should().Be(5m); // $5 over
    }

    // Given: A cash drawer opened with $500 float and $1,500 in cash sales received
    // When: The drawer is closed with an actual count of $2,050 against an expected $2,000
    // Then: A large positive variance of $50.00 is recorded
    [Fact]
    public async Task CashDrawer_Reconciliation_LargePositiveVariance()
    {
        // Arrange - Drawer is over by $50 (suspicious)
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(500m);

        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 1500m));

        // Act
        var result = await grain.CloseAsync(new CloseDrawerCommand(2050m, userId));

        // Assert
        result.ExpectedBalance.Should().Be(2000m);
        result.ActualBalance.Should().Be(2050m);
        result.Variance.Should().Be(50m);
    }

    // Given: A cash drawer opened with $500 float and $1,500 in cash sales received
    // When: The drawer is closed with an actual count of $1,900 against an expected $2,000
    // Then: A large negative variance of -$100.00 is recorded indicating a cash shortage
    [Fact]
    public async Task CashDrawer_Reconciliation_LargeNegativeVariance()
    {
        // Arrange - Drawer is short by $100 (suspicious)
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(500m);

        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 1500m));

        // Act
        var result = await grain.CloseAsync(new CloseDrawerCommand(1900m, userId));

        // Assert
        result.ExpectedBalance.Should().Be(2000m);
        result.ActualBalance.Should().Be(1900m);
        result.Variance.Should().Be(-100m);
    }

    // Given: A cash drawer with multiple small cash sales totaling $225.77 over the opening float
    // When: The drawer is closed with an actual count off by 3 cents
    // Then: A minor variance of -$0.03 is recorded as a rounding discrepancy
    [Fact]
    public async Task CashDrawer_Reconciliation_SmallVariance_Pennies()
    {
        // Arrange - Small rounding variance
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(200m);

        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 150.45m));
        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 75.32m));

        // Act - Off by 3 cents
        var result = await grain.CloseAsync(new CloseDrawerCommand(425.74m, userId));

        // Assert
        result.ExpectedBalance.Should().Be(425.77m);
        result.ActualBalance.Should().Be(425.74m);
        result.Variance.Should().Be(-0.03m);
    }

    // ============================================================================
    // Complex Transaction Sequences
    // ============================================================================

    // Given: A cash drawer opened with $200 float
    // When: Multiple cash-in and cash-out transactions are recorded
    // Then: The expected balance correctly reflects all inflows and outflows
    [Fact]
    public async Task CashDrawer_MultipleTransactions_CorrectBalance()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(200m);

        // Act - Multiple transactions
        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 50m));
        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 75m));
        await grain.RecordCashOutAsync(new RecordCashOutCommand(30m, "Change given"));
        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 100m));
        await grain.RecordCashOutAsync(new RecordCashOutCommand(20m, "Supplier COD"));

        // Assert
        var balance = await grain.GetExpectedBalanceAsync();
        balance.Should().Be(375m); // 200 + 50 + 75 - 30 + 100 - 20 = 375
    }

    // Given: A cash drawer with $500 float and $1,800 in cash sales
    // When: Three separate cash drops totaling $1,200 are made to the safe
    // Then: All three drops are tracked and the expected balance reflects the removals
    [Fact]
    public async Task CashDrawer_MultipleCashDrops_ShouldTrackAll()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(500m);

        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 1000m));
        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 800m));

        // Act - Multiple drops
        await grain.RecordDropAsync(new CashDropCommand(500m, "First drop to safe"));
        await grain.RecordDropAsync(new CashDropCommand(400m, "Second drop to safe"));
        await grain.RecordDropAsync(new CashDropCommand(300m, "Third drop to safe"));

        // Assert
        var state = await grain.GetStateAsync();
        state.CashDrops.Should().HaveCount(3);
        state.CashDrops.Sum(d => d.Amount).Should().Be(1200m);
        state.ExpectedBalance.Should().Be(1100m); // 500 + 1000 + 800 - 1200 = 1100
    }

    // Given: A cash drawer opened with $200 float
    // When: A cash drop of $300 is attempted, exceeding the available balance
    // Then: The drop is rejected due to insufficient funds in the drawer
    [Fact]
    public async Task CashDrawer_DropExceedingBalance_ShouldThrow()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(200m);

        // Act - Try to drop more than available
        var act = () => grain.RecordDropAsync(new CashDropCommand(300m, "Excessive drop"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient*");
    }

    // Given: A cash drawer opened with $100 float
    // When: A cash payout of $150 is attempted, exceeding the available balance
    // Then: The payout is rejected due to insufficient funds in the drawer
    [Fact]
    public async Task CashDrawer_CashOutExceedingBalance_ShouldThrow()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(100m);

        // Act - Try to pay out more than available
        var act = () => grain.RecordCashOutAsync(new RecordCashOutCommand(150m, "Large payout"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient*");
    }

    // ============================================================================
    // Drawer State Tests
    // ============================================================================

    // Given: An open cash drawer with $200 float and $300 in cash sales
    // When: The drawer is counted with an actual amount of $495
    // Then: The drawer transitions to Counting status with the counted amount and timestamp recorded
    [Fact]
    public async Task CashDrawer_CountAsync_ShouldSetCountingStatus()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(200m);

        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 300m));

        // Act
        await grain.CountAsync(new CountDrawerCommand(495m, userId));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(DrawerStatus.Counting);
        state.ActualBalance.Should().Be(495m);
        state.LastCountedAt.Should().NotBeNull();
        state.LastCountedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: An open cash drawer that has been counted once at $195
    // When: The drawer is recounted at $200
    // Then: The actual balance is updated to the latest count
    [Fact]
    public async Task CashDrawer_MultipleCountsBeforeClose_ShouldUpdateActualBalance()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(200m);

        // Act - Multiple counts (recounting)
        await grain.CountAsync(new CountDrawerCommand(195m, userId)); // First count
        var stateAfterFirstCount = await grain.GetStateAsync();
        stateAfterFirstCount.ActualBalance.Should().Be(195m);

        await grain.CountAsync(new CountDrawerCommand(200m, userId)); // Recount - found $5 more

        // Assert
        var state = await grain.GetStateAsync();
        state.ActualBalance.Should().Be(200m);
    }

    // Given: A cash drawer in Counting status with a matching count
    // When: The drawer is closed
    // Then: The drawer transitions to Closed status with zero variance
    [Fact]
    public async Task CashDrawer_CloseFromCountingStatus_ShouldWork()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(200m);

        await grain.CountAsync(new CountDrawerCommand(200m, userId));

        // Act
        var result = await grain.CloseAsync(new CloseDrawerCommand(200m, userId));

        // Assert
        result.Variance.Should().Be(0m);
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(DrawerStatus.Closed);
    }

    // Given: A cash drawer that has been closed
    // When: Cash-in, cash-out, drop, no-sale, or count operations are attempted
    // Then: All operations are rejected because the drawer is closed
    [Fact]
    public async Task CashDrawer_OperationOnClosedDrawer_ShouldThrow()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(200m);
        await grain.CloseAsync(new CloseDrawerCommand(200m, userId));

        // Act & Assert - All operations should fail
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 50m)));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.RecordCashOutAsync(new RecordCashOutCommand(50m, "Test")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.RecordDropAsync(new CashDropCommand(50m)));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.OpenNoSaleAsync(userId));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.CountAsync(new CountDrawerCommand(200m, userId)));
    }

    // Given: A cash drawer that was closed with a $5 shortage
    // When: The drawer is reopened with a new $250 float
    // Then: All transaction totals and counts are reset to start a fresh session
    [Fact]
    public async Task CashDrawer_ReopenAfterClose_ShouldStartFresh()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(200m);

        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 100m));
        await grain.CloseAsync(new CloseDrawerCommand(295m, userId)); // $5 short

        // Act - Reopen with new float
        var reopenResult = await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 250m));

        // Assert
        reopenResult.OpenedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(DrawerStatus.Open);
        state.OpeningFloat.Should().Be(250m);
        state.CashIn.Should().Be(0); // Reset
        state.CashOut.Should().Be(0); // Reset
        state.CashDrops.Should().BeEmpty(); // Reset
        state.ActualBalance.Should().BeNull(); // Reset
    }

    // ============================================================================
    // No-Sale Event Tests
    // ============================================================================

    // Given: An open cash drawer with $200 float
    // When: Three no-sale drawer opens are recorded with different reasons
    // Then: All three no-sale events are tracked without affecting the expected balance
    [Fact]
    public async Task CashDrawer_MultipleNoSales_ShouldTrackAll()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(200m);

        // Act
        await grain.OpenNoSaleAsync(userId, "Customer needed change");
        await grain.OpenNoSaleAsync(userId, "Manager override");
        await grain.OpenNoSaleAsync(userId, "Training");

        // Assert
        var state = await grain.GetStateAsync();
        var noSaleTransactions = state.Transactions.Where(t => t.Type == DrawerTransactionType.NoSale).ToList();
        noSaleTransactions.Should().HaveCount(3);

        // Balance should not change
        state.ExpectedBalance.Should().Be(200m);
    }

    // Given: An open cash drawer
    // When: A no-sale drawer open is recorded without specifying a reason
    // Then: The transaction is recorded with a default "No sale" description
    [Fact]
    public async Task CashDrawer_NoSale_WithoutReason_ShouldHaveDefaultDescription()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(200m);

        // Act
        await grain.OpenNoSaleAsync(userId);

        // Assert
        var state = await grain.GetStateAsync();
        var noSale = state.Transactions.First(t => t.Type == DrawerTransactionType.NoSale);
        noSale.Description.Should().Contain("No sale");
    }

    // ============================================================================
    // Transaction Tracking Tests
    // ============================================================================

    // Given: An open cash drawer with $500 float
    // When: Cash-in, cash-out, drop, and no-sale transactions are all performed
    // Then: All five transaction types (including the opening float) are tracked
    [Fact]
    public async Task CashDrawer_AllTransactionTypes_ShouldBeTracked()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(500m);

        // Act - Perform all transaction types
        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 100m));
        await grain.RecordCashOutAsync(new RecordCashOutCommand(50m, "Payout"));
        await grain.RecordDropAsync(new CashDropCommand(200m, "Safe deposit"));
        await grain.OpenNoSaleAsync(userId, "Customer request");

        // Assert
        var state = await grain.GetStateAsync();

        // Opening float is recorded as a transaction
        state.Transactions.Should().Contain(t => t.Type == DrawerTransactionType.OpeningFloat);
        state.Transactions.Should().Contain(t => t.Type == DrawerTransactionType.CashSale);
        state.Transactions.Should().Contain(t => t.Type == DrawerTransactionType.CashPayout);
        state.Transactions.Should().Contain(t => t.Type == DrawerTransactionType.Drop);
        state.Transactions.Should().Contain(t => t.Type == DrawerTransactionType.NoSale);

        state.Transactions.Should().HaveCount(5);
    }

    // Given: An open cash drawer with sequential transactions
    // When: Multiple cash-in and cash-out transactions are recorded over time
    // Then: All transaction timestamps are in chronological order
    [Fact]
    public async Task CashDrawer_TransactionTimestamps_ShouldBeOrdered()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(200m);

        // Act
        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 50m));
        await Task.Delay(10);
        await grain.RecordCashInAsync(new RecordCashInCommand(Guid.NewGuid(), 75m));
        await Task.Delay(10);
        await grain.RecordCashOutAsync(new RecordCashOutCommand(25m, "Change"));

        // Assert
        var state = await grain.GetStateAsync();
        var transactions = state.Transactions.OrderBy(t => t.Timestamp).ToList();

        for (int i = 1; i < transactions.Count; i++)
        {
            transactions[i].Timestamp.Should().BeOnOrAfter(transactions[i - 1].Timestamp);
        }
    }

    // ============================================================================
    // Edge Cases
    // ============================================================================

    // Given: A new cash drawer
    // When: The drawer is opened with a $0 float
    // Then: The drawer opens successfully with zero opening float and zero expected balance
    [Fact]
    public async Task CashDrawer_ZeroOpeningFloat_ShouldBeAllowed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(
            GrainKeys.CashDrawer(orgId, siteId, drawerId));

        // Act
        var result = await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 0m));

        // Assert
        result.Id.Should().Be(drawerId);

        var state = await grain.GetStateAsync();
        state.OpeningFloat.Should().Be(0m);
        state.ExpectedBalance.Should().Be(0m);
    }

    // Given: A new cash drawer
    // When: The drawer is opened with a $10,000 float
    // Then: The drawer opens successfully with the large float as expected balance
    [Fact]
    public async Task CashDrawer_LargeOpeningFloat_ShouldBeAllowed()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(10000m);

        // Assert
        var state = await grain.GetStateAsync();
        state.OpeningFloat.Should().Be(10000m);
        state.ExpectedBalance.Should().Be(10000m);
    }

    // Given: A cash drawer that transitions through unopened, open, and closed states
    // When: The open status is queried at each stage
    // Then: The status correctly reflects false before opening, true after opening, and false after closing
    [Fact]
    public async Task CashDrawer_IsOpenAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(
            GrainKeys.CashDrawer(orgId, siteId, drawerId));

        // Act & Assert - Before opening
        var isOpenBefore = await grain.IsOpenAsync();
        isOpenBefore.Should().BeFalse();

        // After opening
        await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 200m));
        var isOpenAfter = await grain.IsOpenAsync();
        isOpenAfter.Should().BeTrue();

        // After closing
        await grain.CloseAsync(new CloseDrawerCommand(200m, userId));
        var isOpenClosed = await grain.IsOpenAsync();
        isOpenClosed.Should().BeFalse();
    }

    // Given: A new cash drawer identifier
    // When: The existence is checked before and after opening
    // Then: The drawer reports as not existing before opening and existing after
    [Fact]
    public async Task CashDrawer_ExistsAsync_ShouldReturnCorrectValue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(
            GrainKeys.CashDrawer(orgId, siteId, drawerId));

        // Act & Assert - Before opening
        var existsBefore = await grain.ExistsAsync();
        existsBefore.Should().BeFalse();

        // After opening
        await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, 200m));
        var existsAfter = await grain.ExistsAsync();
        existsAfter.Should().BeTrue();
    }

    // Given: An open cash drawer
    // When: The drawer transitions through Open, Counting, and Closed statuses
    // Then: The status query returns the correct status at each stage
    [Fact]
    public async Task CashDrawer_GetStatusAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        var (grain, orgId, siteId, userId) = await CreateOpenDrawerAsync(200m);

        // Assert - Open status
        var openStatus = await grain.GetStatusAsync();
        openStatus.Should().Be(DrawerStatus.Open);

        // Counting status
        await grain.CountAsync(new CountDrawerCommand(200m, userId));
        var countingStatus = await grain.GetStatusAsync();
        countingStatus.Should().Be(DrawerStatus.Counting);

        // Closed status
        await grain.CloseAsync(new CloseDrawerCommand(200m, userId));
        var closedStatus = await grain.GetStatusAsync();
        closedStatus.Should().Be(DrawerStatus.Closed);
    }

    // ============================================================================
    // Helper Methods
    // ============================================================================

    private async Task<(ICashDrawerGrain grain, Guid orgId, Guid siteId, Guid userId)> CreateOpenDrawerAsync(decimal openingFloat)
    {
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var drawerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICashDrawerGrain>(
            GrainKeys.CashDrawer(orgId, siteId, drawerId));

        await grain.OpenAsync(new OpenDrawerCommand(orgId, siteId, userId, openingFloat));

        return (grain, orgId, siteId, userId);
    }
}
