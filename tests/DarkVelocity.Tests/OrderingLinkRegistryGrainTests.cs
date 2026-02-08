using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class OrderingLinkRegistryGrainTests
{
    private readonly TestClusterFixture _fixture;

    public OrderingLinkRegistryGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IOrderingLinkRegistryGrain GetGrain(Guid orgId, Guid siteId)
    {
        var key = GrainKeys.OrderingLinkRegistry(orgId, siteId);
        return _fixture.Cluster.GrainFactory.GetGrain<IOrderingLinkRegistryGrain>(key);
    }

    // Given: an empty ordering link registry
    // When: a table QR link is registered
    // Then: the registry contains one active link
    [Fact]
    public async Task RegisterLinkAsync_ShouldAddLinkToRegistry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        // Act
        await grain.RegisterLinkAsync(new OrderingLinkSummary(
            LinkId: linkId,
            Name: "Table 1 QR",
            Type: OrderingLinkType.TableQr,
            ShortCode: "ABC12345",
            IsActive: true,
            TableId: Guid.NewGuid(),
            TableNumber: "1"));

        // Assert
        var links = await grain.GetLinksAsync();
        links.Should().HaveCount(1);
        links[0].LinkId.Should().Be(linkId);
        links[0].ShortCode.Should().Be("ABC12345");
    }

    // Given: a registry with 3 links (2 active, 1 inactive)
    // When: listing active links only
    // Then: 2 links are returned
    [Fact]
    public async Task GetLinksAsync_WithoutInactive_ShouldFilterInactive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.RegisterLinkAsync(new OrderingLinkSummary(
            Guid.NewGuid(), "Table 1", OrderingLinkType.TableQr, "CODE0001", true, null, null));
        await grain.RegisterLinkAsync(new OrderingLinkSummary(
            Guid.NewGuid(), "Table 2", OrderingLinkType.TableQr, "CODE0002", false, null, null));
        await grain.RegisterLinkAsync(new OrderingLinkSummary(
            Guid.NewGuid(), "Kiosk", OrderingLinkType.Kiosk, "CODE0003", true, null, null));

        // Act
        var activeLinks = await grain.GetLinksAsync(includeInactive: false);
        var allLinks = await grain.GetLinksAsync(includeInactive: true);

        // Assert
        activeLinks.Should().HaveCount(2);
        allLinks.Should().HaveCount(3);
    }

    // Given: a registry with a link using short code "XYZ98765"
    // When: looking up the link by short code
    // Then: the correct link is returned
    [Fact]
    public async Task FindByShortCodeAsync_ShouldReturnMatchingLink()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.RegisterLinkAsync(new OrderingLinkSummary(
            LinkId: linkId,
            Name: "Takeout QR",
            Type: OrderingLinkType.TakeOut,
            ShortCode: "XYZ98765",
            IsActive: true,
            TableId: null,
            TableNumber: null));

        // Act
        var found = await grain.FindByShortCodeAsync("XYZ98765");

        // Assert
        found.Should().NotBeNull();
        found!.LinkId.Should().Be(linkId);
        found.Name.Should().Be("Takeout QR");
    }

    // Given: a registry with links
    // When: looking up a non-existent short code
    // Then: null is returned
    [Fact]
    public async Task FindByShortCodeAsync_NotFound_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        // Act
        var found = await grain.FindByShortCodeAsync("NONEXIST");

        // Assert
        found.Should().BeNull();
    }

    // Given: a registry with an active link
    // When: the link is updated to inactive
    // Then: the link status is updated in the registry
    [Fact]
    public async Task UpdateLinkAsync_ShouldUpdateActiveStatus()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        await grain.RegisterLinkAsync(new OrderingLinkSummary(
            linkId, "Kiosk 1", OrderingLinkType.Kiosk, "KIOSK001", true, null, null));

        // Act
        await grain.UpdateLinkAsync(linkId, name: "Kiosk 1 (Disabled)", isActive: false);

        // Assert
        var links = await grain.GetLinksAsync(includeInactive: true);
        var link = links.First(l => l.LinkId == linkId);
        link.IsActive.Should().BeFalse();
        link.Name.Should().Be("Kiosk 1 (Disabled)");
    }
}
