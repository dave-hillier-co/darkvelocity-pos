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
}
