using DarkVelocity.Host;
using DarkVelocity.Host.Events;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuItemDocumentGrainTests
{
    private readonly TestClusterFixture _fixture;

    public MenuItemDocumentGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMenuItemDocumentGrain GetGrain(Guid orgId, string documentId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuItemDocumentGrain>(
            GrainKeys.MenuItemDocument(orgId, documentId));
    }

    // Given: no existing menu item document
    // When: a "Caesar Salad" at $12.99 is created with immediate publication
    // Then: the document is at version 1, published with the correct name and price, and has no draft
    [Fact]
    public async Task CreateAsync_ShouldCreateDocument()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);

        // Act
        var result = await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Caesar Salad",
            Price: 12.99m,
            Description: "Crisp romaine with house-made dressing",
            PublishImmediately: true));

        // Assert
        result.DocumentId.Should().Be(documentId);
        result.CurrentVersion.Should().Be(1);
        result.PublishedVersion.Should().Be(1);
        result.DraftVersion.Should().BeNull();
        result.IsArchived.Should().BeFalse();
        result.Published.Should().NotBeNull();
        result.Published!.Name.Should().Be("Caesar Salad");
        result.Published.Price.Should().Be(12.99m);
    }

    // Given: no existing menu item document
    // When: a "Draft Item" is created without immediate publication
    // Then: the document exists only as a draft at version 1 with no published version
    [Fact]
    public async Task CreateAsync_WithoutPublish_ShouldCreateDraft()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);

        // Act
        var result = await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Draft Item",
            Price: 9.99m,
            PublishImmediately: false));

        // Assert
        result.PublishedVersion.Should().BeNull();
        result.DraftVersion.Should().Be(1);
        result.Published.Should().BeNull();
        result.Draft.Should().NotBeNull();
        result.Draft!.Name.Should().Be("Draft Item");
    }

    // Given: a published menu item document at version 1
    // When: a new draft is created with an updated name, price, and change note
    // Then: the draft is at version 2 with the new values while the published version remains at version 1
    [Fact]
    public async Task CreateDraftAsync_ShouldCreateNewDraftVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Original Item",
            Price: 10.00m,
            PublishImmediately: true));

        // Act
        var draft = await grain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            Name: "Updated Item",
            Price: 12.00m,
            ChangeNote: "Price increase"));

        // Assert
        draft.VersionNumber.Should().Be(2);
        draft.Name.Should().Be("Updated Item");
        draft.Price.Should().Be(12.00m);
        draft.ChangeNote.Should().Be("Price increase");

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PublishedVersion.Should().Be(1);
        snapshot.DraftVersion.Should().Be(2);
        snapshot.TotalVersions.Should().Be(2);
    }

    // Given: a published menu item with a pending draft containing an updated name and price
    // When: the draft is published
    // Then: the published version advances to version 2 with the draft's content and the draft is cleared
    [Fact]
    public async Task PublishDraftAsync_ShouldMakeDraftLive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Original",
            Price: 10.00m,
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            Name: "New Version",
            Price: 15.00m));

        // Act
        await grain.PublishDraftAsync(note: "Publishing new price");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PublishedVersion.Should().Be(2);
        snapshot.DraftVersion.Should().BeNull();
        snapshot.Published!.Name.Should().Be("New Version");
        snapshot.Published.Price.Should().Be(15.00m);
    }

    // Given: a published menu item with a pending draft at an extreme price
    // When: the draft is discarded
    // Then: the draft is removed, version count returns to 1, and the original published version remains
    [Fact]
    public async Task DiscardDraftAsync_ShouldRemoveDraft()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Original",
            Price: 10.00m,
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            Name: "Bad Draft",
            Price: 999.00m));

        // Act
        await grain.DiscardDraftAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.DraftVersion.Should().BeNull();
        snapshot.Draft.Should().BeNull();
        snapshot.PublishedVersion.Should().Be(1);
        snapshot.TotalVersions.Should().Be(1);
    }

    // Given: a menu item with version 1 ($10) and version 2 ($20) both published
    // When: the item is reverted to version 1
    // Then: a new version 3 is created with version 1's content ($10) and the total version count is 3
    [Fact]
    public async Task RevertToVersionAsync_ShouldRevertToOlderVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Version 1",
            Price: 10.00m,
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            Name: "Version 2",
            Price: 20.00m));
        await grain.PublishDraftAsync();

        // Act
        await grain.RevertToVersionAsync(1, reason: "Reverting price change");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PublishedVersion.Should().Be(3);
        snapshot.Published!.Name.Should().Be("Version 1");
        snapshot.Published.Price.Should().Be(10.00m);
        snapshot.TotalVersions.Should().Be(3);
    }

    // Given: a menu item document that has been published through 3 versions
    // When: the version history is retrieved
    // Then: all 3 versions are returned in reverse chronological order (newest first)
    [Fact]
    public async Task GetVersionHistoryAsync_ShouldReturnAllVersions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "V1", Price: 10.00m, PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateMenuItemDraftCommand(Name: "V2"));
        await grain.PublishDraftAsync();
        await grain.CreateDraftAsync(new CreateMenuItemDraftCommand(Name: "V3"));
        await grain.PublishDraftAsync();

        // Act
        var history = await grain.GetVersionHistoryAsync();

        // Assert
        history.Should().HaveCount(3);
        history[0].VersionNumber.Should().Be(3);
        history[0].Name.Should().Be("V3");
        history[1].VersionNumber.Should().Be(2);
        history[2].VersionNumber.Should().Be(1);
    }

    // Given: a published "Chicken" menu item in English
    // When: a Spanish (es-ES) translation is added with name, description, and kitchen name
    // Then: the published version includes the Spanish translation with all translated fields
    [Fact]
    public async Task AddTranslationAsync_ShouldAddLocalization()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Chicken",
            Price: 15.00m,
            Description: "Grilled chicken",
            PublishImmediately: true));

        // Act
        await grain.AddTranslationAsync(new AddMenuItemTranslationCommand(
            Locale: "es-ES",
            Name: "Pollo",
            Description: "Pollo a la parrilla",
            KitchenName: "POLLO"));

        // Assert
        var version = await grain.GetPublishedAsync();
        version!.Translations.Should().ContainKey("es-ES");
        version.Translations["es-ES"].Name.Should().Be("Pollo");
        version.Translations["es-ES"].Description.Should().Be("Pollo a la parrilla");
        version.Translations["es-ES"].KitchenName.Should().Be("POLLO");
    }

    // Given: a menu item with a published version and a happy hour pricing version
    // When: the happy hour version is scheduled to activate tomorrow
    // Then: an active schedule entry is created targeting the correct version and activation time
    [Fact]
    public async Task ScheduleChangeAsync_ShouldCreateSchedule()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Regular Menu Item",
            Price: 10.00m,
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            Name: "Happy Hour Item",
            Price: 7.00m));
        await grain.PublishDraftAsync();

        var activateAt = DateTimeOffset.UtcNow.AddDays(1);

        // Act
        var schedule = await grain.ScheduleChangeAsync(
            version: 2,
            activateAt: activateAt,
            name: "Happy Hour Pricing");

        // Assert
        schedule.VersionToActivate.Should().Be(2);
        schedule.ActivateAt.Should().Be(activateAt);
        schedule.Name.Should().Be("Happy Hour Pricing");
        schedule.IsActive.Should().BeTrue();

        var schedules = await grain.GetSchedulesAsync();
        schedules.Should().HaveCount(1);
    }

    // Given: a menu item with a scheduled version change
    // When: the schedule is cancelled
    // Then: the schedule list is empty and no version change is pending
    [Fact]
    public async Task CancelScheduleAsync_ShouldRemoveSchedule()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Item", Price: 10.00m, PublishImmediately: true));

        var schedule = await grain.ScheduleChangeAsync(1, DateTimeOffset.UtcNow.AddDays(1));

        // Act
        await grain.CancelScheduleAsync(schedule.ScheduleId);

        // Assert
        var schedules = await grain.GetSchedulesAsync();
        schedules.Should().BeEmpty();
    }

    [Fact]
    public async Task ArchiveAsync_ShouldArchiveDocument()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "To Archive",
            Price: 10.00m,
            PublishImmediately: true));

        // Act
        await grain.ArchiveAsync(reason: "Discontinued");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task RestoreAsync_ShouldRestoreArchivedDocument()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "To Restore",
            Price: 10.00m,
            PublishImmediately: true));
        await grain.ArchiveAsync();

        // Act
        await grain.RestoreAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task PreviewAtAsync_ShouldReturnScheduledVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Current",
            Price: 10.00m,
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            Name: "Future",
            Price: 15.00m));
        await grain.PublishDraftAsync();

        var futureTime = DateTimeOffset.UtcNow.AddDays(7);
        await grain.ScheduleChangeAsync(2, futureTime);

        // Act
        var previewNow = await grain.PreviewAtAsync(DateTimeOffset.UtcNow);
        var previewFuture = await grain.PreviewAtAsync(futureTime.AddHours(1));

        // Assert
        previewNow!.Name.Should().Be("Future"); // Current published is version 2
        previewFuture!.Name.Should().Be("Future"); // Scheduled version 2
    }

    [Fact]
    public async Task CreateAsync_WithNutritionInfo_ShouldStoreNutrition()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);

        var nutrition = new NutritionInfoData(
            Calories: 450,
            CaloriesFromFat: 180,
            TotalFatGrams: 20,
            SaturatedFatGrams: 8,
            TransFatGrams: 0,
            CholesterolMg: 75,
            SodiumMg: 980,
            TotalCarbohydratesGrams: 35,
            DietaryFiberGrams: 3,
            SugarsGrams: 6,
            ProteinGrams: 28,
            ServingSize: "1 burger (250g)",
            ServingSizeGrams: 250);

        // Act
        var result = await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Classic Burger",
            Price: 14.99m,
            Description: "Angus beef patty with lettuce, tomato, and our special sauce",
            Nutrition: nutrition,
            PublishImmediately: true));

        // Assert
        result.Published.Should().NotBeNull();
        result.Published!.Nutrition.Should().NotBeNull();
        result.Published.Nutrition!.Calories.Should().Be(450);
        result.Published.Nutrition.ProteinGrams.Should().Be(28);
        result.Published.Nutrition.ServingSize.Should().Be("1 burger (250g)");
    }

    [Fact]
    public async Task CreateDraftAsync_WithNutritionInfo_ShouldUpdateNutrition()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);

        var initialNutrition = new NutritionInfoData(
            Calories: 300, CaloriesFromFat: null, TotalFatGrams: 12,
            SaturatedFatGrams: null, TransFatGrams: null, CholesterolMg: null,
            SodiumMg: 500, TotalCarbohydratesGrams: 30, DietaryFiberGrams: null,
            SugarsGrams: null, ProteinGrams: 20, ServingSize: null, ServingSizeGrams: null);

        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Grilled Chicken",
            Price: 12.99m,
            Nutrition: initialNutrition,
            PublishImmediately: true));

        var updatedNutrition = new NutritionInfoData(
            Calories: 280, CaloriesFromFat: 70, TotalFatGrams: 8,
            SaturatedFatGrams: 2, TransFatGrams: 0, CholesterolMg: 85,
            SodiumMg: 450, TotalCarbohydratesGrams: 25, DietaryFiberGrams: 2,
            SugarsGrams: 3, ProteinGrams: 32, ServingSize: "1 breast (170g)", ServingSizeGrams: 170);

        // Act
        var draft = await grain.CreateDraftAsync(new CreateMenuItemDraftCommand(
            Name: "Grilled Chicken Breast",
            Nutrition: updatedNutrition,
            ChangeNote: "Updated nutrition info with complete values"));

        // Assert
        draft.Nutrition.Should().NotBeNull();
        draft.Nutrition!.Calories.Should().Be(280);
        draft.Nutrition.ProteinGrams.Should().Be(32);
        draft.Nutrition.ServingSize.Should().Be("1 breast (170g)");
    }

    [Fact]
    public async Task CreateAsync_WithTagIds_ShouldStoreTags()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);

        var tagIds = new List<string> { "tag-gluten-free", "tag-vegetarian" };

        // Act
        var result = await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Garden Salad",
            Price: 9.99m,
            TagIds: tagIds,
            PublishImmediately: true));

        // Assert
        result.Published.Should().NotBeNull();
        result.Published!.TagIds.Should().HaveCount(2);
        result.Published.TagIds.Should().Contain("tag-gluten-free");
        result.Published.TagIds.Should().Contain("tag-vegetarian");
    }

    [Fact]
    public async Task RemoveTranslationAsync_ShouldRemoveLocalization()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Chicken",
            Price: 15.00m,
            Description: "Grilled chicken",
            PublishImmediately: true));
        await grain.AddTranslationAsync(new AddMenuItemTranslationCommand(
            Locale: "es-ES",
            Name: "Pollo",
            Description: "Pollo a la parrilla"));
        await grain.AddTranslationAsync(new AddMenuItemTranslationCommand(
            Locale: "fr-FR",
            Name: "Poulet",
            Description: "Poulet grille"));

        // Act
        await grain.RemoveTranslationAsync("es-ES");

        // Assert
        var version = await grain.GetPublishedAsync();
        version!.Translations.Should().NotContainKey("es-ES");
        version.Translations.Should().ContainKey("fr-FR");
        version.Translations.Should().ContainKey("en-US");
    }

    [Fact]
    public async Task RemoveTranslationAsync_DefaultLocale_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Test Item",
            Price: 10.00m,
            Locale: "en-US",
            PublishImmediately: true));

        // Act
        var action = () => grain.RemoveTranslationAsync("en-US");

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*default locale*");
    }

    [Fact]
    public async Task AddTranslationAsync_MultipleLocales_ShouldStoreAll()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuItemDocumentCommand(
            Name: "Pizza",
            Price: 12.99m,
            Description: "Classic Italian pizza",
            Locale: "en-US",
            PublishImmediately: true));

        // Act
        await grain.AddTranslationAsync(new AddMenuItemTranslationCommand(
            Locale: "es-ES",
            Name: "Pizza",
            Description: "Pizza italiana clasica"));
        await grain.AddTranslationAsync(new AddMenuItemTranslationCommand(
            Locale: "fr-FR",
            Name: "Pizza",
            Description: "Pizza italienne classique"));
        await grain.AddTranslationAsync(new AddMenuItemTranslationCommand(
            Locale: "de-DE",
            Name: "Pizza",
            Description: "Klassische italienische Pizza"));
        await grain.AddTranslationAsync(new AddMenuItemTranslationCommand(
            Locale: "it-IT",
            Name: "Pizza",
            Description: "Pizza italiana classica"));

        // Assert
        var version = await grain.GetPublishedAsync();
        version!.Translations.Should().HaveCount(5);
        version.Translations.Should().ContainKey("en-US");
        version.Translations.Should().ContainKey("es-ES");
        version.Translations.Should().ContainKey("fr-FR");
        version.Translations.Should().ContainKey("de-DE");
        version.Translations.Should().ContainKey("it-IT");
        version.Translations["de-DE"].Description.Should().Be("Klassische italienische Pizza");
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class MenuCategoryDocumentGrainTests
{
    private readonly TestClusterFixture _fixture;

    public MenuCategoryDocumentGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IMenuCategoryDocumentGrain GetGrain(Guid orgId, string documentId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IMenuCategoryDocumentGrain>(
            GrainKeys.MenuCategoryDocument(orgId, documentId));
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);

        // Act
        var result = await grain.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Appetizers",
            DisplayOrder: 1,
            Color: "#FF5733",
            PublishImmediately: true));

        // Assert
        result.DocumentId.Should().Be(documentId);
        result.Published!.Name.Should().Be("Appetizers");
        result.Published.DisplayOrder.Should().Be(1);
        result.Published.Color.Should().Be("#FF5733");
    }

    [Fact]
    public async Task AddItemAsync_ShouldAddItemToCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var itemId1 = Guid.NewGuid().ToString();
        var itemId2 = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Main Courses",
            PublishImmediately: true));

        // Act
        await grain.AddItemAsync(itemId1);
        await grain.AddItemAsync(itemId2);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Published!.ItemDocumentIds.Should().HaveCount(2);
        snapshot.Published.ItemDocumentIds.Should().Contain(itemId1);
        snapshot.Published.ItemDocumentIds.Should().Contain(itemId2);
    }

    [Fact]
    public async Task RemoveItemAsync_ShouldRemoveItemFromCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var itemId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Test Category",
            PublishImmediately: true));
        await grain.AddItemAsync(itemId);

        // Act
        await grain.RemoveItemAsync(itemId);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Published!.ItemDocumentIds.Should().BeEmpty();
    }

    [Fact]
    public async Task ReorderItemsAsync_ShouldChangeItemOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var itemId1 = "item-1";
        var itemId2 = "item-2";
        var itemId3 = "item-3";
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Reorder Test",
            PublishImmediately: true));
        await grain.AddItemAsync(itemId1);
        await grain.AddItemAsync(itemId2);
        await grain.AddItemAsync(itemId3);

        // Act
        await grain.ReorderItemsAsync(new List<string> { itemId3, itemId1, itemId2 });

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Published!.ItemDocumentIds[0].Should().Be(itemId3);
        snapshot.Published.ItemDocumentIds[1].Should().Be(itemId1);
        snapshot.Published.ItemDocumentIds[2].Should().Be(itemId2);
    }

    [Fact]
    public async Task CreateDraftAsync_ShouldCreateNewDraftVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Original Category",
            DisplayOrder: 1,
            PublishImmediately: true));

        // Act
        var draft = await grain.CreateDraftAsync(new CreateMenuCategoryDraftCommand(
            Name: "Updated Category",
            DisplayOrder: 2,
            ChangeNote: "Updating display order"));

        // Assert
        draft.VersionNumber.Should().Be(2);
        draft.Name.Should().Be("Updated Category");
        draft.DisplayOrder.Should().Be(2);
        draft.ChangeNote.Should().Be("Updating display order");

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PublishedVersion.Should().Be(1);
        snapshot.DraftVersion.Should().Be(2);
    }

    [Fact]
    public async Task PublishDraftAsync_ShouldMakeDraftLive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Original",
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateMenuCategoryDraftCommand(
            Name: "New Version",
            Color: "#00FF00"));

        // Act
        await grain.PublishDraftAsync(note: "Publishing new color");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PublishedVersion.Should().Be(2);
        snapshot.DraftVersion.Should().BeNull();
        snapshot.Published!.Name.Should().Be("New Version");
        snapshot.Published.Color.Should().Be("#00FF00");
    }

    [Fact]
    public async Task DiscardDraftAsync_ShouldRemoveDraft()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Original",
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateMenuCategoryDraftCommand(
            Name: "Bad Draft"));

        // Act
        await grain.DiscardDraftAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.DraftVersion.Should().BeNull();
        snapshot.Draft.Should().BeNull();
        snapshot.PublishedVersion.Should().Be(1);
        snapshot.TotalVersions.Should().Be(1);
    }

    [Fact]
    public async Task RevertToVersionAsync_ShouldRevertToOlderVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Version 1",
            DisplayOrder: 1,
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateMenuCategoryDraftCommand(
            Name: "Version 2",
            DisplayOrder: 2));
        await grain.PublishDraftAsync();

        // Act
        await grain.RevertToVersionAsync(1, reason: "Reverting changes");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PublishedVersion.Should().Be(3);
        snapshot.Published!.Name.Should().Be("Version 1");
        snapshot.Published.DisplayOrder.Should().Be(1);
        snapshot.TotalVersions.Should().Be(3);
    }

    [Fact]
    public async Task ScheduleChangeAsync_ShouldCreateSchedule()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "Regular Category",
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateMenuCategoryDraftCommand(
            Name: "Seasonal Category"));
        await grain.PublishDraftAsync();

        var activateAt = DateTimeOffset.UtcNow.AddDays(1);

        // Act
        var schedule = await grain.ScheduleChangeAsync(
            version: 2,
            activateAt: activateAt,
            name: "Seasonal Menu Activation");

        // Assert
        schedule.VersionToActivate.Should().Be(2);
        schedule.ActivateAt.Should().Be(activateAt);
        schedule.Name.Should().Be("Seasonal Menu Activation");
        schedule.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ArchiveAsync_ShouldArchiveCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "To Archive",
            PublishImmediately: true));

        // Act
        await grain.ArchiveAsync(reason: "Discontinued");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task GetVersionHistoryAsync_ShouldReturnHistory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateMenuCategoryDocumentCommand(
            Name: "V1", PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateMenuCategoryDraftCommand(Name: "V2"));
        await grain.PublishDraftAsync();
        await grain.CreateDraftAsync(new CreateMenuCategoryDraftCommand(Name: "V3"));
        await grain.PublishDraftAsync();

        // Act
        var history = await grain.GetVersionHistoryAsync();

        // Assert
        history.Should().HaveCount(3);
        history[0].VersionNumber.Should().Be(3);
        history[0].Name.Should().Be("V3");
        history[1].VersionNumber.Should().Be(2);
        history[2].VersionNumber.Should().Be(1);
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ModifierBlockGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ModifierBlockGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IModifierBlockGrain GetGrain(Guid orgId, string blockId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IModifierBlockGrain>(
            GrainKeys.ModifierBlock(orgId, blockId));
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateModifierBlock()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, blockId);

        // Act
        var result = await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Size",
            SelectionRule: ModifierSelectionRule.ChooseOne,
            MinSelections: 1,
            MaxSelections: 1,
            IsRequired: true,
            Options:
            [
                new CreateModifierOptionCommand("Small", 0m, true, 1),
                new CreateModifierOptionCommand("Medium", 0.50m, false, 2),
                new CreateModifierOptionCommand("Large", 1.00m, false, 3)
            ],
            PublishImmediately: true));

        // Assert
        result.BlockId.Should().Be(blockId);
        result.Published!.Name.Should().Be("Size");
        result.Published.SelectionRule.Should().Be(ModifierSelectionRule.ChooseOne);
        result.Published.IsRequired.Should().BeTrue();
        result.Published.Options.Should().HaveCount(3);
        result.Published.Options[0].Name.Should().Be("Small");
        result.Published.Options[0].IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task RegisterUsageAsync_ShouldTrackUsage()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var itemId1 = "item-1";
        var itemId2 = "item-2";
        var grain = GetGrain(orgId, blockId);
        await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Temperature",
            PublishImmediately: true));

        // Act
        await grain.RegisterUsageAsync(itemId1);
        await grain.RegisterUsageAsync(itemId2);

        // Assert
        var usage = await grain.GetUsageAsync();
        usage.Should().HaveCount(2);
        usage.Should().Contain(itemId1);
        usage.Should().Contain(itemId2);
    }

    [Fact]
    public async Task ArchiveAsync_WithUsage_ShouldThrowException()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, blockId);
        await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "In Use",
            PublishImmediately: true));
        await grain.RegisterUsageAsync("item-1");

        // Act & Assert
        var action = () => grain.ArchiveAsync();
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*used by*");
    }

    [Fact]
    public async Task UnregisterUsageAsync_ShouldRemoveFromTracking()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var itemId = "item-1";
        var grain = GetGrain(orgId, blockId);
        await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Toppings",
            PublishImmediately: true));
        await grain.RegisterUsageAsync(itemId);

        // Act
        await grain.UnregisterUsageAsync(itemId);

        // Assert
        var usage = await grain.GetUsageAsync();
        usage.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateDraftAsync_ShouldCreateNewDraftVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, blockId);
        await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Original Size",
            SelectionRule: ModifierSelectionRule.ChooseOne,
            MinSelections: 1,
            MaxSelections: 1,
            IsRequired: true,
            Options:
            [
                new CreateModifierOptionCommand("Small", 0m, true, 1),
                new CreateModifierOptionCommand("Large", 1.00m, false, 2)
            ],
            PublishImmediately: true));

        // Act
        var draft = await grain.CreateDraftAsync(new CreateModifierBlockDraftCommand(
            Name: "Updated Size",
            ChangeNote: "Adding medium option",
            Options:
            [
                new CreateModifierOptionCommand("Small", 0m, true, 1),
                new CreateModifierOptionCommand("Medium", 0.50m, false, 2),
                new CreateModifierOptionCommand("Large", 1.00m, false, 3)
            ]));

        // Assert
        draft.VersionNumber.Should().Be(2);
        draft.Name.Should().Be("Updated Size");
        draft.Options.Should().HaveCount(3);
        draft.ChangeNote.Should().Be("Adding medium option");

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PublishedVersion.Should().Be(1);
        snapshot.DraftVersion.Should().Be(2);
    }

    [Fact]
    public async Task PublishDraftAsync_ShouldMakeDraftLive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, blockId);
        await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Original",
            Options:
            [
                new CreateModifierOptionCommand("Option 1", 0m, true, 1)
            ],
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateModifierBlockDraftCommand(
            Name: "Updated",
            Options:
            [
                new CreateModifierOptionCommand("New Option 1", 0m, true, 1),
                new CreateModifierOptionCommand("New Option 2", 0.50m, false, 2)
            ]));

        // Act
        await grain.PublishDraftAsync(note: "Publishing new options");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PublishedVersion.Should().Be(2);
        snapshot.DraftVersion.Should().BeNull();
        snapshot.Published!.Name.Should().Be("Updated");
        snapshot.Published.Options.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_ChooseMultiple_ShouldAllowMultipleSelections()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, blockId);

        // Act
        var result = await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Toppings",
            SelectionRule: ModifierSelectionRule.ChooseMany,
            MinSelections: 0,
            MaxSelections: 5,
            IsRequired: false,
            Options:
            [
                new CreateModifierOptionCommand("Pepperoni", 1.50m, false, 1),
                new CreateModifierOptionCommand("Mushrooms", 1.00m, false, 2),
                new CreateModifierOptionCommand("Olives", 1.00m, false, 3),
                new CreateModifierOptionCommand("Extra Cheese", 2.00m, false, 4)
            ],
            PublishImmediately: true));

        // Assert
        result.Published!.SelectionRule.Should().Be(ModifierSelectionRule.ChooseMany);
        result.Published.MinSelections.Should().Be(0);
        result.Published.MaxSelections.Should().Be(5);
        result.Published.IsRequired.Should().BeFalse();
        result.Published.Options.Should().HaveCount(4);
    }

    [Fact]
    public async Task ArchiveAsync_WithoutUsage_ShouldSucceed()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var blockId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, blockId);
        await grain.CreateAsync(new CreateModifierBlockCommand(
            Name: "Unused Modifier",
            Options:
            [
                new CreateModifierOptionCommand("Option", 0m, true, 1)
            ],
            PublishImmediately: true));

        // Act
        await grain.ArchiveAsync(reason: "No longer needed");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsArchived.Should().BeTrue();
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class ContentTagGrainTests
{
    private readonly TestClusterFixture _fixture;

    public ContentTagGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IContentTagGrain GetGrain(Guid orgId, string tagId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IContentTagGrain>(
            GrainKeys.ContentTag(orgId, tagId));
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateTag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var tagId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, tagId);

        // Act
        var result = await grain.CreateAsync(new CreateContentTagCommand(
            Name: "Gluten Free",
            Category: TagCategory.Dietary,
            IconUrl: "https://example.com/gf.png",
            BadgeColor: "#4CAF50"));

        // Assert
        result.TagId.Should().Be(tagId);
        result.Name.Should().Be("Gluten Free");
        result.Category.Should().Be(TagCategory.Dietary);
        result.IconUrl.Should().Be("https://example.com/gf.png");
        result.BadgeColor.Should().Be("#4CAF50");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateTag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var tagId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, tagId);
        await grain.CreateAsync(new CreateContentTagCommand(
            Name: "Vegan",
            Category: TagCategory.Dietary));

        // Act
        var result = await grain.UpdateAsync(new UpdateContentTagCommand(
            Name: "100% Vegan",
            BadgeColor: "#8BC34A"));

        // Assert
        result.Name.Should().Be("100% Vegan");
        result.BadgeColor.Should().Be("#8BC34A");
    }

    [Fact]
    public async Task DeactivateAsync_ShouldDeactivateTag()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var tagId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, tagId);
        await grain.CreateAsync(new CreateContentTagCommand(
            Name: "Special Offer",
            Category: TagCategory.Promotional));

        // Act
        await grain.DeactivateAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsActive.Should().BeFalse();
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class SiteMenuOverridesGrainTests
{
    private readonly TestClusterFixture _fixture;

    public SiteMenuOverridesGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private ISiteMenuOverridesGrain GetGrain(Guid orgId, Guid siteId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<ISiteMenuOverridesGrain>(
            GrainKeys.SiteMenuOverrides(orgId, siteId));
    }

    [Fact]
    public async Task SetPriceOverrideAsync_ShouldSetOverride()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = "item-1";
        var grain = GetGrain(orgId, siteId);

        // Act
        await grain.SetPriceOverrideAsync(new SetSitePriceOverrideCommand(
            ItemDocumentId: itemId,
            Price: 8.99m,
            Reason: "Local pricing"));

        // Assert
        var price = await grain.GetPriceOverrideAsync(itemId);
        price.Should().Be(8.99m);
    }

    [Fact]
    public async Task RemovePriceOverrideAsync_ShouldRemoveOverride()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = "item-1";
        var grain = GetGrain(orgId, siteId);
        await grain.SetPriceOverrideAsync(new SetSitePriceOverrideCommand(
            ItemDocumentId: itemId,
            Price: 8.99m));

        // Act
        await grain.RemovePriceOverrideAsync(itemId);

        // Assert
        var price = await grain.GetPriceOverrideAsync(itemId);
        price.Should().BeNull();
    }

    [Fact]
    public async Task HideItemAsync_ShouldHideItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = "item-to-hide";
        var grain = GetGrain(orgId, siteId);

        // Act
        await grain.HideItemAsync(itemId);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.HiddenItemIds.Should().Contain(itemId);
    }

    [Fact]
    public async Task UnhideItemAsync_ShouldUnhideItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = "item-to-unhide";
        var grain = GetGrain(orgId, siteId);
        await grain.HideItemAsync(itemId);

        // Act
        await grain.UnhideItemAsync(itemId);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.HiddenItemIds.Should().NotContain(itemId);
    }

    [Fact]
    public async Task SnoozeItemAsync_ShouldSnoozeItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = "item-to-snooze";
        var until = DateTimeOffset.UtcNow.AddHours(2);
        var grain = GetGrain(orgId, siteId);

        // Act
        await grain.SnoozeItemAsync(itemId, until, reason: "Out of stock");

        // Assert
        var isSnoozed = await grain.IsItemSnoozedAsync(itemId);
        isSnoozed.Should().BeTrue();
    }

    [Fact]
    public async Task UnsnoozeItemAsync_ShouldUnsnoozeItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = "item-to-unsnooze";
        var grain = GetGrain(orgId, siteId);
        await grain.SnoozeItemAsync(itemId);

        // Act
        await grain.UnsnoozeItemAsync(itemId);

        // Assert
        var isSnoozed = await grain.IsItemSnoozedAsync(itemId);
        isSnoozed.Should().BeFalse();
    }

    [Fact]
    public async Task AddAvailabilityWindowAsync_ShouldAddWindow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);

        // Act
        var window = await grain.AddAvailabilityWindowAsync(new AddAvailabilityWindowCommand(
            Name: "Happy Hour",
            StartTime: new TimeOnly(16, 0),
            EndTime: new TimeOnly(18, 0),
            DaysOfWeek: new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
            ItemDocumentIds: new List<string> { "drink-1", "drink-2" }));

        // Assert
        window.Name.Should().Be("Happy Hour");
        window.StartTime.Should().Be(new TimeOnly(16, 0));
        window.EndTime.Should().Be(new TimeOnly(18, 0));

        var windows = await grain.GetAvailabilityWindowsAsync();
        windows.Should().HaveCount(1);
    }

    [Fact]
    public async Task RemoveAvailabilityWindowAsync_ShouldRemoveWindow()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var grain = GetGrain(orgId, siteId);
        var window = await grain.AddAvailabilityWindowAsync(new AddAvailabilityWindowCommand(
            Name: "Test Window",
            StartTime: new TimeOnly(10, 0),
            EndTime: new TimeOnly(14, 0),
            DaysOfWeek: new List<DayOfWeek> { DayOfWeek.Saturday }));

        // Act
        await grain.RemoveAvailabilityWindowAsync(window.WindowId);

        // Assert
        var windows = await grain.GetAvailabilityWindowsAsync();
        windows.Should().BeEmpty();
    }

    [Fact]
    public async Task HideCategoryAsync_ShouldHideCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var categoryId = "category-to-hide";
        var grain = GetGrain(orgId, siteId);

        // Act
        await grain.HideCategoryAsync(categoryId);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.HiddenCategoryIds.Should().Contain(categoryId);
    }

    [Fact]
    public async Task UnhideCategoryAsync_ShouldUnhideCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var categoryId = "category-to-unhide";
        var grain = GetGrain(orgId, siteId);
        await grain.HideCategoryAsync(categoryId);

        // Act
        await grain.UnhideCategoryAsync(categoryId);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.HiddenCategoryIds.Should().NotContain(categoryId);
    }

    [Fact]
    public async Task AddLocalItemAsync_ShouldTrackLocalItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var localItemId1 = "local-item-1";
        var localItemId2 = "local-item-2";
        var grain = GetGrain(orgId, siteId);

        // Act
        await grain.AddLocalItemAsync(localItemId1);
        await grain.AddLocalItemAsync(localItemId2);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.LocalItemIds.Should().Contain(localItemId1);
        snapshot.LocalItemIds.Should().Contain(localItemId2);
        snapshot.LocalItemIds.Should().HaveCount(2);
    }

    [Fact]
    public async Task SetPriceOverrideAsync_WithEffectiveDates_ShouldRespectDates()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = "item-with-dated-override";
        var grain = GetGrain(orgId, siteId);
        var effectiveFrom = DateTimeOffset.UtcNow.AddHours(-1);
        var effectiveUntil = DateTimeOffset.UtcNow.AddHours(2);

        // Act
        await grain.SetPriceOverrideAsync(new SetSitePriceOverrideCommand(
            ItemDocumentId: itemId,
            Price: 9.99m,
            EffectiveFrom: effectiveFrom,
            EffectiveUntil: effectiveUntil,
            Reason: "Happy hour pricing"));

        // Assert - current time is within effective range
        var price = await grain.GetPriceOverrideAsync(itemId);
        price.Should().Be(9.99m);
    }

    [Fact]
    public async Task GetPriceOverrideAsync_ExpiredOverride_ShouldReturnNull()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var itemId = "item-with-expired-override";
        var grain = GetGrain(orgId, siteId);

        // Set an override that ended in the past
        await grain.SetPriceOverrideAsync(new SetSitePriceOverrideCommand(
            ItemDocumentId: itemId,
            Price: 5.99m,
            EffectiveFrom: DateTimeOffset.UtcNow.AddHours(-5),
            EffectiveUntil: DateTimeOffset.UtcNow.AddHours(-1),
            Reason: "Past promotion"));

        // Act
        var price = await grain.GetPriceOverrideAsync(itemId);

        // Assert - override has expired so should return null
        price.Should().BeNull();
    }
}
