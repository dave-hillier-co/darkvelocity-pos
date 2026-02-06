using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class SiteGrainTests
{
    private readonly TestClusterFixture _fixture;

    public SiteGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<Guid> CreateOrganizationAsync()
    {
        var orgId = Guid.NewGuid();
        var orgGrain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));
        await orgGrain.CreateAsync(new CreateOrganizationCommand("Test Org", "test-org"));
        return orgId;
    }

    // Given: an organization with no venues
    // When: a new site is created with name, code, and address
    // Then: the site is created with a unique ID, the correct code, and a creation timestamp
    [Fact]
    public async Task CreateAsync_ShouldCreateSite()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));

        var command = new CreateSiteCommand(
            orgId,
            "Downtown Store",
            "DT01",
            new Address { Street = "123 Main St", City = "New York", State = "NY", PostalCode = "10001", Country = "US" });

        // Act
        var result = await grain.CreateAsync(command);

        // Assert
        result.Id.Should().Be(siteId);
        result.Code.Should().Be("DT01");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // Given: an organization
    // When: a new site is created
    // Then: the site is automatically registered with the parent organization
    [Fact]
    public async Task CreateAsync_ShouldRegisterWithOrganization()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var siteId = Guid.NewGuid();
        var siteGrain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));
        var orgGrain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(orgId));

        var command = new CreateSiteCommand(
            orgId,
            "Downtown Store",
            "DT01",
            new Address { Street = "123 Main St", City = "New York", State = "NY", PostalCode = "10001", Country = "US" });

        // Act
        await siteGrain.CreateAsync(command);

        // Assert
        var siteIds = await orgGrain.GetSiteIdsAsync();
        siteIds.Should().Contain(siteId);
    }

    // Given: a site created with timezone, currency, and address details
    // When: the site state is retrieved
    // Then: all configured properties including timezone, currency, status, and address are returned
    [Fact]
    public async Task GetStateAsync_ShouldReturnState()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));

        var address = new Address
        {
            Street = "123 Main St",
            City = "New York",
            State = "NY",
            PostalCode = "10001",
            Country = "US"
        };

        await grain.CreateAsync(new CreateSiteCommand(orgId, "Downtown Store", "DT01", address, "America/New_York", "USD"));

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        state.Id.Should().Be(siteId);
        state.OrganizationId.Should().Be(orgId);
        state.Name.Should().Be("Downtown Store");
        state.Code.Should().Be("DT01");
        state.Timezone.Should().Be("America/New_York");
        state.Currency.Should().Be("USD");
        state.Status.Should().Be(SiteStatus.Open);
        state.Address.City.Should().Be("New York");
    }

    // Given: an existing site named "Downtown Store"
    // When: the site name is updated to "Updated Store Name"
    // Then: the site name changes and the version number increments
    [Fact]
    public async Task UpdateAsync_ShouldUpdateSite()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));

        await grain.CreateAsync(new CreateSiteCommand(
            orgId, "Downtown Store", "DT01",
            new Address { Street = "123 Main St", City = "New York", State = "NY", PostalCode = "10001", Country = "US" }));

        // Act
        var result = await grain.UpdateAsync(new UpdateSiteCommand(Name: "Updated Store Name"));

        // Assert
        result.Version.Should().Be(2);
        var state = await grain.GetStateAsync();
        state.Name.Should().Be("Updated Store Name");
    }

    // Given: a closed site
    // When: the site is reopened
    // Then: the site status changes to Open
    [Fact]
    public async Task OpenAsync_ShouldSetStatusToOpen()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));

        await grain.CreateAsync(new CreateSiteCommand(
            orgId, "Downtown Store", "DT01",
            new Address { Street = "123 Main St", City = "New York", State = "NY", PostalCode = "10001", Country = "US" }));
        await grain.CloseAsync();

        // Act
        await grain.OpenAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(SiteStatus.Open);
    }

    // Given: an open site
    // When: the site is closed
    // Then: the site status changes to Closed
    [Fact]
    public async Task CloseAsync_ShouldSetStatusToClosed()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));

        await grain.CreateAsync(new CreateSiteCommand(
            orgId, "Downtown Store", "DT01",
            new Address { Street = "123 Main St", City = "New York", State = "NY", PostalCode = "10001", Country = "US" }));

        // Act
        await grain.CloseAsync();

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(SiteStatus.Closed);
    }

    // Given: an open site
    // When: the site is temporarily closed for maintenance
    // Then: the site status changes to TemporarilyClosed
    [Fact]
    public async Task CloseTemporarilyAsync_ShouldSetStatusToTemporarilyClosed()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));

        await grain.CreateAsync(new CreateSiteCommand(
            orgId, "Downtown Store", "DT01",
            new Address { Street = "123 Main St", City = "New York", State = "NY", PostalCode = "10001", Country = "US" }));

        // Act
        await grain.CloseTemporarilyAsync("Maintenance");

        // Assert
        var state = await grain.GetStateAsync();
        state.Status.Should().Be(SiteStatus.TemporarilyClosed);
    }

    // Given: a site with no active menu
    // When: a menu is set as the active menu for the site
    // Then: the site settings reflect the newly active menu
    [Fact]
    public async Task SetActiveMenuAsync_ShouldUpdateActiveMenu()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var siteId = Guid.NewGuid();
        var menuId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));

        await grain.CreateAsync(new CreateSiteCommand(
            orgId, "Downtown Store", "DT01",
            new Address { Street = "123 Main St", City = "New York", State = "NY", PostalCode = "10001", Country = "US" }));

        // Act
        await grain.SetActiveMenuAsync(menuId);

        // Assert
        var state = await grain.GetStateAsync();
        state.Settings.ActiveMenuId.Should().Be(menuId);
    }

    // Given: a site with no floor plans
    // When: a floor plan is added to the site
    // Then: the floor plan ID appears in the site's floor plan list
    [Fact]
    public async Task AddFloorAsync_ShouldAddFloorId()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var siteId = Guid.NewGuid();
        var floorId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));

        await grain.CreateAsync(new CreateSiteCommand(
            orgId, "Downtown Store", "DT01",
            new Address { Street = "123 Main St", City = "New York", State = "NY", PostalCode = "10001", Country = "US" }));

        // Act
        await grain.AddFloorAsync(floorId);

        // Assert
        var state = await grain.GetStateAsync();
        state.FloorIds.Should().Contain(floorId);
    }

    // Given: a site with no kitchen stations
    // When: a kitchen station is added to the site
    // Then: the station ID appears in the site's station list
    [Fact]
    public async Task AddStationAsync_ShouldAddStationId()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var siteId = Guid.NewGuid();
        var stationId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));

        await grain.CreateAsync(new CreateSiteCommand(
            orgId, "Downtown Store", "DT01",
            new Address { Street = "123 Main St", City = "New York", State = "NY", PostalCode = "10001", Country = "US" }));

        // Act
        await grain.AddStationAsync(stationId);

        // Assert
        var state = await grain.GetStateAsync();
        state.StationIds.Should().Contain(stationId);
    }

    // Given: a newly created site (defaults to Open status)
    // When: the open status is checked
    // Then: the site reports as open
    [Fact]
    public async Task IsOpenAsync_WhenOpen_ShouldReturnTrue()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));

        await grain.CreateAsync(new CreateSiteCommand(
            orgId, "Downtown Store", "DT01",
            new Address { Street = "123 Main St", City = "New York", State = "NY", PostalCode = "10001", Country = "US" }));

        // Act
        var isOpen = await grain.IsOpenAsync();

        // Assert
        isOpen.Should().BeTrue();
    }

    // Given: a site that has been closed
    // When: the open status is checked
    // Then: the site reports as not open
    [Fact]
    public async Task IsOpenAsync_WhenClosed_ShouldReturnFalse()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));

        await grain.CreateAsync(new CreateSiteCommand(
            orgId, "Downtown Store", "DT01",
            new Address { Street = "123 Main St", City = "New York", State = "NY", PostalCode = "10001", Country = "US" }));
        await grain.CloseAsync();

        // Act
        var isOpen = await grain.IsOpenAsync();

        // Assert
        isOpen.Should().BeFalse();
    }

    // Given: a site that has been created
    // When: the existence check is performed
    // Then: the site reports as existing
    [Fact]
    public async Task ExistsAsync_WhenCreated_ShouldReturnTrue()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));

        await grain.CreateAsync(new CreateSiteCommand(
            orgId, "Downtown Store", "DT01",
            new Address { Street = "123 Main St", City = "New York", State = "NY", PostalCode = "10001", Country = "US" }));

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeTrue();
    }

    // Given: a site ID that has never been created
    // When: the existence check is performed
    // Then: the site reports as not existing
    [Fact]
    public async Task ExistsAsync_WhenNotCreated_ShouldReturnFalse()
    {
        // Arrange
        var orgId = await CreateOrganizationAsync();
        var siteId = Guid.NewGuid();
        var grain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(orgId, siteId));

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeFalse();
    }
}
