using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests.Domains.Payments;

/// <summary>
/// Tests for multi-currency gift card handling.
/// Currency is stored on gift cards and should be preserved across all operations.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MultiCurrencyGiftCardTests
{
    private readonly TestClusterFixture _fixture;

    public MultiCurrencyGiftCardTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // ============================================================================
    // Currency Preservation Tests
    // ============================================================================

    [Fact]
    public async Task GiftCard_Create_WithUSD_ShouldStoreCurrency()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        // Act
        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "USD"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task GiftCard_Create_WithEUR_ShouldStoreCurrency()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        // Act
        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m,
            "EUR"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task GiftCard_Create_WithGBP_ShouldStoreCurrency()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        // Act
        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            75m,
            "GBP"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("GBP");
    }

    [Fact]
    public async Task GiftCard_Create_WithJPY_ShouldStoreCurrency()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        // Act
        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            10000m, // JPY typically has no decimals
            "JPY"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("JPY");
        state.InitialValue.Should().Be(10000m);
    }

    [Fact]
    public async Task GiftCard_Create_DefaultCurrency_ShouldBeUSD()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        // Act - Note: Currency defaults to "USD" in CreateGiftCardCommand
        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100m));

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("USD");
    }

    // ============================================================================
    // Currency Preservation Through Operations
    // ============================================================================

    [Fact]
    public async Task GiftCard_Reload_ShouldPreserveCurrency()
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
            "EUR",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        await grain.ReloadAsync(new ReloadGiftCardCommand(50m, Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("EUR");
        state.CurrentBalance.Should().Be(150m);
    }

    [Fact]
    public async Task GiftCard_Redeem_ShouldPreserveCurrency()
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
            "GBP",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        await grain.RedeemAsync(new RedeemGiftCardCommand(
            30m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("GBP");
        state.CurrentBalance.Should().Be(70m);
    }

    [Fact]
    public async Task GiftCard_RefundToCard_ShouldPreserveCurrency()
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
            "CAD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));
        await grain.RedeemAsync(new RedeemGiftCardCommand(
            40m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Act
        await grain.RefundToCardAsync(new RefundToGiftCardCommand(
            20m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("CAD");
        state.CurrentBalance.Should().Be(80m); // 100 - 40 + 20
    }

    [Fact]
    public async Task GiftCard_Adjust_ShouldPreserveCurrency()
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
            "AUD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        await grain.AdjustBalanceAsync(new AdjustGiftCardCommand(25m, "Bonus", Guid.NewGuid()));

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("AUD");
        state.CurrentBalance.Should().Be(125m);
    }

    // ============================================================================
    // Different Currency Amounts
    // ============================================================================

    [Fact]
    public async Task GiftCard_HighValueCurrency_ShouldHandleLargeAmounts()
    {
        // Arrange - Test with JPY which typically has higher denominations
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            100000m,
            "JPY",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        await grain.RedeemAsync(new RedeemGiftCardCommand(
            45678m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("JPY");
        state.CurrentBalance.Should().Be(54322m);
    }

    [Fact]
    public async Task GiftCard_DecimalCurrency_ShouldHandlePrecision()
    {
        // Arrange - Test precise decimal handling
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            99.99m,
            "USD",
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        await grain.RedeemAsync(new RedeemGiftCardCommand(
            33.33m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        var state = await grain.GetStateAsync();
        state.CurrentBalance.Should().Be(66.66m);
    }

    // ============================================================================
    // Transaction Currency Tracking
    // ============================================================================

    [Fact]
    public async Task GiftCard_Transactions_ShouldMaintainConsistentCurrency()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            200m,
            "CHF", // Swiss Franc
            DateTime.UtcNow.AddYears(1)));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act - Multiple operations
        await grain.RedeemAsync(new RedeemGiftCardCommand(50m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        await grain.ReloadAsync(new ReloadGiftCardCommand(30m, Guid.NewGuid(), Guid.NewGuid()));
        await grain.RedeemAsync(new RedeemGiftCardCommand(25m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("CHF");
        state.CurrentBalance.Should().Be(155m); // 200 - 50 + 30 - 25
        state.Transactions.Should().HaveCount(4); // Activation + 2 Redemptions + 1 Reload

        // All transaction balances should be in the same currency
        foreach (var tx in state.Transactions)
        {
            tx.BalanceAfter.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    // ============================================================================
    // Different Card Types with Currency
    // ============================================================================

    [Fact]
    public async Task GiftCard_DigitalCard_ShouldPreserveCurrency()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        // Act
        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Digital,
            50m,
            "EUR"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Type.Should().Be(GiftCardType.Digital);
        state.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task GiftCard_PromotionalCard_ShouldPreserveCurrency()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        // Act
        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"PROMO-{cardId.ToString()[..8]}",
            GiftCardType.Promotional,
            25m,
            "GBP"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Type.Should().Be(GiftCardType.Promotional);
        state.Currency.Should().Be("GBP");
    }

    // ============================================================================
    // Edge Cases
    // ============================================================================

    [Fact]
    public async Task GiftCard_ZeroValueCard_ShouldPreserveCurrency()
    {
        // Arrange - Some promotional cards start with zero value
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Promotional,
            0m,
            "MXN"));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act - Reload the empty card
        await grain.ReloadAsync(new ReloadGiftCardCommand(500m, Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("MXN");
        state.InitialValue.Should().Be(0m);
        state.CurrentBalance.Should().Be(500m);
    }

    [Fact]
    public async Task GiftCard_Cancel_ShouldPreserveCurrency()
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
            "NZD"));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        await grain.CancelAsync("Lost card", Guid.NewGuid());

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("NZD");
        state.Status.Should().Be(GiftCardStatus.Cancelled);
    }

    [Fact]
    public async Task GiftCard_Expire_ShouldPreserveCurrency()
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
            "SEK"));

        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));

        // Act
        await grain.ExpireAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Currency.Should().Be("SEK");
        state.Status.Should().Be(GiftCardStatus.Expired);
    }
}
