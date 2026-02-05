using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
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

    // Given: a new gift card grain with no prior state
    // When: a digital gift card is created with a $50 value and 6-month expiry
    // Then: the card is created in inactive status with the correct initial balance
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

    // Given: an inactive gift card with a $100 balance
    // When: the card is activated at a site with purchaser details
    // Then: the card becomes active with an activation transaction recorded
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

    // Given: an inactive gift card that has been created
    // When: recipient details including name, email, phone, and a personal message are set
    // Then: the gift card stores the recipient information and message
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

    // Given: an active gift card with a $100 balance
    // When: $30 is redeemed against an order
    // Then: the balance decreases to $70 and the redemption count increments
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

    // Given: an active gift card with a $50 balance
    // When: a $100 redemption is attempted, exceeding the available balance
    // Then: the redemption is rejected with an insufficient balance error
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

    // Given: an active gift card with a $50 balance
    // When: the full $50 balance is redeemed
    // Then: the card balance reaches zero and the status changes to depleted
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

    // Given: an active gift card with a $50 balance
    // When: $25 is reloaded onto the card with a birthday note
    // Then: the balance increases to $75 and the reload amount is tracked
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

    // Given: a depleted gift card with a zero balance after full redemption
    // When: $30 is reloaded onto the card
    // Then: the card reactivates with a $30 balance and returns to active status
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

    // Given: an active gift card with $70 remaining after a $30 redemption
    // When: $15 is refunded back to the card from a previous order
    // Then: the balance increases to $85
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
            Amount: 15m,
            OriginalPaymentId: Guid.NewGuid(),
            SiteId: Guid.NewGuid(),
            PerformedBy: Guid.NewGuid(),
            Notes: "Partial refund"));

        // Assert
        newBalance.Should().Be(85m); // 100 - 30 + 15
    }

    // Given: an active gift card with a $100 balance
    // When: a -$20 balance adjustment is applied with a correction reason
    // Then: the balance decreases to $80 and an adjustment transaction is recorded
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

    // Given: an active gift card with a $50 balance
    // When: a -$100 adjustment is attempted that would result in a negative balance
    // Then: the adjustment is rejected with a negative balance error
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

    // Given: a gift card created with PIN "1234"
    // When: the PIN "1234" is validated
    // Then: validation succeeds
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

    // Given: a gift card created with PIN "1234"
    // When: an incorrect PIN "5678" is validated
    // Then: validation fails
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

    // Given: a gift card created without a PIN
    // When: any PIN is submitted for validation
    // Then: validation succeeds because no PIN is required
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

    // Given: an active gift card with a $50 balance
    // When: the card is expired
    // Then: the status changes to expired, the balance is zeroed out, and an expiration transaction is logged
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

    // Given: an active gift card with a $75 balance
    // When: the card is cancelled due to a lost card report
    // Then: the status changes to cancelled, the balance is zeroed, and a void transaction is recorded
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

    // Given: an active gift card that has had a $30 redemption applied
    // When: the redemption transaction is voided due to a customer dispute
    // Then: the balance is restored to $100 and a void transaction is recorded
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

    // Given: an active gift card with a $75 balance
    // When: the balance information is queried
    // Then: the response includes the current balance, active status, and expiry date
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

    // Given: an active gift card with a $100 balance
    // When: a $50 sufficiency check is performed
    // Then: the card confirms it has sufficient funds
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

    // Given: an active gift card with a $30 balance
    // When: a $50 sufficiency check is performed
    // Then: the card reports insufficient funds
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

    // Given: an inactive (not yet activated) gift card with a $100 value
    // When: a $50 sufficiency check is performed
    // Then: the card reports insufficient funds because it is not yet active
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

    // Given: an active gift card that has been activated, had a $20 redemption, and a $10 reload
    // When: the transaction history is queried
    // Then: all three transactions are returned in chronological order
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
