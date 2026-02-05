using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuRegistryGrainTests
{
    private readonly TestClusterFixture _fixture;

    public MenuRegistryGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMenuRegistryGrain GetGrain(Guid orgId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuRegistryGrain>(
            GrainKeys.MenuRegistry(orgId));
    }

    // ============================================================================
    // Menu Item Registration Tests
    // ============================================================================

    [Fact]
    public async Task RegisterItemAsync_ShouldRegisterMenuItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId);

        // Act
        await grain.RegisterItemAsync(
            documentId: documentId,
            name: "Caesar Salad",
            price: 12.99m,
            categoryId: "category-1");

        // Assert
        var items = await grain.GetItemsAsync();
        items.Should().ContainSingle();
        items[0].DocumentId.Should().Be(documentId);
        items[0].Name.Should().Be("Caesar Salad");
        items[0].Price.Should().Be(12.99m);
        items[0].CategoryId.Should().Be("category-1");
        items[0].HasDraft.Should().BeFalse();
        items[0].IsArchived.Should().BeFalse();
        items[0].PublishedVersion.Should().Be(1);
    }

    [Fact]
    public async Task RegisterItemAsync_WithNullCategoryId_ShouldRegisterMenuItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId);

        // Act
        await grain.RegisterItemAsync(
            documentId: documentId,
            name: "Uncategorized Item",
            price: 9.99m,
            categoryId: null);

        // Assert
        var items = await grain.GetItemsAsync();
        items.Should().ContainSingle();
        items[0].CategoryId.Should().BeNull();
    }

    [Fact]
    public async Task RegisterItemAsync_MultipleItems_ShouldRegisterAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act
        await grain.RegisterItemAsync("item-1", "Item One", 10.00m, "cat-1");
        await grain.RegisterItemAsync("item-2", "Item Two", 15.00m, "cat-1");
        await grain.RegisterItemAsync("item-3", "Item Three", 20.00m, "cat-2");

        // Assert
        var items = await grain.GetItemsAsync();
        items.Should().HaveCount(3);
        items.Should().Contain(i => i.Name == "Item One");
        items.Should().Contain(i => i.Name == "Item Two");
        items.Should().Contain(i => i.Name == "Item Three");
    }

    [Fact]
    public async Task RegisterItemAsync_SameDocumentId_ShouldReplaceExisting()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = "item-to-replace";
        var grain = GetGrain(orgId);
        await grain.RegisterItemAsync(documentId, "Original Name", 10.00m, "cat-1");

        // Act
        await grain.RegisterItemAsync(documentId, "Updated Name", 15.00m, "cat-2");

        // Assert
        var items = await grain.GetItemsAsync();
        items.Should().ContainSingle();
        items[0].Name.Should().Be("Updated Name");
        items[0].Price.Should().Be(15.00m);
        items[0].CategoryId.Should().Be("cat-2");
    }

    [Fact]
    public async Task UpdateItemAsync_ShouldUpdateExistingItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = "item-to-update";
        var grain = GetGrain(orgId);
        await grain.RegisterItemAsync(documentId, "Original", 10.00m, "cat-1");

        // Act
        await grain.UpdateItemAsync(
            documentId: documentId,
            name: "Updated Name",
            price: 12.99m,
            categoryId: "cat-2",
            hasDraft: true,
            isArchived: false);

        // Assert
        var items = await grain.GetItemsAsync();
        items.Should().ContainSingle();
        items[0].Name.Should().Be("Updated Name");
        items[0].Price.Should().Be(12.99m);
        items[0].CategoryId.Should().Be("cat-2");
        items[0].HasDraft.Should().BeTrue();
        items[0].IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateItemAsync_WithArchivedFlag_ShouldSetArchived()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = "item-to-archive";
        var grain = GetGrain(orgId);
        await grain.RegisterItemAsync(documentId, "Item", 10.00m, null);

        // Act
        await grain.UpdateItemAsync(
            documentId: documentId,
            name: "Item",
            price: 10.00m,
            categoryId: null,
            hasDraft: false,
            isArchived: true);

        // Assert - archived items not returned by default
        var items = await grain.GetItemsAsync(includeArchived: false);
        items.Should().BeEmpty();

        // Assert - archived items returned when requested
        var allItems = await grain.GetItemsAsync(includeArchived: true);
        allItems.Should().ContainSingle();
        allItems[0].IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateItemAsync_NonExistingItem_ShouldCreateNewEntry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = "new-item";
        var grain = GetGrain(orgId);

        // Act
        await grain.UpdateItemAsync(
            documentId: documentId,
            name: "New Item",
            price: 5.00m,
            categoryId: "cat-1",
            hasDraft: false,
            isArchived: false);

        // Assert
        var items = await grain.GetItemsAsync();
        items.Should().ContainSingle();
        items[0].DocumentId.Should().Be(documentId);
    }

    [Fact]
    public async Task UnregisterItemAsync_ShouldRemoveItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = "item-to-remove";
        var grain = GetGrain(orgId);
        await grain.RegisterItemAsync(documentId, "Item to Remove", 10.00m, null);

        // Act
        await grain.UnregisterItemAsync(documentId);

        // Assert
        var items = await grain.GetItemsAsync(includeArchived: true);
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task UnregisterItemAsync_NonExistingItem_ShouldNotThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act & Assert - should not throw
        await grain.UnregisterItemAsync("non-existing-item");
    }

    [Fact]
    public async Task GetItemsAsync_FilterByCategory_ShouldReturnOnlyMatchingItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        await grain.RegisterItemAsync("item-1", "Item 1", 10.00m, "cat-food");
        await grain.RegisterItemAsync("item-2", "Item 2", 15.00m, "cat-food");
        await grain.RegisterItemAsync("item-3", "Item 3", 8.00m, "cat-drinks");
        await grain.RegisterItemAsync("item-4", "Item 4", 12.00m, null);

        // Act
        var foodItems = await grain.GetItemsAsync(categoryId: "cat-food");
        var drinkItems = await grain.GetItemsAsync(categoryId: "cat-drinks");

        // Assert
        foodItems.Should().HaveCount(2);
        foodItems.Should().OnlyContain(i => i.CategoryId == "cat-food");
        drinkItems.Should().ContainSingle();
        drinkItems[0].CategoryId.Should().Be("cat-drinks");
    }

    [Fact]
    public async Task GetItemsAsync_ExcludesArchived_ByDefault()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        await grain.RegisterItemAsync("active-item", "Active", 10.00m, null);
        await grain.UpdateItemAsync("archived-item", "Archived", 10.00m, null, false, true);

        // Act
        var items = await grain.GetItemsAsync();

        // Assert
        items.Should().ContainSingle();
        items[0].DocumentId.Should().Be("active-item");
    }

    [Fact]
    public async Task GetItemsAsync_IncludesArchived_WhenRequested()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        await grain.RegisterItemAsync("active-item", "Active", 10.00m, null);
        await grain.UpdateItemAsync("archived-item", "Archived", 10.00m, null, false, true);

        // Act
        var items = await grain.GetItemsAsync(includeArchived: true);

        // Assert
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetItemsAsync_EmptyRegistry_ShouldReturnEmptyList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act
        var items = await grain.GetItemsAsync();

        // Assert
        items.Should().BeEmpty();
    }

    // ============================================================================
    // Menu Category Registration Tests
    // ============================================================================

    [Fact]
    public async Task RegisterCategoryAsync_ShouldRegisterCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId);

        // Act
        await grain.RegisterCategoryAsync(
            documentId: documentId,
            name: "Appetizers",
            displayOrder: 1,
            color: "#FF5733");

        // Assert
        var categories = await grain.GetCategoriesAsync();
        categories.Should().ContainSingle();
        categories[0].DocumentId.Should().Be(documentId);
        categories[0].Name.Should().Be("Appetizers");
        categories[0].DisplayOrder.Should().Be(1);
        categories[0].Color.Should().Be("#FF5733");
        categories[0].HasDraft.Should().BeFalse();
        categories[0].IsArchived.Should().BeFalse();
        categories[0].ItemCount.Should().Be(0);
    }

    [Fact]
    public async Task RegisterCategoryAsync_WithNullColor_ShouldRegisterCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId);

        // Act
        await grain.RegisterCategoryAsync(
            documentId: documentId,
            name: "No Color Category",
            displayOrder: 1,
            color: null);

        // Assert
        var categories = await grain.GetCategoriesAsync();
        categories.Should().ContainSingle();
        categories[0].Color.Should().BeNull();
    }

    [Fact]
    public async Task RegisterCategoryAsync_MultipleCategories_ShouldRegisterAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act
        await grain.RegisterCategoryAsync("cat-1", "Appetizers", 1, "#FF0000");
        await grain.RegisterCategoryAsync("cat-2", "Main Courses", 2, "#00FF00");
        await grain.RegisterCategoryAsync("cat-3", "Desserts", 3, "#0000FF");

        // Assert
        var categories = await grain.GetCategoriesAsync();
        categories.Should().HaveCount(3);
    }

    [Fact]
    public async Task RegisterCategoryAsync_SameDocumentId_ShouldReplaceExisting()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = "cat-to-replace";
        var grain = GetGrain(orgId);
        await grain.RegisterCategoryAsync(documentId, "Original", 1, "#FF0000");

        // Act
        await grain.RegisterCategoryAsync(documentId, "Updated", 2, "#00FF00");

        // Assert
        var categories = await grain.GetCategoriesAsync();
        categories.Should().ContainSingle();
        categories[0].Name.Should().Be("Updated");
        categories[0].DisplayOrder.Should().Be(2);
        categories[0].Color.Should().Be("#00FF00");
    }

    [Fact]
    public async Task UpdateCategoryAsync_ShouldUpdateExistingCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = "cat-to-update";
        var grain = GetGrain(orgId);
        await grain.RegisterCategoryAsync(documentId, "Original", 1, null);

        // Act
        await grain.UpdateCategoryAsync(
            documentId: documentId,
            name: "Updated Name",
            displayOrder: 5,
            color: "#FFFFFF",
            hasDraft: true,
            isArchived: false,
            itemCount: 10);

        // Assert
        var categories = await grain.GetCategoriesAsync();
        categories.Should().ContainSingle();
        categories[0].Name.Should().Be("Updated Name");
        categories[0].DisplayOrder.Should().Be(5);
        categories[0].Color.Should().Be("#FFFFFF");
        categories[0].HasDraft.Should().BeTrue();
        categories[0].ItemCount.Should().Be(10);
    }

    [Fact]
    public async Task UpdateCategoryAsync_WithArchivedFlag_ShouldSetArchived()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = "cat-to-archive";
        var grain = GetGrain(orgId);
        await grain.RegisterCategoryAsync(documentId, "Category", 1, null);

        // Act
        await grain.UpdateCategoryAsync(
            documentId: documentId,
            name: "Category",
            displayOrder: 1,
            color: null,
            hasDraft: false,
            isArchived: true,
            itemCount: 0);

        // Assert - archived categories not returned by default
        var categories = await grain.GetCategoriesAsync(includeArchived: false);
        categories.Should().BeEmpty();

        // Assert - archived categories returned when requested
        var allCategories = await grain.GetCategoriesAsync(includeArchived: true);
        allCategories.Should().ContainSingle();
        allCategories[0].IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task UnregisterCategoryAsync_ShouldRemoveCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = "cat-to-remove";
        var grain = GetGrain(orgId);
        await grain.RegisterCategoryAsync(documentId, "Category to Remove", 1, null);

        // Act
        await grain.UnregisterCategoryAsync(documentId);

        // Assert
        var categories = await grain.GetCategoriesAsync(includeArchived: true);
        categories.Should().BeEmpty();
    }

    [Fact]
    public async Task UnregisterCategoryAsync_NonExistingCategory_ShouldNotThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act & Assert - should not throw
        await grain.UnregisterCategoryAsync("non-existing-category");
    }

    [Fact]
    public async Task GetCategoriesAsync_ShouldReturnOrderedByDisplayOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        await grain.RegisterCategoryAsync("cat-3", "Desserts", 3, null);
        await grain.RegisterCategoryAsync("cat-1", "Appetizers", 1, null);
        await grain.RegisterCategoryAsync("cat-2", "Main Courses", 2, null);

        // Act
        var categories = await grain.GetCategoriesAsync();

        // Assert
        categories.Should().HaveCount(3);
        categories[0].DisplayOrder.Should().Be(1);
        categories[1].DisplayOrder.Should().Be(2);
        categories[2].DisplayOrder.Should().Be(3);
    }

    [Fact]
    public async Task GetCategoriesAsync_EmptyRegistry_ShouldReturnEmptyList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act
        var categories = await grain.GetCategoriesAsync();

        // Assert
        categories.Should().BeEmpty();
    }

    // ============================================================================
    // Modifier Block Registration Tests
    // ============================================================================

    [Fact]
    public async Task RegisterModifierBlockAsync_ShouldRegisterBlock()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId);

        // Act
        await grain.RegisterModifierBlockAsync(blockId, "Size Options");

        // Assert
        var blockIds = await grain.GetModifierBlockIdsAsync();
        blockIds.Should().ContainSingle();
        blockIds[0].Should().Be(blockId);
    }

    [Fact]
    public async Task RegisterModifierBlockAsync_MultipleBlocks_ShouldRegisterAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act
        await grain.RegisterModifierBlockAsync("block-1", "Size");
        await grain.RegisterModifierBlockAsync("block-2", "Temperature");
        await grain.RegisterModifierBlockAsync("block-3", "Toppings");

        // Assert
        var blockIds = await grain.GetModifierBlockIdsAsync();
        blockIds.Should().HaveCount(3);
        blockIds.Should().Contain("block-1");
        blockIds.Should().Contain("block-2");
        blockIds.Should().Contain("block-3");
    }

    [Fact]
    public async Task RegisterModifierBlockAsync_SameBlockId_ShouldNotDuplicate()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = "same-block";
        var grain = GetGrain(orgId);

        // Act
        await grain.RegisterModifierBlockAsync(blockId, "Original Name");
        await grain.RegisterModifierBlockAsync(blockId, "Updated Name");

        // Assert
        var blockIds = await grain.GetModifierBlockIdsAsync();
        blockIds.Should().ContainSingle();
        blockIds[0].Should().Be(blockId);
    }

    [Fact]
    public async Task UnregisterModifierBlockAsync_ShouldRemoveBlock()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = "block-to-remove";
        var grain = GetGrain(orgId);
        await grain.RegisterModifierBlockAsync(blockId, "Block to Remove");

        // Act
        await grain.UnregisterModifierBlockAsync(blockId);

        // Assert
        var blockIds = await grain.GetModifierBlockIdsAsync();
        blockIds.Should().BeEmpty();
    }

    [Fact]
    public async Task UnregisterModifierBlockAsync_NonExistingBlock_ShouldNotThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act & Assert - should not throw
        await grain.UnregisterModifierBlockAsync("non-existing-block");
    }

    [Fact]
    public async Task GetModifierBlockIdsAsync_EmptyRegistry_ShouldReturnEmptyList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act
        var blockIds = await grain.GetModifierBlockIdsAsync();

        // Assert
        blockIds.Should().BeEmpty();
    }

    // ============================================================================
    // Tag Registration Tests
    // ============================================================================

    [Fact]
    public async Task RegisterTagAsync_ShouldRegisterTag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var tagId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId);

        // Act
        await grain.RegisterTagAsync(tagId, "Gluten Free", TagCategory.Dietary);

        // Assert
        var tagIds = await grain.GetTagIdsAsync();
        tagIds.Should().ContainSingle();
        tagIds[0].Should().Be(tagId);
    }

    [Fact]
    public async Task RegisterTagAsync_MultipleTags_ShouldRegisterAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act
        await grain.RegisterTagAsync("tag-1", "Vegan", TagCategory.Dietary);
        await grain.RegisterTagAsync("tag-2", "Contains Nuts", TagCategory.Allergen);
        await grain.RegisterTagAsync("tag-3", "Chef's Special", TagCategory.Promotional);

        // Assert
        var tagIds = await grain.GetTagIdsAsync();
        tagIds.Should().HaveCount(3);
    }

    [Fact]
    public async Task RegisterTagAsync_SameTagId_ShouldReplaceMetadata()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var tagId = "same-tag";
        var grain = GetGrain(orgId);

        // Act
        await grain.RegisterTagAsync(tagId, "Original", TagCategory.Dietary);
        await grain.RegisterTagAsync(tagId, "Updated", TagCategory.Allergen);

        // Assert
        var tagIds = await grain.GetTagIdsAsync();
        tagIds.Should().ContainSingle();

        // Filter by new category should include the tag
        var allergenTags = await grain.GetTagIdsAsync(TagCategory.Allergen);
        allergenTags.Should().ContainSingle();

        // Filter by old category should not include the tag
        var dietaryTags = await grain.GetTagIdsAsync(TagCategory.Dietary);
        dietaryTags.Should().BeEmpty();
    }

    [Fact]
    public async Task UnregisterTagAsync_ShouldRemoveTag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var tagId = "tag-to-remove";
        var grain = GetGrain(orgId);
        await grain.RegisterTagAsync(tagId, "Tag to Remove", TagCategory.Dietary);

        // Act
        await grain.UnregisterTagAsync(tagId);

        // Assert
        var tagIds = await grain.GetTagIdsAsync();
        tagIds.Should().BeEmpty();
    }

    [Fact]
    public async Task UnregisterTagAsync_NonExistingTag_ShouldNotThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act & Assert - should not throw
        await grain.UnregisterTagAsync("non-existing-tag");
    }

    [Fact]
    public async Task GetTagIdsAsync_FilterByCategory_ShouldReturnMatchingTags()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        await grain.RegisterTagAsync("tag-1", "Vegan", TagCategory.Dietary);
        await grain.RegisterTagAsync("tag-2", "Vegetarian", TagCategory.Dietary);
        await grain.RegisterTagAsync("tag-3", "Contains Nuts", TagCategory.Allergen);
        await grain.RegisterTagAsync("tag-4", "Special Offer", TagCategory.Promotional);

        // Act
        var dietaryTags = await grain.GetTagIdsAsync(TagCategory.Dietary);
        var allergenTags = await grain.GetTagIdsAsync(TagCategory.Allergen);
        var promotionalTags = await grain.GetTagIdsAsync(TagCategory.Promotional);

        // Assert
        dietaryTags.Should().HaveCount(2);
        allergenTags.Should().ContainSingle();
        promotionalTags.Should().ContainSingle();
    }

    [Fact]
    public async Task GetTagIdsAsync_NoFilter_ShouldReturnAllTags()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        await grain.RegisterTagAsync("tag-1", "Vegan", TagCategory.Dietary);
        await grain.RegisterTagAsync("tag-2", "Contains Nuts", TagCategory.Allergen);
        await grain.RegisterTagAsync("tag-3", "Special Offer", TagCategory.Promotional);

        // Act
        var allTags = await grain.GetTagIdsAsync();

        // Assert
        allTags.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetTagIdsAsync_EmptyRegistry_ShouldReturnEmptyList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act
        var tagIds = await grain.GetTagIdsAsync();

        // Assert
        tagIds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTagIdsAsync_CategoryWithNoTags_ShouldReturnEmptyList()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        await grain.RegisterTagAsync("tag-1", "Vegan", TagCategory.Dietary);

        // Act
        var allergenTags = await grain.GetTagIdsAsync(TagCategory.Allergen);

        // Assert
        allergenTags.Should().BeEmpty();
    }

    // ============================================================================
    // Cross-Entity Tests
    // ============================================================================

    [Fact]
    public async Task Registry_ShouldHandleMultipleEntityTypes()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act - register multiple types
        await grain.RegisterItemAsync("item-1", "Item 1", 10.00m, "cat-1");
        await grain.RegisterItemAsync("item-2", "Item 2", 15.00m, "cat-1");
        await grain.RegisterCategoryAsync("cat-1", "Category 1", 1, "#FF0000");
        await grain.RegisterCategoryAsync("cat-2", "Category 2", 2, "#00FF00");
        await grain.RegisterModifierBlockAsync("block-1", "Size");
        await grain.RegisterTagAsync("tag-1", "Vegan", TagCategory.Dietary);

        // Assert
        var items = await grain.GetItemsAsync();
        var categories = await grain.GetCategoriesAsync();
        var blocks = await grain.GetModifierBlockIdsAsync();
        var tags = await grain.GetTagIdsAsync();

        items.Should().HaveCount(2);
        categories.Should().HaveCount(2);
        blocks.Should().ContainSingle();
        tags.Should().ContainSingle();
    }

    [Fact]
    public async Task Registry_ShouldIsolateByOrganization()
    {
        // Arrange
        var orgId1 = Guid.NewGuid();
        var orgId2 = Guid.NewGuid();
        var grain1 = GetGrain(orgId1);
        var grain2 = GetGrain(orgId2);

        // Act
        await grain1.RegisterItemAsync("item-1", "Org1 Item", 10.00m, null);
        await grain1.RegisterCategoryAsync("cat-1", "Org1 Category", 1, null);
        await grain2.RegisterItemAsync("item-2", "Org2 Item", 20.00m, null);
        await grain2.RegisterCategoryAsync("cat-2", "Org2 Category", 1, null);

        // Assert - each org should only see their own data
        var org1Items = await grain1.GetItemsAsync();
        var org1Categories = await grain1.GetCategoriesAsync();
        var org2Items = await grain2.GetItemsAsync();
        var org2Categories = await grain2.GetCategoriesAsync();

        org1Items.Should().ContainSingle();
        org1Items[0].Name.Should().Be("Org1 Item");
        org1Categories.Should().ContainSingle();
        org1Categories[0].Name.Should().Be("Org1 Category");

        org2Items.Should().ContainSingle();
        org2Items[0].Name.Should().Be("Org2 Item");
        org2Categories.Should().ContainSingle();
        org2Categories[0].Name.Should().Be("Org2 Category");
    }

    // ============================================================================
    // Edge Case Tests
    // ============================================================================

    [Fact]
    public async Task RegisterItemAsync_LastModified_ShouldBeSet()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        var before = DateTimeOffset.UtcNow;

        // Act
        await grain.RegisterItemAsync("item-1", "Item", 10.00m, null);

        // Assert
        var items = await grain.GetItemsAsync();
        items[0].LastModified.Should().BeOnOrAfter(before);
        items[0].LastModified.Should().BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task RegisterCategoryAsync_LastModified_ShouldBeSet()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        var before = DateTimeOffset.UtcNow;

        // Act
        await grain.RegisterCategoryAsync("cat-1", "Category", 1, null);

        // Assert
        var categories = await grain.GetCategoriesAsync();
        categories[0].LastModified.Should().BeOnOrAfter(before);
        categories[0].LastModified.Should().BeOnOrBefore(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task UpdateItemAsync_ShouldUpdateLastModified()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = "item-1";
        var grain = GetGrain(orgId);
        await grain.RegisterItemAsync(documentId, "Item", 10.00m, null);

        var items = await grain.GetItemsAsync();
        var originalModified = items[0].LastModified;

        // Small delay to ensure time difference
        await Task.Delay(10);

        // Act
        await grain.UpdateItemAsync(documentId, "Updated Item", 15.00m, null, false, false);

        // Assert
        var updatedItems = await grain.GetItemsAsync();
        updatedItems[0].LastModified.Should().BeAfter(originalModified);
    }

    [Fact]
    public async Task UpdateCategoryAsync_ShouldUpdateLastModified()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = "cat-1";
        var grain = GetGrain(orgId);
        await grain.RegisterCategoryAsync(documentId, "Category", 1, null);

        var categories = await grain.GetCategoriesAsync();
        var originalModified = categories[0].LastModified;

        // Small delay to ensure time difference
        await Task.Delay(10);

        // Act
        await grain.UpdateCategoryAsync(documentId, "Updated Category", 2, "#FF0000", false, false, 5);

        // Assert
        var updatedCategories = await grain.GetCategoriesAsync();
        updatedCategories[0].LastModified.Should().BeAfter(originalModified);
    }

    [Fact]
    public async Task UpdateItemAsync_PreservesPublishedVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = "item-1";
        var grain = GetGrain(orgId);
        await grain.RegisterItemAsync(documentId, "Item", 10.00m, null);

        // Act
        await grain.UpdateItemAsync(documentId, "Updated", 15.00m, null, true, false);

        // Assert
        var items = await grain.GetItemsAsync();
        items[0].PublishedVersion.Should().Be(1);
    }

    [Fact]
    public async Task GetItemsAsync_WithArchivedAndCategoryFilter_ShouldCombineFilters()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        await grain.RegisterItemAsync("item-1", "Active Food", 10.00m, "food");
        await grain.UpdateItemAsync("item-2", "Archived Food", 10.00m, "food", false, true);
        await grain.RegisterItemAsync("item-3", "Active Drink", 5.00m, "drinks");
        await grain.UpdateItemAsync("item-4", "Archived Drink", 5.00m, "drinks", false, true);

        // Act
        var activeFoodItems = await grain.GetItemsAsync(categoryId: "food", includeArchived: false);
        var allFoodItems = await grain.GetItemsAsync(categoryId: "food", includeArchived: true);

        // Assert
        activeFoodItems.Should().ContainSingle();
        activeFoodItems[0].Name.Should().Be("Active Food");
        allFoodItems.Should().HaveCount(2);
    }

    [Fact]
    public async Task Registry_ShouldHandleLargeNumberOfItems()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        const int itemCount = 100;

        // Act
        for (int i = 0; i < itemCount; i++)
        {
            await grain.RegisterItemAsync($"item-{i}", $"Item {i}", i * 1.5m, i % 5 == 0 ? "cat-a" : "cat-b");
        }

        // Assert
        var allItems = await grain.GetItemsAsync();
        var catAItems = await grain.GetItemsAsync(categoryId: "cat-a");
        var catBItems = await grain.GetItemsAsync(categoryId: "cat-b");

        allItems.Should().HaveCount(itemCount);
        catAItems.Should().HaveCount(itemCount / 5);
        catBItems.Should().HaveCount(itemCount - itemCount / 5);
    }

    [Fact]
    public async Task Registry_ShouldPersistAcrossGrainCalls()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain1 = GetGrain(orgId);

        // Act
        await grain1.RegisterItemAsync("item-1", "Item 1", 10.00m, null);
        await grain1.RegisterCategoryAsync("cat-1", "Category 1", 1, null);

        // Get a new grain reference (simulates separate call)
        var grain2 = GetGrain(orgId);

        // Assert
        var items = await grain2.GetItemsAsync();
        var categories = await grain2.GetCategoriesAsync();

        items.Should().ContainSingle();
        categories.Should().ContainSingle();
    }
}
