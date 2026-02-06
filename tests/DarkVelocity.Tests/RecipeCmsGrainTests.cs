using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using FluentAssertions;

namespace DarkVelocity.Tests;

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class RecipeDocumentGrainTests
{
    private readonly TestClusterFixture _fixture;

    public RecipeDocumentGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IRecipeDocumentGrain GetGrain(Guid orgId, string documentId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IRecipeDocumentGrain>(
            GrainKeys.RecipeDocument(orgId, documentId));
    }

    // Given: a new Classic Margarita recipe with tequila, triple sec, and lime juice
    // When: creating the recipe document with immediate publication
    // Then: version 1 is published with all three ingredients and no draft
    [Fact]
    public async Task CreateAsync_ShouldCreateDocument()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);

        // Act
        var result = await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Classic Margarita",
            Description: "Traditional lime margarita",
            PortionYield: 1,
            YieldUnit: "cocktail",
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new CreateRecipeIngredientCommand(
                    Guid.NewGuid(), "Tequila", 60, "ml", 0, 0.05m),
                new CreateRecipeIngredientCommand(
                    Guid.NewGuid(), "Triple Sec", 30, "ml", 0, 0.03m),
                new CreateRecipeIngredientCommand(
                    Guid.NewGuid(), "Fresh Lime Juice", 30, "ml", 10, 0.02m)
            },
            PublishImmediately: true));

        // Assert
        result.DocumentId.Should().Be(documentId);
        result.CurrentVersion.Should().Be(1);
        result.PublishedVersion.Should().Be(1);
        result.DraftVersion.Should().BeNull();
        result.IsArchived.Should().BeFalse();
        result.Published.Should().NotBeNull();
        result.Published!.Name.Should().Be("Classic Margarita");
        result.Published.Ingredients.Should().HaveCount(3);
    }

    // Given: a new recipe created without immediate publication
    // When: the recipe document is created
    // Then: only a draft version exists with no published version
    [Fact]
    public async Task CreateAsync_WithoutPublish_ShouldCreateDraft()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);

        // Act
        var result = await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Draft Recipe",
            PortionYield: 4,
            PublishImmediately: false));

        // Assert
        result.PublishedVersion.Should().BeNull();
        result.DraftVersion.Should().Be(1);
        result.Published.Should().BeNull();
        result.Draft.Should().NotBeNull();
        result.Draft!.Name.Should().Be("Draft Recipe");
    }

    // Given: a published recipe calling for 200g flour
    // When: creating a draft that increases flour to 250g
    // Then: a version 2 draft exists alongside the unchanged published version 1
    [Fact]
    public async Task CreateDraftAsync_ShouldCreateNewDraftVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        var ingredientId = Guid.NewGuid();
        await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Original Recipe",
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new CreateRecipeIngredientCommand(ingredientId, "Flour", 200, "g", 0, 0.005m)
            },
            PublishImmediately: true));

        // Act
        var draft = await grain.CreateDraftAsync(new CreateRecipeDraftCommand(
            Name: "Updated Recipe",
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new CreateRecipeIngredientCommand(ingredientId, "Flour", 250, "g", 0, 0.005m)
            },
            ChangeNote: "Increased flour quantity"));

        // Assert
        draft.VersionNumber.Should().Be(2);
        draft.Name.Should().Be("Updated Recipe");
        draft.Ingredients.Should().HaveCount(1);
        draft.Ingredients[0].Quantity.Should().Be(250);
        draft.ChangeNote.Should().Be("Increased flour quantity");

        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PublishedVersion.Should().Be(1);
        snapshot.DraftVersion.Should().Be(2);
        snapshot.TotalVersions.Should().Be(2);
    }

    // Given: a published recipe (4 portions) with a draft version changing yield to 6 portions
    // When: publishing the draft
    // Then: the new version becomes live with the updated 6-portion yield and no draft remains
    [Fact]
    public async Task PublishDraftAsync_ShouldMakeDraftLive()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Original",
            PortionYield: 4,
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateRecipeDraftCommand(
            Name: "New Version",
            PortionYield: 6));

        // Act
        await grain.PublishDraftAsync(note: "Publishing new yield");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PublishedVersion.Should().Be(2);
        snapshot.DraftVersion.Should().BeNull();
        snapshot.Published!.Name.Should().Be("New Version");
        snapshot.Published.PortionYield.Should().Be(6);
    }

    // Given: a published recipe with an unwanted draft version
    // When: discarding the draft
    // Then: the draft is removed, the published version remains, and version count resets to 1
    [Fact]
    public async Task DiscardDraftAsync_ShouldRemoveDraft()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Original",
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateRecipeDraftCommand(
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

    // Given: a recipe at version 2 (yield changed from 4 to 8 portions)
    // When: reverting to version 1
    // Then: a new version 3 is created with the original 4-portion yield from version 1
    [Fact]
    public async Task RevertToVersionAsync_ShouldRevertToOlderVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Version 1",
            PortionYield: 4,
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateRecipeDraftCommand(
            Name: "Version 2",
            PortionYield: 8));
        await grain.PublishDraftAsync();

        // Act
        await grain.RevertToVersionAsync(1, reason: "Reverting yield change");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.PublishedVersion.Should().Be(3);
        snapshot.Published!.Name.Should().Be("Version 1");
        snapshot.Published.PortionYield.Should().Be(4);
        snapshot.TotalVersions.Should().Be(3);
    }

    // Given: a recipe that has been published three times (V1, V2, V3)
    // When: retrieving the version history
    // Then: all three versions are returned in reverse chronological order
    [Fact]
    public async Task GetVersionHistoryAsync_ShouldReturnAllVersions()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "V1", PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateRecipeDraftCommand(Name: "V2"));
        await grain.PublishDraftAsync();
        await grain.CreateDraftAsync(new CreateRecipeDraftCommand(Name: "V3"));
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

    // Given: a published grilled chicken recipe in English
    // When: adding a Spanish translation
    // Then: the recipe includes the "es-ES" locale with the translated name and description
    [Fact]
    public async Task AddTranslationAsync_ShouldAddLocalization()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Chicken",
            Description: "Grilled chicken recipe",
            PublishImmediately: true));

        // Act
        await grain.AddTranslationAsync(new AddRecipeTranslationCommand(
            Locale: "es-ES",
            Name: "Pollo",
            Description: "Receta de pollo a la parrilla"));

        // Assert
        var version = await grain.GetPublishedAsync();
        version!.Translations.Should().ContainKey("es-ES");
        version.Translations["es-ES"].Name.Should().Be("Pollo");
        version.Translations["es-ES"].Description.Should().Be("Receta de pollo a la parrilla");
    }

    // Given: a recipe with a published seasonal version (version 2)
    // When: scheduling that version to activate tomorrow
    // Then: an active schedule is created targeting version 2 at the specified time
    [Fact]
    public async Task ScheduleChangeAsync_ShouldCreateSchedule()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Regular Recipe",
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateRecipeDraftCommand(
            Name: "Seasonal Recipe"));
        await grain.PublishDraftAsync();

        var activateAt = DateTimeOffset.UtcNow.AddDays(1);

        // Act
        var schedule = await grain.ScheduleChangeAsync(
            version: 2,
            activateAt: activateAt,
            name: "Seasonal Menu");

        // Assert
        schedule.VersionToActivate.Should().Be(2);
        schedule.ActivateAt.Should().Be(activateAt);
        schedule.Name.Should().Be("Seasonal Menu");
        schedule.IsActive.Should().BeTrue();

        var schedules = await grain.GetSchedulesAsync();
        schedules.Should().HaveCount(1);
    }

    // Given: a recipe with a pending scheduled version change
    // When: cancelling the schedule
    // Then: the schedule is removed and no pending changes remain
    [Fact]
    public async Task CancelScheduleAsync_ShouldRemoveSchedule()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Recipe", PublishImmediately: true));

        var schedule = await grain.ScheduleChangeAsync(1, DateTimeOffset.UtcNow.AddDays(1));

        // Act
        await grain.CancelScheduleAsync(schedule.ScheduleId);

        // Assert
        var schedules = await grain.GetSchedulesAsync();
        schedules.Should().BeEmpty();
    }

    // Given: a published recipe that is no longer needed
    // When: archiving the recipe as discontinued
    // Then: the recipe is marked as archived
    [Fact]
    public async Task ArchiveAsync_ShouldArchiveDocument()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "To Archive",
            PublishImmediately: true));

        // Act
        await grain.ArchiveAsync(reason: "Discontinued");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsArchived.Should().BeTrue();
    }

    // Given: a previously archived recipe document
    // When: restoring the recipe
    // Then: the recipe is no longer marked as archived and is available again
    [Fact]
    public async Task RestoreAsync_ShouldRestoreArchivedDocument()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "To Restore",
            PublishImmediately: true));
        await grain.ArchiveAsync();

        // Act
        await grain.RestoreAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsArchived.Should().BeFalse();
    }

    // Given: a published recipe with an ingredient costed at $0.01/g
    // When: the ingredient price doubles to $0.02/g and costs are recalculated
    // Then: the recipe reflects the updated ingredient unit cost
    [Fact]
    public async Task RecalculateCostAsync_ShouldUpdateCosts()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var ingredientId = Guid.NewGuid();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Costed Recipe",
            PortionYield: 4,
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new CreateRecipeIngredientCommand(ingredientId, "Ingredient A", 100, "g", 0, 0.01m)
            },
            PublishImmediately: true));

        // Act - Update ingredient price
        await grain.RecalculateCostAsync(new Dictionary<Guid, decimal>
        {
            [ingredientId] = 0.02m // Double the price
        });

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Published!.Ingredients[0].UnitCost.Should().Be(0.02m);
    }

    // Given: a published recipe not linked to any menu item
    // When: linking the recipe to a menu item
    // Then: the menu item appears in the recipe's linked items list
    [Fact]
    public async Task LinkMenuItemAsync_ShouldLinkMenuItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var menuItemId = "menu-item-1";
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Linked Recipe",
            PublishImmediately: true));

        // Act
        await grain.LinkMenuItemAsync(menuItemId);

        // Assert
        var linkedItems = await grain.GetLinkedMenuItemsAsync();
        linkedItems.Should().Contain(menuItemId);
    }

    // Given: a recipe linked to a specific menu item
    // When: unlinking the menu item from the recipe
    // Then: the menu item no longer appears in the recipe's linked items list
    [Fact]
    public async Task UnlinkMenuItemAsync_ShouldUnlinkMenuItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var menuItemId = "menu-item-1";
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Recipe to Unlink",
            PublishImmediately: true));
        await grain.LinkMenuItemAsync(menuItemId);

        // Act
        await grain.UnlinkMenuItemAsync(menuItemId);

        // Assert
        var linkedItems = await grain.GetLinkedMenuItemsAsync();
        linkedItems.Should().NotContain(menuItemId);
    }

    // Given: a recipe with 100g ingredient A (no waste, $0.01/g) and 50g ingredient B (10% waste, $0.02/g) yielding 4 portions
    // When: creating the recipe and calculating costs
    // Then: theoretical cost is ~$2.11 and cost per portion is ~$0.53
    [Fact]
    public async Task CostCalculation_ShouldCalculateCorrectly()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);

        // Create recipe with known costs
        // Ingredient A: 100g @ £0.01/g = £1.00 (no waste)
        // Ingredient B: 50g @ £0.02/g with 10% waste = 55.56g * £0.02 = £1.11
        // Total theoretical cost = £2.11
        // Portion yield = 4, so cost per portion = £0.53
        var result = await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Costed Recipe",
            PortionYield: 4,
            Ingredients: new List<CreateRecipeIngredientCommand>
            {
                new CreateRecipeIngredientCommand(Guid.NewGuid(), "A", 100, "g", 0, 0.01m),
                new CreateRecipeIngredientCommand(Guid.NewGuid(), "B", 50, "g", 10, 0.02m)
            },
            PublishImmediately: true));

        // Assert
        var published = result.Published!;
        published.Ingredients[0].LineCost.Should().Be(1.00m);
        published.Ingredients[1].EffectiveQuantity.Should().BeApproximately(55.56m, 0.01m);
        published.Ingredients[1].LineCost.Should().BeApproximately(1.11m, 0.01m);
        published.TheoreticalCost.Should().BeApproximately(2.11m, 0.01m);
        published.CostPerPortion.Should().BeApproximately(0.53m, 0.01m);
    }

    // Given: a recipe with version 2 published and scheduled for activation in 7 days
    // When: previewing the recipe at the current time and after the scheduled activation
    // Then: both previews show version 2 content since it is already the published version
    [Fact]
    public async Task PreviewAtAsync_ShouldReturnScheduledVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeDocumentCommand(
            Name: "Current",
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateRecipeDraftCommand(
            Name: "Future"));
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
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class RecipeCategoryDocumentGrainTests
{
    private readonly TestClusterFixture _fixture;

    public RecipeCategoryDocumentGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IRecipeCategoryDocumentGrain GetGrain(Guid orgId, string documentId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IRecipeCategoryDocumentGrain>(
            GrainKeys.RecipeCategoryDocument(orgId, documentId));
    }

    // Given: a new "Cocktails" recipe category with display order 1 and a red color code
    // When: creating the category with immediate publication
    // Then: the published category has the correct name, display order, and color
    [Fact]
    public async Task CreateAsync_ShouldCreateCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);

        // Act
        var result = await grain.CreateAsync(new CreateRecipeCategoryDocumentCommand(
            Name: "Cocktails",
            DisplayOrder: 1,
            Color: "#FF5733",
            PublishImmediately: true));

        // Assert
        result.DocumentId.Should().Be(documentId);
        result.Published!.Name.Should().Be("Cocktails");
        result.Published.DisplayOrder.Should().Be(1);
        result.Published.Color.Should().Be("#FF5733");
    }

    // Given: a published "Main Courses" recipe category with no recipes
    // When: adding two recipes to the category
    // Then: the category contains both recipe document IDs
    [Fact]
    public async Task AddRecipeAsync_ShouldAddRecipeToCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var recipeId1 = Guid.NewGuid().ToString();
        var recipeId2 = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeCategoryDocumentCommand(
            Name: "Main Courses",
            PublishImmediately: true));

        // Act
        await grain.AddRecipeAsync(recipeId1);
        await grain.AddRecipeAsync(recipeId2);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Published!.RecipeDocumentIds.Should().HaveCount(2);
        snapshot.Published.RecipeDocumentIds.Should().Contain(recipeId1);
        snapshot.Published.RecipeDocumentIds.Should().Contain(recipeId2);
    }

    [Fact]
    public async Task RemoveRecipeAsync_ShouldRemoveRecipeFromCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var recipeId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeCategoryDocumentCommand(
            Name: "Test Category",
            PublishImmediately: true));
        await grain.AddRecipeAsync(recipeId);

        // Act
        await grain.RemoveRecipeAsync(recipeId);

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Published!.RecipeDocumentIds.Should().BeEmpty();
    }

    [Fact]
    public async Task ReorderRecipesAsync_ShouldChangeRecipeOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var recipeId1 = "recipe-1";
        var recipeId2 = "recipe-2";
        var recipeId3 = "recipe-3";
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeCategoryDocumentCommand(
            Name: "Reorder Test",
            PublishImmediately: true));
        await grain.AddRecipeAsync(recipeId1);
        await grain.AddRecipeAsync(recipeId2);
        await grain.AddRecipeAsync(recipeId3);

        // Act
        await grain.ReorderRecipesAsync(new List<string> { recipeId3, recipeId1, recipeId2 });

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.Published!.RecipeDocumentIds[0].Should().Be(recipeId3);
        snapshot.Published.RecipeDocumentIds[1].Should().Be(recipeId1);
        snapshot.Published.RecipeDocumentIds[2].Should().Be(recipeId2);
    }

    [Fact]
    public async Task ArchiveAsync_ShouldArchiveCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeCategoryDocumentCommand(
            Name: "To Archive",
            PublishImmediately: true));

        // Act
        await grain.ArchiveAsync(reason: "No longer needed");

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task RestoreAsync_ShouldRestoreArchivedCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeCategoryDocumentCommand(
            Name: "To Restore",
            PublishImmediately: true));
        await grain.ArchiveAsync();

        // Act
        await grain.RestoreAsync();

        // Assert
        var snapshot = await grain.GetSnapshotAsync();
        snapshot.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task CreateDraftAsync_ShouldCreateNewDraftVersion()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var documentId = Guid.NewGuid().ToString();
        var grain = GetGrain(orgId, documentId);
        await grain.CreateAsync(new CreateRecipeCategoryDocumentCommand(
            Name: "Original Category",
            DisplayOrder: 1,
            PublishImmediately: true));

        // Act
        var draft = await grain.CreateDraftAsync(new CreateRecipeCategoryDraftCommand(
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
        await grain.CreateAsync(new CreateRecipeCategoryDocumentCommand(
            Name: "Original",
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateRecipeCategoryDraftCommand(
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
        await grain.CreateAsync(new CreateRecipeCategoryDocumentCommand(
            Name: "Original",
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateRecipeCategoryDraftCommand(
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
        await grain.CreateAsync(new CreateRecipeCategoryDocumentCommand(
            Name: "Version 1",
            DisplayOrder: 1,
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateRecipeCategoryDraftCommand(
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
        await grain.CreateAsync(new CreateRecipeCategoryDocumentCommand(
            Name: "Regular Category",
            PublishImmediately: true));
        await grain.CreateDraftAsync(new CreateRecipeCategoryDraftCommand(
            Name: "Seasonal Category"));
        await grain.PublishDraftAsync();

        var activateAt = DateTimeOffset.UtcNow.AddDays(1);

        // Act
        var schedule = await grain.ScheduleChangeAsync(
            version: 2,
            activateAt: activateAt,
            name: "Seasonal Recipe Activation");

        // Assert
        schedule.VersionToActivate.Should().Be(2);
        schedule.ActivateAt.Should().Be(activateAt);
        schedule.Name.Should().Be("Seasonal Recipe Activation");
        schedule.IsActive.Should().BeTrue();
    }
}

[Collection(ClusterCollection.Name)]
[Trait("Category", "Integration")]
public class RecipeRegistryGrainTests
{
    private readonly TestClusterFixture _fixture;

    public RecipeRegistryGrainTests(TestClusterFixture fixture)
    {
        _fixture = fixture;
    }

    private IRecipeRegistryGrain GetGrain(Guid orgId)
    {
        return _fixture.Cluster.GrainFactory.GetGrain<IRecipeRegistryGrain>(
            GrainKeys.RecipeRegistry(orgId));
    }

    [Fact]
    public async Task RegisterRecipeAsync_ShouldAddRecipeToRegistry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        var documentId = Guid.NewGuid().ToString();

        // Act
        await grain.RegisterRecipeAsync(documentId, "Test Recipe", 5.50m, null);

        // Assert
        var recipes = await grain.GetRecipesAsync();
        recipes.Should().HaveCount(1);
        recipes[0].DocumentId.Should().Be(documentId);
        recipes[0].Name.Should().Be("Test Recipe");
        recipes[0].CostPerPortion.Should().Be(5.50m);
    }

    [Fact]
    public async Task UpdateRecipeAsync_ShouldUpdateRecipeInRegistry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        var documentId = Guid.NewGuid().ToString();
        await grain.RegisterRecipeAsync(documentId, "Old Name", 5.00m, null);

        // Act
        await grain.UpdateRecipeAsync(documentId, "New Name", 6.00m, null, hasDraft: true, isArchived: false, linkedMenuItemCount: 2);

        // Assert
        var recipes = await grain.GetRecipesAsync();
        recipes[0].Name.Should().Be("New Name");
        recipes[0].CostPerPortion.Should().Be(6.00m);
        recipes[0].HasDraft.Should().BeTrue();
        recipes[0].LinkedMenuItemCount.Should().Be(2);
    }

    [Fact]
    public async Task GetRecipesAsync_WithCategoryFilter_ShouldFilterByCategoryId()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        var categoryId = Guid.NewGuid().ToString();
        await grain.RegisterRecipeAsync("recipe-1", "Recipe 1", 5.00m, categoryId);
        await grain.RegisterRecipeAsync("recipe-2", "Recipe 2", 6.00m, "other-category");
        await grain.RegisterRecipeAsync("recipe-3", "Recipe 3", 7.00m, categoryId);

        // Act
        var recipes = await grain.GetRecipesAsync(categoryId: categoryId);

        // Assert
        recipes.Should().HaveCount(2);
        recipes.Should().OnlyContain(r => r.CategoryId == categoryId);
    }

    [Fact]
    public async Task GetRecipesAsync_ExcludesArchivedByDefault()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        await grain.RegisterRecipeAsync("active-recipe", "Active", 5.00m, null);
        await grain.RegisterRecipeAsync("archived-recipe", "Archived", 5.00m, null);
        await grain.UpdateRecipeAsync("archived-recipe", "Archived", 5.00m, null, hasDraft: false, isArchived: true, linkedMenuItemCount: 0);

        // Act
        var recipes = await grain.GetRecipesAsync();
        var allRecipes = await grain.GetRecipesAsync(includeArchived: true);

        // Assert
        recipes.Should().HaveCount(1);
        recipes[0].Name.Should().Be("Active");
        allRecipes.Should().HaveCount(2);
    }

    [Fact]
    public async Task SearchRecipesAsync_ShouldSearchByName()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        await grain.RegisterRecipeAsync("recipe-1", "Chicken Curry", 8.00m, null);
        await grain.RegisterRecipeAsync("recipe-2", "Beef Stew", 10.00m, null);
        await grain.RegisterRecipeAsync("recipe-3", "Chicken Tikka", 9.00m, null);

        // Act
        var results = await grain.SearchRecipesAsync("Chicken");

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(r => r.Name.Contains("Chicken"));
    }

    [Fact]
    public async Task RegisterCategoryAsync_ShouldAddCategoryToRegistry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        var documentId = Guid.NewGuid().ToString();

        // Act
        await grain.RegisterCategoryAsync(documentId, "Mains", 1, "#FF0000");

        // Assert
        var categories = await grain.GetCategoriesAsync();
        categories.Should().HaveCount(1);
        categories[0].DocumentId.Should().Be(documentId);
        categories[0].Name.Should().Be("Mains");
        categories[0].DisplayOrder.Should().Be(1);
        categories[0].Color.Should().Be("#FF0000");
    }

    [Fact]
    public async Task GetCategoriesAsync_ShouldReturnSortedByDisplayOrder()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        await grain.RegisterCategoryAsync("cat-3", "Desserts", 3, null);
        await grain.RegisterCategoryAsync("cat-1", "Starters", 1, null);
        await grain.RegisterCategoryAsync("cat-2", "Mains", 2, null);

        // Act
        var categories = await grain.GetCategoriesAsync();

        // Assert
        categories.Should().HaveCount(3);
        categories[0].Name.Should().Be("Starters");
        categories[1].Name.Should().Be("Mains");
        categories[2].Name.Should().Be("Desserts");
    }

    [Fact]
    public async Task UnregisterRecipeAsync_ShouldRemoveFromRegistry()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        var documentId = Guid.NewGuid().ToString();
        await grain.RegisterRecipeAsync(documentId, "Recipe to Remove", 5.00m, null);

        // Act
        await grain.UnregisterRecipeAsync(documentId);

        // Assert
        var recipes = await grain.GetRecipesAsync(includeArchived: true);
        recipes.Should().NotContain(r => r.DocumentId == documentId);
    }

    [Fact]
    public async Task GetBatchPrepRecipesAsync_ShouldFilterByType()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        await grain.RegisterRecipeAsync("recipe-1", "Individual Recipe", 5.00m, null, recipeType: RecipeType.MadeToOrder);
        await grain.RegisterRecipeAsync("recipe-2", "Batch Prep 1", 10.00m, null, recipeType: RecipeType.BatchPrep);
        await grain.RegisterRecipeAsync("recipe-3", "Batch Prep 2", 15.00m, null, recipeType: RecipeType.BatchPrep);
        await grain.RegisterRecipeAsync("recipe-4", "Component Recipe", 8.00m, null, recipeType: RecipeType.MadeToOrder);

        // Act
        var batchPrepRecipes = await grain.GetBatchPrepRecipesAsync();

        // Assert
        batchPrepRecipes.Should().HaveCount(2);
        batchPrepRecipes.Should().OnlyContain(r => r.RecipeType == RecipeType.BatchPrep);
    }

    [Fact]
    public async Task GetRecipesByOutputItemAsync_ShouldFilterByOutputItem()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        var outputItemId = Guid.NewGuid();
        var otherItemId = Guid.NewGuid();
        await grain.RegisterRecipeAsync("recipe-1", "Recipe for Output A", 5.00m, null, outputInventoryItemId: outputItemId);
        await grain.RegisterRecipeAsync("recipe-2", "Recipe for Output B", 6.00m, null, outputInventoryItemId: otherItemId);
        await grain.RegisterRecipeAsync("recipe-3", "Another for Output A", 7.00m, null, outputInventoryItemId: outputItemId);

        // Act
        var recipesForItem = await grain.GetRecipesByOutputItemAsync(outputItemId);

        // Assert
        recipesForItem.Should().HaveCount(2);
        recipesForItem.Should().OnlyContain(r => r.OutputInventoryItemId == outputItemId);
    }

    [Fact]
    public async Task UpdateCategoryAsync_ShouldUpdateCategory()
    {
        // Arrange
        var orgId = Guid.NewGuid();
        var grain = GetGrain(orgId);
        var documentId = Guid.NewGuid().ToString();
        await grain.RegisterCategoryAsync(documentId, "Old Name", 1, "#FF0000");

        // Act
        await grain.UpdateCategoryAsync(documentId, "New Name", 2, "#00FF00", hasDraft: false, isArchived: false, recipeCount: 5);

        // Assert
        var categories = await grain.GetCategoriesAsync();
        var category = categories.First(c => c.DocumentId == documentId);
        category.Name.Should().Be("New Name");
        category.DisplayOrder.Should().Be(2);
        category.Color.Should().Be("#00FF00");
    }
}
