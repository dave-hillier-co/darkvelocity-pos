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
