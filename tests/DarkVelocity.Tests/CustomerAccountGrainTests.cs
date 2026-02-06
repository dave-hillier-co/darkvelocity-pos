using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class CustomerAccountGrainTests
{
    private readonly TestClusterFixture _fixture;

    public CustomerAccountGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    // Given: A new customer with no existing house account
    // When: A house account is opened with a $500 credit limit and 30-day payment terms
    // Then: The account is created in Active status with zero balance and the specified credit terms
    [Fact]
    public async Task OpenAsync_ShouldCreateAccount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));

        // Act
        var result = await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));

        // Assert
        result.CustomerId.Should().Be(customerId);
        result.CreditLimit.Should().Be(500m);

        var state = await grain.GetStateAsync();
        state.Status.Should().Be(CustomerAccountStatus.Active);
        state.Balance.Should().Be(0);
        state.CreditLimit.Should().Be(500m);
        state.PaymentTermsDays.Should().Be(30);
    }

    // Given: An active customer house account with a $500 credit limit and zero balance
    // When: A $100 dinner charge is posted to the account
    // Then: The balance increases to $100 and available credit decreases to $400
    [Fact]
    public async Task ChargeAsync_ShouldIncreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));

        // Act
        var result = await grain.ChargeAsync(new ChargeAccountCommand(orderId, 100m, "Dinner", userId));

        // Assert
        result.NewBalance.Should().Be(100m);
        result.AvailableCredit.Should().Be(400m);

        var state = await grain.GetStateAsync();
        state.Balance.Should().Be(100m);
        state.TotalCharges.Should().Be(100m);
    }

    // Given: An active customer house account with a $100 credit limit
    // When: A $150 charge is attempted that would exceed the credit limit
    // Then: The charge is rejected to prevent exceeding the customer's approved credit
    [Fact]
    public async Task ChargeAsync_ExceedsCreditLimit_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 100m, 30, userId));

        // Act
        var act = () => grain.ChargeAsync(new ChargeAccountCommand(Guid.NewGuid(), 150m, "Large order", userId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceed credit limit*");
    }

    // Given: A customer house account with a $200 outstanding balance
    // When: A $150 credit card payment is applied to the account
    // Then: The balance decreases to $50 and total payments reflect the $150 received
    [Fact]
    public async Task ApplyPaymentAsync_ShouldDecreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));
        await grain.ChargeAsync(new ChargeAccountCommand(Guid.NewGuid(), 200m, "Charges", userId));

        // Act
        var result = await grain.ApplyPaymentAsync(new ApplyPaymentCommand(150m, PaymentMethod.CreditCard, "CHK-123", userId));

        // Assert
        result.NewBalance.Should().Be(50m);
        result.PaymentAmount.Should().Be(150m);

        var state = await grain.GetStateAsync();
        state.Balance.Should().Be(50m);
        state.TotalPayments.Should().Be(150m);
    }

    // Given: A customer house account with a $100 outstanding balance
    // When: A $25 goodwill credit adjustment is applied
    // Then: The balance decreases to $75
    [Fact]
    public async Task ApplyCreditAsync_ShouldDecreaseBalance()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));
        await grain.ChargeAsync(new ChargeAccountCommand(Guid.NewGuid(), 100m, "Charges", userId));

        // Act
        await grain.ApplyCreditAsync(new ApplyCreditCommand(25m, "Goodwill adjustment", userId));

        // Assert
        var balance = await grain.GetBalanceAsync();
        balance.Should().Be(75m);
    }

    // Given: A customer house account with a $500 credit limit
    // When: The credit limit is increased to $1000 due to good payment history
    // Then: The account reflects the new $1000 credit limit
    [Fact]
    public async Task ChangeCreditLimitAsync_ShouldUpdateLimit()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));

        // Act
        await grain.ChangeCreditLimitAsync(1000m, "Good payment history", userId);

        // Assert
        var state = await grain.GetStateAsync();
        state.CreditLimit.Should().Be(1000m);
    }

    // Given: A customer house account with a $300 outstanding balance and $500 credit limit
    // When: The credit limit is reduced to $200, below the current balance
    // Then: The reduction is rejected to prevent the balance from exceeding the limit
    [Fact]
    public async Task ChangeCreditLimitAsync_BelowBalance_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));
        await grain.ChargeAsync(new ChargeAccountCommand(Guid.NewGuid(), 300m, "Charges", userId));

        // Act
        var act = () => grain.ChangeCreditLimitAsync(200m, "Reducing limit", userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*less than current balance*");
    }

    // Given: An active customer house account
    // When: The account is suspended due to overdue payments
    // Then: The account status changes to Suspended with the reason recorded
    [Fact]
    public async Task SuspendAsync_ShouldSuspendAccount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));

        // Act
        await grain.SuspendAsync("Overdue payments", userId);

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(CustomerAccountStatus.Suspended);
        state.SuspensionReason.Should().Be("Overdue payments");
    }

    // Given: A customer house account that has been suspended for overdue payments
    // When: A new charge is attempted against the suspended account
    // Then: The charge is rejected since the account is not active
    [Fact]
    public async Task ChargeAsync_SuspendedAccount_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));
        await grain.SuspendAsync("Overdue", userId);

        // Act
        var act = () => grain.ChargeAsync(new ChargeAccountCommand(Guid.NewGuid(), 50m, "New charge", userId));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not active*");
    }

    // Given: A suspended customer house account
    // When: The account is reactivated by an operator
    // Then: The account status returns to Active
    [Fact]
    public async Task ReactivateAsync_ShouldReactivateAccount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));
        await grain.SuspendAsync("Overdue", userId);

        // Act
        await grain.ReactivateAsync(userId);

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(CustomerAccountStatus.Active);
    }

    // Given: A customer house account with a $100 outstanding balance
    // When: An attempt is made to close the account
    // Then: Closure is rejected because the account has an outstanding balance
    [Fact]
    public async Task CloseAsync_WithBalance_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));
        await grain.ChargeAsync(new ChargeAccountCommand(Guid.NewGuid(), 100m, "Charges", userId));

        // Act
        var act = () => grain.CloseAsync("Customer request", userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*outstanding balance*");
    }

    // Given: A customer house account with zero balance
    // When: The account is closed at the customer's request
    // Then: The account status changes to Closed
    [Fact]
    public async Task CloseAsync_ZeroBalance_ShouldCloseAccount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));

        // Act
        await grain.CloseAsync("Customer request", userId);

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(CustomerAccountStatus.Closed);
    }

    // Given: A customer house account with two charges ($100 and $75) and one $50 payment
    // When: A statement is generated for the transaction period
    // Then: The statement shows $175 in charges, $50 in payments, and a $125 closing balance
    [Fact]
    public async Task GenerateStatementAsync_ShouldGenerateStatement()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));

        // Add some transactions
        await grain.ChargeAsync(new ChargeAccountCommand(Guid.NewGuid(), 100m, "Meal 1", userId));
        await grain.ChargeAsync(new ChargeAccountCommand(Guid.NewGuid(), 75m, "Meal 2", userId));
        await grain.ApplyPaymentAsync(new ApplyPaymentCommand(50m, PaymentMethod.Cash, null, userId));

        // Act
        var statement = await grain.GenerateStatementAsync(new GenerateStatementCommand(
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))));

        // Assert
        statement.StatementId.Should().NotBeEmpty();
        statement.TotalCharges.Should().Be(175m);
        statement.TotalPayments.Should().Be(50m);
        statement.ClosingBalance.Should().Be(125m);
    }

    // Given: A customer house account with two charges and one payment
    // When: The recent transaction history is retrieved
    // Then: Three transactions are returned with the most recent (payment) listed first
    [Fact]
    public async Task GetTransactionsAsync_ShouldReturnRecentTransactions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));
        await grain.ChargeAsync(new ChargeAccountCommand(Guid.NewGuid(), 100m, "Charge 1", userId));
        await grain.ChargeAsync(new ChargeAccountCommand(Guid.NewGuid(), 50m, "Charge 2", userId));
        await grain.ApplyPaymentAsync(new ApplyPaymentCommand(30m, PaymentMethod.Cash, null, userId));

        // Act
        var transactions = await grain.GetTransactionsAsync(10);

        // Assert
        transactions.Should().HaveCount(3);
        transactions[0].Type.Should().Be(AccountTransactionType.Payment); // Most recent first
    }

    // Given: A customer house account with $200 charged against a $500 credit limit
    // When: A $100 charge eligibility check is performed
    // Then: The check confirms the charge is allowed within available credit
    [Fact]
    public async Task CanChargeAsync_WithAvailableCredit_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));
        await grain.ChargeAsync(new ChargeAccountCommand(Guid.NewGuid(), 200m, "Charges", userId));

        // Act
        var canCharge = await grain.CanChargeAsync(100m);

        // Assert
        canCharge.Should().BeTrue();
    }

    // Given: A customer house account with $450 charged against a $500 credit limit
    // When: A $100 charge eligibility check is performed
    // Then: The check denies the charge as it would exceed available credit
    [Fact]
    public async Task CanChargeAsync_ExceedingCredit_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 500m, 30, userId));
        await grain.ChargeAsync(new ChargeAccountCommand(Guid.NewGuid(), 450m, "Charges", userId));

        // Act
        var canCharge = await grain.CanChargeAsync(100m);

        // Assert
        canCharge.Should().BeFalse();
    }

    // Given: A customer house account with $250 in charges and $100 in payments against a $1000 limit
    // When: The account summary is retrieved
    // Then: The summary shows $150 balance, $850 available credit, and accurate charge/payment totals
    [Fact]
    public async Task GetSummaryAsync_ShouldReturnAccountSummary()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var grain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerAccountGrain>(
            GrainKeys.CustomerAccount(orgId, customerId));
        await grain.OpenAsync(new OpenCustomerAccountCommand(orgId, 1000m, 30, userId));
        await grain.ChargeAsync(new ChargeAccountCommand(Guid.NewGuid(), 250m, "Charges", userId));
        await grain.ApplyPaymentAsync(new ApplyPaymentCommand(100m, PaymentMethod.CreditCard, null, userId));

        // Act
        var summary = await grain.GetSummaryAsync();

        // Assert
        summary.CustomerId.Should().Be(customerId);
        summary.Balance.Should().Be(150m);
        summary.CreditLimit.Should().Be(1000m);
        summary.AvailableCredit.Should().Be(850m);
        summary.TotalCharges.Should().Be(250m);
        summary.TotalPayments.Should().Be(100m);
    }
}
