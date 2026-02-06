using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests.Domains.Payments;

/// <summary>
/// Tests for gift card edge cases including expiry handling, balance edge cases,
/// and advanced scenarios not covered by basic tests.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class GiftCardEdgeCaseTests
{
    private readonly TestClusterFixture _fixture;

    public GiftCardEdgeCaseTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================================================
    // Gift Card Expiry Handling Tests
    // ============================================================================

    // Given: An activated gift card with an expiry date in the past
    // When: A redemption is attempted on the expired card
    // Then: The redemption is rejected because the card has expired
    [Fact]
    public async Task GiftCard_RedeemExpiredCard_ShouldThrow()
    {
        // Arrange - Create a card that expires immediately
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD",
            DateTime.UtcNow.AddSeconds(-1))); // Already expired

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var act = () => grain.RedeemAsync(new RedeemGiftCardCommand(
            50m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");
    }

    // Given: An activated gift card with $100 balance that has expired
    // When: Checking whether the card has sufficient balance for a $50 purchase
    // Then: The balance check returns false because the card is expired
    [Fact]
    public async Task GiftCard_HasSufficientBalanceAsync_WhenExpired_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD",
            DateTime.UtcNow.AddSeconds(-1))); // Already expired

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var hasSufficient = await grain.HasSufficientBalanceAsync(50m);

        // Assert
        hasSufficient.Should().BeFalse();
    }

    // Given: A fully depleted gift card with zero remaining balance
    // When: The card is expired
    // Then: The card status changes to Expired while maintaining zero balance
    [Fact]
    public async Task GiftCard_ExpireAsync_WithZeroBalance_ShouldChangeStatusOnly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            50m,
            "USD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Fully redeem first
        await grain.RedeemAsync(new RedeemGiftCardCommand(
            50m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        var stateBefore = await grain.GetStateAsync();
        stateBefore.Status.Should().Be(GiftCardStatus.Depleted);

        // Act
        await grain.ExpireAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(GiftCardStatus.Expired);
        state.CurrentBalance.Should().Be(0);
    }

    // Given: An activated gift card that has been cancelled
    // When: An expiration is attempted on the cancelled card
    // Then: The expiration is rejected because cancelled cards cannot be expired
    [Fact]
    public async Task GiftCard_ExpireAsync_AlreadyCancelled_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD"));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));
        await grain.CancelAsync("Cancelled", Guid.NewGuid());

        // Act
        var act = () => grain.ExpireAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot expire*");
    }

    // Given: A gift card that has already been expired
    // When: A second expiration is attempted
    // Then: The expiration is rejected because the card is already expired
    [Fact]
    public async Task GiftCard_ExpireAsync_AlreadyExpired_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD"));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));
        await grain.ExpireAsync();

        // Act
        var act = () => grain.ExpireAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot expire*");
    }

    // Given: An activated gift card that has been expired
    // When: A reload of $50 is attempted on the expired card
    // Then: The reload is rejected because expired cards cannot be reloaded
    [Fact]
    public async Task GiftCard_ReloadExpiredCard_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD"));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));
        await grain.ExpireAsync();

        // Act
        var act = () => grain.ReloadAsync(new ReloadGiftCardCommand(
            50m, Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot reload*");
    }

    // Given: An activated gift card that has been cancelled as lost
    // When: A reload of $50 is attempted on the cancelled card
    // Then: The reload is rejected because cancelled cards cannot be reloaded
    [Fact]
    public async Task GiftCard_ReloadCancelledCard_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD"));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));
        await grain.CancelAsync("Lost card", Guid.NewGuid());

        // Act
        var act = () => grain.ReloadAsync(new ReloadGiftCardCommand(
            50m, Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot reload*");
    }

    // Given: An activated gift card that has been expired
    // When: A refund of $25 is attempted back to the expired card
    // Then: The refund is rejected because expired cards cannot receive refunds
    [Fact]
    public async Task GiftCard_RefundToExpiredCard_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD"));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));
        await grain.ExpireAsync();

        // Act
        var act = () => grain.RefundToCardAsync(new RefundToGiftCardCommand(
            25m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot refund*");
    }

    // ============================================================================
    // Gift Card Balance Edge Cases
    // ============================================================================

    // Given: An activated gift card with a $75.50 balance
    // When: The exact balance of $75.50 is redeemed
    // Then: The card is fully depleted with zero remaining balance and Depleted status
    [Fact]
    public async Task GiftCard_RedeemExactBalance_ShouldDepleteCard()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            75.50m,
            "USD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var result = await grain.RedeemAsync(new RedeemGiftCardCommand(
            75.50m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        result.AmountRedeemed.Should().Be(75.50m);
        result.RemainingBalance.Should().Be(0);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(GiftCardStatus.Depleted);
    }

    // Given: An activated gift card with a $100 balance
    // When: A redemption of $100.01 is attempted, one cent over the balance
    // Then: The redemption is rejected due to insufficient balance
    [Fact]
    public async Task GiftCard_RedeemOnePennyMore_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var act = () => grain.RedeemAsync(new RedeemGiftCardCommand(
            100.01m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient balance*");
    }

    // Given: An activated gift card with a $100 balance
    // When: Three consecutive redemptions of $25, $30, and $20 are made
    // Then: The remaining balance correctly decreases after each redemption and total redeemed is tracked
    [Fact]
    public async Task GiftCard_ConsecutiveRedemptions_ShouldMaintainCorrectBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act - Multiple consecutive redemptions
        var result1 = await grain.RedeemAsync(new RedeemGiftCardCommand(25m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        var result2 = await grain.RedeemAsync(new RedeemGiftCardCommand(30m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        var result3 = await grain.RedeemAsync(new RedeemGiftCardCommand(20m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        result1.RemainingBalance.Should().Be(75m);
        result2.RemainingBalance.Should().Be(45m);
        result3.RemainingBalance.Should().Be(25m);

        var state = await grain.GetStateAsync();
        state.CurrentBalance.Should().Be(25m);
        state.TotalRedeemed.Should().Be(75m);
        state.RedemptionCount.Should().Be(3);
    }

    // Given: A gift card that was created but never activated
    // When: A redemption of $50 is attempted
    // Then: The redemption is rejected because the card is not active
    [Fact]
    public async Task GiftCard_RedeemInactiveCar_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD",
            DateTime.UtcNow.AddYears(1)));

        // Note: Not activated

        // Act
        var act = () => grain.RedeemAsync(new RedeemGiftCardCommand(
            50m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not active*");
    }

    // Given: An activated gift card with a $100 initial balance
    // When: Three consecutive reloads of $25, $50, and $10 are made
    // Then: The balance increases correctly after each reload and total reloaded is tracked
    [Fact]
    public async Task GiftCard_MultipleReloads_ShouldAccumulateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var balance1 = await grain.ReloadAsync(new ReloadGiftCardCommand(25m, Guid.NewGuid(), Guid.NewGuid()));
        var balance2 = await grain.ReloadAsync(new ReloadGiftCardCommand(50m, Guid.NewGuid(), Guid.NewGuid()));
        var balance3 = await grain.ReloadAsync(new ReloadGiftCardCommand(10m, Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        balance1.Should().Be(125m);
        balance2.Should().Be(175m);
        balance3.Should().Be(185m);

        var state = await grain.GetStateAsync();
        state.CurrentBalance.Should().Be(185m);
        state.TotalReloaded.Should().Be(85m);
    }

    // Given: An activated gift card with a $100 balance that received a $50 reload
    // When: The reload transaction is voided as a duplicate
    // Then: The balance is reversed back to the original $100
    [Fact]
    public async Task GiftCard_VoidTransaction_ForReload_ShouldReverseCredit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));
        await grain.ReloadAsync(new ReloadGiftCardCommand(50m, Guid.NewGuid(), Guid.NewGuid()));

        var stateBefore = await grain.GetStateAsync();
        var reloadTx = stateBefore.Transactions.Last(t => t.Type == GiftCardTransactionType.Reload);
        stateBefore.CurrentBalance.Should().Be(150m);

        // Act
        await grain.VoidTransactionAsync(reloadTx.Id, "Duplicate reload", Guid.NewGuid());

        // Assert
        var state = await grain.GetStateAsync();
        state.CurrentBalance.Should().Be(100m); // Back to original
    }

    // Given: An activated gift card
    // When: A void is attempted for a transaction ID that does not exist
    // Then: The void is rejected because the transaction is not found
    [Fact]
    public async Task GiftCard_VoidNonExistentTransaction_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var act = () => grain.VoidTransactionAsync(Guid.NewGuid(), "Test", Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    // Given: A gift card that has already been activated
    // When: A second activation is attempted
    // Then: The activation is rejected because the card is already active
    [Fact]
    public async Task GiftCard_ActivateTwice_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD"));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var act = () => grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot activate*");
    }

    // Given: A gift card that has already been cancelled
    // When: A second cancellation is attempted
    // Then: The cancellation is rejected because the card is already cancelled
    [Fact]
    public async Task GiftCard_CancelTwice_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD"));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));
        await grain.CancelAsync("First cancel", Guid.NewGuid());

        // Act
        var act = () => grain.CancelAsync("Second cancel", Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already cancelled*");
    }

    // Given: An activated gift card that has been fully redeemed to zero balance
    // When: A further redemption of $10 is attempted on the depleted card
    // Then: The redemption is rejected because the card is not active (Depleted status)
    [Fact]
    public async Task GiftCard_RedeemDepletedCard_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            50m,
            "USD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));
        await grain.RedeemAsync(new RedeemGiftCardCommand(50m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(GiftCardStatus.Depleted);

        // Act
        var act = () => grain.RedeemAsync(new RedeemGiftCardCommand(
            10m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not active*");
    }

    // Given: An activated gift card with a $100 balance
    // When: A minimal redemption of $0.01 is made
    // Then: One cent is redeemed and the remaining balance is $99.99
    [Fact]
    public async Task GiftCard_SmallAmountRedemption_ShouldWork()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act - Redeem 1 cent
        var result = await grain.RedeemAsync(new RedeemGiftCardCommand(
            0.01m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        result.AmountRedeemed.Should().Be(0.01m);
        result.RemainingBalance.Should().Be(99.99m);
    }

    // Given: A gift card that has already been created
    // When: A second creation is attempted for the same card
    // Then: The creation is rejected because the card already exists
    [Fact]
    public async Task GiftCard_CreateTwice_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD"));

        // Act
        var act = () => grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            50m,
            "USD"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    // Given: An activated gift card with a $100 balance
    // When: A positive balance adjustment of $25 is applied as a bonus credit
    // Then: The card balance increases to $125
    [Fact]
    public async Task GiftCard_PositiveAdjustment_ShouldIncreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var newBalance = await grain.AdjustBalanceAsync(new AdjustGiftCardCommand(25m, "Bonus credit", Guid.NewGuid()));

        // Assert
        newBalance.Should().Be(125m);
    }

    // Given: An activated gift card with a $50 balance
    // When: A negative balance adjustment of -$60 is attempted, exceeding the balance
    // Then: The adjustment is rejected because it would result in a negative balance
    [Fact]
    public async Task GiftCard_NegativeAdjustment_ExceedingBalance_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            50m,
            "USD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var act = () => grain.AdjustBalanceAsync(new AdjustGiftCardCommand(-60m, "Excessive deduction", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*negative balance*");
    }

    // Given: An activated gift card with a $50 balance
    // When: A negative balance adjustment of exactly -$50 is applied
    // Then: The card balance reaches zero and the status changes to Depleted
    [Fact]
    public async Task GiftCard_NegativeAdjustment_ExactBalance_ShouldDepleteCard()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            50m,
            "USD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var newBalance = await grain.AdjustBalanceAsync(new AdjustGiftCardCommand(-50m, "Full deduction", Guid.NewGuid()));

        // Assert
        newBalance.Should().Be(0);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(GiftCardStatus.Depleted);
    }

    // Given: An activated gift card with a $100 balance and $40 redeemed
    // When: Retrieving the card balance information
    // Then: The balance info shows $60 remaining, Active status, and the correct expiry date
    [Fact]
    public async Task GiftCard_GetBalanceInfo_ShouldReturnCorrectInfo()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var expiryDate = DateTime.UtcNow.AddYears(2);
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD",
            expiryDate));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));
        await grain.RedeemAsync(new RedeemGiftCardCommand(40m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var info = await grain.GetBalanceInfoAsync();

        // Assert
        info.CurrentBalance.Should().Be(60m);
        info.Status.Should().Be(GiftCardStatus.Active);
        info.ExpiresAt.Should().BeCloseTo(expiryDate, TimeSpan.FromSeconds(1));
    }
}
