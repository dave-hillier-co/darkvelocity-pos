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

    // Given: an empty menu registry for an organization
    // When: a "Caesar Salad" item at $12.99 is registered in category "category-1"
    // Then: the registry contains the item with correct name, price, category, and default flags
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

    // Given: an empty menu registry for an organization
    // When: an "Uncategorized Item" is registered with no category assignment
    // Then: the item is registered with a null category ID
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

    // Given: an empty menu registry for an organization
    // When: three items are registered across two categories
    // Then: all three items are present in the registry
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

    // Given: a menu item "Original Name" already registered in the registry
    // When: a new item is registered with the same document ID but different name and price
    // Then: the existing entry is replaced with the updated name, price, and category
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

    // Given: a registered menu item "Original" at $10.00 in cat-1
    // When: the item is updated with a new name, price, category, and hasDraft flag
    // Then: the registry entry reflects all updated fields
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

    // Given: a registered menu item
    // When: the item is updated with isArchived=true
    // Then: the item is excluded from default queries but included when includeArchived=true
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

    // Given: an empty menu registry
    // When: an update is performed for a non-existing document ID
    // Then: a new registry entry is created for the item
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

    // Given: a registered menu item in the registry
    // When: the item is unregistered
    // Then: the item is completely removed from the registry
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

    // Given: an empty menu registry
    // When: an unregister is attempted for a non-existing item
    // Then: no exception is thrown (idempotent operation)
    [Fact]
    public async Task UnregisterItemAsync_NonExistingItem_ShouldNotThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act & Assert - should not throw
        await grain.UnregisterItemAsync("non-existing-item");
    }

    // Given: four items registered across "cat-food", "cat-drinks", and uncategorized
    // When: items are queried filtered by "cat-food" and "cat-drinks" respectively
    // Then: only items matching the requested category are returned
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

    // Given: one active item and one archived item in the registry
    // When: items are queried with default parameters (no includeArchived flag)
    // Then: only the active item is returned
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

    // Given: one active item and one archived item in the registry
    // When: items are queried with includeArchived=true
    // Then: both active and archived items are returned
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

    // Given: a menu registry with no items registered
    // When: items are queried
    // Then: an empty list is returned
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

    // Given: an empty menu registry for an organization
    // When: an "Appetizers" category is registered with display order 1 and a color
    // Then: the registry contains the category with correct metadata and default flags
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

    // Given: an empty menu registry for an organization
    // When: a category is registered with no color specified
    // Then: the category is registered with a null color
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

    // Given: an empty menu registry
    // When: three categories (Appetizers, Main Courses, Desserts) are registered
    // Then: all three categories are present in the registry
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

    // Given: a category "Original" already registered in the registry
    // When: a new category with the same document ID but different name, order, and color is registered
    // Then: the existing entry is replaced with the updated metadata
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

    // Given: a registered category "Original" at display order 1
    // When: the category is updated with a new name, display order, color, draft flag, and item count
    // Then: all updated fields are reflected in the registry
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

    // Given: a registered menu category
    // When: the category is updated with isArchived=true
    // Then: the category is excluded from default queries but included when includeArchived=true
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

    // Given: a registered category in the registry
    // When: the category is unregistered
    // Then: the category is completely removed from the registry
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

    // Given: an empty menu registry
    // When: an unregister is attempted for a non-existing category
    // Then: no exception is thrown (idempotent operation)
    [Fact]
    public async Task UnregisterCategoryAsync_NonExistingCategory_ShouldNotThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act & Assert - should not throw
        await grain.UnregisterCategoryAsync("non-existing-category");
    }

    // Given: three categories registered out of display order (Desserts=3, Appetizers=1, Main Courses=2)
    // When: categories are queried from the registry
    // Then: categories are returned sorted by display order ascending
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

    // Given: a menu registry with no categories registered
    // When: categories are queried
    // Then: an empty list is returned
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

    // Given: an empty menu registry
    // When: a "Size Options" modifier block is registered
    // Then: the block ID appears in the registry's modifier block list
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

    // Given: an empty menu registry
    // When: three modifier blocks (Size, Temperature, Toppings) are registered
    // Then: all three block IDs appear in the registry
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

    // Given: a modifier block already registered in the registry
    // When: a block with the same ID but a different name is registered again
    // Then: only one entry exists (no duplicate block IDs)
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

    // Given: a registered modifier block in the registry
    // When: the modifier block is unregistered
    // Then: the block is removed from the registry
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

    // Given: an empty menu registry
    // When: an unregister is attempted for a non-existing modifier block
    // Then: no exception is thrown (idempotent operation)
    [Fact]
    public async Task UnregisterModifierBlockAsync_NonExistingBlock_ShouldNotThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act & Assert - should not throw
        await grain.UnregisterModifierBlockAsync("non-existing-block");
    }

    // Given: a menu registry with no modifier blocks registered
    // When: modifier block IDs are queried
    // Then: an empty list is returned
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

    // Given: an empty menu registry
    // When: a "Gluten Free" dietary tag is registered
    // Then: the tag ID appears in the registry's tag list
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

    // Given: an empty menu registry
    // When: three tags (Vegan, Contains Nuts, Chef's Special) across different categories are registered
    // Then: all three tag IDs appear in the registry
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

    // Given: a tag registered as "Original" in the Dietary category
    // When: the same tag ID is re-registered as "Updated" in the Allergen category
    // Then: the tag metadata is replaced; it appears under Allergen but not Dietary
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

    // Given: a registered content tag in the registry
    // When: the tag is unregistered
    // Then: the tag is removed from the registry
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

    // Given: an empty menu registry
    // When: an unregister is attempted for a non-existing tag
    // Then: no exception is thrown (idempotent operation)
    [Fact]
    public async Task UnregisterTagAsync_NonExistingTag_ShouldNotThrow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);

        // Act & Assert - should not throw
        await grain.UnregisterTagAsync("non-existing-tag");
    }

    // Given: tags registered across Dietary (2), Allergen (1), and Promotional (1) categories
    // When: tags are queried filtered by each category
    // Then: only tags matching the requested category are returned
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

    // Given: three tags registered across different categories
    // When: tags are queried without a category filter
    // Then: all three tags are returned regardless of category
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

    // Given: a menu registry with no tags registered
    // When: tag IDs are queried
    // Then: an empty list is returned
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

    // Given: a registry with only a Dietary tag and no Allergen tags
    // When: tags are queried filtered by the Allergen category
    // Then: an empty list is returned
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

    // Given: an empty menu registry
    // When: items, categories, modifier blocks, and tags are all registered in the same registry
    // Then: each entity type is stored and retrievable independently
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

    // Given: two separate organizations each with their own menu registry
    // When: items and categories are registered in each organization's registry
    // Then: each organization only sees its own data (tenant isolation)
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

    // Given: an empty menu registry
    // When: a new item is registered
    // Then: the item's LastModified timestamp is set to approximately the current time
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

    // Given: an empty menu registry
    // When: a new category is registered
    // Then: the category's LastModified timestamp is set to approximately the current time
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

    // Given: a registered item with an initial LastModified timestamp
    // When: the item is updated after a brief delay
    // Then: the LastModified timestamp advances to reflect the update time
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

    // Given: a registered category with an initial LastModified timestamp
    // When: the category is updated after a brief delay
    // Then: the LastModified timestamp advances to reflect the update time
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

    // Given: a registered item at published version 1
    // When: the item is updated with a new name and hasDraft=true
    // Then: the published version remains at 1 (updates don't increment the version)
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

    // Given: active and archived items across "food" and "drinks" categories
    // When: items are queried with category="food" and includeArchived toggled
    // Then: the category and archived filters combine correctly
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

    // Given: an empty menu registry
    // When: 100 items are registered across two categories
    // Then: all 100 items are retrievable, with correct counts per category filter
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

    // Given: an item and category registered via one grain reference
    // When: data is queried via a new grain reference to the same registry
    // Then: the previously registered data is still present (event-sourced persistence)
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
