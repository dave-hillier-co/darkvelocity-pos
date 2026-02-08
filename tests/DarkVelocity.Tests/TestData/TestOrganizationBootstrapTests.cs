using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Tests.TestData;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
public class TestOrganizationBootstrapTests
{
    private readonly TestClusterFixture _fixture;

    public TestOrganizationBootstrapTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task BootstrapAll_CreatesOrganizationAndKeySiteData()
    {
        var bootstrap = new TestOrganizationBootstrap(_fixture.Cluster.GrainFactory);
        await bootstrap.BootstrapAllAsync();

        // Verify organization exists
        var orgGrain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(
            GrainKeys.Organization(UkTestData.OrgId));
        var orgState = await orgGrain.GetStateAsync();
        Assert.Equal(UkTestData.Organization.Name, orgState.Name);

        // Verify London site exists
        var siteGrain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(
            GrainKeys.Site(UkTestData.OrgId, UkTestData.LondonSiteId));
        var siteState = await siteGrain.GetStateAsync();
        Assert.Equal("The Plough & Harrow - Shoreditch", siteState.Name);

        // Verify Fish & Chips menu item exists
        var fishAndChipsId = UkTestData.MenuItems.Mains.First(m => m.Name == "Fish & Chips").Id;
        var menuItemGrain = _fixture.Cluster.GrainFactory.GetGrain<IMenuItemGrain>(
            GrainKeys.MenuItem(UkTestData.OrgId, fishAndChipsId));
        var menuItemSnapshot = await menuItemGrain.GetSnapshotAsync();
        Assert.Equal("Fish & Chips", menuItemSnapshot.Name);
        Assert.Equal(16.95m, menuItemSnapshot.Price);

        // Verify at least one customer exists
        var firstCustomer = UkTestData.Customers.All.First();
        var customerGrain = _fixture.Cluster.GrainFactory.GetGrain<ICustomerGrain>(
            GrainKeys.Customer(UkTestData.OrgId, firstCustomer.Id));
        Assert.True(await customerGrain.ExistsAsync());

        // Verify a loyalty program was created
        var loyaltyGrain = _fixture.Cluster.GrainFactory.GetGrain<ILoyaltyProgramGrain>(
            GrainKeys.LoyaltyProgram(UkTestData.OrgId, UkTestData.LoyaltyProgramId));
        Assert.True(await loyaltyGrain.ExistsAsync());
    }

    [Fact]
    public async Task BootstrapAll_IsIdempotent()
    {
        var bootstrap = new TestOrganizationBootstrap(_fixture.Cluster.GrainFactory);

        // Run twice - should not throw
        await bootstrap.BootstrapAllAsync();
        await bootstrap.BootstrapAllAsync();

        // Verify still correct
        var orgGrain = _fixture.Cluster.GrainFactory.GetGrain<IOrganizationGrain>(
            GrainKeys.Organization(UkTestData.OrgId));
        var orgState = await orgGrain.GetStateAsync();
        Assert.Equal(UkTestData.Organization.Name, orgState.Name);
    }

    [Fact]
    public async Task BootstrapSites_CanTargetSingleSite()
    {
        var bootstrap = new TestOrganizationBootstrap(_fixture.Cluster.GrainFactory);
        await bootstrap.BootstrapOrganizationAsync();
        await bootstrap.BootstrapSitesAsync(UkTestData.EdinburghSiteId);

        var siteGrain = _fixture.Cluster.GrainFactory.GetGrain<ISiteGrain>(
            GrainKeys.Site(UkTestData.OrgId, UkTestData.EdinburghSiteId));
        var siteState = await siteGrain.GetStateAsync();
        Assert.Equal("The Plough & Harrow - Grassmarket", siteState.Name);
    }

    [Fact]
    public async Task BootstrapComposed_MenuOnlyForLondon()
    {
        var bootstrap = new TestOrganizationBootstrap(_fixture.Cluster.GrainFactory);
        await bootstrap.BootstrapOrganizationAsync();
        await bootstrap.BootstrapSitesAsync(UkTestData.LondonSiteId);
        await bootstrap.BootstrapAccountingGroupsAsync();
        await bootstrap.BootstrapMenuAsync();

        // Verify menu categories exist
        var startersGrain = _fixture.Cluster.GrainFactory.GetGrain<IMenuCategoryGrain>(
            GrainKeys.MenuCategory(UkTestData.OrgId, UkTestData.MenuCategories.StartersId));
        var startersSnapshot = await startersGrain.GetSnapshotAsync();
        Assert.Equal("Starters", startersSnapshot.Name);
    }
}
