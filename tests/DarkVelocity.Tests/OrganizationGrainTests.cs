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
}
