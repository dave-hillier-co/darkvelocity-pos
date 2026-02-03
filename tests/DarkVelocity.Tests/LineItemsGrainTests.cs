using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class LineItemsGrainTests
{
    private readonly TestClusterFixture _fixture;

    public LineItemsGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ILineItemsGrain GetLineItemsGrain(Guid orgId, string ownerType, Guid ownerId)
        => _fixture.Cluster.GrainFactory.GetGrain<ILineItemsGrain>(
            GrainKeys.LineItems(orgId, ownerType, ownerId));

    [Fact]
    public async Task AddAsync_ShouldAddLineItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);

        // Act
        var result = await grain.AddAsync("menu-item", 2, 10.00m);

        // Assert
        result.LineId.Should().NotBeEmpty();
        result.Index.Should().Be(0);
        result.ExtendedPrice.Should().Be(20.00m);
        result.Totals.LineCount.Should().Be(1);
        result.Totals.Subtotal.Should().Be(20.00m);
    }

    [Fact]
    public async Task AddAsync_WithMetadata_ShouldStoreMetadata()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);
        var metadata = new Dictionary<string, string>
        {
            ["MenuItemId"] = Guid.NewGuid().ToString(),
            ["Name"] = "Cheeseburger"
        };

        // Act
        var result = await grain.AddAsync("menu-item", 1, 12.99m, metadata);

        // Assert
        var lines = await grain.GetLinesAsync();
        lines.Should().HaveCount(1);
        lines[0].Metadata.Should().ContainKey("MenuItemId");
        lines[0].Metadata.Should().ContainKey("Name");
        lines[0].Metadata["Name"].Should().Be("Cheeseburger");
    }

    [Fact]
    public async Task AddAsync_MultipleTimes_ShouldIncrementIndex()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);

        // Act
        var result1 = await grain.AddAsync("menu-item", 1, 10.00m);
        var result2 = await grain.AddAsync("menu-item", 2, 5.00m);
        var result3 = await grain.AddAsync("ingredient", 3, 2.50m);

        // Assert
        result1.Index.Should().Be(0);
        result2.Index.Should().Be(1);
        result3.Index.Should().Be(2);

        var totals = await grain.GetTotalsAsync();
        totals.LineCount.Should().Be(3);
        totals.TotalQuantity.Should().Be(6);
        totals.Subtotal.Should().Be(10.00m + 10.00m + 7.50m);
    }

    [Fact]
    public async Task AddAsync_WithZeroQuantity_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);

        // Act
        var act = () => grain.AddAsync("menu-item", 0, 10.00m);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Quantity must be positive*");
    }

    [Fact]
    public async Task AddAsync_WithEmptyItemType_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);

        // Act
        var act = () => grain.AddAsync("", 1, 10.00m);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Item type is required*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateLine()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);
        var result = await grain.AddAsync("menu-item", 1, 10.00m);

        // Act
        await grain.UpdateAsync(result.LineId, quantity: 3, unitPrice: 12.00m);

        // Assert
        var lines = await grain.GetLinesAsync();
        lines.Should().HaveCount(1);
        lines[0].Quantity.Should().Be(3);
        lines[0].UnitPrice.Should().Be(12.00m);
        lines[0].ExtendedPrice.Should().Be(36.00m);
        lines[0].UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_PartialUpdate_ShouldOnlyUpdateSpecifiedFields()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);
        var result = await grain.AddAsync("menu-item", 2, 10.00m);

        // Act
        await grain.UpdateAsync(result.LineId, quantity: 5);

        // Assert
        var lines = await grain.GetLinesAsync();
        lines[0].Quantity.Should().Be(5);
        lines[0].UnitPrice.Should().Be(10.00m); // Unchanged
        lines[0].ExtendedPrice.Should().Be(50.00m);
    }

    [Fact]
    public async Task UpdateAsync_WithMetadata_ShouldReplaceMetadata()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);
        var result = await grain.AddAsync("menu-item", 1, 10.00m, new Dictionary<string, string> { ["key1"] = "value1" });

        // Act
        await grain.UpdateAsync(result.LineId, metadata: new Dictionary<string, string> { ["key2"] = "value2" });

        // Assert
        var lines = await grain.GetLinesAsync();
        lines[0].Metadata.Should().ContainKey("key2");
        lines[0].Metadata.Should().NotContainKey("key1");
    }

    [Fact]
    public async Task UpdateAsync_NonExistentLine_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);

        // Act
        var act = () => grain.UpdateAsync(Guid.NewGuid(), quantity: 5);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Line not found*");
    }

    [Fact]
    public async Task UpdateAsync_VoidedLine_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);
        var result = await grain.AddAsync("menu-item", 1, 10.00m);
        await grain.VoidAsync(result.LineId, Guid.NewGuid(), "Wrong item");

        // Act
        var act = () => grain.UpdateAsync(result.LineId, quantity: 5);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot update a voided line*");
    }

    [Fact]
    public async Task VoidAsync_ShouldVoidLine()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var voidedBy = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);
        var result = await grain.AddAsync("menu-item", 2, 10.00m);

        // Act
        await grain.VoidAsync(result.LineId, voidedBy, "Customer changed mind");

        // Assert
        var linesWithVoided = await grain.GetLinesAsync(includeVoided: true);
        linesWithVoided.Should().HaveCount(1);
        linesWithVoided[0].IsVoided.Should().BeTrue();
        linesWithVoided[0].VoidedBy.Should().Be(voidedBy);
        linesWithVoided[0].VoidedAt.Should().NotBeNull();
        linesWithVoided[0].VoidReason.Should().Be("Customer changed mind");

        var linesWithoutVoided = await grain.GetLinesAsync(includeVoided: false);
        linesWithoutVoided.Should().BeEmpty();
    }

    [Fact]
    public async Task VoidAsync_ShouldExcludeFromTotals()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);
        var result1 = await grain.AddAsync("menu-item", 1, 10.00m);
        await grain.AddAsync("menu-item", 2, 5.00m);

        // Act
        await grain.VoidAsync(result1.LineId, Guid.NewGuid(), "Voided");

        // Assert
        var totals = await grain.GetTotalsAsync();
        totals.LineCount.Should().Be(1);
        totals.VoidedCount.Should().Be(1);
        totals.TotalQuantity.Should().Be(2);
        totals.Subtotal.Should().Be(10.00m); // Only second line
    }

    [Fact]
    public async Task VoidAsync_AlreadyVoided_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);
        var result = await grain.AddAsync("menu-item", 1, 10.00m);
        await grain.VoidAsync(result.LineId, Guid.NewGuid(), "First void");

        // Act
        var act = () => grain.VoidAsync(result.LineId, Guid.NewGuid(), "Second void");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Line is already voided*");
    }

    [Fact]
    public async Task VoidAsync_WithoutReason_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);
        var result = await grain.AddAsync("menu-item", 1, 10.00m);

        // Act
        var act = () => grain.VoidAsync(result.LineId, Guid.NewGuid(), "");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Void reason is required*");
    }

    [Fact]
    public async Task RemoveAsync_ShouldRemoveLine()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);
        var result = await grain.AddAsync("menu-item", 1, 10.00m);
        await grain.AddAsync("menu-item", 2, 5.00m);

        // Act
        await grain.RemoveAsync(result.LineId);

        // Assert
        var lines = await grain.GetLinesAsync(includeVoided: true);
        lines.Should().HaveCount(1);
        lines.Should().NotContain(l => l.Id == result.LineId);
    }

    [Fact]
    public async Task RemoveAsync_NonExistentLine_ShouldThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);

        // Act
        var act = () => grain.RemoveAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Line not found*");
    }

    [Fact]
    public async Task GetLinesAsync_ShouldReturnOrderedByIndex()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);
        await grain.AddAsync("item-a", 1, 10.00m);
        await grain.AddAsync("item-b", 2, 20.00m);
        await grain.AddAsync("item-c", 3, 30.00m);

        // Act
        var lines = await grain.GetLinesAsync();

        // Assert
        lines.Should().HaveCount(3);
        lines[0].ItemType.Should().Be("item-a");
        lines[0].Index.Should().Be(0);
        lines[1].ItemType.Should().Be("item-b");
        lines[1].Index.Should().Be(1);
        lines[2].ItemType.Should().Be("item-c");
        lines[2].Index.Should().Be(2);
    }

    [Fact]
    public async Task GetTotalsAsync_EmptyCollection_ShouldReturnZeros()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);

        // Act
        var totals = await grain.GetTotalsAsync();

        // Assert
        totals.LineCount.Should().Be(0);
        totals.VoidedCount.Should().Be(0);
        totals.TotalQuantity.Should().Be(0);
        totals.Subtotal.Should().Be(0);
    }

    [Fact]
    public async Task GetStateAsync_ShouldReturnState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);
        await grain.AddAsync("menu-item", 1, 10.00m);

        // Act
        var state = await grain.GetStateAsync();

        // Assert
        state.OrganizationId.Should().Be(orgId);
        state.OwnerType.Should().Be("order");
        state.OwnerId.Should().Be(ownerId);
        state.Lines.Should().HaveCount(1);
        state.Version.Should().Be(1);
    }

    [Fact]
    public async Task HasLinesAsync_WithNoLines_ShouldReturnFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);

        // Act
        var hasLines = await grain.HasLinesAsync();

        // Assert
        hasLines.Should().BeFalse();
    }

    [Fact]
    public async Task HasLinesAsync_WithLines_ShouldReturnTrue()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var grain = GetLineItemsGrain(orgId, "order", ownerId);
        await grain.AddAsync("menu-item", 1, 10.00m);

        // Act
        var hasLines = await grain.HasLinesAsync();

        // Assert
        hasLines.Should().BeTrue();
    }

    [Fact]
    public async Task DifferentOwnerTypes_ShouldHaveIsolatedState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var orderGrain = GetLineItemsGrain(orgId, "order", ownerId);
        var purchaseGrain = GetLineItemsGrain(orgId, "purchase-doc", ownerId);

        // Act
        await orderGrain.AddAsync("menu-item", 1, 10.00m);
        await purchaseGrain.AddAsync("ingredient", 5, 2.00m);

        // Assert
        var orderLines = await orderGrain.GetLinesAsync();
        orderLines.Should().HaveCount(1);
        orderLines[0].ItemType.Should().Be("menu-item");

        var purchaseLines = await purchaseGrain.GetLinesAsync();
        purchaseLines.Should().HaveCount(1);
        purchaseLines[0].ItemType.Should().Be("ingredient");
    }
}
