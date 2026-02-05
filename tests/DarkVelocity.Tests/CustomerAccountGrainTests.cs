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
