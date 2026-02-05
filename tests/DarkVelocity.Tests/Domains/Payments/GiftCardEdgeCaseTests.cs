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
