using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests.Domains.Payments;

/// <summary>
/// Tests for payment method validation edge cases including card validation,
/// bank account validation, and additional payment method scenarios.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PaymentMethodValidationTests
{
    private readonly TestClusterFixture _fixture;

    public PaymentMethodValidationTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPaymentMethodGrain GetPaymentMethodGrain(Guid accountId, Guid paymentMethodId)
        => _fixture.Cluster.GrainFactory.GetGrain<IPaymentMethodGrain>($"{accountId}:pm:{paymentMethodId}");

    // ============================================================================
    // Card Validation Edge Cases
    // ============================================================================

    // Given: A Discover card number is provided for payment method creation
    // When: The payment method is created
    // Then: The card brand is detected as "discover" with the correct last four digits
    [Fact]
    public async Task CreateAsync_WithDiscoverCard_ShouldDetectBrand()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("6011111111111117", 12, 2030));

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Card!.Brand.Should().Be("discover");
        result.Card.Last4.Should().Be("1117");
    }

    // Given: A Diners Club card number is provided for payment method creation
    // When: The payment method is created
    // Then: The card brand is detected as "diners" with the correct last four digits
    [Fact]
    public async Task CreateAsync_WithDinersCard_ShouldDetectBrand()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("30569309025904", 12, 2030));

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Card!.Brand.Should().Be("diners");
        result.Card.Last4.Should().Be("5904");
    }

    // Given: A JCB card number is provided for payment method creation
    // When: The payment method is created
    // Then: The card brand is detected as "jcb" with the correct last four digits
    [Fact]
    public async Task CreateAsync_WithJCBCard_ShouldDetectBrand()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("3530111333300000", 12, 2030));

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Card!.Brand.Should().Be("jcb");
        result.Card.Last4.Should().Be("0000");
    }

    // Given: A card with an expiry date in the current month and year
    // When: The payment method is created
    // Then: The card is accepted as still valid for the current month
    [Fact]
    public async Task CreateAsync_WithExpiryInCurrentMonth_ShouldSucceed()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var now = DateTime.UtcNow;
        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", now.Month, now.Year));

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Card!.ExpMonth.Should().Be(now.Month);
        result.Card.ExpYear.Should().Be(now.Year);
    }

    // Given: A card with a two-digit expiry year of 35
    // When: The payment method is created
    // Then: The expiry year is expanded to 2035
    [Fact]
    public async Task CreateAsync_WithTwoDigitYear_ShouldExpandToFourDigits()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 35)); // 2035

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Card!.ExpYear.Should().Be(2035);
    }

    // Given: A card with a cardholder name provided
    // When: The payment method is created
    // Then: The cardholder name is stored on the payment method
    [Fact]
    public async Task CreateAsync_WithCardholderName_ShouldStoreCardholderName()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030, "123", "John Smith"));

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Card!.CardholderName.Should().Be("John Smith");
    }

    // Given: An American Express card with a 3-digit CVC (Amex requires 4 digits)
    // When: The payment method creation is attempted
    // Then: The creation is rejected because the CVC is invalid for Amex
    [Fact]
    public async Task CreateAsync_WithInvalidCvc_ForAmex_ShouldThrow()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        // Amex requires 4-digit CVC
        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("378282246310005", 12, 2030, "123")); // 3-digit CVC for Amex

        // Act
        var act = () => grain.CreateAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid CVC");
    }

    // Given: An American Express card with a valid 4-digit CVC
    // When: The payment method is created
    // Then: The card is accepted and detected as "amex" brand
    [Fact]
    public async Task CreateAsync_WithValidCvc_ForAmex_ShouldSucceed()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("378282246310005", 12, 2030, "1234")); // 4-digit CVC for Amex

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Card!.Brand.Should().Be("amex");
    }

    // Given: A card with expiry month 13, which is invalid
    // When: The payment method creation is attempted
    // Then: The creation is rejected due to an invalid expiry month
    [Fact]
    public async Task CreateAsync_WithInvalidMonth_ShouldThrow()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 13, 2030)); // Month 13 is invalid

        // Act
        var act = () => grain.CreateAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // Given: A card with expiry month 0, which is invalid
    // When: The payment method creation is attempted
    // Then: The creation is rejected due to an invalid expiry month
    [Fact]
    public async Task CreateAsync_WithZeroMonth_ShouldThrow()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 0, 2030)); // Month 0 is invalid

        // Act
        var act = () => grain.CreateAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ============================================================================
    // Bank Account Validation Tests
    // ============================================================================

    // Given: A German IBAN bank account with individual holder details
    // When: The bank account payment method is created
    // Then: The last 4 digits are extracted from the IBAN along with country and currency
    [Fact]
    public async Task CreateAsync_WithIbanBankAccount_ShouldExtractLast4()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var bankDetails = new BankAccountDetails(
            Country: "DE",
            Currency: "eur",
            AccountHolderName: "Hans Mueller",
            AccountHolderType: "individual",
            Iban: "DE89370400440532013000");

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.BankAccount,
            BankAccount: bankDetails);

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.BankAccount!.Last4.Should().Be("3000");
        result.BankAccount.Country.Should().Be("DE");
        result.BankAccount.Currency.Should().Be("eur");
    }

    // Given: A US business bank account with routing and account numbers
    // When: The bank account payment method is created
    // Then: The account holder type is stored as "company" with the business name
    [Fact]
    public async Task CreateAsync_WithBusinessBankAccount_ShouldStoreHolderType()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var bankDetails = new BankAccountDetails(
            Country: "US",
            Currency: "usd",
            AccountHolderName: "Acme Corp",
            AccountHolderType: "company",
            RoutingNumber: "110000000",
            AccountNumber: "000999888777");

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.BankAccount,
            BankAccount: bankDetails);

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.BankAccount!.AccountHolderType.Should().Be("company");
        result.BankAccount.AccountHolderName.Should().Be("Acme Corp");
    }

    // ============================================================================
    // Update Operations Tests
    // ============================================================================

    // Given: An existing card payment method with valid expiry details
    // When: The expiry month is updated to 14, which is invalid
    // Then: The update is rejected due to an invalid expiry month
    [Fact]
    public async Task UpdateAsync_WithInvalidExpiryMonth_ShouldThrow()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        await grain.CreateAsync(new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030)));

        // Act
        var act = () => grain.UpdateAsync(expMonth: 14);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid expiry month");
    }

    // Given: An existing card payment method with valid expiry details
    // When: The expiry month is updated to 0, which is invalid
    // Then: The update is rejected due to an invalid expiry month
    [Fact]
    public async Task UpdateAsync_WithZeroExpiryMonth_ShouldThrow()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        await grain.CreateAsync(new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030)));

        // Act
        var act = () => grain.UpdateAsync(expMonth: 0);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid expiry month");
    }

    // Given: An existing card payment method expiring in 12/2030
    // When: Only the expiry month is updated to 6
    // Then: The expiry month changes to 6 while the year remains 2030
    [Fact]
    public async Task UpdateAsync_ExpiryOnlyMonth_ShouldKeepExistingYear()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        await grain.CreateAsync(new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030)));

        // Act
        var result = await grain.UpdateAsync(expMonth: 6);

        // Assert
        result.Card!.ExpMonth.Should().Be(6);
        result.Card.ExpYear.Should().Be(2030);
    }

    // Given: An existing card payment method expiring in 12/2030
    // When: Only the expiry year is updated to 2035
    // Then: The expiry year changes to 2035 while the month remains 12
    [Fact]
    public async Task UpdateAsync_ExpiryOnlyYear_ShouldKeepExistingMonth()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        await grain.CreateAsync(new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030)));

        // Act
        var result = await grain.UpdateAsync(expYear: 2035);

        // Assert
        result.Card!.ExpMonth.Should().Be(12);
        result.Card.ExpYear.Should().Be(2035);
    }

    // Given: An existing card payment method without billing details
    // When: Billing details including name, email, phone, and address are provided
    // Then: All billing details are stored on the payment method
    [Fact]
    public async Task UpdateAsync_BillingDetails_ShouldUpdateAddress()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        await grain.CreateAsync(new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030)));

        var newBillingDetails = new BillingDetails(
            Name: "Updated Name",
            Email: "updated@example.com",
            Phone: "+15555555555",
            Address: new PaymentMethodAddress(
                Line1: "456 New St",
                City: "New York",
                State: "NY",
                PostalCode: "10001",
                Country: "US"));

        // Act
        var result = await grain.UpdateAsync(billingDetails: newBillingDetails);

        // Assert
        result.BillingDetails!.Name.Should().Be("Updated Name");
        result.BillingDetails.Email.Should().Be("updated@example.com");
        result.BillingDetails.Address!.City.Should().Be("New York");
    }

    // Given: An existing card payment method with metadata key "key1"
    // When: Metadata is updated with a new key "key2"
    // Then: The metadata is fully replaced, not merged, so only "key2" exists
    [Fact]
    public async Task UpdateAsync_Metadata_ShouldReplaceExistingMetadata()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        await grain.CreateAsync(new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030),
            Metadata: new Dictionary<string, string> { ["key1"] = "value1" }));

        // Act
        var result = await grain.UpdateAsync(metadata: new Dictionary<string, string> { ["key2"] = "value2" });

        // Assert
        result.Metadata.Should().ContainKey("key2");
        result.Metadata.Should().NotContainKey("key1"); // Replaced, not merged
    }

    // ============================================================================
    // Customer Attachment Edge Cases
    // ============================================================================

    // Given: A card payment method that is not attached to any customer
    // When: A customer detachment is attempted
    // Then: The detachment is rejected because the card is not attached
    [Fact]
    public async Task DetachFromCustomerAsync_WhenNotAttached_ShouldThrow()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        await grain.CreateAsync(new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030)));

        // Act
        var act = () => grain.DetachFromCustomerAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not attached*");
    }

    // Given: A created card payment method
    // When: The processor token is retrieved multiple times
    // Then: The same token is returned each time
    [Fact]
    public async Task GetProcessorTokenAsync_MultipleCalls_ShouldReturnSameToken()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        await grain.CreateAsync(new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030)));

        // Act
        var token1 = await grain.GetProcessorTokenAsync();
        var token2 = await grain.GetProcessorTokenAsync();

        // Assert
        token1.Should().Be(token2);
    }

    // ============================================================================
    // Card Fingerprint Tests
    // ============================================================================

    // Given: Two payment methods created with the same card number on different accounts
    // When: Both payment methods are created
    // Then: Both produce the same card fingerprint for duplicate detection
    [Fact]
    public async Task CreateAsync_SameCardNumber_ShouldHaveSameFingerprint()
    {
        // Arrange
        var accountId1 = Guid.NewGuid();
        var accountId2 = Guid.NewGuid();
        var paymentMethodId1 = Guid.NewGuid();
        var paymentMethodId2 = Guid.NewGuid();
        var grain1 = GetPaymentMethodGrain(accountId1, paymentMethodId1);
        var grain2 = GetPaymentMethodGrain(accountId2, paymentMethodId2);

        // Act
        var result1 = await grain1.CreateAsync(new CreatePaymentMethodCommand(
            accountId1,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030)));

        var result2 = await grain2.CreateAsync(new CreatePaymentMethodCommand(
            accountId2,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030)));

        // Assert - Same card number should produce same fingerprint
        result1.Card!.Fingerprint.Should().Be(result2.Card!.Fingerprint);
    }

    // Given: Two payment methods created with different card numbers (Visa and Mastercard)
    // When: Both payment methods are created
    // Then: Each produces a distinct card fingerprint
    [Fact]
    public async Task CreateAsync_DifferentCardNumbers_ShouldHaveDifferentFingerprints()
    {
        // Arrange
        var accountId1 = Guid.NewGuid();
        var accountId2 = Guid.NewGuid();
        var paymentMethodId1 = Guid.NewGuid();
        var paymentMethodId2 = Guid.NewGuid();
        var grain1 = GetPaymentMethodGrain(accountId1, paymentMethodId1);
        var grain2 = GetPaymentMethodGrain(accountId2, paymentMethodId2);

        // Act
        var result1 = await grain1.CreateAsync(new CreatePaymentMethodCommand(
            accountId1,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030)));

        var result2 = await grain2.CreateAsync(new CreatePaymentMethodCommand(
            accountId2,
            PaymentMethodType.Card,
            new CardDetails("5555555555554444", 12, 2030))); // Mastercard

        // Assert - Different card numbers should produce different fingerprints
        result1.Card!.Fingerprint.Should().NotBe(result2.Card!.Fingerprint);
    }

    // ============================================================================
    // Non-existent Payment Method Tests
    // ============================================================================

    // Given: A payment method identifier that has never been created
    // When: Retrieving the payment method snapshot
    // Then: The request is rejected because the payment method does not exist
    [Fact]
    public async Task GetSnapshotAsync_WhenNotCreated_ShouldThrow()
    {
        // Arrange
        var grain = GetPaymentMethodGrain(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = () => grain.GetSnapshotAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist*");
    }

    // Given: A payment method identifier that has never been created
    // When: Retrieving the processor token
    // Then: The request is rejected because the payment method does not exist
    [Fact]
    public async Task GetProcessorTokenAsync_WhenNotCreated_ShouldThrow()
    {
        // Arrange
        var grain = GetPaymentMethodGrain(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = () => grain.GetProcessorTokenAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist*");
    }

    // Given: A payment method identifier that has never been created
    // When: Attaching the payment method to a customer
    // Then: The request is rejected because the payment method does not exist
    [Fact]
    public async Task AttachToCustomerAsync_WhenNotCreated_ShouldThrow()
    {
        // Arrange
        var grain = GetPaymentMethodGrain(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = () => grain.AttachToCustomerAsync("cus_123");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist*");
    }

    // Given: A payment method identifier that has never been created
    // When: Updating the payment method expiry
    // Then: The request is rejected because the payment method does not exist
    [Fact]
    public async Task UpdateAsync_WhenNotCreated_ShouldThrow()
    {
        // Arrange
        var grain = GetPaymentMethodGrain(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = () => grain.UpdateAsync(expMonth: 6);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*does not exist*");
    }

    // ============================================================================
    // Duplicate Creation Test
    // ============================================================================

    // Given: A payment method that has already been created
    // When: A second creation is attempted for the same payment method
    // Then: The creation is rejected because the payment method already exists
    [Fact]
    public async Task CreateAsync_Twice_ShouldThrow()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        await grain.CreateAsync(new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030)));

        // Act
        var act = () => grain.CreateAsync(new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("5555555555554444", 12, 2030)));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }
}
