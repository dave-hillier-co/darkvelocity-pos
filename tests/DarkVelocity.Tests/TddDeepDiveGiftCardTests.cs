using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class TddDeepDiveGiftCardTests
{
    private readonly TestClusterFixture _fixture;

    public TddDeepDiveGiftCardTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IGiftCardGrain> CreateCardAsync(Guid orgId, Guid cardId, decimal value = 100m, string? pin = null, DateTime? expiresAt = null)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));
        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            value,
            "USD",
            expiresAt ?? DateTime.UtcNow.AddYears(1),
            pin));
        return grain;
    }

    private async Task<IGiftCardGrain> CreateAndActivateCardAsync(Guid orgId, Guid cardId, decimal value = 100m, DateTime? expiresAt = null)
    {
        var grain = await CreateCardAsync(orgId, cardId, value, expiresAt: expiresAt);
        await grain.ActivateAsync(new ActivateGiftCardCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "John",
            "john@test.com"));
        return grain;
    }

    // Given: a gift card that has been created but NOT activated
    // When: a redemption is attempted
    // Then: an InvalidOperationException is thrown indicating the card is not active
    [Fact]
    public async Task RedeemFromInactiveCard_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateCardAsync(orgId, cardId, 100m);

        // Act
        var act = () => grain.RedeemAsync(new RedeemGiftCardCommand(
            50m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Gift card is not active*");
    }

    // Given: an active gift card with an expiry date in the past
    // When: a redemption is attempted
    // Then: an InvalidOperationException is thrown indicating the card has expired
    [Fact]
    public async Task RedeemFromExpiredCard_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 100m, expiresAt: DateTime.UtcNow.AddSeconds(-1));

        // Act
        var act = () => grain.RedeemAsync(new RedeemGiftCardCommand(
            50m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Gift card has expired*");
    }

    // Given: an active gift card with a $50 balance
    // When: a redemption of $75 is attempted, exceeding the available balance
    // Then: an InvalidOperationException is thrown from LedgerGrainBase with insufficient balance
    [Fact]
    public async Task RedeemMoreThanBalance_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 50m);

        // Act
        var act = () => grain.RedeemAsync(new RedeemGiftCardCommand(
            75m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient balance*");
    }

    // Given: an active gift card with a $100 balance
    // When: the full $100 balance is redeemed
    // Then: the card status becomes Depleted and the balance equals 0
    [Fact]
    public async Task RedeemExactBalance_CardBecomesDepleted()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 100m);

        // Act
        var result = await grain.RedeemAsync(new RedeemGiftCardCommand(
            100m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()));

        // Assert
        result.RemainingBalance.Should().Be(0m);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(GiftCardStatus.Depleted);
        state.CurrentBalance.Should().Be(0m);
    }

    // Given: a depleted gift card with a zero balance
    // When: the card is reloaded with $50
    // Then: the card status returns to Active and the balance equals $50
    [Fact]
    public async Task ReloadAfterDepleted_CardBecomesActive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 100m);

        // Deplete the card
        await grain.RedeemAsync(new RedeemGiftCardCommand(
            100m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()));

        var depletedState = await grain.GetStateAsync();
        depletedState.Status.Should().Be(GiftCardStatus.Depleted);
        depletedState.CurrentBalance.Should().Be(0m);

        // Act
        var newBalance = await grain.ReloadAsync(new ReloadGiftCardCommand(
            50m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Reload notes"));

        // Assert
        newBalance.Should().Be(50m);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(GiftCardStatus.Active);
        state.CurrentBalance.Should().Be(50m);
    }

    // Given: an active gift card with a $75 balance
    // When: CancelAsync is called with a reason
    // Then: the card status becomes Cancelled and the balance is zeroed out
    [Fact]
    public async Task CancelCardWithBalance_BalanceZeroed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 75m);

        // Act
        await grain.CancelAsync("Customer requested cancellation", Guid.NewGuid());

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(GiftCardStatus.Cancelled);
        state.CurrentBalance.Should().Be(0m);
    }

    // Given: an active gift card with a $50 balance
    // When: ExpireAsync is called
    // Then: the card status becomes Expired and the balance is zeroed out
    [Fact]
    public async Task ExpireCardWithBalance_BalanceZeroed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 50m);

        // Act
        await grain.ExpireAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(GiftCardStatus.Expired);
        state.CurrentBalance.Should().Be(0m);
    }

    // Given: a gift card created with PIN "1234"
    // When: ValidatePinAsync is called with "1234"
    // Then: the method returns true
    [Fact]
    public async Task ValidatePin_CorrectPin_ReturnsTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateCardAsync(orgId, cardId, 100m, pin: "1234");

        // Act
        var result = await grain.ValidatePinAsync("1234");

        // Assert
        result.Should().BeTrue();
    }

    // Given: a gift card created with PIN "1234"
    // When: ValidatePinAsync is called with "5678"
    // Then: the method returns false
    [Fact]
    public async Task ValidatePin_WrongPin_ReturnsFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateCardAsync(orgId, cardId, 100m, pin: "1234");

        // Act
        var result = await grain.ValidatePinAsync("5678");

        // Assert
        result.Should().BeFalse();
    }

    // Given: a gift card created without a PIN
    // When: ValidatePinAsync is called with any value
    // Then: the method returns true because no PIN is required
    [Fact]
    public async Task ValidatePin_NoPinSet_ReturnsTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateCardAsync(orgId, cardId, 100m);

        // Act
        var result = await grain.ValidatePinAsync("anything");

        // Assert
        result.Should().BeTrue();
    }

    // Given: a cancelled gift card
    // When: a redemption is attempted
    // Then: an InvalidOperationException is thrown indicating the card is not active
    [Fact]
    public async Task RedeemFromCancelledCard_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 100m);
        await grain.CancelAsync("No longer needed", Guid.NewGuid());

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(GiftCardStatus.Cancelled);

        // Act
        var act = () => grain.RedeemAsync(new RedeemGiftCardCommand(
            25m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Gift card is not active*");
    }
}
