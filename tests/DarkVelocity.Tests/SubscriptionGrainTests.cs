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

    // Given: a new organization with no existing subscription
    // When: a Starter plan subscription with monthly billing is created
    // Then: the subscription should be associated with the organization, on the Starter plan, with a recent creation timestamp
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

    // Given: an organization that already has an active subscription
    // When: a second subscription creation is attempted
    // Then: the system should reject the duplicate subscription
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

    // Given: an organization on the Pro plan with annual billing
    // When: the subscription state is retrieved
    // Then: the plan limits should allow 10 sites, 50 users, advanced reporting, and custom branding
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

    // Given: an organization on the Free plan
    // When: a 14-day trial of the Pro plan is started
    // Then: the subscription should be in trialing status on the Pro plan with a trial end date 14 days out
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

    // Given: an organization that has already used its trial for the Pro plan
    // When: a second trial is attempted for the Enterprise plan
    // Then: the system should reject the trial since the organization has already used its trial
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

    // Given: an organization trialing the Pro plan
    // When: the trial ends and the organization converts to a paid subscription
    // Then: the subscription should become active on the Pro plan
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

    // Given: an organization trialing the Pro plan
    // When: the trial ends without converting to a paid subscription
    // Then: the subscription should revert to the Free plan with cancelled status
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

    // Given: an organization on the Starter plan
    // When: the plan is upgraded to Pro
    // Then: the change should record the old (Starter) and new (Pro) plans, and limits should update to Pro tier (10 sites)
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

    // Given: an organization already on the Pro plan
    // When: a plan change to Pro is attempted (same plan)
    // Then: the system should reject the no-op change
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

    // Given: an organization on the Starter plan with no prior usage recorded
    // When: usage is recorded with 2 sites, 5 users, and 100 transactions
    // Then: the current usage should reflect 2 sites, 5 users, and 100 monthly transactions
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

    // Given: a Starter plan subscription (max 3 sites) with 2 sites currently in use
    // When: usage limits are checked
    // Then: the sites limit should show 2 of 3 used (~67%), not exceeded
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

    // Given: a Starter plan subscription (max 3 sites) with all 3 sites already in use
    // When: a check is made whether another site can be added
    // Then: the check should return false since the site limit has been reached
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

    // Given: a Starter plan subscription (max 10 users) with 5 users currently active
    // When: a check is made whether another user can be added
    // Then: the check should return true since the user limit has not been reached
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

    // Given: a Starter plan subscription with no payment methods
    // When: a Visa card ending in 4242 is added as a payment method
    // Then: the payment method should be stored as the default with correct card details
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

    // Given: a paid Starter plan subscription with only one payment method on file
    // When: removal of the sole payment method is attempted
    // Then: the system should reject the removal to prevent a paid plan from having no payment method
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

    // Given: an active Starter plan subscription
    // When: the subscription is cancelled immediately with a reason
    // Then: the status should change to cancelled with a recent cancellation timestamp
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

    // Given: an active Starter plan subscription
    // When: the subscription is cancelled at the end of the current billing period (not immediate)
    // Then: the subscription should remain active but with a scheduled cancellation date at period end
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

    // Given: a previously cancelled Starter plan subscription
    // When: the subscription is reactivated
    // Then: the status should return to active with the cancellation timestamp cleared
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

    // Given: an organization on the Pro plan
    // When: access to the advanced_reporting feature is checked
    // Then: the feature should be available on the Pro tier
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

    // Given: an organization on the Starter plan
    // When: access to the advanced_reporting feature is checked
    // Then: the feature should not be available on the Starter tier
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

    // Given: an organization on the Enterprise plan
    // When: access to the SSO feature is checked
    // Then: SSO should be available as an Enterprise-tier feature
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

    // Given: an organization on the Starter plan with monthly billing
    // When: the current subscription price is retrieved
    // Then: the monthly price should be $49
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

    // Given: an organization on the Pro plan with annual billing
    // When: the current subscription price is retrieved
    // Then: the annual price should be $1,490
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
