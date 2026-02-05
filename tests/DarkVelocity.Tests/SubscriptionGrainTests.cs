using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class SubscriptionGrainTests
{
    private readonly TestClusterFixture _fixture;

    public SubscriptionGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    #region Subscription Creation Tests

    [Fact]
    public async Task CreateAsync_ShouldCreateSubscription()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));

        // Act
        var result = await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Starter, BillingCycle.Monthly));

        // Assert
        result.OrganizationId.Should().Be(orgId);
        result.Plan.Should().Be(SubscriptionPlan.Starter);
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAsync_WhenAlreadyExists_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand());

        // Act
        var act = () => grain.CreateAsync(new CreateSubscriptionCommand());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Subscription already exists");
    }

    [Fact]
    public async Task GetStateAsync_ShouldReturnCorrectPlanLimits()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Pro, BillingCycle.Annual));

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        state.Plan.Should().Be(SubscriptionPlan.Pro);
        state.BillingCycle.Should().Be(BillingCycle.Annual);
        state.Limits.MaxSites.Should().Be(10);
        state.Limits.MaxUsers.Should().Be(50);
        state.Limits.AdvancedReporting.Should().BeTrue();
        state.Limits.CustomBranding.Should().BeTrue();
    }

    #endregion

    #region Trial Management Tests

    [Fact]
    public async Task StartTrialAsync_ShouldStartTrial()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Free));

        // Act
        await grain.StartTrialAsync(new StartTrialCommand(SubscriptionPlan.Pro, TrialDays: 14));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(SubscriptionStatus.Trialing);
        state.Plan.Should().Be(SubscriptionPlan.Pro);
        state.TrialEndsAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(14), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task StartTrialAsync_WhenTrialAlreadyUsed_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Free));
        await grain.StartTrialAsync(new StartTrialCommand(SubscriptionPlan.Pro));

        // Act
        var act = () => grain.StartTrialAsync(new StartTrialCommand(SubscriptionPlan.Enterprise));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Trial has already been used");
    }

    [Fact]
    public async Task EndTrialAsync_ConvertToPaid_ShouldKeepPlan()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Free));
        await grain.StartTrialAsync(new StartTrialCommand(SubscriptionPlan.Pro));

        // Act
        await grain.EndTrialAsync(convertToPaid: true);

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(SubscriptionStatus.Active);
        state.Plan.Should().Be(SubscriptionPlan.Pro);
    }

    [Fact]
    public async Task EndTrialAsync_NotConverted_ShouldRevertToFree()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Free));
        await grain.StartTrialAsync(new StartTrialCommand(SubscriptionPlan.Pro));

        // Act
        await grain.EndTrialAsync(convertToPaid: false);

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(SubscriptionStatus.Cancelled);
        state.Plan.Should().Be(SubscriptionPlan.Free);
    }

    #endregion

    #region Plan Change Tests

    [Fact]
    public async Task ChangePlanAsync_ShouldChangePlan()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Starter));

        // Act
        var result = await grain.ChangePlanAsync(new ChangePlanCommand(SubscriptionPlan.Pro));

        // Assert
        result.OldPlan.Should().Be(SubscriptionPlan.Starter);
        result.NewPlan.Should().Be(SubscriptionPlan.Pro);

        var state = await grain.GetStateAsync();
        state.Plan.Should().Be(SubscriptionPlan.Pro);
        state.Limits.MaxSites.Should().Be(10);
    }

    [Fact]
    public async Task ChangePlanAsync_ToSamePlan_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Pro));

        // Act
        var act = () => grain.ChangePlanAsync(new ChangePlanCommand(SubscriptionPlan.Pro));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Already on this plan");
    }

    #endregion

    #region Usage Tracking Tests

    [Fact]
    public async Task RecordUsageAsync_ShouldUpdateUsageMetrics()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Starter));

        // Act
        await grain.RecordUsageAsync(new RecordUsageCommand(
            SitesCount: 2,
            UsersCount: 5,
            TransactionsDelta: 100));

        // Assert
        var usage = await grain.GetCurrentUsageAsync();
        usage.CurrentSites.Should().Be(2);
        usage.CurrentUsers.Should().Be(5);
        usage.TransactionsThisMonth.Should().Be(100);
    }

    [Fact]
    public async Task CheckUsageLimitsAsync_ShouldReturnCorrectStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Starter)); // MaxSites: 3
        await grain.RecordUsageAsync(new RecordUsageCommand(SitesCount: 2));

        // Act
        var limits = await grain.CheckUsageLimitsAsync();

        // Assert
        var siteLimit = limits.FirstOrDefault(l => l.LimitType == "sites");
        siteLimit.Should().NotBeNull();
        siteLimit!.CurrentValue.Should().Be(2);
        siteLimit.LimitValue.Should().Be(3);
        siteLimit.Exceeded.Should().BeFalse();
        siteLimit.PercentageUsed.Should().BeApproximately(66.67, 1);
    }

    [Fact]
    public async Task CanAddSiteAsync_WhenAtLimit_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Starter)); // MaxSites: 3
        await grain.RecordUsageAsync(new RecordUsageCommand(SitesCount: 3));

        // Act
        var canAdd = await grain.CanAddSiteAsync();

        // Assert
        canAdd.Should().BeFalse();
    }

    [Fact]
    public async Task CanAddUserAsync_WhenUnderLimit_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Starter)); // MaxUsers: 10
        await grain.RecordUsageAsync(new RecordUsageCommand(UsersCount: 5));

        // Act
        var canAdd = await grain.CanAddUserAsync();

        // Assert
        canAdd.Should().BeTrue();
    }

    #endregion

    #region Payment Method Tests

    [Fact]
    public async Task AddPaymentMethodAsync_ShouldAddPaymentMethod()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Starter));

        // Act
        await grain.AddPaymentMethodAsync(new AddPaymentMethodCommand(
            PaymentMethodId: "pm_test123",
            Type: "card",
            Last4: "4242",
            Brand: "visa",
            ExpMonth: 12,
            ExpYear: 2025));

        // Assert
        var state = await grain.GetStateAsync();
        state.PaymentMethods.Should().HaveCount(1);
        state.PaymentMethods[0].Last4.Should().Be("4242");
        state.PaymentMethods[0].Brand.Should().Be("visa");
        state.DefaultPaymentMethodId.Should().Be("pm_test123");
    }

    [Fact]
    public async Task RemovePaymentMethodAsync_WhenOnlyMethodOnPaidPlan_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Starter));
        await grain.AddPaymentMethodAsync(new AddPaymentMethodCommand("pm_test", "card", "4242"));

        // Act
        var act = () => grain.RemovePaymentMethodAsync("pm_test");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot remove the only payment method on a paid plan");
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task CancelAsync_Immediate_ShouldCancelImmediately()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Starter));

        // Act
        await grain.CancelAsync(new CancelSubscriptionCommand(Immediate: true, Reason: "No longer needed"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(SubscriptionStatus.Cancelled);
        state.CancelledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CancelAsync_EndOfPeriod_ShouldSetCancelAtPeriodEnd()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Starter));

        // Act
        await grain.CancelAsync(new CancelSubscriptionCommand(Immediate: false));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(SubscriptionStatus.Active); // Still active until period end
        state.CancelAtPeriodEnd.Should().NotBeNull();
    }

    [Fact]
    public async Task ReactivateAsync_ShouldReactivateSubscription()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Starter));
        await grain.CancelAsync(new CancelSubscriptionCommand(Immediate: true));

        // Act
        await grain.ReactivateAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(SubscriptionStatus.Active);
        state.CancelledAt.Should().BeNull();
    }

    #endregion

    #region Feature Access Tests

    [Fact]
    public async Task HasFeatureAsync_ProPlan_ShouldHaveAdvancedReporting()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Pro));

        // Act
        var hasFeature = await grain.HasFeatureAsync("advanced_reporting");

        // Assert
        hasFeature.Should().BeTrue();
    }

    [Fact]
    public async Task HasFeatureAsync_StarterPlan_ShouldNotHaveAdvancedReporting()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Starter));

        // Act
        var hasFeature = await grain.HasFeatureAsync("advanced_reporting");

        // Assert
        hasFeature.Should().BeFalse();
    }

    [Fact]
    public async Task HasFeatureAsync_EnterprisePlan_ShouldHaveSso()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Enterprise));

        // Act
        var hasFeature = await grain.HasFeatureAsync("sso");

        // Assert
        hasFeature.Should().BeTrue();
    }

    #endregion

    #region Pricing Tests

    [Fact]
    public async Task GetCurrentPriceAsync_MonthlyStarter_ShouldReturn49()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Starter, BillingCycle.Monthly));

        // Act
        var price = await grain.GetCurrentPriceAsync();

        // Assert
        price.Should().Be(49m);
    }

    [Fact]
    public async Task GetCurrentPriceAsync_AnnualPro_ShouldReturn1490()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISubscriptionGrain>(GrainKeys.Subscription(orgId));
        await grain.CreateAsync(new CreateSubscriptionCommand(SubscriptionPlan.Pro, BillingCycle.Annual));

        // Act
        var price = await grain.GetCurrentPriceAsync();

        // Assert
        price.Should().Be(1490m);
    }

    #endregion
}
