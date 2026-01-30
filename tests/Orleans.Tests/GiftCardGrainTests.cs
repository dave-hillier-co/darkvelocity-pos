using DarkVelocity.Orleans.Abstractions;
using DarkVelocity.Orleans.Abstractions.Grains;
using DarkVelocity.Orleans.Abstractions.State;
using FluentAssertions;

namespace DarkVelocity.Orleans.Tests;

[Collection(ClusterCollection.Name)]
public class GiftCardGrainTests
{
    private readonly TestClusterFixture _fixture;

    public GiftCardGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<IGiftCardGrain> CreateCardAsync(Guid orgId, Guid cardId, decimal value = 100m, string pin = null!)
    {
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));
        await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            $"GC-{cardId.ToString()[..8]}",
            GiftCardType.Physical,
            value,
            "USD",
            DateTime.UtcNow.AddYears(1),
            pin));
        return grain;
    }

    private async Task<IGiftCardGrain> CreateAndActivateCardAsync(Guid orgId, Guid cardId, decimal value = 100m)
    {
        var grain = await CreateCardAsync(orgId, cardId, value);
        await grain.ActivateAsync(new ActivateGiftCardCommand(Guid.NewGuid(), Guid.NewGuid()));
        return grain;
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateGiftCard()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IGiftCardGrain>(GrainKeys.GiftCard(orgId, cardId));

        // Act
        var result = await grain.CreateAsync(new CreateGiftCardCommand(
            orgId,
            "GC-12345678",
            GiftCardType.Digital,
            50m,
            "USD",
            DateTime.UtcNow.AddMonths(6)));

        // Assert
        result.Id.Should().Be(cardId);
        result.CardNumber.Should().Be("GC-12345678");

        var state = await grain.GetStateAsync();
        state.Type.Should().Be(GiftCardType.Digital);
        state.Status.Should().Be(GiftCardStatus.Inactive);
        state.InitialValue.Should().Be(50m);
        state.CurrentBalance.Should().Be(50m);
    }

    [Fact]
    public async Task ActivateAsync_ShouldActivateCard()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var activatedBy = Guid.NewGuid();
        var grain = await CreateCardAsync(orgId, cardId);

        // Act
        var result = await grain.ActivateAsync(new ActivateGiftCardCommand(
            activatedBy,
            siteId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "John Doe",
            "john@example.com"));

        // Assert
        result.Balance.Should().Be(100m);
        result.ActivatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(GiftCardStatus.Active);
        state.ActivatedBy.Should().Be(activatedBy);
        state.PurchaserName.Should().Be("John Doe");
        state.Transactions.Should().HaveCount(1);
        state.Transactions[0].Type.Should().Be(GiftCardTransactionType.Activation);
    }

    [Fact]
    public async Task SetRecipientAsync_ShouldSetRecipient()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var grain = await CreateCardAsync(orgId, cardId);

        // Act
        await grain.SetRecipientAsync(new SetRecipientCommand(
            customerId,
            "Jane Smith",
            "jane@example.com",
            "+1234567890",
            "Happy Birthday!"));

        // Assert
        var state = await grain.GetStateAsync();
        state.RecipientCustomerId.Should().Be(customerId);
        state.RecipientName.Should().Be("Jane Smith");
        state.RecipientEmail.Should().Be("jane@example.com");
        state.PersonalMessage.Should().Be("Happy Birthday!");
    }

    [Fact]
    public async Task RedeemAsync_ShouldDeductBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 100m);

        // Act
        var result = await grain.RedeemAsync(new RedeemGiftCardCommand(
            30m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()));

        // Assert
        result.AmountRedeemed.Should().Be(30m);
        result.RemainingBalance.Should().Be(70m);

        var state = await grain.GetStateAsync();
        state.CurrentBalance.Should().Be(70m);
        state.TotalRedeemed.Should().Be(30m);
        state.RedemptionCount.Should().Be(1);
    }

    [Fact]
    public async Task RedeemAsync_InsufficientBalance_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 50m);

        // Act
        var act = () => grain.RedeemAsync(new RedeemGiftCardCommand(
            100m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Insufficient balance*");
    }

    [Fact]
    public async Task RedeemAsync_FullBalance_ShouldDepleteCard()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 50m);

        // Act
        await grain.RedeemAsync(new RedeemGiftCardCommand(
            50m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid()));

        // Assert
        var state = await grain.GetStateAsync();
        state.CurrentBalance.Should().Be(0);
        state.Status.Should().Be(GiftCardStatus.Depleted);
    }

    [Fact]
    public async Task ReloadAsync_ShouldIncreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 50m);

        // Act
        var newBalance = await grain.ReloadAsync(new ReloadGiftCardCommand(
            25m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "Birthday reload"));

        // Assert
        newBalance.Should().Be(75m);

        var state = await grain.GetStateAsync();
        state.CurrentBalance.Should().Be(75m);
        state.TotalReloaded.Should().Be(25m);
    }

    [Fact]
    public async Task ReloadAsync_DepletedCard_ShouldReactivate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 50m);
        await grain.RedeemAsync(new RedeemGiftCardCommand(50m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        var stateBefore = await grain.GetStateAsync();
        stateBefore.Status.Should().Be(GiftCardStatus.Depleted);

        // Act
        await grain.ReloadAsync(new ReloadGiftCardCommand(30m, Guid.NewGuid(), Guid.NewGuid()));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(GiftCardStatus.Active);
        state.CurrentBalance.Should().Be(30m);
    }

    [Fact]
    public async Task RefundToCardAsync_ShouldIncreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 100m);
        await grain.RedeemAsync(new RedeemGiftCardCommand(30m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var newBalance = await grain.RefundToCardAsync(new RefundToGiftCardCommand(
            15m,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Partial refund"));

        // Assert
        newBalance.Should().Be(85m); // 100 - 30 + 15
    }

    [Fact]
    public async Task AdjustBalanceAsync_ShouldAdjustBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 100m);

        // Act
        var newBalance = await grain.AdjustBalanceAsync(new AdjustGiftCardCommand(
            -20m,
            "Correction",
            Guid.NewGuid()));

        // Assert
        newBalance.Should().Be(80m);

        var state = await grain.GetStateAsync();
        state.Transactions.Last().Type.Should().Be(GiftCardTransactionType.Adjustment);
        state.Transactions.Last().Notes.Should().Be("Correction");
    }

    [Fact]
    public async Task AdjustBalanceAsync_NegativeResult_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 50m);

        // Act
        var act = () => grain.AdjustBalanceAsync(new AdjustGiftCardCommand(-100m, "Bad adjustment", Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*negative balance*");
    }

    [Fact]
    public async Task ValidatePinAsync_WithCorrectPin_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateCardAsync(orgId, cardId, 100m, "1234");

        // Act
        var isValid = await grain.ValidatePinAsync("1234");

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task ValidatePinAsync_WithIncorrectPin_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateCardAsync(orgId, cardId, 100m, "1234");

        // Act
        var isValid = await grain.ValidatePinAsync("5678");

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidatePinAsync_WithNoPin_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateCardAsync(orgId, cardId);

        // Act
        var isValid = await grain.ValidatePinAsync("anypin");

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExpireAsync_ShouldExpireCard()
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
        state.CurrentBalance.Should().Be(0);
        state.Transactions.Last().Type.Should().Be(GiftCardTransactionType.Expiration);
    }

    [Fact]
    public async Task CancelAsync_ShouldCancelCard()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var cancelledBy = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 75m);

        // Act
        await grain.CancelAsync("Lost card reported", cancelledBy);

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(GiftCardStatus.Cancelled);
        state.CurrentBalance.Should().Be(0);
        state.Transactions.Last().Type.Should().Be(GiftCardTransactionType.Void);
        state.Transactions.Last().Notes.Should().Contain("Lost card reported");
    }

    [Fact]
    public async Task VoidTransactionAsync_ShouldReverseTransaction()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var voidedBy = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 100m);
        await grain.RedeemAsync(new RedeemGiftCardCommand(30m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        var state = await grain.GetStateAsync();
        var redemptionTx = state.Transactions.Last(t => t.Type == GiftCardTransactionType.Redemption);

        // Act
        await grain.VoidTransactionAsync(redemptionTx.Id, "Customer dispute", voidedBy);

        // Assert
        state = await grain.GetStateAsync();
        state.CurrentBalance.Should().Be(100m); // Restored
        state.Transactions.Last().Type.Should().Be(GiftCardTransactionType.Void);
    }

    [Fact]
    public async Task GetBalanceInfoAsync_ShouldReturnInfo()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 75m);

        // Act
        var info = await grain.GetBalanceInfoAsync();

        // Assert
        info.CurrentBalance.Should().Be(75m);
        info.Status.Should().Be(GiftCardStatus.Active);
        info.ExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HasSufficientBalanceAsync_WithSufficientBalance_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 100m);

        // Act
        var hasSufficient = await grain.HasSufficientBalanceAsync(50m);

        // Assert
        hasSufficient.Should().BeTrue();
    }

    [Fact]
    public async Task HasSufficientBalanceAsync_WithInsufficientBalance_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 30m);

        // Act
        var hasSufficient = await grain.HasSufficientBalanceAsync(50m);

        // Assert
        hasSufficient.Should().BeFalse();
    }

    [Fact]
    public async Task HasSufficientBalanceAsync_WhenInactive_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateCardAsync(orgId, cardId, 100m);

        // Act
        var hasSufficient = await grain.HasSufficientBalanceAsync(50m);

        // Assert
        hasSufficient.Should().BeFalse();
    }

    [Fact]
    public async Task GetTransactionsAsync_ShouldReturnAllTransactions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var cardId = Guid.NewGuid();
        var grain = await CreateAndActivateCardAsync(orgId, cardId, 100m);
        await grain.RedeemAsync(new RedeemGiftCardCommand(20m, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));
        await grain.ReloadAsync(new ReloadGiftCardCommand(10m, Guid.NewGuid(), Guid.NewGuid()));

        // Act
        var transactions = await grain.GetTransactionsAsync();

        // Assert
        transactions.Should().HaveCount(3);
        transactions[0].Type.Should().Be(GiftCardTransactionType.Activation);
        transactions[1].Type.Should().Be(GiftCardTransactionType.Redemption);
        transactions[2].Type.Should().Be(GiftCardTransactionType.Reload);
    }
}
