using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class OrganizationGrainTests
{
    private readonly TestClusterFixture _fixture;

    public OrganizationGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateOrganization()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));

        var command = new CreateOrganizationCommand("Test Org", "test-org");

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Id.Should().Be(orgId);
        result.Slug.Should().Be("test-org");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateAsync_WhenAlreadyExists_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));

        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org"));

        // Act
        var act = () => grain.CreateAsync(new CreateOrganizationCommand("Another Org", "another-org"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Organization already exists");
    }

    [Fact]
    public async Task GetStateAsync_ShouldReturnState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));

        var settings = new OrganizationSettings
        {
            DefaultCurrency = "GBP",
            DefaultTimezone = "Europe/London"
        };
        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org", settings));

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        state.Id.Should().Be(orgId);
        state.Name.Should().Be("Test Org");
        state.Slug.Should().Be("test-org");
        state.Status.Should().Be(OrganizationStatus.Active);
        state.Settings.DefaultCurrency.Should().Be("GBP");
        state.Settings.DefaultTimezone.Should().Be("Europe/London");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateOrganization()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));

        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org"));

        // Act
        var result = await grain.UpdateAsync(new UpdateOrganizationCommand(Name: "Updated Org"));

        // Assert
        result.Version.Should().Be(2);
        var state = await grain.GetStateAsync();
        state.Name.Should().Be("Updated Org");
    }

    [Fact]
    public async Task SuspendAsync_ShouldSetStatusToSuspended()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));

        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org"));

        // Act
        await grain.SuspendAsync("Non-payment");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrganizationStatus.Suspended);
    }

    [Fact]
    public async Task ReactivateAsync_ShouldSetStatusToActive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));

        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org"));
        await grain.SuspendAsync("Non-payment");

        // Act
        await grain.ReactivateAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrganizationStatus.Active);
    }

    [Fact]
    public async Task ReactivateAsync_WhenNotSuspended_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));

        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org"));

        // Act
        var act = () => grain.ReactivateAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Organization is not suspended");
    }

    [Fact]
    public async Task AddSiteAsync_ShouldAddSiteId()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));

        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org"));

        // Act
        await grain.AddSiteAsync(siteId);

        // Assert
        var siteIds = await grain.GetSiteIdsAsync();
        siteIds.Should().Contain(siteId);
    }

    [Fact]
    public async Task AddSiteAsync_ShouldBeIdempotent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));

        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org"));

        // Act
        await grain.AddSiteAsync(siteId);
        await grain.AddSiteAsync(siteId);

        // Assert
        var siteIds = await grain.GetSiteIdsAsync();
        siteIds.Count(id => id == siteId).Should().Be(1);
    }

    [Fact]
    public async Task RemoveSiteAsync_ShouldRemoveSiteId()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));

        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org"));
        await grain.AddSiteAsync(siteId);

        // Act
        await grain.RemoveSiteAsync(siteId);

        // Assert
        var siteIds = await grain.GetSiteIdsAsync();
        siteIds.Should().NotContain(siteId);
    }

    [Fact]
    public async Task ExistsAsync_WhenCreated_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));

        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org"));

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WhenNotCreated_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeFalse();
    }

    #region Extended Organization Domain Tests

    [Fact]
    public async Task UpdateBrandingAsync_ShouldUpdateBranding()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org-branding"));

        // Act
        await grain.UpdateBrandingAsync(new UpdateBrandingCommand(
            LogoUrl: "https://example.com/logo.png",
            PrimaryColor: "#ff0000",
            SecondaryColor: "#00ff00"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Branding.LogoUrl.Should().Be("https://example.com/logo.png");
        state.Branding.PrimaryColor.Should().Be("#ff0000");
        state.Branding.SecondaryColor.Should().Be("#00ff00");
    }

    [Fact]
    public async Task SetFeatureFlagAsync_ShouldSetFeatureFlag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org-feature"));

        // Act
        await grain.SetFeatureFlagAsync("beta_feature", true);

        // Assert
        var isEnabled = await grain.GetFeatureFlagAsync("beta_feature");
        isEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task GetFeatureFlagAsync_WhenNotSet_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org-feature-unset"));

        // Act
        var isEnabled = await grain.GetFeatureFlagAsync("nonexistent_feature");

        // Assert
        isEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task ConfigureCustomDomainAsync_ShouldSetupDomainWithVerificationToken()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org-domain"));

        // Act
        await grain.ConfigureCustomDomainAsync("custom.example.com");

        // Assert
        var state = await grain.GetStateAsync();
        state.CustomDomain.Should().NotBeNull();
        state.CustomDomain!.Domain.Should().Be("custom.example.com");
        state.CustomDomain.Verified.Should().BeFalse();
        state.CustomDomain.VerificationToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InitiateCancellationAsync_Immediate_ShouldCancelImmediately()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org-cancel"));

        // Act
        var result = await grain.InitiateCancellationAsync(new InitiateCancellationCommand(
            Reason: "No longer needed",
            Immediate: true));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrganizationStatus.Cancelled);
        state.Cancellation.Should().NotBeNull();
        state.Cancellation!.Reason.Should().Be("No longer needed");
        result.EffectiveDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InitiateCancellationAsync_EndOfPeriod_ShouldSetPendingCancellation()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org-cancel-pending"));

        // Act
        var result = await grain.InitiateCancellationAsync(new InitiateCancellationCommand(
            Reason: "Cost reduction",
            Immediate: false));

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrganizationStatus.PendingCancellation);
        state.Cancellation.Should().NotBeNull();
        result.EffectiveDate.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task ReactivateFromCancellationAsync_ShouldReactivateOrganization()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org-reactivate"));
        await grain.InitiateCancellationAsync(new InitiateCancellationCommand(Immediate: false));

        // Act
        await grain.ReactivateFromCancellationAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(OrganizationStatus.Active);
        state.Cancellation.Should().BeNull();
    }

    [Fact]
    public async Task ChangeSlugAsync_ShouldChangeSlugAndRecordHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "old-slug"));

        // Act
        await grain.ChangeSlugAsync(new ChangeSlugCommand("new-slug"));

        // Assert
        var state = await grain.GetStateAsync();
        state.Slug.Should().Be("new-slug");
        state.SlugHistory.Should().Contain("old-slug");
    }

    [Fact]
    public async Task GetVersionAsync_ShouldReturnCorrectVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
        await grain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org-version"));

        // Act
        await grain.UpdateAsync(new UpdateOrganizationCommand(Name: "Updated Name"));
        await grain.SetFeatureFlagAsync("feature1", true);
        var version = await grain.GetVersionAsync();

        // Assert
        version.Should().BeGreaterThanOrEqualTo(3); // Create + Update + SetFeatureFlag
    }

    #endregion
}
