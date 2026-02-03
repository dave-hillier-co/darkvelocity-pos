using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class PaymentMethodGrainTests
{
    private readonly TestClusterFixture _fixture;

    public PaymentMethodGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IPaymentMethodGrain GetPaymentMethodGrain(Guid accountId, Guid paymentMethodId)
        => _fixture.Cluster.GrainFactory.GetGrain<IPaymentMethodGrain>($"{accountId}:pm:{paymentMethodId}");

    [Fact]
    public async Task CreateAsync_WithValidCard_ShouldCreatePaymentMethod()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030, "123", "John Doe"));

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Id.Should().Be(paymentMethodId);
        result.AccountId.Should().Be(accountId);
        result.Type.Should().Be(PaymentMethodType.Card);
        result.Card.Should().NotBeNull();
        result.Card!.Brand.Should().Be("visa");
        result.Card.Last4.Should().Be("4242");
        result.Card.ExpMonth.Should().Be(12);
        result.Card.ExpYear.Should().Be(2030);
        result.Card.Fingerprint.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateAsync_WithMastercard_ShouldDetectBrand()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("5555555555554444", 6, 2028));

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Card!.Brand.Should().Be("mastercard");
        result.Card.Last4.Should().Be("4444");
    }

    [Fact]
    public async Task CreateAsync_WithAmex_ShouldDetectBrand()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("378282246310005", 3, 2027, "1234"));

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Card!.Brand.Should().Be("amex");
        result.Card.Last4.Should().Be("0005");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidCardNumber_ShouldThrow()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("1234567890123456", 12, 2030));

        // Act
        var act = () => grain.CreateAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid card number");
    }

    [Fact]
    public async Task CreateAsync_WithExpiredCard_ShouldThrow()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 1, 2020));

        // Act
        var act = () => grain.CreateAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Card is expired");
    }

    [Fact]
    public async Task CreateAsync_WithBillingDetails_ShouldStoreBillingDetails()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var billingDetails = new BillingDetails(
            Name: "John Doe",
            Email: "john@example.com",
            Phone: "+1234567890",
            Address: new PaymentMethodAddress(
                Line1: "123 Main St",
                City: "San Francisco",
                State: "CA",
                PostalCode: "94105",
                Country: "US"));

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030),
            BillingDetails: billingDetails);

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.BillingDetails.Should().NotBeNull();
        result.BillingDetails!.Name.Should().Be("John Doe");
        result.BillingDetails.Email.Should().Be("john@example.com");
        result.BillingDetails.Address!.City.Should().Be("San Francisco");
    }

    [Fact]
    public async Task GetProcessorTokenAsync_ShouldReturnToken()
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
        var token = await grain.GetProcessorTokenAsync();

        // Assert
        token.Should().NotBeNullOrEmpty();
        token.Should().StartWith("pm_card_");
        token.Should().EndWith("4242");
    }

    [Fact]
    public async Task AttachToCustomerAsync_ShouldAttachCustomer()
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
        var result = await grain.AttachToCustomerAsync("cus_abc123");

        // Assert
        result.CustomerId.Should().Be("cus_abc123");
    }

    [Fact]
    public async Task AttachToCustomerAsync_WhenAlreadyAttached_ShouldThrow()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        await grain.CreateAsync(new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030)));

        await grain.AttachToCustomerAsync("cus_abc123");

        // Act
        var act = () => grain.AttachToCustomerAsync("cus_xyz456");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already attached*");
    }

    [Fact]
    public async Task DetachFromCustomerAsync_ShouldDetachCustomer()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        await grain.CreateAsync(new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030)));

        await grain.AttachToCustomerAsync("cus_abc123");

        // Act
        var result = await grain.DetachFromCustomerAsync();

        // Assert
        result.CustomerId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateExpiry()
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
        var result = await grain.UpdateAsync(expMonth: 6, expYear: 2032);

        // Assert
        result.Card!.ExpMonth.Should().Be(6);
        result.Card.ExpYear.Should().Be(2032);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateMetadata()
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
        var result = await grain.UpdateAsync(
            metadata: new Dictionary<string, string> { ["order_id"] = "12345" });

        // Assert
        result.Metadata.Should().ContainKey("order_id");
        result.Metadata!["order_id"].Should().Be("12345");
    }

    [Fact]
    public async Task ExistsAsync_WhenNotCreated_ShouldReturnFalse()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenCreated_ShouldReturnTrue()
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
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WithBankAccount_ShouldCreateBankPaymentMethod()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        var bankDetails = new BankAccountDetails(
            Country: "US",
            Currency: "usd",
            AccountHolderName: "John Doe",
            AccountHolderType: "individual",
            RoutingNumber: "110000000",
            AccountNumber: "000123456789");

        var command = new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.BankAccount,
            BankAccount: bankDetails);

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Type.Should().Be(PaymentMethodType.BankAccount);
        result.BankAccount.Should().NotBeNull();
        result.BankAccount!.Last4.Should().Be("6789");
        result.BankAccount.AccountHolderName.Should().Be("John Doe");
        result.BankAccount.Country.Should().Be("US");
    }

    [Fact]
    public async Task GetSnapshotAsync_ShouldReturnCurrentState()
    {
        // Arrange
        var accountId = Guid.NewGuid();
        var paymentMethodId = Guid.NewGuid();
        var grain = GetPaymentMethodGrain(accountId, paymentMethodId);

        await grain.CreateAsync(new CreatePaymentMethodCommand(
            accountId,
            PaymentMethodType.Card,
            new CardDetails("4242424242424242", 12, 2030, "123", "Jane Smith")));

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.Id.Should().Be(paymentMethodId);
        snapshot.Card!.CardholderName.Should().Be("Jane Smith");
        snapshot.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
