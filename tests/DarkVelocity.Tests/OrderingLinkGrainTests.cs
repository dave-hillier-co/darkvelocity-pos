using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class OrderingLinkGrainTests
{
    private readonly TestClusterFixture _fixture;

    public OrderingLinkGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IOrderingLinkGrain GetGrain(Guid orgId, Guid linkId)
    {
        var key = GrainKeys.OrderingLink(orgId, linkId);
        return _fixture.Cluster.GrainFactory.GetGrain<IOrderingLinkGrain>(key);
    }

    // Given: no existing ordering link
    // When: a new table QR ordering link is created for table 5
    // Then: the link is active with correct properties and a generated short code
    [Fact]
    public async Task CreateAsync_TableQr_ShouldCreateLinkWithShortCode()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var grain = GetGrain(orgId, linkId);

        // Act
        var result = await grain.CreateAsync(new CreateOrderingLinkCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Type: OrderingLinkType.TableQr,
            Name: "Table 5 QR",
            TableId: tableId,
            TableNumber: "5"));

        // Assert
        result.LinkId.Should().Be(linkId);
        result.OrganizationId.Should().Be(orgId);
        result.SiteId.Should().Be(siteId);
        result.Type.Should().Be(OrderingLinkType.TableQr);
        result.Name.Should().Be("Table 5 QR");
        result.TableId.Should().Be(tableId);
        result.TableNumber.Should().Be("5");
        result.IsActive.Should().BeTrue();
        result.ShortCode.Should().NotBeNullOrEmpty();
        result.ShortCode.Length.Should().Be(8);
    }

    // Given: no existing ordering link
    // When: a kiosk ordering link is created without a table
    // Then: the link is active with kiosk type and no table assignment
    [Fact]
    public async Task CreateAsync_Kiosk_ShouldCreateLinkWithoutTable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var grain = GetGrain(orgId, linkId);

        // Act
        var result = await grain.CreateAsync(new CreateOrderingLinkCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Type: OrderingLinkType.Kiosk,
            Name: "Main Entrance Kiosk"));

        // Assert
        result.Type.Should().Be(OrderingLinkType.Kiosk);
        result.Name.Should().Be("Main Entrance Kiosk");
        result.TableId.Should().BeNull();
        result.TableNumber.Should().BeNull();
        result.IsActive.Should().BeTrue();
        result.ShortCode.Should().NotBeNullOrEmpty();
    }

    // Given: an existing ordering link
    // When: the same grain is created again
    // Then: an InvalidOperationException is thrown
    [Fact]
    public async Task CreateAsync_AlreadyExists_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var grain = GetGrain(orgId, linkId);
        await grain.CreateAsync(new CreateOrderingLinkCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Type: OrderingLinkType.TakeOut,
            Name: "Takeout QR"));

        // Act & Assert
        var act = () => grain.CreateAsync(new CreateOrderingLinkCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Type: OrderingLinkType.TakeOut,
            Name: "Duplicate"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // Given: an existing table QR ordering link
    // When: the link name is updated
    // Then: the name is changed and the short code remains the same
    [Fact]
    public async Task UpdateAsync_ShouldUpdateNamePreservingShortCode()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var grain = GetGrain(orgId, linkId);
        var created = await grain.CreateAsync(new CreateOrderingLinkCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Type: OrderingLinkType.TableQr,
            Name: "Table 1",
            TableId: Guid.NewGuid(),
            TableNumber: "1"));

        // Act
        var result = await grain.UpdateAsync(new UpdateOrderingLinkCommand(
            Name: "VIP Table 1"));

        // Assert
        result.Name.Should().Be("VIP Table 1");
        result.ShortCode.Should().Be(created.ShortCode);
    }

    // Given: an existing ordering link linked to table 1
    // When: the table is changed to table 7
    // Then: the table ID and number are updated
    [Fact]
    public async Task UpdateAsync_ShouldChangeTable()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var newTableId = Guid.NewGuid();
        var grain = GetGrain(orgId, linkId);
        await grain.CreateAsync(new CreateOrderingLinkCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Type: OrderingLinkType.TableQr,
            Name: "Table QR",
            TableId: Guid.NewGuid(),
            TableNumber: "1"));

        // Act
        var result = await grain.UpdateAsync(new UpdateOrderingLinkCommand(
            TableId: newTableId,
            TableNumber: "7"));

        // Assert
        result.TableId.Should().Be(newTableId);
        result.TableNumber.Should().Be("7");
    }

    // Given: an active ordering link
    // When: the link is deactivated
    // Then: the link is no longer active
    [Fact]
    public async Task DeactivateAsync_ShouldMakeLinkInactive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var grain = GetGrain(orgId, linkId);
        await grain.CreateAsync(new CreateOrderingLinkCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Type: OrderingLinkType.Kiosk,
            Name: "Kiosk 1"));

        // Act
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
    }

    // Given: a deactivated ordering link
    // When: the link is reactivated
    // Then: the link is active again
    [Fact]
    public async Task ActivateAsync_ShouldMakeLinkActiveAgain()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var grain = GetGrain(orgId, linkId);
        await grain.CreateAsync(new CreateOrderingLinkCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Type: OrderingLinkType.TakeOut,
            Name: "Takeout QR"));
        await grain.DeactivateAsync();

        // Act
        await grain.ActivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeTrue();
    }

    // Given: a newly created ordering link
    // When: checking if it exists
    // Then: it returns true
    [Fact]
    public async Task ExistsAsync_AfterCreate_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var grain = GetGrain(orgId, linkId);
        await grain.CreateAsync(new CreateOrderingLinkCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Type: OrderingLinkType.Kiosk,
            Name: "Kiosk"));

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeTrue();
    }

    // Given: no ordering link has been created
    // When: checking if it exists
    // Then: it returns false
    [Fact]
    public async Task ExistsAsync_NeverCreated_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        var grain = GetGrain(orgId, linkId);

        // Act
        var exists = await grain.ExistsAsync();

        // Assert
        exists.Should().BeFalse();
    }

    // Given: two ordering links created separately
    // When: getting their short codes
    // Then: the short codes are unique
    [Fact]
    public async Task CreateAsync_MultipleLinks_ShouldHaveUniqueShortCodes()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain1 = GetGrain(orgId, Guid.NewGuid());
        var grain2 = GetGrain(orgId, Guid.NewGuid());

        // Act
        var result1 = await grain1.CreateAsync(new CreateOrderingLinkCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Type: OrderingLinkType.TableQr,
            Name: "Table 1"));
        var result2 = await grain2.CreateAsync(new CreateOrderingLinkCommand(
            OrganizationId: orgId,
            SiteId: siteId,
            Type: OrderingLinkType.TableQr,
            Name: "Table 2"));

        // Assert
        result1.ShortCode.Should().NotBe(result2.ShortCode);
    }
}
