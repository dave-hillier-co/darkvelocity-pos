using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using FluentAssertions;
using Orleans.TestingHost;
using Xunit;

namespace DarkVelocity.Tests.Grains.Costing;

/// <summary>
/// Tests for AccountingGroupGrain - manages accounting groups for financial reporting.
/// </summary>
[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class AccountingGroupGrainTests
{
    private readonly TestCluster _cluster;

    public AccountingGroupGrainTests(TestClusterFixture fixture)
    {
        _cluster = fixture.Cluster;
    }

    private string GetGrainKey(Guid orgId, Guid groupId)
        => $"{orgId}:accountinggroup:{groupId}";

    // ============================================================================
    // Create Tests
    // ============================================================================

    // Given: a new accounting group grain with no prior state
    // When: the accounting group is created with name, code, description, and account codes
    // Then: the snapshot reflects all provided fields with active status and zero item count
    [Fact]
    public async Task CreateAsync_ShouldCreateAccountingGroupSuccessfully()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        var command = new CreateAccountingGroupCommand(
            LocationId: locationId,
            Name: "Food",
            Code: "FOOD-001",
            Description: "Food cost of goods sold",
            RevenueAccountCode: "4000",
            CogsAccountCode: "5000");

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.AccountingGroupId.Should().Be(groupId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.Name.Should().Be("Food");
        snapshot.Code.Should().Be("FOOD-001");
        snapshot.Description.Should().Be("Food cost of goods sold");
        snapshot.RevenueAccountCode.Should().Be("4000");
        snapshot.CogsAccountCode.Should().Be("5000");
        snapshot.IsActive.Should().BeTrue();
        snapshot.ItemCount.Should().Be(0);
    }

    // Given: an accounting group that has already been created
    // When: attempting to create the same accounting group again
    // Then: an InvalidOperationException is thrown indicating the group already exists
    [Fact]
    public async Task CreateAsync_AlreadyExists_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        await grain.CreateAsync(new CreateAccountingGroupCommand(
            locationId, "Food", "FOOD-001", "Description", "4000", "5000"));

        // Act
        var act = () => grain.CreateAsync(new CreateAccountingGroupCommand(
            locationId, "Another", "ANOTHER-001", null, null, null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Accounting group already exists");
    }

    // Given: a new accounting group grain with no prior state
    // When: the accounting group is created with only required fields (name and code)
    // Then: optional fields remain null and the group is successfully created
    [Fact]
    public async Task CreateAsync_WithMinimalData_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        var command = new CreateAccountingGroupCommand(
            LocationId: locationId,
            Name: "Beverages",
            Code: "BEV-001",
            Description: null,
            RevenueAccountCode: null,
            CogsAccountCode: null);

        // Act
        var snapshot = await grain.CreateAsync(command);

        // Assert
        snapshot.Name.Should().Be("Beverages");
        snapshot.Code.Should().Be("BEV-001");
        snapshot.Description.Should().BeNull();
        snapshot.RevenueAccountCode.Should().BeNull();
        snapshot.CogsAccountCode.Should().BeNull();
    }

    // ============================================================================
    // Update Tests
    // ============================================================================

    // Given: an existing accounting group with initial field values
    // When: all fields are updated with new values
    // Then: the snapshot reflects all the updated field values
    [Fact]
    public async Task UpdateAsync_ShouldUpdateAllFields()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        await grain.CreateAsync(new CreateAccountingGroupCommand(
            locationId, "Food", "FOOD-001", "Original description", "4000", "5000"));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateAccountingGroupCommand(
            Name: "Food & Beverage",
            Code: "FB-001",
            Description: "Combined food and beverage",
            RevenueAccountCode: "4100",
            CogsAccountCode: "5100",
            IsActive: null));

        // Assert
        snapshot.Name.Should().Be("Food & Beverage");
        snapshot.Code.Should().Be("FB-001");
        snapshot.Description.Should().Be("Combined food and beverage");
        snapshot.RevenueAccountCode.Should().Be("4100");
        snapshot.CogsAccountCode.Should().Be("5100");
        snapshot.IsActive.Should().BeTrue(); // Unchanged
    }

    // Given: an existing accounting group with all fields populated
    // When: only the name is updated while other fields are null in the command
    // Then: the name changes but all other fields retain their original values
    [Fact]
    public async Task UpdateAsync_PartialUpdate_ShouldPreserveOtherFields()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        await grain.CreateAsync(new CreateAccountingGroupCommand(
            locationId, "Food", "FOOD-001", "Description", "4000", "5000"));

        // Act - only update name
        var snapshot = await grain.UpdateAsync(new UpdateAccountingGroupCommand(
            Name: "Updated Food",
            Code: null,
            Description: null,
            RevenueAccountCode: null,
            CogsAccountCode: null,
            IsActive: null));

        // Assert
        snapshot.Name.Should().Be("Updated Food");
        snapshot.Code.Should().Be("FOOD-001"); // Preserved
        snapshot.Description.Should().Be("Description"); // Preserved
        snapshot.RevenueAccountCode.Should().Be("4000"); // Preserved
        snapshot.CogsAccountCode.Should().Be("5000"); // Preserved
    }

    // Given: an active accounting group
    // When: the group is updated with IsActive set to false
    // Then: the accounting group becomes inactive
    [Fact]
    public async Task UpdateAsync_Deactivate_ShouldSetIsActiveFalse()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        await grain.CreateAsync(new CreateAccountingGroupCommand(
            locationId, "Food", "FOOD-001", null, null, null));

        // Act
        var snapshot = await grain.UpdateAsync(new UpdateAccountingGroupCommand(
            Name: null,
            Code: null,
            Description: null,
            RevenueAccountCode: null,
            CogsAccountCode: null,
            IsActive: false));

        // Assert
        snapshot.IsActive.Should().BeFalse();
    }

    // Given: an accounting group grain that has never been created
    // When: an update is attempted
    // Then: an InvalidOperationException is thrown indicating the grain is not initialized
    [Fact]
    public async Task UpdateAsync_NotInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        // Act
        var act = () => grain.UpdateAsync(new UpdateAccountingGroupCommand(
            Name: "Updated", Code: null, Description: null,
            RevenueAccountCode: null, CogsAccountCode: null, IsActive: null));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Accounting group grain not initialized");
    }

    // ============================================================================
    // GetSnapshot Tests
    // ============================================================================

    // Given: a created accounting group with known field values
    // When: the snapshot is retrieved
    // Then: the snapshot contains the correct group ID, location, name, and code
    [Fact]
    public async Task GetSnapshotAsync_ShouldReturnCurrentState()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        await grain.CreateAsync(new CreateAccountingGroupCommand(
            locationId, "Food", "FOOD-001", "Description", "4000", "5000"));

        // Act
        var snapshot = await grain.GetSnapshotAsync();

        // Assert
        snapshot.AccountingGroupId.Should().Be(groupId);
        snapshot.LocationId.Should().Be(locationId);
        snapshot.Name.Should().Be("Food");
        snapshot.Code.Should().Be("FOOD-001");
    }

    // Given: an accounting group grain that has never been created
    // When: the snapshot is requested
    // Then: an InvalidOperationException is thrown indicating the grain is not initialized
    [Fact]
    public async Task GetSnapshotAsync_NotInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        // Act
        var act = () => grain.GetSnapshotAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Accounting group grain not initialized");
    }

    // ============================================================================
    // Item Count Tests
    // ============================================================================

    // Given: a created accounting group with zero items
    // When: the item count is incremented three times
    // Then: the item count reflects three assigned items
    [Fact]
    public async Task IncrementItemCountAsync_ShouldIncreaseCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        await grain.CreateAsync(new CreateAccountingGroupCommand(
            locationId, "Food", "FOOD-001", null, null, null));

        // Act
        await grain.IncrementItemCountAsync();
        await grain.IncrementItemCountAsync();
        await grain.IncrementItemCountAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemCount.Should().Be(3);
    }

    // Given: an accounting group with three items assigned
    // When: the item count is decremented once
    // Then: the item count decreases to two
    [Fact]
    public async Task DecrementItemCountAsync_ShouldDecreaseCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        await grain.CreateAsync(new CreateAccountingGroupCommand(
            locationId, "Food", "FOOD-001", null, null, null));

        await grain.IncrementItemCountAsync();
        await grain.IncrementItemCountAsync();
        await grain.IncrementItemCountAsync();

        // Act
        await grain.DecrementItemCountAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemCount.Should().Be(2);
    }

    // Given: a created accounting group with zero items
    // When: the item count is decremented multiple times
    // Then: the item count remains at zero and does not go negative
    [Fact]
    public async Task DecrementItemCountAsync_AtZero_ShouldNotGoNegative()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        await grain.CreateAsync(new CreateAccountingGroupCommand(
            locationId, "Food", "FOOD-001", null, null, null));

        // Act - decrement when already at 0
        await grain.DecrementItemCountAsync();
        await grain.DecrementItemCountAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemCount.Should().Be(0);
    }

    // Given: an accounting group grain that has never been created
    // When: the item count increment is attempted
    // Then: an InvalidOperationException is thrown indicating the grain is not initialized
    [Fact]
    public async Task IncrementItemCountAsync_NotInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        // Act
        var act = () => grain.IncrementItemCountAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Accounting group grain not initialized");
    }

    // Given: an accounting group grain that has never been created
    // When: the item count decrement is attempted
    // Then: an InvalidOperationException is thrown indicating the grain is not initialized
    [Fact]
    public async Task DecrementItemCountAsync_NotInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        // Act
        var act = () => grain.DecrementItemCountAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Accounting group grain not initialized");
    }

    // ============================================================================
    // Multiple Accounting Groups Tests
    // ============================================================================

    // Given: two separate accounting groups (Food and Beverages) for the same location
    // When: items are assigned independently to each group
    // Then: each group maintains its own name and item count without cross-contamination
    [Fact]
    public async Task MultipleAccountingGroups_ShouldBeIndependent()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var foodGroupId = Guid.NewGuid();
        var beverageGroupId = Guid.NewGuid();

        var foodGrain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, foodGroupId));
        var beverageGrain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, beverageGroupId));

        // Act
        await foodGrain.CreateAsync(new CreateAccountingGroupCommand(
            locationId, "Food", "FOOD-001", null, "4000", "5000"));
        await beverageGrain.CreateAsync(new CreateAccountingGroupCommand(
            locationId, "Beverages", "BEV-001", null, "4100", "5100"));

        await foodGrain.IncrementItemCountAsync();
        await foodGrain.IncrementItemCountAsync();
        await beverageGrain.IncrementItemCountAsync();

        // Assert
        var foodSnapshot = await foodGrain.GetSnapshotAsync();
        var beverageSnapshot = await beverageGrain.GetSnapshotAsync();

        foodSnapshot.Name.Should().Be("Food");
        foodSnapshot.ItemCount.Should().Be(2);

        beverageSnapshot.Name.Should().Be("Beverages");
        beverageSnapshot.ItemCount.Should().Be(1);
    }

    // ============================================================================
    // Common Accounting Group Scenarios
    // ============================================================================

    // Given: a set of standard hospitality accounting groups (Food, Beverage, Alcohol, Merchandise, Other)
    // When: each group is created with its respective code and account mappings
    // Then: all groups are active with correct names and codes
    [Fact]
    public async Task CreateStandardAccountingGroups_ShouldSucceed()
    {
        // Arrange - Create standard hospitality accounting groups
        var orgId = Guid.NewGuid();
        var locationId = Guid.NewGuid();

        var standardGroups = new[]
        {
            ("Food", "FOOD", "Food cost of goods sold", "4000", "5000"),
            ("Beverage", "BEV", "Beverage cost of goods sold", "4100", "5100"),
            ("Alcohol", "ALC", "Alcohol cost of goods sold", "4200", "5200"),
            ("Merchandise", "MERCH", "Retail merchandise", "4300", "5300"),
            ("Other", "OTHER", "Other revenue items", "4900", "5900")
        };

        var grains = new List<IAccountingGroupGrain>();

        // Act
        foreach (var (name, code, description, revenueCode, cogsCode) in standardGroups)
        {
            var groupId = Guid.NewGuid();
            var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));
            await grain.CreateAsync(new CreateAccountingGroupCommand(
                locationId, name, code, description, revenueCode, cogsCode));
            grains.Add(grain);
        }

        // Assert
        foreach (var (grain, index) in grains.Select((g, i) => (g, i)))
        {
            var snapshot = await grain.GetSnapshotAsync();
            snapshot.Name.Should().Be(standardGroups[index].Item1);
            snapshot.Code.Should().Be(standardGroups[index].Item2);
            snapshot.IsActive.Should().BeTrue();
        }
    }

    // Given: an accounting group with menu items being assigned and unassigned over time
    // When: five items are assigned, two removed, and three more assigned
    // Then: the item count accurately reflects the net total of six items
    [Fact]
    public async Task AccountingGroup_TrackMenuItemAssignment_ShouldMaintainAccurateCount()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var locationId = Guid.NewGuid();
        var grain = _cluster.GrainFactory.GetGrain<IAccountingGroupGrain>(GetGrainKey(orgId, groupId));

        await grain.CreateAsync(new CreateAccountingGroupCommand(
            locationId, "Food", "FOOD-001", null, null, null));

        // Act - Simulate menu items being assigned and unassigned
        // 5 items assigned
        for (int i = 0; i < 5; i++)
        {
            await grain.IncrementItemCountAsync();
        }

        // 2 items removed
        await grain.DecrementItemCountAsync();
        await grain.DecrementItemCountAsync();

        // 3 more items assigned
        for (int i = 0; i < 3; i++)
        {
            await grain.IncrementItemCountAsync();
        }

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.ItemCount.Should().Be(6); // 5 - 2 + 3 = 6
    }
}
