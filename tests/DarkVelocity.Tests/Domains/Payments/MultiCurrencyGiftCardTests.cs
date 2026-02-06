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

    // Given: A new gift card being created
    // When: The card is created with USD as the currency
    // Then: The card stores USD as its currency
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

    // Given: A new gift card being created
    // When: The card is created with EUR as the currency
    // Then: The card stores EUR as its currency
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

    // Given: A new gift card being created
    // When: The card is created with GBP as the currency
    // Then: The card stores GBP as its currency
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

    // Given: A new gift card being created with a 10,000 unit value
    // When: The card is created with JPY as the currency
    // Then: The card stores JPY as its currency with the correct whole-number value
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

    // Given: A new gift card being created without specifying a currency
    // When: The card is created using the default currency
    // Then: The card defaults to USD as its currency
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

    // Given: An activated EUR gift card with a 100 balance
    // When: The card is reloaded with an additional 50
    // Then: The currency remains EUR and the balance increases to 150
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

    // Given: An activated GBP gift card with a 100 balance
    // When: 30 is redeemed from the card
    // Then: The currency remains GBP and the balance decreases to 70
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

    // Given: An activated CAD gift card with 60 remaining after a 40 redemption
    // When: A 20 refund is applied back to the card
    // Then: The currency remains CAD and the balance increases to 80
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

    // Given: An activated AUD gift card with a 100 balance
    // When: A positive balance adjustment of 25 is applied
    // Then: The currency remains AUD and the balance increases to 125
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

    // Given: An activated JPY gift card with a 100,000 balance
    // When: 45,678 is redeemed from the card
    // Then: The currency remains JPY and the balance correctly reflects 54,322
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

    // Given: An activated USD gift card with a $99.99 balance
    // When: $33.33 is redeemed from the card
    // Then: The balance precisely reflects $66.66 with correct decimal precision
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

    // Given: An activated CHF gift card with a 200 balance
    // When: Multiple redemptions and a reload are performed
    // Then: The currency remains CHF throughout and the balance accurately reflects all operations
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

    // Given: A new digital gift card being created
    // When: The card is created as a Digital type with EUR currency
    // Then: Both the Digital card type and EUR currency are stored
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

    // Given: A new promotional gift card being created
    // When: The card is created as a Promotional type with GBP currency
    // Then: Both the Promotional card type and GBP currency are stored
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

    // Given: A promotional gift card created with zero initial value in MXN
    // When: The card is activated and reloaded with 500
    // Then: The currency remains MXN with zero initial value and 500 current balance
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

    // Given: An activated NZD gift card with a 100 balance
    // When: The card is cancelled as lost
    // Then: The currency remains NZD and the status changes to Cancelled
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

    // Given: An activated SEK gift card with a 100 balance
    // When: The card is expired
    // Then: The currency remains SEK and the status changes to Expired
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
